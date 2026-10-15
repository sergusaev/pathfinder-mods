using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Enums;
using Kingmaker.Items;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UI.SettingsUI;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Parts;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UnitLogic.FactLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.UnitLogic.Mechanics.Components;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.UnitLogic.Buffs.Blueprints;
#if WOTR
using Kingmaker.UnitLogic.Mechanics.Actions;
using Kingmaker.Blueprints.Items.Weapons;
using Kingmaker.Utility;
#endif
#if WOTR
using DamageGracePart = Kingmaker.UnitLogic.Parts.UnitPartDamageGrace;
#endif

namespace SuccessChance
{
    // The chance of one attack with a weapon, as RuleAttackRoll.OnTrigger decides it, with the bonuses of a dry run.
    class AttackPrediction
    {
        public string Weapon;
        public int Penalty;
        public int AttackBonus;
        public int TargetAC;
        public bool FlatFooted;
        public bool AutoHit;
        public bool AutoMiss;
        public Concealment Concealment;
        public int MissChance;
        public bool ConcealmentReroll;
        public double PassConcealment = 1;
        public double RollHits;
        public int CriticalEdge;
        public bool CanCrit;
        public int CriticalAC;
        public int ConfirmationBonus;
        public double Confirm;
        public int MirrorImages;
        public int ParryBonus = int.MinValue;
        public int Fortification;
        public List<D20Modifier> Rerolls;
        public int OnAttackBonus;
        public List<Part> AttackBonusParts;
        public List<Part> ArmorClassParts;
        public List<string> Notes = new List<string>();
        // Terms of abilities that turn on at the attack, named by their buff.
        public List<Part> PendingParts = new List<Part>();
        public double Hit;
        public double Crit;
        // Damage of a hit that is not a critical one: the weapon's dice and bonuses (no sneak attack and other damage
        // added when the damage is dealt).
        public int DamageMin;
        public int DamageMax;
        // Damage of a confirmed critical hit: as RuleAttackWithWeapon.CreateDamage, the critical multiplier goes on the
        // weapon's own damage (its dice rolled that many times, its bonus multiplied), the rest (energy dice) once.
        public int CriticalMultiplier;
        public int CritDamageMin;
        public int CritDamageMax;
        // Time the prediction took.
        public double Milliseconds;

        public override string ToString()
        {
            var s = string.Format("hit {0:0.#}% crit {1:0.#}% | AB {2} vs AC {3}{4} | damage {5}-{6} | {7:0.0} ms", Hit * 100, Crit * 100,
                AttackBonus, TargetAC, FlatFooted ? " (flat-footed)" : "", DamageMin, DamageMax, Milliseconds);
            if (AutoHit) s += " | auto hit";
            if (AutoMiss) s += " | auto miss";
            if (PassConcealment < 1)
                s += string.Format(" | {0} {1:0.#}% pass", MissChance > 0 ? "miss chance " + MissChance + "%" : "concealment " + Concealment,
                    PassConcealment * 100) + (ConcealmentReroll ? " (reroll)" : "");
            if (CanCrit) s += string.Format(" | crit {0}-20 confirm {1:0.#}% vs AC {2}", CriticalEdge, Confirm * 100, CriticalAC);
            if (MirrorImages > 0) s += " | mirror images " + MirrorImages;
            if (ParryBonus != int.MinValue) s += " | parry +" + ParryBonus;
            if (Fortification > 0) s += " | fortification " + Fortification + "%";
            if (Rerolls != null && Rerolls.Count > 0) s += " | d20: " + D20.Describe(Rerolls);
            if (Notes.Count > 0) s += " | " + string.Join("; ", Notes.ToArray());
            return s;
        }
    }

    static class AttackChance
    {
        static bool? s_SuccessRollFixed;

        // Whether some mod patches RuleAttackRoll.IsSuccessRoll (Kingmaker Fixes makes it test the given die).
        internal static bool AttackSuccessRollFixed
        {
            get
            {
                if (s_SuccessRollFixed == null)
                {
                    var info = Harmony.GetPatchInfo(AccessTools.Method(typeof(RuleAttackRoll), nameof(RuleAttackRoll.IsSuccessRoll)));
                    s_SuccessRollFixed = info != null && (info.Prefixes.Count > 0 || info.Transpilers.Count > 0);
                    ChanceLog.Write("RuleAttackRoll.IsSuccessRoll " + (s_SuccessRollFixed.Value
                        ? "is patched (" + string.Join(", ", info.Owners.ToArray()) + "): rerolls if failed test the die"
                        : "as shipped: rerolls if failed treat every die but 20 as failed"));
                }
                return s_SuccessRollFixed.Value;
            }
        }

