using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Kingmaker.Assets.UI.LevelUp;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
using Kingmaker.ElementsSystem;
using Kingmaker.UnitLogic;
using UnityEngine;
using UnityModManagerNet;

namespace Level1Companions
{
    public static class Main
    {
        internal const int Limit = 1;

        internal static UnityModManager.ModEntry.ModLogger Log;
        // Companion blueprint -> its original ClassLevelLimit.LevelLimit.
        internal static readonly Dictionary<BlueprintUnit, int> Patched = new Dictionary<BlueprintUnit, int>();
#if KINGMAKER
        internal static int[] XpTable;
#endif

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Log = modEntry.Logger;
            modEntry.OnGUI = OnGUI;
            new Harmony(modEntry.Info.Id).PatchAll(typeof(Main).Assembly);
            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.Label($"Story companions join at level {Limit}; respec also returns them to level {Limit}.");
            if (GUILayout.Button("Respec (free)", GUILayout.Width(320)))
                Respec.Open();
            GUILayout.Label($"A story companion goes back to level {Limit} and keeps its class, stats and abilities of level {Limit}. The main character and mercenaries are rebuilt from scratch, everything can be changed, as by the game's respec.");
#if WOTR
            GUILayout.Label("Mythic levels are not touched: a respec keeps the mythic experience and the mythic path.");
            GUILayout.Label("The respec window closes with \"Complete\" as soon as the respec is applied; the levels left are taken later with the portrait button.");
#endif
            if (Respec.Status != null)
                GUILayout.Label(Respec.Status);
            GUILayout.Label("Set Auto Level Up to \"Off\" (or \"Main character only\") in the difficulty settings, otherwise the game levels companions up by their recommended build.");
            GUILayout.Label($"Companion blueprints patched: {Patched.Count} (preset level limit -> {Limit}):");
            foreach (var p in Patched.OrderBy(p => p.Key.name))
                GUILayout.Label($"    {p.Key.name}: {p.Value}");
        }
    }

    // AddClassLevel raises Experience to the XP of the reached level, and recruiting matches the main character's XP
    // only when every companion gets experience. With "only active companions receive experience" a patched companion
    // would join with level 1 XP, so give it the XP of the level its vanilla limit would have reached.
    // Preset levels come from several AddClassLevels (on the unit and on its facts), so the vanilla level is tracked
    // across calls the way AddClassLevels.LevelUp counts it: add levels while below the limit.
#if KINGMAKER
    [HarmonyPatch(typeof(AddClassLevels), "LevelUp", new Type[] { typeof(UnitDescriptor), typeof(int), typeof(bool) })]
#else
    // WotR: the static overload does the work and runs only for levels not applied yet (AddClassLevelsData.Applied
    // is checked before it); it reads the limit from the unit's original blueprint and skips mythic classes here.
    [HarmonyPatch(typeof(AddClassLevels), nameof(AddClassLevels.LevelUp), new Type[] { typeof(AddClassLevels), typeof(UnitDescriptor), typeof(int), typeof(UnitFact) })]
#endif
    static class AddClassLevelsPatch
    {
        class VanillaLevel { public int Value; }

        static readonly ConditionalWeakTable<UnitDescriptor, VanillaLevel> s_Vanilla = new ConditionalWeakTable<UnitDescriptor, VanillaLevel>();
        static readonly MethodInfo SetExperience =
            AccessTools.PropertySetter(typeof(UnitProgressionData), nameof(UnitProgressionData.Experience));

#if KINGMAKER
        static void Prefix(UnitDescriptor unit, int levels, bool fromFact, bool ___m_Applied, out int __state)
        {
            __state = -1;
            if (fromFact && ___m_Applied) return;
            __state = Track(unit, unit?.Blueprint, levels, ElementsContext.GetData<DefaultBuildData>() != null);
        }
#else
        static void Prefix(AddClassLevels c, UnitDescriptor unit, int levels, out int __state)
        {
            __state = -1;
            if (c?.CharacterClass == null || c.CharacterClass.IsMythic) return;
            __state = Track(unit, unit?.OriginalBlueprint, levels, ContextData<DefaultBuildData>.Current != null);
        }
#endif

        static int Track(UnitDescriptor unit, BlueprintUnit blueprint, int levels, bool defaultBuild)
        {
            try
            {
                if (unit == null || blueprint == null || defaultBuild || Respec.InProgress) return -1;
                if (!Main.Patched.TryGetValue(blueprint, out int originalLimit)) return -1;
                VanillaLevel vanilla;
                if (!s_Vanilla.TryGetValue(unit, out vanilla))
                {
                    vanilla = new VanillaLevel { Value = unit.Progression.CharacterLevel };
                    s_Vanilla.Add(unit, vanilla);
                }
                if (vanilla.Value < originalLimit)
                    vanilla.Value = Math.Min(originalLimit, vanilla.Value + levels);
                return vanilla.Value;
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
                return -1;
            }
        }

        static void Postfix(UnitDescriptor unit, int __state)
        {
            if (__state < 0) return;
            try
            {
#if KINGMAKER
                int[] table = Main.XpTable;
#else
                int[] table = unit.Progression.ExperienceTable?.Bonuses;
#endif
                if (table == null) return;
                int level = Math.Min(__state, Math.Min(20, table.Length - 1));
                int xp = table[level];
                if (unit.Progression.Experience < xp)
                {
                    SetExperience.Invoke(unit.Progression, new object[] { xp });
                    Main.Log.Log($"{unit.Blueprint.name}: experience {xp} (vanilla level {level}, now level {unit.Progression.CharacterLevel})");
                }
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
            }
        }
    }
}
