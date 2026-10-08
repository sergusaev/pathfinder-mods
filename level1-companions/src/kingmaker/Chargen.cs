using System;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI.ServiceWindow;
using Kingmaker.UI._ConsoleUI.CharGen;
using Kingmaker.UI._ConsoleUI.Context;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.View;
using Kingmaker.Visual.CharacterSystem;

namespace Level1Companions
{
    // Console chargen in Respec mode of a story companion: LevelUpState already forbids name, voice, race, gender,
    // portrait and alignment, and the portrait, race, appearance and alignment phases are hidden, but the voice and
    // name phases are still shown (they cannot apply anything). Hide them too.
    // The controller comes through field injection: mcs cannot call AccessTools.FieldRef (a ref return).
    [HarmonyPatch(typeof(CharGenVM))]
    static class ChargenPhasesPatch
    {
        [HarmonyPatch("NeedVoicePhase"), HarmonyPostfix]
        static void Voice(LevelUpController ___m_LevelUpController, ref bool __result)
        {
            if (StoryRespec(___m_LevelUpController)) __result = false;
        }

        [HarmonyPatch("NeedNamePhase"), HarmonyPostfix]
        static void Name(LevelUpController ___m_LevelUpController, ref bool __result)
        {
            if (StoryRespec(___m_LevelUpController)) __result = false;
        }

        static bool StoryRespec(LevelUpController controller)
        {
            LevelUpState state = controller?.State;
            return state != null && state.Mode == LevelUpState.CharBuildMode.Respec && state.IsLoreCompanion;
        }
    }

    // The console chargen doll room builds the avatar from the unit's DollState (male or female chargen doll with
    // the class outfit), which story companions do not have, so a respec showed a bare chargen doll. Show the
    // companion's own model, as the PreGen mode does, and keep it: while that respec is open CharGenDollRoom ignores
    // doll state updates, which would put the chargen doll back on every build change.
    static class StoryDoll
    {
        internal static bool Active;

        internal static readonly FieldInfo PendingState = AccessTools.Field(typeof(CharGenDollRoom), "m_DollStateForUpdate");
    }

    [HarmonyPatch(typeof(CharcaterServiceContext), nameof(CharcaterServiceContext.HandleLevelUpStart))]
    static class ChargenDollPatch
    {
        static void Postfix(UnitDescriptor unit, LevelUpState.CharBuildMode mode)
        {
            try
            {
                StoryDoll.Active = false;
                if (mode != LevelUpState.CharBuildMode.Respec || unit == null || !Respec.IsStoryCompanion(unit)) return;
                DollRoom dollRoom = Game.Instance.UI.Common?.DollRoom;
                UnitEntityView view = unit.Blueprint.Prefab.Load();
                Character original = view != null ? view.GetComponent<Character>() : null;
                if (dollRoom == null || original == null)
                {
                    Main.Log.Log($"respec: no model for {unit.CharacterName} (doll room {dollRoom != null}, prefab {view != null})");
                    return;
                }
                CharGenDollRoom chargenRoom = dollRoom.GetComponent<CharGenDollRoom>();
                if (chargenRoom != null) StoryDoll.PendingState.SetValue(chargenRoom, null);
                Character avatar = dollRoom.CreateAvatar(original, unit.Blueprint.name);
                avatar.AnimationManager.IsInCombat = false;
                dollRoom.SetAvatar(avatar);
                StoryDoll.Active = true;
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
            }
        }
    }

    [HarmonyPatch(typeof(CharGenDollRoom), nameof(CharGenDollRoom.HandleDollStateUpdated))]
    static class ChargenDollStatePatch
    {
        static bool Prefix()
        {
            return !StoryDoll.Active;
        }
    }

    // Closing the chargen (confirmed or cancelled) ends the override; every HandleLevelUpStart sets it anew.
    [HarmonyPatch(typeof(CharGenVM), "DisposeImplementation")]
    static class ChargenCloseDollPatch
    {
        static void Postfix()
        {
            StoryDoll.Active = false;
        }
    }
}
