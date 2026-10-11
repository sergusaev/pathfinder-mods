using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.AreaLogic.QuestSystem;
using Kingmaker.GameModes;
using Kingmaker.Kingdom;

namespace TimeControl
{
    class SkipPlan
    {
        public int Hours;
        // What comes during the skip.
        public List<GameTimer> Passed = new List<GameTimer>();
        // Deadlines after which something fails, and the player's own deadlines: the skip may not pass them.
        public List<string> Blockers = new List<string>();
        public int MaxSafeHours;

        public bool Allowed
        {
            get { return Blockers.Count == 0; }
        }
    }

    // Skipping time as the game's own cheat does (AdvanceGameTime, then MatchTimeOfDay), one day at a time so the kingdom
    // runs every day of its timeline and timed quests are checked after each day, as if the time had passed in play.
    static class Skipper
    {
        internal static bool Running;

        internal static bool CanSkipNow
        {
            get
            {
                var game = Game.Instance;
                if (!Main.InGame || game.Player.IsInCombat || game.CutsceneLock) return false;
                var mode = game.CurrentMode;
                return mode == GameModeType.Default || mode == GameModeType.Pause || mode == GameModeType.GlobalMap;
            }
        }

        internal static SkipPlan Check(int hours)
        {
            var plan = new SkipPlan { Hours = hours };
            TimeSpan now = Game.Instance.Player.GameTime;
            TimeSpan end = now + TimeSpan.FromHours(hours);
            TimeSpan firstBlock = TimeSpan.MaxValue;
            foreach (var t in Timers.Collect())
            {
                if (t.Due <= now) continue;
                if (t.Due <= end) plan.Passed.Add(t);
                var tracked = Tracker.Get(t.Key);
                if (t.Critical && t.Due <= end)
                {
                    plan.Blockers.Add(t.Line);
                    if (t.Due < firstBlock) firstBlock = t.Due;
                }
                if (tracked != null && tracked.DeadlineHours > 0)
                {
                    TimeSpan deadline = t.Due - TimeSpan.FromHours(tracked.DeadlineHours);
                    if (deadline > now && deadline <= end)
                    {
                        plan.Blockers.Add(string.Format(S.LeftToDeadline, t.Name, Timers.Period(deadline - now)) + " (" + S.TrackedDeadline + ")");
                        if (deadline < firstBlock) firstBlock = deadline;
                    }
                }
            }
            // Up to an hour before the first deadline: the skip stops while it can still be met.
            plan.MaxSafeHours = firstBlock == TimeSpan.MaxValue ? hours : Math.Max(0, (int)Math.Floor((firstBlock - now).TotalHours) - 1);
            return plan;
        }

        internal static void Run(int hours)
        {
            if (hours <= 0 || !CanSkipNow) return;
            var game = Game.Instance;
            TimeSpan start = game.Player.GameTime;
            Main.Log(string.Format("skipping {0} h from {1}", hours, Timers.Date(start)));
            Running = true;
            try
            {
                int left = hours;
                while (left > 0)
                {
                    int step = Math.Min(left, 24);
                    left -= step;
                    game.AdvanceGameTime(TimeSpan.FromHours(step));
                    if (KingdomState.Founded) new KingdomTimelineManager().UpdateTimeline();
                    foreach (Quest quest in game.Player.QuestBook.Quests.ToList())
                        foreach (QuestObjective objective in quest.Objectives.ToList())
                            objective.HandleTimePassed();
                }
                game.MatchTimeOfDay();
            }
            catch (Exception e)
            {
                Main.Error(e, "skipping time");
            }
            finally
            {
                Running = false;
            }
            Tracker.Scan();
        }
    }
}
