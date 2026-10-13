using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.AreaLogic.QuestSystem;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Quests.Logic;
using Kingmaker.GameModes;
using Kingmaker.Kingdom;
using Kingmaker.Kingdom.Artisans;
using Kingmaker.Kingdom.Blueprints;
using Kingmaker.Kingdom.Settlements;
using Kingmaker.Kingdom.Tasks;

namespace TimeControl
{
    // One countdown of the game: when something happens on its own unless the player acts first.
    class GameTimer
    {
        public string Key;
        // What the list shows: the quest, the event, the building.
        public string Title;
        // What kind of countdown it is, in full: shown under the list and in the alerts.
        public string Name;
        // Absolute game time (Player.GameTime) when it happens.
        public TimeSpan Due;
        // The game shows no countdown for it.
        public bool Hidden;
        // Something fails when it runs out.
        public bool Critical;
        // Follows the kingdom duration coefficients.
        public bool Kingdom;

        public TimeSpan Remaining
        {
            get { return Due - Game.Instance.Player.GameTime; }
        }

        // "До события «X» осталось 3 дня и 5 часов"
        public string Line
        {
            get { return Remaining > TimeSpan.Zero ? string.Format(S.Left, Short, Timers.Period(Remaining)) : string.Format(S.Due, Short); }
        }

        public string Short
        {
            get { return string.IsNullOrEmpty(Title) ? Name : Title; }
        }
    }

    static class Timers
    {
        static readonly System.Reflection.FieldInfo s_ObjectiveStartField = AccessTools.Field(typeof(QuestObjective), "m_ObjectiveStartTime");

        internal static string Period(TimeSpan span)
        {
            if (span < TimeSpan.Zero) span = TimeSpan.Zero;
            try
            {
                return Game.Instance.BlueprintRoot.Calendar.GetPeriodString(span);
            }
            catch (Exception)
            {
                return S.Span((int)span.TotalHours);
            }
        }

        internal static string Date(TimeSpan gameTime)
        {
            try
            {
                return Game.Instance.BlueprintRoot.Calendar.GetDateText(gameTime);
            }
            catch (Exception)
            {
                return "";
            }
        }

        // Game time when the kingdom processes the given kingdom day: its timeline runs every day once the integer day of
        // the game time minus StartDay reaches it.
        static TimeSpan KingdomDay(int day)
        {
            return TimeSpan.FromDays(KingdomState.Instance.StartDay + day);
        }

        // All countdowns, soonest first.
        internal static List<GameTimer> Collect()
        {
            var list = new List<GameTimer>();
            try
            {
                Quests(list);
            }
            catch (Exception e)
            {
                Main.Error(e, "quest timers");
            }
            try
            {
                if (KingdomState.Founded && KingdomState.Instance != null) Kingdom(list);
            }
            catch (Exception e)
            {
                Main.Error(e, "kingdom timers");
            }
            list.Sort((a, b) => a.Due.CompareTo(b.Due));
            return list;
        }

        static string Guid(BlueprintScriptableObject blueprint)
        {
            return blueprint == null ? "-" : blueprint.AssetGuid;
        }

        static void Quests(List<GameTimer> list)
        {
            TimeSpan now = Game.Instance.Player.GameTime;
            foreach (Quest quest in Game.Instance.Player.QuestBook.Quests)
            {
                if (quest.State != QuestState.Started) continue;
                string questName = quest.Blueprint.Title;
                foreach (QuestObjective objective in quest.Objectives)
                {
                    if (objective.State != QuestObjectiveState.Started) continue;
                    var fail = objective.TimeToFail;
                    string title = objective.Blueprint.GetTitile();
                    if (string.IsNullOrEmpty(title)) title = objective.Blueprint.name;
                    if (fail.HasValue)
                    {
                        list.Add(new GameTimer
                        {
                            Key = "qo:" + Guid(objective.Blueprint),
                            Title = questName,
                            Name = string.Format(S.ObjectiveFail, title, questName),
                            Due = now + fail.Value,
                            Hidden = !objective.IsVisible,
                            Critical = true
                        });
                    }
                    int index = 0;
                    // As QuestObjective.HandleTimePassed reads them.
                    foreach (TimedObjectiveTrigger trigger in objective.SelectComponents<TimedObjectiveTrigger>())
                    {
                        index++;
                        if (trigger.DidTrigger || s_ObjectiveStartField == null) continue;
                        var start = (TimeSpan)s_ObjectiveStartField.GetValue(objective);
                        list.Add(new GameTimer
                        {
                            Key = "qt:" + Guid(objective.Blueprint) + ":" + index,
                            Title = questName,
                            Name = string.Format(S.QuestTrigger, questName),
                            Due = start + TimeSpan.FromDays(trigger.DaysTillTrigger),
                            Hidden = true
                        });
                    }
                }
            }
        }

