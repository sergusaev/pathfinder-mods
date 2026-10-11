using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Controllers.GlobalMap;
using Kingmaker.GameModes;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Blueprints;
using Kingmaker.Kingdom.Settlements;
using Kingmaker.Kingdom.Tasks;
using UnityEngine;

namespace TimeControl
{
    // Duration multipliers. Kingdom durations are computed by the game every time they are read (a running task ends on
    // StartedOn + Duration), so a new multiplier moves running events and projects too; a building gets its end day when
    // it is laid down, so only new buildings follow. Travel and camping scale the game time they advance.
    static class Factors
    {
        internal const float Min = 0.1f;
        internal const float Max = 10f;

        internal static float Clamp(float value)
        {
            if (float.IsNaN(value)) return 1f;
            return Mathf.Clamp((float)Math.Round(value, 3), Min, Max);
        }

        internal static int Scale(int days, float factor)
        {
            if (days <= 0 || Mathf.Approximately(factor, 1f)) return days;
            return Math.Max(1, Mathf.RoundToInt(days * factor));
        }

        internal static float For(BlueprintKingdomEventBase blueprint)
        {
            var s = Main.Settings;
            if (blueprint is BlueprintKingdomClaim || blueprint is BlueprintKingdomUpgrade) return s.ClaimFactor;
            var project = blueprint as BlueprintKingdomProject;
            if (project != null) return project.IsRankupProject ? s.RankupFactor : s.ProjectFactor;
            return s.EventFactor;
        }

        internal static string Text(float factor)
        {
            return "×" + factor.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // Sets the multipliers and shows what happened to the deadlines they move.
        internal static void ApplyAndReport(float[] values)
        {
            List<GameTimer> before = Main.InGame ? Timers.Collect() : new List<GameTimer>();
            float travel = Main.Settings.TravelFactor, rest = Main.Settings.RestFactor;
            for (int i = 0; i < values.Length; i++) Main.SetFactor(i, values[i]);
            Main.Settings.Save(Main.Mod);
            Main.Log(string.Format("coefficients: events {0}, projects {1}, claims {2}, rank-ups {3}, ruler {4}, buildings {5}, travel {6}, rest {7}",
                Text(Main.Settings.EventFactor), Text(Main.Settings.ProjectFactor), Text(Main.Settings.ClaimFactor), Text(Main.Settings.RankupFactor),
                Text(Main.Settings.RulerFactor), Text(Main.Settings.BuildingFactor), Text(Main.Settings.TravelFactor), Text(Main.Settings.RestFactor)));
            if (!Main.InGame) return;

            List<GameTimer> after = Timers.Collect();
            var old = new Dictionary<string, GameTimer>();
            foreach (var t in before) old[t.Key] = t;
            var lines = new List<string>();
            foreach (var t in after)
            {
                GameTimer was;
                if (!old.TryGetValue(t.Key, out was) || Math.Abs((was.Due - t.Due).TotalHours) < 0.5) continue;
                if (t.Remaining <= TimeSpan.Zero)
                    lines.Add(string.Format(S.CheckNow, t.Name, Timers.Period(was.Remaining)));
                else
                    lines.Add(string.Format(S.CheckChanged, t.Name, Timers.Period(was.Remaining), Timers.Period(t.Remaining)));
                Tracker.Rebase(t);
            }
            var body = new List<string> { S.CheckApplied, "" };
            if (lines.Count == 0) body.Add(S.CheckNone);
            body.AddRange(lines);
            if (!Mathf.Approximately(travel, Main.Settings.TravelFactor) || !Mathf.Approximately(rest, Main.Settings.RestFactor))
            {
                float mean = (Main.Settings.TravelFactor + Main.Settings.RestFactor) / 2f;
                string how = Mathf.Approximately(mean, 1f) ? S.AsBefore : mean < 1f ? S.Slower : S.Faster;
                body.Add("");
                body.Add(string.Format(S.CheckTravel, Main.Settings.TravelFactor.ToString("0.##", CultureInfo.InvariantCulture),
                    Main.Settings.RestFactor.ToString("0.##", CultureInfo.InvariantCulture), how));
                body.Add(S.Nearest);
                foreach (var t in after.Where(x => x.Remaining > TimeSpan.Zero).Take(5))
                    body.Add(t.Line);
            }
            MessageWindow.Show(S.CheckTitle, body, null, null);
        }
    }

    [HarmonyPatch(typeof(KingdomEvent), "CalculateResolutionTime")]
    static class ResolutionTimePatch
    {
        static void Postfix(KingdomEvent __instance, ref int __result)
        {
            __result = Factors.Scale(__result, Factors.For(__instance.EventBlueprint));
        }
    }

    [HarmonyPatch(typeof(KingdomEvent), "CalculateRulerTime")]
    static class RulerTimePatch
    {
        static void Postfix(ref int __result)
        {
            __result = Factors.Scale(__result, Main.Settings.RulerFactor);
        }
    }

    [HarmonyPatch(typeof(BuildingsCollection), "Build")]
    static class BuildTimePatch
    {
        static void Postfix(SettlementBuilding __result)
        {
            if (__result == null || KingdomState.Instance == null) return;
            int today = KingdomState.Instance.CurrentDay;
            int days = __result.FinishedOn - today;
            if (days > 0) __result.FinishedOn = today + Factors.Scale(days, Main.Settings.BuildingFactor);
        }
    }

    // Game time spent on the global map: hours = distance / speed.
    [HarmonyPatch(typeof(MapMovementController), "UpdateGameTime")]
    static class TravelTimePatch
    {
        static void Prefix(ref float deltaDistance)
        {
            deltaDistance *= Main.Settings.TravelFactor;
        }
    }

    // Camping advances the time in steps (preparation, roles, sleep, hunting); all of them happen in the rest mode.
    [HarmonyPatch(typeof(Game), "AdvanceGameTime")]
    static class RestTimePatch
    {
        static bool s_Logged;

        static void Prefix(ref TimeSpan delta)
        {
            if (Skipper.Running || Mathf.Approximately(Main.Settings.RestFactor, 1f)) return;
            var game = Game.Instance;
            if (game == null || game.CurrentMode != GameModeType.Rest) return;
            if (!s_Logged)
            {
                s_Logged = true;
                Main.Log("camping time scaled " + Factors.Text(Main.Settings.RestFactor));
            }
            delta = TimeSpan.FromTicks((long)(delta.Ticks * (double)Main.Settings.RestFactor));
        }
    }
}
