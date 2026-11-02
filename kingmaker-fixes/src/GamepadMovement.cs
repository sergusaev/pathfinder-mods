using System;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI._ConsoleUI.LocalMap;
using Kingmaker.UI._ConsoleUI.Models;
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.Utility;
using Kingmaker.View;

namespace KingmakerFixes
{
    // Walking with the stick (gamepad interface, out of combat) moves the selected character by UnitMoveContiniously,
    // the others by move commands to their places in the formation (InGameInputLayer.MakeCompanionsFollow). The others go
    // at the speed of the slowest of the group (ClickGroundHandler.MoveSelectedUnitsToPoint: SpeedLimit, at least 30 ft),
    // 1.8 times that when the walk is accelerated; the character under the stick goes at its own speed, times the 1.25
    // that UnitMovementAgentContinious adds on top. So it walks away from the group and the formation stretches; with the
    // keyboard and mouse every character of the group gets the same limit. Here the character under the stick goes no
    // faster than the group: the group's limit, accelerated as the group is, times how far the stick is pushed.
    [HarmonyPatch(typeof(UnitEntityData), nameof(UnitEntityData.CurrentSpeedMps), MethodType.Getter)]
    static class StickWalkKeepsGroupSpeed
    {
        // UnitMovementAgentContinious goes at MaxSpeed (this speed) times 1.25.
        const float AgentFactor = 1.25f;
        const float AcceleratedFactor = 1.8f;

        static void Postfix(UnitEntityData __instance, ref float __result)
        {
            try
            {
                if (!Game.Instance.IsControllerGamepad) return;
                var agent = __instance.View != null ? __instance.View.AgentOverride as UnitMovementAgentContinious : null;
                if (agent == null || __instance.IsInCombat) return;
                var move = __instance.Commands.MoveContiniously;
                if (move == null) return;
                var selected = Game.Instance.UI.SelectionManager.SelectedUnits;
                if (selected == null || selected.Count < 2 || !selected.Contains(__instance)) return;
                float group = float.MaxValue;
                foreach (var unit in selected)
                    if (unit != null) group = Math.Min(group, unit.ModifiedSpeedMps);
                group = Math.Max(group, 30.Feet().Meters / 3f);
                if (move.IsAccelerated) group *= AcceleratedFactor;
                float limit = group * agent.DirectionFromControllerMagnitude / AgentFactor;
                if (__result > limit) __result = limit;
            }
            catch (Exception e)
            {
                if (!s_Logged)
                {
                    s_Logged = true;
                    Main.Log.Error("stick walk speed: " + e);
                }
            }
        }

        static bool s_Logged;
    }

    // The stick runs the character only while it is pushed at least 0.95 of the way (InGameInputLayer.UpdateMovement:
    // Accelerate at >= 0.95, Deaccelerate below), checked every frame. An Xbox controller on Windows gives less than
    // that on part of its rim, and turning the stick to another direction takes it near the centre (0.06-0.5 in the
    // log): the character dropped to a walk for a moment. Now the run starts at 0.85, lasts while the stick is past
    // half way, and lasts through a dip of up to 0.35 s (the stick crossing the centre, also into a new move command);
    // a light push from standing still walks. The first dips that kept the run are written to the log ("stick run:").
    [HarmonyPatch(typeof(UnitCommand), nameof(UnitCommand.Deaccelerate))]
    static class StickRunHysteresis
    {
        const float Start = 0.85f, Keep = 0.5f, Grace = 0.35f;
        static int s_Logged;
        static float s_Lowest = 1f;
        internal static float LastRun = -100f;

        static bool Prefix(UnitCommand __instance)
        {
            try
            {
                if (!(__instance is UnitMoveContiniously)) return true;
                var view = __instance.Executor != null ? __instance.Executor.View : null;
                var agent = view != null ? view.AgentOverride as UnitMovementAgentContinious : null;
                if (agent == null) return true;
                float m = agent.DirectionFromControllerMagnitude, now = UnityEngine.Time.unscaledTime;
                bool run;
                if (m >= Start || (__instance.IsAccelerated && m >= Keep)) { run = true; LastRun = now; }
                else run = m > 0f && now - LastRun < Grace;
                if (!run) return true;
                if (m < 0.95f && m < s_Lowest - 0.02f && s_Logged < 8)
                {
                    s_Lowest = m;
                    s_Logged++;
                    Main.Log.Log(string.Format("stick run: kept at stick {0:F2} (the game walks below 0.95)", m));
                }
                if (!__instance.IsAccelerated)
                {
                    float last = LastRun;
                    __instance.Accelerate();
                    LastRun = last;   // running on in the grace does not prolong it
                }
                return false;
            }
            catch (Exception e)
            {
                if (s_Logged < 100) Main.Log.Error("stick run: " + e);
                s_Logged = 100;
                return true;
            }
        }
    }

    // The game's own run (stick at 0.95 and more) counts for the grace above too.
    [HarmonyPatch(typeof(UnitCommand), nameof(UnitCommand.Accelerate))]
    static class StickRunSeen
    {
        static void Postfix(UnitCommand __instance)
        {
            if (__instance is UnitMoveContiniously && __instance.IsAccelerated) StickRunHysteresis.LastRun = UnityEngine.Time.unscaledTime;
        }
    }

    // The gamepad local map sends the selected characters to the point under its cursor (A), but the camera follows
    // them only if it still followed the character before: moving the map with the right stick (LocalMapView.OnMoveCamera)
    // lets the camera go, and so does the cursor at the edge of the screen, and nothing takes it up again. The view
    // outline on the map then stays where it was while the characters walk off. After the order the camera follows the
    // selected character again, as it does when the character is walked with the stick.
    [HarmonyPatch(typeof(LocalMapView), "OnConfirm")]
    static class LocalMapOrderFollowsUnits
    {
        static void Postfix()
        {
            try
            {
                var unit = UnitSelectionManager.Instance?.CurrentSelectUnit?.Value ?? Game.Instance.UI.SelectionManager?.FirstSelectUnit;
                if (unit != null) Game.Instance.CameraController?.Follower?.Follow(unit);
            }
            catch (Exception e) { Main.Log.Error("local map follow: " + e); }
        }
    }
}