        static string EventName(KingdomEvent e)
        {
            string name = e.EventBlueprint.DisplayName;
            if (string.IsNullOrEmpty(name)) name = e.EventBlueprint.name;
            if (e.Region != null)
            {
                string region = e.Region.Blueprint.LocalizedName;
                if (!string.IsNullOrEmpty(region)) name += " — " + region;
            }
            return name;
        }

        static void Kingdom(List<GameTimer> list)
        {
            var k = KingdomState.Instance;
            int today = k.CurrentDay;
            foreach (KingdomEvent e in k.ActiveEvents)
            {
                if (e.IsFinished || e.EventBlueprint == null) continue;
                string key = "ke:" + Guid(e.EventBlueprint) + ":" + Guid(e.Region == null ? null : e.Region.Blueprint);
                string name = EventName(e);
                bool project = e.EventBlueprint is BlueprintKingdomProject;
                if (e.IsPlanned)
                {
                    list.Add(new GameTimer { Key = key + ":start", Title = name, Name = string.Format(S.EventStart, name), Due = KingdomDay(e.StartedOn), Hidden = true });
                    continue;
                }
                KingdomTaskEvent task = e.AssociatedTask;
                if (task != null && task.IsStarted && task.EndsOn > today)
                {
                    list.Add(new GameTimer
                    {
                        Key = key + ":task",
                        Title = name,
                        Name = string.Format(project ? S.ProjectEnd : S.TaskEnd, name),
                        Due = KingdomDay(task.EndsOn),
                        Kingdom = true
                    });
                }
                if (e.EventBlueprint.ResolveAutomatically)
                {
                    int end = e.StartedOn + e.CalculateResolutionTime();
                    list.Add(new GameTimer { Key = key + ":auto", Title = name, Name = string.Format(S.AutoResolve, name), Due = KingdomDay(Math.Max(end, today + 1)), Kingdom = true });
                }
                var recurrence = e.EventBlueprint.GetComponent<EventRecurrence>();
                if (recurrence != null && recurrence.RecurrencePeriod > 0 && (task == null || !task.IsFinished))
                {
                    int next = today + 1;
                    int period = recurrence.RecurrencePeriod;
                    int phase = ((next - e.StartedOn) % period + period) % period;
                    if (phase != 0) next += period - phase;
                    if (next <= e.StartedOn && !recurrence.TickOnStart) next += period;
                    list.Add(new GameTimer { Key = key + ":recur", Title = name, Name = string.Format(S.Recurrence, name), Due = KingdomDay(next) });
                }
                // KingdomTimelineManager.FailIgnoredEvents: an event nobody took fails on the next day; at the start of a month an
                // event without an advisor fails when it began more than 10 days earlier (after free advisors are assigned).
                if (!project && recurrence == null && e.NeedsTask && (task == null || !task.HasAssignedLeader))
                {
                    int fail;
                    // Ignored by the player: the failure is the player's choice, not a deadline to keep.
                    if (task == null || (e.HistoryEntry != null && e.HistoryEntry.WasIgnored))
                    {
                        list.Add(new GameTimer { Key = key + ":ignored", Title = name, Name = string.Format(S.IgnoredEvent, name), Due = KingdomDay(today + 1), Hidden = true });
                        continue;
                    }
                    else
                    {
                        fail = today + k.DaysTillNextMonth;
                        if (fail == today) fail += 1;
                        while (!(e.StartedOn < fail - 10)) fail = k.ToDay(k.ToDate(fail).AddMonths(1));
                    }
                    list.Add(new GameTimer { Key = key + ":fail", Title = name, Name = string.Format(S.IgnoredFail, name), Due = KingdomDay(fail), Hidden = true, Critical = true });
                }
            }

            var timeline = Game.Instance.BlueprintRoot.Kingdom.Timeline;
            if (timeline != null && timeline.Entries != null && timeline.Entries.Entries != null)
            {
                foreach (var entry in timeline.Entries.Entries)
                {
                    if (entry == null || entry.Type != BlueprintKingdomEventTimeline.EntryType.EventHappens || entry.Event == null || entry.Day <= today) continue;
                    string name = entry.Event.DisplayName;
                    if (string.IsNullOrEmpty(name)) name = entry.Event.name;
                    list.Add(new GameTimer { Key = "tl:" + entry.Day + ":" + Guid(entry.Event), Title = name, Name = string.Format(S.Timeline, name), Due = KingdomDay(entry.Day), Hidden = true });
                }
            }

            foreach (KingdomBuff buff in k.ActiveBuffs.Enumerable)
            {
                if (buff.EndsOnDay <= 0 || buff.EndsOnDay <= today) continue;
                string name = buff.Blueprint.DisplayName;
                if (string.IsNullOrEmpty(name)) name = buff.Blueprint.name;
                list.Add(new GameTimer { Key = "kb:" + Guid(buff.Blueprint) + ":" + buff.EndsOnDay, Title = name, Name = string.Format(S.BuffEnd, name), Due = KingdomDay(buff.EndsOnDay) });
            }

            foreach (RegionState region in k.Regions)
            {
                SettlementState settlement = region.Settlement;
                if (settlement != null)
                {
                    foreach (SettlementBuilding building in settlement.Buildings)
                    {
                        if (building.IsFinished) continue;
                        // SettlementState.Update finishes a building once FinishedOn < CurrentDay.
                        string name = building.Blueprint.Name;
                        if (string.IsNullOrEmpty(name)) name = building.Blueprint.name;
                        list.Add(new GameTimer
                        {
                            Key = "bd:" + Guid(region.Blueprint) + ":" + Guid(building.Blueprint) + ":" + building.FinishedOn,
                            Title = name + " (" + settlement.Name + ")",
                            Name = string.Format(S.Building, name, settlement.Name),
                            Due = KingdomDay(Math.Max(building.FinishedOn + 1, today + 1))
                        });
                    }
                }
                foreach (Artisan artisan in region.Artisans)
                {
                    if (!artisan.BuildingUnlocked || artisan.CurrentProduction.Count == 0 || artisan.ProductionEndsOn <= today) continue;
                    string shop = artisan.Blueprint.ShopBlueprint != null ? (string)artisan.Blueprint.ShopBlueprint.Name : artisan.Blueprint.name;
                    list.Add(new GameTimer { Key = "ar:" + Guid(artisan.Blueprint), Title = shop, Name = string.Format(S.Artisan, shop), Due = KingdomDay(artisan.ProductionEndsOn) });
                }
            }

            int income = today + 1;
            while (income % 7 != 0) income++;
            list.Add(new GameTimer { Key = "bp", Name = string.Format(S.Income, k.BPPerTurnTotal), Due = KingdomDay(income) });
            int month = today + Math.Max(1, k.DaysTillNextMonth);
            list.Add(new GameTimer { Key = "mo", Name = S.Month, Due = KingdomDay(month) });
            if (Game.Instance.CurrentMode != GameModeType.Kingdom)
            {
                int raven = Math.Max(today + 1, k.LastRavenVisitDay + KingdomRoot.Instance.RavenVisitDelayDays + 1);
                list.Add(new GameTimer { Key = "rv", Name = S.Raven, Due = KingdomDay(raven), Hidden = true });
            }
        }
    }
}
