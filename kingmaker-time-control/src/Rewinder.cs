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
using Kingmaker.UnitLogic.Commands;
using Kingmaker.UnitLogic.Commands.Base;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.View;

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
    // buffs, temporary enchantments, fatigue, the last rest and camp, the last random encounters, temporary negative
    // levels, and every other moment a unit keeps as an absolute game time (last move, grapple, confusion, magus,
    // touch charge, ferocity, idle of roaming NPCs, death). Left as they were, these would lie ahead of the clock
    // by the whole span and hold their checks ("not yet") for that long.
    // The same goes for the moments that live only in memory and are not saved: when a running command approaches
    // its target again, its cooldown exemption, and the avoidance after a charge.
    static class Rewinder
    {
        static readonly PropertyInfo s_NextTick = AccessTools.Property(typeof(Buff), "NextTickTime");
        static readonly FieldInfo s_RegionEncounters = AccessTools.Field(typeof(RandomEncounterManager), "m_LastEncounterInRegionTime");
        static readonly FieldInfo s_CampEncounter = AccessTools.Field(typeof(RandomEncounterManager), "m_LastCampEncounterTime");

        static readonly FieldInfo s_NegativeLevels = AccessTools.Field(typeof(UnitPartNegativeLevels), "m_LevelsData");
        static readonly Type s_NegativeLevelType = AccessTools.Inner(typeof(UnitPartNegativeLevels), "Data");
        static readonly FieldInfo s_NegativeLevelEnd = s_NegativeLevelType == null ? null : AccessTools.Field(s_NegativeLevelType, "EndTime");
        static readonly PropertyInfo s_GrappleAttempt = AccessTools.Property(typeof(UnitPartGrappleTarget), "LastBreakFreeAttempt");
        static readonly PropertyInfo s_ConfusionRound = AccessTools.Property(typeof(UnitPartConfusion), "RoundStartTime");
        static readonly PropertyInfo s_MagusCast = AccessTools.Property(typeof(UnitPartMagus), "LastCastedMagusSpellTime");
        static readonly PropertyInfo s_MagusAttack = AccessTools.Property(typeof(UnitPartMagus), "LastAttackTime");
        static readonly PropertyInfo s_MagusOpportunity = AccessTools.Property(typeof(UnitPartMagus), "LastSpellCombatOpportunityTime");
        static readonly PropertyInfo s_MagusArcher = AccessTools.Property(typeof(UnitPartMagus), "EldritchArcherSpellCastTime");
        static readonly PropertyInfo s_TouchAppear = AccessTools.Property(typeof(UnitPartTouch), "AppearTime");
        static readonly FieldInfo s_FerocityStart = AccessTools.Field(typeof(UnitPartFerocity), "m_StartTime");
        static readonly FieldInfo s_RoamingIdle = AccessTools.Field(typeof(UnitPartRoaming), "IdleEndTime");
        static readonly FieldInfo s_Commands = AccessTools.Field(typeof(UnitCommands), "m_Commands");
        static readonly FieldInfo s_CommandIgnoreCooldown = AccessTools.Field(typeof(UnitCommand), "m_IgnoreCooldownBeforeTime");
        static readonly FieldInfo s_ChargeAvoidance = AccessTools.Field(typeof(UnitMovementAgent), "m_ChargeAvoidanceFinishTime");

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
                moved += ShiftUnitMoments(unit, delta);
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

        // Each part on its own: one that fails must not keep the others from moving.
        static int ShiftUnitMoments(UnitEntityData unit, TimeSpan delta)
        {
            int moved = 0;
            unit.LastMoveTime = Back(unit.LastMoveTime, delta);
            unit.Descriptor.State.DeathTime = Back(unit.Descriptor.State.DeathTime, delta);
            Guard(unit, "negative levels", () =>
            {
                var levels = s_NegativeLevels == null || s_NegativeLevelEnd == null ? null
                    : s_NegativeLevels.GetValue(unit.Get<UnitPartNegativeLevels>()) as IList;
                if (levels == null) return;
                foreach (object data in levels)
                {
                    var end = (TimeSpan?)s_NegativeLevelEnd.GetValue(data);
                    if (!end.HasValue) continue;
                    s_NegativeLevelEnd.SetValue(data, (TimeSpan?)Back(end.Value, delta));
                    moved++;
                }
            });
            Guard(unit, "part timers", () =>
            {
                ShiftProperty(unit.Get<UnitPartGrappleTarget>(), s_GrappleAttempt, delta);
                ShiftProperty(unit.Get<UnitPartConfusion>(), s_ConfusionRound, delta);
                var magus = unit.Get<UnitPartMagus>();
                ShiftProperty(magus, s_MagusCast, delta);
                ShiftProperty(magus, s_MagusAttack, delta);
                ShiftProperty(magus, s_MagusOpportunity, delta);
                ShiftProperty(magus, s_MagusArcher, delta);
                ShiftProperty(unit.Get<UnitPartTouch>(), s_TouchAppear, delta);
                ShiftField(unit.Get<UnitPartFerocity>(), s_FerocityStart, delta);
                ShiftField(unit.Get<UnitPartRoaming>(), s_RoamingIdle, delta);
            });
            Guard(unit, "commands", () =>
            {
                var commands = new List<UnitCommand>();
                var slots = s_Commands == null ? null : s_Commands.GetValue(unit.Commands) as UnitCommand[];
                if (slots != null) commands.AddRange(slots.Where(c => c != null));
                commands.AddRange(unit.Commands.Queue.Where(c => c != null && !commands.Contains(c)));
                foreach (var cmd in commands)
                {
                    cmd.NextApproachTime = Back(cmd.NextApproachTime, delta);
                    if (s_CommandIgnoreCooldown == null) continue;
                    var ignore = (TimeSpan?)s_CommandIgnoreCooldown.GetValue(cmd);
                    if (ignore.HasValue) s_CommandIgnoreCooldown.SetValue(cmd, (TimeSpan?)Back(ignore.Value, delta));
                }
            });
            Guard(unit, "movement", () =>
            {
                var view = unit.View;
                ShiftField(view == null ? null : view.AgentASP, s_ChargeAvoidance, delta);
            });
            return moved;
        }

        static void Guard(UnitEntityData unit, string what, Action action)
        {
            try { action(); }
            catch (Exception e) { Main.Error(e, string.Format("moving {0} of {1} back", what, unit)); }
        }

        static void ShiftProperty(object owner, PropertyInfo p, TimeSpan delta)
        {
            if (owner == null || p == null) return;
            p.SetValue(owner, Back((TimeSpan)p.GetValue(owner, null), delta), null);
        }

        static void ShiftField(object owner, FieldInfo f, TimeSpan delta)
        {
            if (owner == null || f == null) return;
            f.SetValue(owner, Back((TimeSpan)f.GetValue(owner), delta));
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
