using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Enums;
using Kingmaker.UI.SettingsUI;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Damage;
using Kingmaker.UnitLogic.ActivatableAbilities;
using Kingmaker.UnitLogic.Parts;

namespace SuccessChance
{
    // What the rules of Kingmaker and Wrath of the Righteous name or decide differently, Kingmaker's side.
    static class GameRules
    {
        // RuleAttackRoll.IsSuccessRoll tests the die it is given: only when a mod fixes it (see AttackSuccessRollFixed).
        internal const bool SuccessRollTestsDie = false;

        internal static bool IsStarted(ActivatableAbility ability) => ability.IsRunning;

        internal static bool IsTriggered(RuleCalculateWeaponStats stats) => stats.IsTriggererd;

        internal static int CriticalMultiplier(RuleCalculateWeaponStats stats) => stats.CriticalMultiplier;

        internal static DiceFormula Dice(DamageDescription damage) => damage.Dice;

        internal static int MissChance(RuleAttackRoll roll) => roll.MissChance;

        // The chance a concealment check fails, after a dry run of its handlers ("reroll if failed" on it).
        internal static double ConcealmentFail(RuleConcealmentCheck check, Concealment concealment, out bool reroll)
        {
            reroll = check.RerollIfFailed;
            double fail = (concealment == Concealment.Partial ? 20 : 50) / 100.0;
            return reroll ? fail * fail : fail;
        }

        internal static int Die(RuleAttackRoll roll) => roll.Roll.Value;

        internal static int Result(RuleCalculateAC ac) => ac.TargetAC;

        internal static bool AlwaysChance(UnitEntityData unit) => false;

        internal static bool CanCrit(RuleAttackRoll roll, UnitEntityData target)
        {
            var critsOnParty = Game.Instance.Player.Difficulty.CritsOnParty;
            return !roll.ImmuneToCriticalHit && (!target.IsPlayerFaction || critsOnParty == CriticalHitPower.Weak || critsOnParty == CriticalHitPower.Normal);
        }
    }
}
