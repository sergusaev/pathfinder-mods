using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.UI._ConsoleUI.CharGen.Phases;
using Kingmaker.UI._ConsoleUI.CharGen.Phases.Portrait;
using Kingmaker.UnitLogic.Class.LevelUp;
using UnityEngine;
using UnityModManagerNet;

namespace KingmakerFixes
{
    // The gamepad character creation lists only the built-in portraits; the custom ones from the Portraits folder are
    // appended after them. UnitUISettings.SetPortrait stores a portrait with custom Data as m_CustomPortrait, so saves
    // stay vanilla. Formerly the separate mod ConsoleCustomPortraits: while that one is on, it adds them instead.
    [HarmonyPatch(typeof(CharGenPortraitPhaseVM), MethodType.Constructor, new Type[] { typeof(LevelUpController) })]
    static class CustomPortraitsInGamepadCharGen
    {
        const string OldModId = "ConsoleCustomPortraits";
        static readonly Dictionary<string, BlueprintPortrait> Cache = new Dictionary<string, BlueprintPortrait>();
        static bool s_ToldOldMod;

        static void Postfix(CharGenPortraitPhaseVM __instance)
        {
            try
            {
                var old = UnityModManager.FindMod(OldModId);
                if (old != null && old.Active)
                {
                    if (!s_ToldOldMod)
                        Main.Log.Log("custom portraits: left to the old mod " + OldModId + ", it can be removed");
                    s_ToldOldMod = true;
                    return;
                }
                var list = __instance.SelectorGroupVM?.VisibleCollection;
                if (list == null) return;
                string[] ids = CustomPortraitsManager.Instance.GetExistingCustomPortraitIds();
                Array.Sort(ids, StringComparer.OrdinalIgnoreCase);
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
                }
                Main.Log.Log("custom portraits: added " + ids.Length);
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
            }
        }
    }
}
