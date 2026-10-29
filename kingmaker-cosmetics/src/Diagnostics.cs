using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kingmaker.UI.ServiceWindow;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;

namespace KingmakerCosmetics
{
    // Before the character creation doll loads its entities, the mod's ones are made if not yet, and kept library
    // entries dropped by anything else are put back.
    [HarmonyPatch(typeof(CharGenDollRoom), "DollStateUpdated")]
    static class EnsureBeforeDoll
    {
        static void Prefix(DollState dollState)
        {
            try
            {
                foreach (var link in dollState.CollectEntitiesLinks())
                    if (link != null)
                        Styles.Ensure(link.AssetId);
                Styles.Restore("character creation doll");
                // The styles next to the one chosen are made while the doll is looked at.
                var race = dollState.Race;
                var hair = race == null || dollState.Hair == null ? null
                    : (dollState.Gender == Kingmaker.Blueprints.Gender.Male ? race.MaleOptions : race.FemaleOptions).Hair;
                int at = hair == null ? -1 : Array.FindIndex(hair, l => l != null && l.AssetId == dollState.Hair.AssetId);
                if (at >= 0)
                    foreach (var step in new[] { 1, -1, 2, -2 })
                    {
                        var next = hair[(at + step + hair.Length * 2) % hair.Length];
                        if (next != null)
                            Styles.Soon(next.AssetId);
                    }
            }
            catch (Exception ex)
            {
                Main.Log.Error("character creation doll: " + ex);
            }
        }
    }

    // The same for a saved character: its entities are preloaded with the area and loaded when its view is made.
    [HarmonyPatch]
    static class EnsureBeforeSavedDoll
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(DollData), nameof(DollData.PreloadEquipmentEntities));
            yield return AccessTools.Method(typeof(DollData), nameof(DollData.CreateUnitView));
        }

        static void Prefix(DollData __instance)
        {
            try
            {
                foreach (var id in __instance.EquipmentEntityIds)
                    Styles.Ensure(id);
                Styles.Restore("saved doll");
            }
            catch (Exception ex)
            {
                Main.Log.Error("saved doll: " + ex);
            }
        }
    }

    // While the styles are tried: the hair the character creation doll gets when it is one of the mod's, and any link
    // it got empty.
    [HarmonyPatch(typeof(CharGenDollRoom), "DollStateLoadedAllEquipmentEntities")]
    static class DollDiagnostics
    {
        static void Prefix(DollState dollState)
        {
            try
            {
                var hair = dollState.Hair;
                if (hair != null && Styles.OwnIds.Contains(hair.AssetId))
                    Main.Log.Log($"doll: {dollState.Race?.name} {dollState.Gender}, hair {hair.AssetId} ({hair.Load()?.name ?? "none"})");
                foreach (var link in dollState.CollectEntitiesLinks())
                    if (link != null && link.Load() == null)
                        Main.Log.Log($"doll: link {link.AssetId} is empty (in library: {link.IsLoaded()})"
                            + (Styles.KeptIds.Contains(link.AssetId) ? ": " + Styles.Describe(link.AssetId) : ""));
            }
            catch (Exception ex)
            {
                Main.Log.Error("doll diagnostics: " + ex);
            }
        }
    }
}
