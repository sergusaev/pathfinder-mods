using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.Items.Slots;
using Kingmaker.PubSubSystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker.UnitLogic.ActivatableAbilities;

namespace BeltConsumablesRefill
{
    // The refills of Bag of Tricks and ToyBox insert an item that may sit in another character's belt slot. A one-item
    // stack is not split, so the item itself moves: the new slot equips it (ability for the new owner), but the old
    // owner's slot still points at it and the old owner keeps the ability, since the item now remembers only the new
    // one. Once the item is used up that ability stays in the list with 0 charges and does nothing.
    // On every area load (also after loading a save) drop such leftovers of the party:
    //  - a belt slot whose item is held by another slot;
    //  - an ability or activatable ability from a usable item that is not this unit's own equipped fact of that item.
    class Cleanup :
#if KINGMAKER
        ISceneHandler
#else
        IAreaHandler
#endif
    {
        internal static string LastReport;

#if KINGMAKER
        static readonly FieldInfo SlotItem = AccessTools.Field(typeof(ItemSlot), "m_Item");
#else
        static readonly FieldInfo SlotItem = AccessTools.Field(typeof(ItemSlot), "m_ItemRef");
#endif
        static readonly FieldInfo AppliedFacts = AccessTools.Field(typeof(ItemEntity), "m_FactsAppliedToWielder");

        public void OnAreaBeginUnloading()
        {
        }

        public void OnAreaDidLoad()
        {
            OtherRefill.WarnOnce();
            if (!Main.Settings.Cleanup) return;
            try
            {
                Run();
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
            }
        }

        static void Run()
        {
            int slots = 0, facts = 0;
            foreach (UnitEntityData unit in Game.Instance.Player.AllCharacters.ToList())
            {
                UnitDescriptor owner = unit?.Descriptor;
                if (owner?.Body == null) continue;
                foreach (UsableSlot slot in owner.Body.QuickSlots)
                {
                    ItemEntity item = slot.MaybeItem;
                    if (item == null || item.HoldingSlot == slot) continue;
                    // Not this slot's item any more: forget it without unequipping, the item belongs to the other slot.
                    SlotItem.SetValue(slot, SlotItem.FieldType.IsValueType ? Activator.CreateInstance(SlotItem.FieldType) : null);
                    slots++;
                    Main.Log.Log($"cleanup: {owner.CharacterName}: belt slot released {item.Blueprint.name}, held by {item.Wielder?.CharacterName ?? "nobody"}");
                }
                foreach (Ability fact in owner.Abilities.Enumerable.ToList())
                {
                    if (!Orphan(owner, fact, fact.SourceItem)) continue;
                    Main.Log.Log($"cleanup: {owner.CharacterName}: ability {fact.Blueprint.name} of {fact.SourceItem.Blueprint.name} removed (charges {fact.SourceItem.Charges})");
                    UnitHelper.RemoveFact(owner, fact);
                    facts++;
                }
                foreach (ActivatableAbility fact in owner.ActivatableAbilities.Enumerable.ToList())
                {
                    if (!Orphan(owner, fact, fact.SourceItem)) continue;
                    Main.Log.Log($"cleanup: {owner.CharacterName}: activatable {fact.Blueprint.name} of {fact.SourceItem.Blueprint.name} removed (charges {fact.SourceItem.Charges})");
                    UnitHelper.RemoveFact(owner, fact);
                    facts++;
                }
            }
            LastReport = $"{DateTime.Now:HH:mm:ss}, belt slots released {slots}, abilities removed {facts}";
            if (slots > 0 || facts > 0)
                Main.Log.Log("cleanup: " + LastReport);
        }

        // Only usable items (belt consumables, wands, rods): other items, such as natural weapons or a kineticist
        // blade, have no inventory and are left alone.
        static bool Orphan(UnitDescriptor owner, object fact, ItemEntity source)
        {
            if (!(source is ItemEntityUsable)) return false;
            if (source.Wielder != owner || source.HoldingSlot == null || source.Collection == null) return true;
            if (ReferenceEquals(source.Ability, fact) || ReferenceEquals(source.ActivatableAbility, fact)) return false;
            if (AppliedFacts?.GetValue(source) is IEnumerable applied)
                foreach (object f in applied)
                    if (ReferenceEquals(f, fact)) return false;
            return true;
        }
    }
}
