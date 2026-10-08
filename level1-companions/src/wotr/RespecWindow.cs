using System;
using System.Reflection;
using HarmonyLib;
using Kingmaker.UI.MVVM._VM.CharGen;

namespace Level1Companions
{
    // After a respec WotR keeps the respec window open until every level is spent: its Complete button is enabled
    // only when the unit can level up neither normally nor mythically, and closing the editor reopens the window.
    // Unspent levels are normal for any character (the level up button on the portrait), so once the respec itself
    // is applied (the first level up confirmed, CanStop false) the window can be closed with levels left.
    // The forced level up window (HandleForceLevelUp, IsRespec false) is left as it is.
    [HarmonyPatch(typeof(RespecWindowVM), "UpdateProperties")]
    static class RespecWindowFinishPatch
    {
        static void Postfix(RespecWindowVM __instance)
        {
            if (__instance.IsRespec && !__instance.CanStop && !__instance.IsFinished.Value)
                __instance.IsFinished.Value = true;
        }
    }

    [HarmonyPatch(typeof(RespecWindowVM), nameof(RespecWindowVM.Complete))]
    static class RespecWindowCompletePatch
    {
        static readonly FieldInfo EndAction = AccessTools.Field(typeof(RespecWindowVM), "m_EndAction");

        static bool Prefix(RespecWindowVM __instance)
        {
            if (!__instance.IsRespec || __instance.CanStop) return true;
            try
            {
                Main.Log.Log($"respec: window closed with levels left for {__instance.CurrentUnit.Value?.CharacterName}");
                (EndAction.GetValue(__instance) as Action)?.Invoke();
                return false;
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
                return true;
            }
        }
    }
}