        internal static AttackPrediction Predict(UnitEntityData attacker, UnitEntityData target, ItemEntityWeapon weapon, int penalty,
            RuleAttackWithWeapon attack = null, RuleReason reason = null)
        {
            var stats = new RuleCalculateWeaponStats(attacker, weapon, attack);
            var roll = new RuleAttackRoll(attacker, target, stats, penalty) { RuleAttackWithWeapon = attack, Reason = reason };
            if (attack != null)
            {
                roll.AutoHit = attack.AutoHit;
                roll.AutoCriticalThreat = attack.AutoCriticalThreat;
                roll.AutoCriticalConfirmation = attack.AutoCriticalConfirmation;
                roll.Reason = attack;
            }
            var p = new AttackPrediction { Weapon = weapon.Blueprint.Name, Penalty = penalty };
            var watch = System.Diagnostics.Stopwatch.StartNew();
            DryRun.Run(roll, () => Calculate(roll, p));
            p.Milliseconds = watch.Elapsed.TotalMilliseconds;
            return p;
        }

        // Abilities switched on that put their buff on only when an attack starts (ActivateOnUnitAction = Attack, as
        // Power Attack and Deadly Aim): before the attack the buff is not there yet, so its attack bonus is added here
        // as WeaponParametersAttackBonus would add it. Components of other kinds are named in the notes. In WotR that
        // buff (and any other buff of the attacker) may in turn put a buff on right before each attack with a weapon
        // (AddInitiatorAttackWithWeaponTrigger, TriggerBeforeAttack): Power Attack's own bonus comes so.
        static int OnAttackBonus(UnitEntityData attacker, ItemEntityWeapon weapon, RuleAttackWithWeapon attack, AttackPrediction p)
        {
            var pending = new List<KeyValuePair<BlueprintBuff, string>>();
            foreach (var ability in attacker.Descriptor.ActivatableAbilities.Enumerable)
            {
                var bp = ability.Blueprint;
                if (!ability.IsOn || GameRules.IsStarted(ability) || !bp.ActivateOnUnitAction
                    || bp.ActivateOnUnitActionType != AbilityActivateOnUnitActionType.Attack || bp.Buff == null)
                    continue;
                if (attacker.Descriptor.Buffs.HasFact(bp.Buff)) continue;
                pending.Add(new KeyValuePair<BlueprintBuff, string>(bp.Buff, bp.Name));
            }
#if WOTR
            var sources = new List<KeyValuePair<BlueprintBuff, string>>(pending);
            foreach (var buff in attacker.Descriptor.Buffs.Enumerable)
                sources.Add(new KeyValuePair<BlueprintBuff, string>(buff.Blueprint, buff.Name));
            foreach (var source in sources)
                foreach (var applied in BeforeAttackBuffs(source.Key, attacker, weapon, attack))
                    if (!attacker.Descriptor.Buffs.HasFact(applied) && !pending.Any(x => x.Key == applied))
                        pending.Add(new KeyValuePair<BlueprintBuff, string>(applied, source.Value));
#endif
            int total = 0;
            foreach (var entry in pending)
            {
                int bonus = BuffAttackBonus(entry.Key, attacker, weapon, attack, p);
                if (bonus != 0) p.Notes.Add(string.Format("{0} {1:+0;-0} (turns on at the attack)", entry.Value, bonus));
                total += bonus;
            }
            return total;
        }

#if WOTR
        // The buffs a buff puts on its owner right before an attack with this weapon, as AbstractWeaponTrigger tests
        // the attack before it (the parts about the roll's result do not apply then).
        static IEnumerable<BlueprintBuff> BeforeAttackBuffs(BlueprintBuff buff, UnitEntityData attacker, ItemEntityWeapon weapon, RuleAttackWithWeapon attack)
        {
            foreach (var component in buff.ComponentsArray)
            {
                var trigger = component as AddInitiatorAttackWithWeaponTrigger;
                if (trigger == null || !trigger.TriggerBeforeAttack || !trigger.ActionsOnInitiator || trigger.Action == null) continue;
                var bp = weapon.Blueprint;
                if (trigger.CheckWeaponBlueprint ? bp != trigger.WeaponBlueprint
                    : (trigger.WeaponType != null && trigger.WeaponType != bp.Type)
                      || (trigger.CheckWeaponCategory && trigger.Category != bp.Category)
                      || (trigger.CheckWeaponRangeType && !trigger.RangeType.IsSuitableWeapon(weapon))
                      || (trigger.CheckWeaponGroup && !bp.FighterGroup.Contains(trigger.Group))
                      || (trigger.AllNaturalAndUnarmed && !bp.Type.IsNatural && !bp.Type.IsUnarmed))
                    continue;
                if (trigger.OnlyOnFullAttack && attack != null && !attack.IsFullAttack) continue;
                if (trigger.OnlyOnFirstAttack && attack != null && !attack.IsFirstAttack) continue;
                if (trigger.Action.Actions == null) continue;
                foreach (var action in trigger.Action.Actions)
                {
                    var apply = action as ContextActionApplyBuff;
                    if (apply?.Buff != null) yield return apply.Buff;
                }
            }
        }
#endif

