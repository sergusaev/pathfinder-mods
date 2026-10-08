using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.CharGen;
using Kingmaker.Blueprints.Classes;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Root;

namespace Level1Companions
{
    // Companions get preset levels from AddClassLevels only up to ClassLevelLimit; the rest is left to the player.
    // Lowering the limit of every story companion to 1 makes the player build them from level 2.
    // Respec (Player.RespecCompanion) rebuilds a companion from the same limit, so it also goes back to level 1.
    [HarmonyPatch(typeof(LibraryScriptableObject), nameof(LibraryScriptableObject.LoadDictionary))]
    static class LoadDictionaryPatch
    {
        static bool s_Done;

        static void Postfix(LibraryScriptableObject __instance)
        {
            if (s_Done) return;
            s_Done = true;
            try
            {
                Patch(__instance.GetAllBlueprints());
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
            }
        }

        static void Patch(List<BlueprintScriptableObject> all)
        {
            // Game.Instance is not up yet when the library loads, so the root comes from the library itself.
            BlueprintRoot root = all.OfType<BlueprintRoot>().FirstOrDefault();
            if (root == null)
            {
                Main.Log.Error("BlueprintRoot not found, nothing patched");
                return;
            }
            Main.XpTable = root.Progression.XPTable.Bonuses;

            // Recruitable blueprints are named <Name>_Companion; the NPC forms (_Capital, _Neutral, _Cutscene...) are
            // separate blueprints and keep their levels. CheatRoot.DefaultParties is a debug list and is not used.
            var listed = new HashSet<BlueprintUnit>(root.Achievements?.FollowersCompanions ?? new BlueprintUnit[0]);

            // Mercenaries and pregens: chargen units built by the player anyway.
            var excluded = new HashSet<BlueprintUnit> { root.CustomCompanion };
            excluded.UnionWith(root.CharGen.CustomCompanions ?? new List<BlueprintUnit>());
            excluded.UnionWith(root.CharGen.Pregens ?? new BlueprintUnit[0]);
            excluded.UnionWith(root.CharGen.VarnholdPregens ?? new BlueprintUnit[0]);

            foreach (BlueprintUnit unit in all.OfType<BlueprintUnit>())
            {
                ClassLevelLimit limit = unit.GetComponent<ClassLevelLimit>();
                if (limit == null) continue;
                AddClassLevels[] levels = unit.GetComponents<AddClassLevels>()
                    .Concat((unit.AddFacts ?? new BlueprintUnitFact[0]).SelectMany(f => f.GetComponents<AddClassLevels>()))
                    .ToArray();
                string info = $"{unit.name} {unit.AssetGuid} limit {limit.LevelLimit} [" +
                    string.Join(" + ", levels.Select(l => $"{l.CharacterClass?.name} {l.Levels}").ToArray()) + "]";

                bool companion = listed.Contains(unit) || unit.name.EndsWith("_Companion", StringComparison.OrdinalIgnoreCase);
                string skip = null;
                if (!companion) skip = "not a companion";
                else if (excluded.Contains(unit) || unit.GetComponent<PregenUnitComponent>() != null) skip = "mercenary or pregen";
                else if (levels.Any(l => l.CharacterClass == root.Progression.AnimalCompanion)) skip = "animal companion";
                else if (limit.LevelLimit <= Main.Limit) skip = "limit already low";

                if (skip != null)
                {
                    if (companion) Main.Log.Log($"skip ({skip}): {info}");
                    continue;
                }
                Main.Patched[unit] = limit.LevelLimit;
                limit.LevelLimit = Main.Limit;
                Main.Log.Log($"patched: {info} -> {Main.Limit}");
            }
            Main.Log.Log($"Companions patched: {Main.Patched.Count}");
        }
    }
}
