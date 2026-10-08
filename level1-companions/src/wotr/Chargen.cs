using System;
using System.Reflection;
using HarmonyLib;
using Kingmaker.UI.MVVM._VM.CharGen.Phases;
using Kingmaker.UI.MVVM._VM.CharGen.Phases.Class;
using Kingmaker.UI.ServiceWindow;
using Kingmaker.UnitLogic.Class.LevelUp;

namespace Level1Companions
{
    // The class phase shows the chargen doll (male or female with the class outfit) when the level up has a
    // DollState. A respec creates one for a story companion too, so the editor showed a bare chargen doll instead
    // of the companion. The phase is left as it is; the doll room shows the level up preview unit instead of that
    // DollState, as it does for a level up without one. Nothing of the DollState is written to the companion: the
    // appearance phases are hidden for it.
    static class StoryDoll
    {
        // The level up of the class phase last bound, when it is a story companion's respec.
        internal static LevelUpController Controller;

        static readonly MethodInfo GetController = AccessTools.PropertyGetter(typeof(CharGenPhaseBaseVM), "LevelUpController");

        internal static void Track(CharGenPhaseBaseVM phase)
        {
            LevelUpController controller = GetController.Invoke(phase, null) as LevelUpController;
            LevelUpState state = controller?.State;
            Controller = state != null && state.Mode == LevelUpState.CharBuildMode.Respec && state.IsLoreCompanion ? controller : null;
        }
    }

    [HarmonyPatch(typeof(CharGenClassPhaseVM), nameof(CharGenClassPhaseVM.DollState), MethodType.Getter)]
    static class ChargenClassDollPatch
    {
        static void Postfix(CharGenClassPhaseVM __instance)
        {
            try
            {
                StoryDoll.Track(__instance);
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
            }
        }
    }

    [HarmonyPatch(typeof(DollRoom), nameof(DollRoom.BindDollState))]
    static class DollRoomBindPatch
    {
        static bool Prefix(DollRoom __instance, DollState dollState)
        {
            try
            {
                LevelUpController controller = StoryDoll.Controller;
                if (dollState == null || controller == null || controller.Doll != dollState || controller.Preview == null) return true;
                __instance.SetupInfo(controller.Preview, true);
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
