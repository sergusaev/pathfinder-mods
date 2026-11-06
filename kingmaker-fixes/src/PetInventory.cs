using System;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI.ServiceWindow;
using Kingmaker.UI._ConsoleUI.Inventory;
using Kingmaker.UnitLogic;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Visual.Animation.Kingmaker;
using UnityEngine;
using UnityEngine.Playables;

namespace KingmakerFixes
{
    // An animal companion (the wolf, say) can wear items: the keyboard and mouse inventory has its slots and stash.
    // The gamepad inventory takes a pet for a polymorphed character and shows a picture instead of its doll page and
    // its stash, so nothing can be put on it. A pet is treated as any character now; a polymorphed character keeps
    // the picture, as in the game.
    [HarmonyPatch(typeof(InventoryDollVM), MethodType.Constructor, new[] { typeof(UniRx.IReactiveProperty<UnitDescriptor>), typeof(UniRx.BoolReactiveProperty) })]
    static class PetDollPage
    {
        static readonly FieldInfo s_IsPet = AccessTools.Field(typeof(InventoryDollVM), nameof(InventoryDollVM.IsPet));

        static void Postfix(InventoryDollVM __instance, UniRx.IReactiveProperty<UnitDescriptor> unit)
        {
            try
            {
                var descriptor = unit?.Value;
                if (descriptor != null && descriptor.IsPet && !descriptor.Body.IsPolymorphed)
                    s_IsPet.SetValue(__instance, false);
            }
            catch (Exception e)
            {
                Main.Log.Error("pet doll page: " + e);
            }
        }
    }

    [HarmonyPatch(typeof(InventoryStashVM), MethodType.Constructor, new[] { typeof(UnitDescriptor) })]
    static class PetStashPage
    {
        static readonly FieldInfo s_IsPet = AccessTools.Field(typeof(InventoryStashVM), nameof(InventoryStashVM.IsPet));

        static void Postfix(InventoryStashVM __instance)
        {
            try
            {
                if (__instance.IsPet)
                    s_IsPet.SetValue(__instance, false);
            }
            catch (Exception e)
            {
                Main.Log.Error("pet stash page: " + e);
            }
        }
    }

    // The keyboard and mouse inventory covers the doll of a pet with a picture, though the doll room builds the pet
    // (a copy of its view, as for any unit without a character avatar). The picture stays for a polymorphed character.
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.SetCharacter))]
    static class PetDollPicture
    {
        static void Postfix(Inventory __instance)
        {
            try
            {
                var unit = Kingmaker.UI.Group.GroupController.Instance?.GetCurrentCharacter();
                if (unit != null && unit.Descriptor.IsPet && !unit.Body.IsPolymorphed && __instance.Placeholder != null)
                    __instance.Placeholder.gameObject.SetActive(false);
            }
            catch (Exception e)
            {
                Main.Log.Error("pet doll picture: " + e);
            }
        }
    }

    // The doll of a pet is a copy of its view as it is in the world: switched off while the pet is not on the map (on
    // the global map, in the capital), so the doll room stayed empty. The copy is switched on and animated as the game
    // does with a copy that comes out on. Both interfaces.
    [HarmonyPatch(typeof(DollRoom), nameof(DollRoom.SetupInfo))]
    static class PetDollModel
    {
        static readonly FieldInfo s_Simple = AccessTools.Field(typeof(DollRoom), "m_SimpleAvatar");

        static void Postfix(DollRoom __instance, UnitEntityData player)
        {
            try
            {
                if (player == null || !player.Descriptor.IsPet || player.Body.IsPolymorphed)
                    return;
                var simple = s_Simple.GetValue(__instance) as Component;
                if (simple == null || simple.gameObject.activeSelf)
                    return;
                simple.gameObject.SetActive(true);
                var animation = simple.GetComponentInChildren<UnitAnimationManager>();
                if (animation != null)
                {
                    if (animation.PlayableGraph.IsValid())
                        animation.PlayableGraph.SetTimeUpdateMode(DirectorUpdateMode.UnscaledGameTime);
                    animation.IsInCombat = true;
                    animation.Tick();
                }
                Main.Log.Log("pet doll: the copy of " + player.CharacterName + " switched on");
            }
            catch (Exception e)
            {
                Main.Log.Error("pet doll model: " + e);
            }
        }
    }
}
