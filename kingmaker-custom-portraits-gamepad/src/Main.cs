using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.UI._ConsoleUI.CharGen.Phases;
using Kingmaker.UI._ConsoleUI.CharGen.Phases.Portrait;
using Kingmaker.UnitLogic.Class.LevelUp;
using UnityEngine;
using UnityModManagerNet;

namespace ConsoleCustomPortraits
{
    public static class Main
    {
        internal static UnityModManager.ModEntry.ModLogger Log;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Log = modEntry.Logger;
            new Harmony(modEntry.Info.Id).PatchAll(typeof(Main).Assembly);
            return true;
        }
    }

    // Gamepad chargen lists only built-in blueprints; append custom portraits from the Portraits folder.
    // UnitUISettings.SetPortrait stores any blueprint with custom Data as m_CustomPortrait, so saves stay vanilla.
    [HarmonyPatch(typeof(CharGenPortraitPhaseVM), MethodType.Constructor, new Type[] { typeof(LevelUpController) })]
    static class PortraitPhaseCtorPatch
    {
        static readonly Dictionary<string, BlueprintPortrait> Cache = new Dictionary<string, BlueprintPortrait>();

        static void Postfix(CharGenPortraitPhaseVM __instance)
        {
            try
            {
                var list = __instance.SelectorGroupVM?.VisibleCollection;
                if (list == null) return;
                string[] ids = CustomPortraitsManager.Instance.GetExistingCustomPortraitIds();
                Array.Sort(ids, StringComparer.OrdinalIgnoreCase);
                int added = 0;
                foreach (string id in ids)
                {
                    BlueprintPortrait bp;
                    if (!Cache.TryGetValue(id, out bp) || bp == null)
                    {
                        bp = ScriptableObject.CreateInstance<BlueprintPortrait>();
                        bp.name = "CustomPortrait_" + id;
                        bp.Data = new PortraitData(id);
                        Cache[id] = bp;
                    }
                    list.Add(new CharGenPortraitSelectorItemVM(bp));
                    added++;
                }
                Main.Log?.Log("Added custom portraits: " + added);
            }
            catch (Exception e)
            {
                Main.Log?.Error(e.ToString());
            }
        }
    }
}
