using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.Kingdom;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Parts;

namespace TimeControl
{
    // Turning the clock back. Player.GameTime goes back; what is anchored to the calendar then lies further ahead:
    // - quest deadlines (objective start + AutoFailDays) and timed quest events count from their absolute start times;
    // - the kingdom runs its days strictly forward (KingdomTimelineManager.UpdateTimeline: while CurrentDay < game day),
    //   CurrentDay is left as it is, so the days it has already lived are not lived again: until the calendar is back at
    //   CurrentDay the kingdom waits, and all its deadlines, which are kingdom days, move away by the same span.
    // Nothing that has happened comes back: finished quests and objectives keep their state, fired timed quest events
    // keep DidTrigger, resolved kingdom events are in the history, expired buffs are gone.
    // Timers of the units themselves are moved back together with the clock, so their remaining time stays the same:
    // buffs, temporary enchantments, fatigue, the last rest and camp, the last random encounters.
    static class Rewinder
    {
        static readonly PropertyInfo s_NextTick = AccessTools.Property(typeof(Buff), "NextTickTime");
        static readonly FieldInfo s_RegionEncounters = AccessTools.Field(typeof(RandomEncounterManager), "m_LastEncounterInRegionTime");
        static readonly FieldInfo s_CampEncounter = AccessTools.Field(typeof(RandomEncounterManager), "m_LastCampEncounterTime");

        // The earliest the clock may go: the start of the game.
        internal static int MaxHours
        {
            get
            {
                TimeSpan start = Game.Instance.BlueprintRoot.Calendar.GetStartTime();
                return Math.Max(0, (int)Math.Floor((Game.Instance.Player.GameTime - start).TotalHours));
            }
        }

        // The kingdom day the calendar has to reach again before the kingdom goes on, or -1.
        internal static int KingdomWaitsUntil(TimeSpan gameTime)
        {
            if (!KingdomState.Founded || KingdomState.Instance == null) return -1;
            var k = KingdomState.Instance;
            int day = (int)gameTime.TotalDays - k.StartDay;
            return day < k.CurrentDay ? k.CurrentDay : -1;
        }

        internal static void Run(int hours)
        {
            if (hours <= 0 || !Skipper.CanSkipNow) return;
            hours = Math.Min(hours, MaxHours);
            if (hours <= 0) return;
            var game = Game.Instance;
            TimeSpan delta = TimeSpan.FromHours(hours);
            Main.Log(string.Format("rewinding {0} h from {1}", hours, Timers.Date(game.Player.GameTime)));
            game.Player.GameTime -= delta;
            int moved = 0;
            try
            {
                moved = ShiftUnits(delta);
                ShiftCamp(delta);
            }
            catch (Exception e)
            {
                Main.Error(e, "moving unit timers back");
            }
            game.MatchTimeOfDay();
            Main.Log(string.Format("rewound to {0}, {1} unit timers moved back", Timers.Date(game.Player.GameTime), moved));
            Tracker.Rearm();
            Tracker.Scan();
        }

        static TimeSpan Back(TimeSpan t, TimeSpan delta)
        {
            // Zero and the extremes mean "never" or "forever".
            if (t == TimeSpan.Zero || t == TimeSpan.MaxValue || t == TimeSpan.MinValue) return t;
            return t - delta;
        }

        static int ShiftUnits(TimeSpan delta)
        {
            var game = Game.Instance;
            var units = new HashSet<UnitEntityData>(game.Player.AllCharacters);
            foreach (var u in game.State.Units) units.Add(u);
            int moved = 0;
            foreach (var unit in units)
            {
                if (unit == null || unit.Descriptor == null) continue;
                foreach (Buff buff in unit.Descriptor.Buffs.Enumerable.ToList())
                {
                    if (buff.EndTime != TimeSpan.MaxValue)
                    {
                        buff.EndTime = Back(buff.EndTime, delta);
                        moved++;
                    }
                    if (s_NextTick != null)
                    {
                        var next = (TimeSpan)s_NextTick.GetValue(buff, null);
                        s_NextTick.SetValue(buff, Back(next, delta), null);
                    }
                }
                if (unit.Descriptor.LastRestTime.HasValue)
                    unit.Descriptor.LastRestTime = Back(unit.Descriptor.LastRestTime.Value, delta);
                var weariness = unit.Get<UnitPartWeariness>();
                if (weariness != null)
                {
                    weariness.LastStackTime = Back(weariness.LastStackTime, delta);
                    weariness.LastBuffApplyTime = Back(weariness.LastBuffApplyTime, delta);
                }
            }
            foreach (ItemEntity item in game.Player.Inventory.ToList())
            {
                var enchantments = item.Enchantments;
                if (enchantments == null) continue;
                foreach (var e in enchantments)
                {
                    if (!e.IsTemporary) continue;
                    e.EndTime = Back(e.EndTime, delta);
                    moved++;
                }
            }
            return moved;
        }

        static void ShiftCamp(TimeSpan delta)
        {
            var player = Game.Instance.Player;
            player.Camping.LastCampTime = Back(player.Camping.LastCampTime, delta);
            var re = player.REManager;
            if (s_CampEncounter != null) s_CampEncounter.SetValue(re, Back((TimeSpan)s_CampEncounter.GetValue(re), delta));
            var regions = s_RegionEncounters == null ? null : s_RegionEncounters.GetValue(re) as IDictionary;
            if (regions != null)
                foreach (var key in regions.Keys.Cast<object>().ToList())
                    regions[key] = Back((TimeSpan)regions[key], delta);
        }
    }

    // While the kingdom waits for the calendar after a rewind, its date shows the calendar's day, not the day it has
    // already lived (the same day once the calendar is there).
    [HarmonyPatch(typeof(KingdomState), "Date", MethodType.Getter)]
    static class KingdomDatePatch
    {
        static void Postfix(KingdomState __instance, ref DateTime __result)
        {
            if (Game.Instance == null || Game.Instance.Player == null) return;
            int day = (int)Game.Instance.Player.GameTime.TotalDays - __instance.StartDay;
            if (day < __instance.CurrentDay) __result = __instance.ToDate(day);
        }
    }
}
