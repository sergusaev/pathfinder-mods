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

        public override string ToString()
        {
            var s = string.Format("hit {0:0.#}% crit {1:0.#}% | AB {2} vs AC {3}{4}", Hit * 100, Crit * 100, AttackBonus, TargetAC,
                FlatFooted ? " (flat-footed)" : "");
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
            RuleAttackWithWeapon attack = null)
        {
            var stats = new RuleCalculateWeaponStats(attacker, weapon, attack);
            var roll = new RuleAttackRoll(attacker, target, stats, penalty) { RuleAttackWithWeapon = attack };
            if (attack != null)
            {
                roll.AutoHit = attack.AutoHit;
                roll.AutoCriticalThreat = attack.AutoCriticalThreat;
                roll.AutoCriticalConfirmation = attack.AutoCriticalConfirmation;
                roll.Reason = attack;
            }
            var p = new AttackPrediction { Weapon = weapon.Blueprint.Name, Penalty = penalty };
            DryRun.Run(roll, () => Calculate(roll, p));
            return p;
        }

        // Abilities switched on that put their buff on only when an attack starts (ActivateOnUnitAction = Attack, as
        // Power Attack and Deadly Aim): before the attack the buff is not there yet, so its attack bonus is added here
        // as WeaponParametersAttackBonus would add it. Components of other kinds are named in the notes.
        static int OnAttackBonus(UnitEntityData attacker, ItemEntityWeapon weapon, RuleAttackWithWeapon attack, AttackPrediction p)
        {
            int total = 0;
            foreach (var ability in attacker.Descriptor.ActivatableAbilities.Enumerable)
            {
                var bp = ability.Blueprint;
                if (!ability.IsOn || ability.IsRunning || !bp.ActivateOnUnitAction
                    || bp.ActivateOnUnitActionType != AbilityActivateOnUnitActionType.Attack || bp.Buff == null)
                    continue;
                if (attacker.Descriptor.Buffs.HasFact(bp.Buff)) continue;
                int bonus = 0;
                MechanicsContext context = null;
                foreach (var component in bp.Buff.ComponentsArray)
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
                        p.PendingParts.Add(new Part(bp.Buff.Name + " [" + statBonus.Descriptor + "]", v));
                        continue;
                    }
                    var contextBonus = component as AddContextStatBonus;
                    if (contextBonus != null)
                    {
                        if (contextBonus.Stat != StatType.AdditionalAttackBonus) continue;
                        if (context == null)
                        {
                            context = new MechanicsContext(attacker, attacker.Descriptor, bp.Buff);
                            context.RecalculateRanks();
                        }
                        int cv = contextBonus.Value.Calculate(context) * contextBonus.Multiplier;
                        bonus += cv;
                        p.PendingParts.Add(new Part(bp.Buff.Name + " [" + contextBonus.Descriptor + "]", cv));
                        continue;
                    }
                    var abilityValue = component as AddStatBonusAbilityValue;
                    if (abilityValue != null)
                    {
                        if (abilityValue.Stat != StatType.AdditionalAttackBonus) continue;
                        if (context == null)
                        {
                            context = new MechanicsContext(attacker, attacker.Descriptor, bp.Buff);
                            context.RecalculateRanks();
                        }
                        int av = abilityValue.Value.Calculate(context);
                        bonus += av;
                        p.PendingParts.Add(new Part(bp.Buff.Name + " [" + abilityValue.Descriptor + "]", av));
                        continue;
                    }
                    var w = component as WeaponParametersAttackBonus;
                    if (w == null)
                    {
                        // Components that only feed the others (ranks, recalculation) say nothing about the attack.
                        if (component != null && !(component is ContextRankConfig) && !(component is RecalculateOnFactsChange))
                        {
                            string note = bp.Buff.Name + ": " + component.GetType().Name + " not counted";
                            if (!p.Notes.Contains(note)) p.Notes.Add(note);
                        }
                        continue;
                    }
                    if (w.OnlyForFullAttack && attack != null && !attack.IsFullAttack) continue;
                    bool finesse = !w.OnlyFinessable || weapon.Blueprint.Type.Category.HasSubCategory(WeaponSubCategory.Finessable)
                        || (attacker.Get<DamageGracePart>()?.HasEntry(weapon.Blueprint.Category) ?? false);
                    bool range = w.Ranged == weapon.Blueprint.IsRanged;
                    bool hands = !w.OnlyTwoHanded || weapon.Blueprint.IsTwoHanded;
                    if (!finesse || !range || !hands) continue;
                    int value = w.AttackBonus;
                    if (w.ScaleByBasicAttackBonus) value *= 1 + attacker.Stats.BaseAttackBonus.ModifiedValue / 4;
                    bonus += value;
                    p.PendingParts.Add(new Part(bp.Buff.Name, value));
                }
                if (bonus != 0) p.Notes.Add(string.Format("{0} {1:+0;-0} (turns on at the attack)", bp.Name, bonus));
                total += bonus;
            }
            return total;
        }

        static void Calculate(RuleAttackRoll roll, AttackPrediction p)
        {
            var attacker = roll.Initiator;
            var target = roll.Target;
            if (!roll.WeaponStats.IsTriggererd) Rulebook.Trigger(roll.WeaponStats);
            p.CriticalEdge = roll.WeaponStats.CriticalEdge;

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
            p.MissChance = roll.MissChance;
            if (p.MissChance > 0 && (p.Concealment != Concealment.Total || p.MissChance > 50) && (p.Concealment != Concealment.Partial || p.MissChance > 20))
                p.PassConcealment = Math.Max(0, 100 - p.MissChance) / 100.0;
            else
            {
                p.MissChance = 0;
                if (!roll.IgnoreConcealment && p.Concealment != Concealment.None)
                {
                    var check = new RuleConcealmentCheck(attacker, target, true);
                    DryRun.Run(check, null);
                    p.ConcealmentReroll = check.RerollIfFailed;
                    double fail = (p.Concealment == Concealment.Partial ? 20 : 50) / 100.0;
                    p.PassConcealment = 1 - (p.ConcealmentReroll ? fail * fail : fail);
                }
            }

            var ac = Rulebook.Trigger(new RuleCalculateAC(attacker, target, roll.AttackType));
            p.TargetAC = ac.TargetAC;
            p.FlatFooted = ac.IsTargetFlatFooted;
            p.ArmorClassParts = Breakdown.ArmorClass(ac, target);
            var abRule = Rulebook.Trigger(new RuleCalculateAttackBonus(attacker, target, roll.Weapon, roll.AttackBonusPenalty));
            p.AttackBonus = abRule.Result;
            p.AttackBonusParts = Breakdown.AttackBonus(abRule, attacker);
            p.OnAttackBonus = OnAttackBonus(attacker, roll.Weapon, roll.RuleAttackWithWeapon, p);
            p.AttackBonus += p.OnAttackBonus;
            p.AttackBonusParts.AddRange(p.PendingParts);
            int ab = p.AttackBonus, acValue = p.TargetAC;
            Func<int, bool> hits = d => d == 20 || (d > 1 && d + ab >= acValue);
            // ModifyD20 tests "failed" with RuleAttackRoll.IsSuccessRoll. In the game as shipped it adds the not yet
            // rolled Roll (0) instead of the die: every die but a natural 20 counts as failed unless AB >= AC. Kingmaker
            // Fixes (or any mod that patches the method) makes it test the die.
            Func<int, bool> failedForReroll = AttackSuccessRollFixed
                ? (Func<int, bool>)(d => !hits(d))
                : d => !(d > 1 && (d == 20 || ab >= acValue));
            p.Rerolls = DryRun.Rerolls(roll);
            var pmf = D20.Pmf(p.Rerolls, failedForReroll);
            p.RollHits = D20.P(pmf, hits);

            var critsOnParty = Game.Instance.Player.Difficulty.CritsOnParty;
            p.CanCrit = !roll.ImmuneToCriticalHit && (!target.IsPlayerFaction || critsOnParty == CriticalHitPower.Weak || critsOnParty == CriticalHitPower.Normal);
            int edge = p.CriticalEdge;
            bool autoThreat = roll.AutoCriticalThreat;
            Func<int, bool> threat = d => p.CanCrit && hits(d) && (d >= edge || autoThreat);
            if (p.CanCrit && D20.P(pmf, threat) > 0)
            {
                p.CriticalAC = Rulebook.Trigger(new RuleCalculateAC(attacker, target, roll.AttackType) { IsCritical = true }).TargetAC;
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
