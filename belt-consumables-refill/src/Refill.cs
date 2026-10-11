using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Items;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Items;
using Kingmaker.Items.Slots;
using Kingmaker.UI.UnitSettings;
using Kingmaker.UnitLogic;
using UnityEngine;

namespace BeltConsumablesRefill
{
    // A belt (quick) slot holds one item: ItemSlot.InsertItem splits one off a stack. When its last charge is spent,
    // ItemEntity.SpendCharges removes the item from the inventory, which empties the slot, and the game never fills it
    // again. Refill it with the same item from the party inventory, taking only free items (HoldingSlot == null):
    // the refills of Bag of Tricks and ToyBox also took items from other characters' belts.
    // Only spending refills a slot; taking an item off the belt by hand leaves the slot empty.
    [HarmonyPatch(typeof(ItemEntity), nameof(ItemEntity.SpendCharges), new Type[] { typeof(UnitDescriptor) })]
    static class SpendChargesPatch
    {
        static void Prefix(ItemEntity __instance, out UsableSlot __state)
        {
            __state = Main.Settings.Refill ? __instance.HoldingSlot as UsableSlot : null;
        }

        static void Postfix(ItemEntity __instance, UsableSlot __state)
        {
            if (__state == null) return;
            try
            {
                // Still in the slot: charges left (a wand) or the spend failed.
                if (__state.HasItem) return;
                UsableSlot slot = __state;
                BlueprintItem blueprint = __instance.Blueprint;
                // SpendCharges runs inside the cast; insert after it, before the next game tick (also while paused).
                Game.Instance.ScheduleAction(() => Stock.Fill(slot, blueprint));
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
            }
        }
    }

    // The action bar shows a belt item's charges (1 for a potion or a scroll), or the count of a stack. For a
    // single-charge consumable add the free items of the same kind, so the slot shows how many uses are left.
    // Only items still on the belt are counted: for them the vanilla result is at least 1.
    [HarmonyPatch(typeof(MechanicActionBarSlotItem), nameof(MechanicActionBarSlotItem.GetResource))]
    static class GetResourcePatch
    {
        static void Postfix(MechanicActionBarSlotItem __instance, ref int __result)
        {
            if (!Main.Settings.ShowStock || __result < 1) return;
            try
            {
                ItemEntity item = __instance.Item;
                if (item == null || item.HoldingSlot == null || item.Count > 1) return;
                var blueprint = item.Blueprint as BlueprintItemEquipment;
                if (blueprint == null || !blueprint.SpendCharges || blueprint.Charges > 1 || blueprint.RestoreChargesOnRest) return;
                __result += Stock.Free(blueprint);
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
            }
        }
    }

    static class Stock
    {
        // Free items of each blueprint, counted once per frame: the action bar asks every slot every frame.
        static readonly Dictionary<BlueprintItem, int> s_Free = new Dictionary<BlueprintItem, int>();
        static int s_Frame = -1;

        static bool IsFree(ItemEntity i)
        {
            return i.HoldingSlot == null && i.Count > 0 && i.IsIdentified && (!i.IsSpendCharges || i.Charges > 0);
        }

        internal static int Free(BlueprintItem blueprint)
        {
            if (s_Frame != Time.frameCount)
            {
                s_Frame = Time.frameCount;
                s_Free.Clear();
            }
            int count;
            if (s_Free.TryGetValue(blueprint, out count)) return count;
            count = 0;
            foreach (ItemEntity i in Game.Instance.Player.Inventory.Items)
                if (i.Blueprint == blueprint && IsFree(i)) count += i.Count;
            s_Free[blueprint] = count;
            return count;
        }

        internal static void Fill(UsableSlot slot, BlueprintItem blueprint)
        {
            try
            {
                ItemsCollection inventory = Game.Instance.Player.Inventory;
                // Filled meanwhile (by the player or another mod), or a unit with an inventory of its own.
                if (slot.HasItem || slot.Owner == null || slot.Owner.Inventory != inventory) return;
                ItemEntity found = null;
                foreach (ItemEntity i in inventory.Items)
                {
                    if (i.Blueprint == blueprint && IsFree(i))
                    {
                        found = i;
                        break;
                    }
                }
                if (found == null || !slot.CanInsertItem(found)) return;
                slot.InsertItem(found);
                s_Frame = -1;
                Main.Log.Log($"refill: {slot.Owner.CharacterName} <- {blueprint.name}, {Free(blueprint)} left");
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
            }
        }
    }
}