        // The attack bonus a buff that is not on yet would give to an attack with this weapon.
        static int BuffAttackBonus(BlueprintBuff buff, UnitEntityData attacker, ItemEntityWeapon weapon, RuleAttackWithWeapon attack, AttackPrediction p)
        {
            int bonus = 0;
            MechanicsContext context = null;
            foreach (var component in buff.ComponentsArray)
            {
                // A bonus to the attack stat, as Fighting Defensively has: its value comes from the buff's
                // context (ranks and properties of the unit), calculated here as the buff would on turning on.
                var statBonus = component as AddStatBonus;
                if (statBonus != null)
                {
                    if (statBonus.Stat != StatType.AdditionalAttackBonus) continue;
                    int v = statBonus.Value;
                    if (statBonus.ScaleByBasicAttackBonus) v *= 1 + attacker.Stats.BaseAttackBonus.ModifiedValue / 4;
                    bonus += v;
                    p.PendingParts.Add(new Part(buff.Name + " [" + statBonus.Descriptor + "]", v));
                    continue;
                }
                var contextBonus = component as AddContextStatBonus;
                if (contextBonus != null)
                {
                    if (contextBonus.Stat != StatType.AdditionalAttackBonus) continue;
                    if (context == null)
                    {
                        context = new MechanicsContext(attacker, attacker.Descriptor, buff);
                        context.RecalculateRanks();
                    }
                    int cv = contextBonus.Value.Calculate(context) * contextBonus.Multiplier;
                    bonus += cv;
                    p.PendingParts.Add(new Part(buff.Name + " [" + contextBonus.Descriptor + "]", cv));
                    continue;
                }
                var abilityValue = component as AddStatBonusAbilityValue;
                if (abilityValue != null)
                {
                    if (abilityValue.Stat != StatType.AdditionalAttackBonus) continue;
                    if (context == null)
                    {
                        context = new MechanicsContext(attacker, attacker.Descriptor, buff);
                        context.RecalculateRanks();
                    }
                    int av = abilityValue.Value.Calculate(context);
                    bonus += av;
                    p.PendingParts.Add(new Part(buff.Name + " [" + abilityValue.Descriptor + "]", av));
                    continue;
                }
                var w = component as WeaponParametersAttackBonus;
                if (w == null)
                {
                    // Components that only feed the others (ranks, recalculation, triggers before the attack, counted
                    // above) say nothing about the attack.
                    if (component != null && !(component is ContextRankConfig) && !(component is RecalculateOnFactsChange)
#if WOTR
                        && !(component is AddInitiatorAttackWithWeaponTrigger)
#endif
                        )
                    {
                        string note = buff.Name + ": " + component.GetType().Name + " not counted";
                        if (!p.Notes.Contains(note)) p.Notes.Add(note);
                    }
                    continue;
                }
                if (w.OnlyForFullAttack && attack != null && !attack.IsFullAttack) continue;
                bool finesse = !w.OnlyFinessable || weapon.Blueprint.Type.Category.HasSubCategory(WeaponSubCategory.Finessable)
                    || (attacker.Get<DamageGracePart>()?.HasEntry(weapon.Blueprint.Category) ?? false);
                bool range = w.Ranged == weapon.Blueprint.IsRanged;
                bool hands = !w.OnlyTwoHanded || weapon.Blueprint.IsTwoHanded;
#if WOTR
                if (w.SpecificWeaponBlueprint ? w.WeaponBlueprint != weapon.Blueprint : !finesse || !range || !hands) continue;
                int value = w.AttackBonus;
                if (w.UseContextIstead)
                {
                    if (context == null)
                    {
                        context = new MechanicsContext(attacker, attacker.Descriptor, buff);
                        context.RecalculateRanks();
                    }
                    value = w.ContextAttackBonus.Calculate(context);
                }
                if (w.ScaleByBasicAttackBonus) value *= 1 + attacker.Stats.BaseAttackBonus.ModifiedValue / 4;
                if (w.MultiplierFact != null && attacker.HasFact(w.MultiplierFact)) value *= w.Multiplier;
#else
                if (!finesse || !range || !hands) continue;
                int value = w.AttackBonus;
                if (w.ScaleByBasicAttackBonus) value *= 1 + attacker.Stats.BaseAttackBonus.ModifiedValue / 4;
#endif
                bonus += value;
                p.PendingParts.Add(new Part(buff.Name, value));
            }
            return bonus;
        }

