using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.UI.SaveLoadWindow;

namespace KingmakerCosmetics
{
    // A save whose characters wear a style of the mod gets an unknown DLC number in its header. Without the mod the
    // game shows such a save greyed out as needing a DLC and does not load it from the save list, instead of loading
    // characters without a head or hair. The ids stay in DollData either way, so the looks come back with the mod.
    // ("Continue", quick load and load after death skip the check; such a load only loses the looks until the mod is back.)
    [HarmonyPatch(typeof(SaveManager), nameof(SaveManager.PrepareSave))]
    static class SaveMark
    {
        internal const DlcType Mark = (DlcType)1001;

        static void Postfix(SaveInfo save)
        {
            if (!WearsStyle())
                return;
            // save.DLC is the list of DlcRoot itself, a new one is made.
            save.DLC = new List<DlcType>(save.DLC ?? new List<DlcType>()) { Mark };
        }

        static bool WearsStyle()
        {
            var player = Game.Instance?.Player;
            if (player == null)
                return false;
            return player.AllCharacters.Concat(player.AllCrossSceneUnits).Any(Wears);
        }

        static bool Wears(UnitEntityData unit)
        {
            var ids = unit?.Descriptor?.Doll?.EquipmentEntityIds;
            return ids != null && ids.Any(Styles.OwnIds.Contains);
        }

        // With the mod the mark does not count as a missing DLC.
        internal static bool Required(List<DlcType> dlc)
        {
            return dlc != null && dlc.Where(d => d != Mark).Except(BlueprintRoot.Instance.DlcSettings.ListForSaves).Any();
        }
    }

    [HarmonyPatch(typeof(SaveInfo), nameof(SaveInfo.DlcRequired), MethodType.Getter)]
    static class SaveInfoIgnoresMark
    {
        static void Postfix(SaveInfo __instance, ref bool __result)
        {
            if (__result)
                __result = SaveMark.Required(__instance.DLC);
        }
    }

    [HarmonyPatch(typeof(SaveSlot), nameof(SaveSlot.DlcRequired), MethodType.Getter)]
    static class SaveSlotIgnoresMark
    {
        static void Postfix(SaveSlot __instance, ref bool __result)
        {
            if (__result)
                __result = SaveMark.Required(__instance.SaveInfo?.DLC);
        }
    }
}