        static void Calculate(RuleAttackRoll roll, AttackPrediction p)
        {
            var attacker = roll.Initiator;
            var target = roll.Target;
            if (!GameRules.IsTriggered(roll.WeaponStats)) Rulebook.Trigger(roll.WeaponStats);
            p.CriticalEdge = roll.WeaponStats.CriticalEdge;
            int multiplier = p.CriticalMultiplier = GameRules.CriticalMultiplier(roll.WeaponStats);
            for (int i = 0; i < roll.WeaponStats.DamageDescription.Count; i++)
            {
                var damage = roll.WeaponStats.DamageDescription[i];
                var dice = GameRules.Dice(damage);
                int min = dice.Rolls + damage.Bonus, max = dice.Rolls * dice.Dice.Sides() + damage.Bonus;
                p.DamageMin += min;
                p.DamageMax += max;
                p.CritDamageMin += i == 0 ? min * multiplier : min;
                p.CritDamageMax += i == 0 ? max * multiplier : max;
            }
            p.DamageMin = Math.Max(1, p.DamageMin);
            p.DamageMax = Math.Max(p.DamageMin, p.DamageMax);
            p.CritDamageMin = Math.Max(1, p.CritDamageMin);
            p.CritDamageMax = Math.Max(p.CritDamageMin, p.CritDamageMax);

            bool confused = target.Descriptor.State.HasCondition(UnitCondition.Confusion);
            bool friend = !target.IsEnemy(attacker) && !target.Faction.Neutral && !confused;
            p.AutoHit = roll.AutoHit || attacker == target || (roll.AttackType.IsTouch() && friend);
            p.AutoMiss = !p.AutoHit && roll.AutoMiss;
            p.Fortification = target.Get<UnitPartFortification>()?.Value ?? 0;
            double fortificationPassed = p.Fortification > 0 ? Math.Max(0, 100 - p.Fortification) / 100.0 : 1;
            if (p.AutoHit)
            {
                p.Hit = 1;
                p.Crit = roll.AutoCriticalThreat && roll.AutoCriticalConfirmation ? fortificationPassed : 0;
                return;
            }
            if (p.AutoMiss) return;

            // Concealment or a miss chance (d100 over the value), as TryOvercomeTargetConcealmentAndMissChance.
            p.Concealment = roll.IgnoreConcealment ? Concealment.None : UnitPartConcealment.Calculate(attacker, target, true);
            p.MissChance = GameRules.MissChance(roll);
            if (p.MissChance > 0 && (p.Concealment != Concealment.Total || p.MissChance > 50) && (p.Concealment != Concealment.Partial || p.MissChance > 20))
                p.PassConcealment = Math.Max(0, 100 - p.MissChance) / 100.0;
            else
            {
                p.MissChance = 0;
                if (!roll.IgnoreConcealment && p.Concealment != Concealment.None)
                {
                    var check = new RuleConcealmentCheck(attacker, target, true);
                    double fail = 0;
                    DryRun.Run(check, () => fail = GameRules.ConcealmentFail(check, p.Concealment, out p.ConcealmentReroll));
                    p.PassConcealment = 1 - fail;
                }
            }

#if WOTR
            // As RuleAttackRoll.OnTrigger: flat-footed forced by the roll, the roll's own bonuses copied to the attack
            // bonus, with the weapon stats of the roll.
            var ac = Rulebook.Trigger(new RuleCalculateAC(attacker, target, roll.AttackType) { ForceFlatFooted = roll.ForceFlatFooted });
            var abRule = new RuleCalculateAttackBonus(attacker, target, roll.WeaponStats, roll.AttackBonusPenalty) { TargetIsFlanked = roll.TargetIsFlanked };
            abRule.CopyModifiersFrom(roll);
            Rulebook.Trigger(abRule);
#else
            var ac = Rulebook.Trigger(new RuleCalculateAC(attacker, target, roll.AttackType));
            var abRule = Rulebook.Trigger(new RuleCalculateAttackBonus(attacker, target, roll.Weapon, roll.AttackBonusPenalty));
#endif
            p.TargetAC = GameRules.Result(ac);
            p.FlatFooted = ac.IsTargetFlatFooted;
            p.ArmorClassParts = Breakdown.ArmorClass(ac, target);
            p.AttackBonus = abRule.Result;
            p.AttackBonusParts = Breakdown.AttackBonus(abRule, attacker);
            p.OnAttackBonus = OnAttackBonus(attacker, roll.Weapon, roll.RuleAttackWithWeapon, p);
            p.AttackBonus += p.OnAttackBonus;
            p.AttackBonusParts.AddRange(p.PendingParts);
            int ab = p.AttackBonus, acValue = p.TargetAC;
            // WotR: a feature lets a natural 1 hit if the total does (State.Features.AlwaysChance).
            bool alwaysChance = GameRules.AlwaysChance(attacker);
            Func<int, bool> hits = d => d == 20 || ((d > 1 || alwaysChance) && d + ab >= acValue);
            // ModifyD20 tests "failed" with RuleAttackRoll.IsSuccessRoll. In the game as shipped it adds the not yet
            // rolled Roll (0) instead of the die: every die but a natural 20 counts as failed unless AB >= AC. Kingmaker
            // Fixes (or any mod that patches the method) makes it test the die.
            Func<int, bool> failedForReroll = GameRules.SuccessRollTestsDie || AttackSuccessRollFixed
                ? (Func<int, bool>)(d => !hits(d))
                : d => !(d > 1 && (d == 20 || ab >= acValue));
            p.Rerolls = DryRun.Rerolls(roll);
            var pmf = D20.Pmf(p.Rerolls, failedForReroll);
            p.RollHits = D20.P(pmf, hits);

            p.CanCrit = GameRules.CanCrit(roll, target);
            int edge = p.CriticalEdge;
            bool autoThreat = roll.AutoCriticalThreat;
            Func<int, bool> threat = d => p.CanCrit && hits(d) && (d >= edge || autoThreat);
            if (p.CanCrit && D20.P(pmf, threat) > 0)
            {
                p.CriticalAC = GameRules.Result(Rulebook.Trigger(new RuleCalculateAC(attacker, target, roll.AttackType) { IsCritical = true }));
                p.ConfirmationBonus = roll.CriticalConfirmationBonus;
                int need = p.CriticalAC - ab - p.ConfirmationBonus;
                // The confirmation die goes through the same d20 modifiers, no natural 1 or 20 rule here. By then the
                // attack die has hit, so for "reroll only if failed" only a natural 1 counts as failed.
                p.Confirm = roll.AutoCriticalConfirmation ? 1 : D20.P(D20.Pmf(p.Rerolls, d => d == 1), d => d >= need);
            }

            // Mirror images take a hit with chance images/(images+1); a parry beats the attack roll.
            if (!attacker.Descriptor.IsImmuneToVisualEffects)
                p.MirrorImages = target.Get<UnitPartMirrorImage>()?.MechanicsImages.Count ?? 0;
            double image = p.MirrorImages > 0 ? p.MirrorImages / (p.MirrorImages + 1.0) : 0;
            Func<int, double> parried = d => 0;
            if (roll.Parry != null)
            {
                p.ParryBonus = Rulebook.Trigger(roll.Parry.AttackBonusRule).Result;
                int parry = p.ParryBonus;
                parried = d =>
                {
                    int beat = 0;
                    for (int q = 1; q <= 20; q++) if (q + parry > d + ab) beat++;
                    return beat / 20.0;
                };
            }

            double hit = 0, crit = 0;
            for (int d = 1; d <= 20; d++)
            {
                if (pmf[d] <= 0 || !hits(d)) continue;
                double stays = pmf[d] * (1 - image) * (1 - parried(d));
                hit += stays;
                if (threat(d)) crit += stays * p.Confirm * fortificationPassed;
            }
            p.Hit = p.PassConcealment * hit;
            p.Crit = p.PassConcealment * crit;
        }
    }
}
