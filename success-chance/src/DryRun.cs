using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.PubSubSystem;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UnitLogic.Mechanics.Components;
#if WOTR
using Kingmaker.EntitySystem;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Mechanics;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.EquipmentEnchants;
using Kingmaker.Enums;
using Kingmaker.Utility;
#endif

namespace SuccessChance
{
    // A rule "run dry": everything that adds its bonuses before the dice is called, the dice are not rolled and nothing
    // that happens after a roll is called. A rule is triggered by the game in three steps: the handlers "about to
    // trigger" (bonuses, as temporary modifiers of stats or fields of the rule), OnTrigger (the dice), the handlers "did
    // trigger" (combat log, spent rerolls, removed buffs, attacks of opportunity). A dry run does the first step only,
    // then removes the temporary modifiers as the game does at the end of a rule.
    //
    // The run has a context of its own, so a dry rule never shows up in the history of real rules (LastEvent and
    // PreviousEvent of a real attack). Rules that only calculate (attack bonus, armor class, flat-footed) may be
    // triggered for real inside a dry run: nothing listens to them but bonuses, as the game itself does in
    // PowerAttackWatcher.
    static class DryRun
    {
        static readonly FieldInfo ContextField = AccessTools.Field(typeof(Rulebook), "m_RuleContext");
        static int s_Depth;
        static readonly List<List<Action>> s_Cleanups = new List<List<Action>>();

        internal static bool Active => s_Depth > 0;

        // The d20 modifiers met by the last dry RuleRollD20 (see Rerolls).
        internal static readonly List<D20Modifier> D20Modifiers = new List<D20Modifier>();

        internal static void Run(RulebookEvent evt, Action body)
        {
            var rulebook = Game.Instance.Rulebook;
            bool outer = s_Depth == 0;
            object saved = null;
            if (outer)
            {
                saved = ContextField.GetValue(rulebook);
                ContextField.SetValue(rulebook, new RulebookEventContext());
            }
            s_Depth++;
            s_Cleanups.Add(new List<Action>());
            var context = rulebook.Context;
            if (evt.Reason == null) evt.Reason = evt;
            context.PushEvent(evt);
            try
            {
                RulebookEventBus.OnEventAboutToTrigger(evt);
                body?.Invoke();
            }
            finally
            {
                // Undo what handlers did to the game besides bonuses (see CleanedUpHandlers).
                var cleanups = s_Cleanups[s_Cleanups.Count - 1];
                s_Cleanups.RemoveAt(s_Cleanups.Count - 1);
                for (int i = cleanups.Count - 1; i >= 0; i--)
                    try { cleanups[i](); }
                    catch (Exception e) { Main.Error("dry run cleanup", e); }
                // The base end of a rule: marks it triggered and removes its temporary modifiers. The handlers
                // "did trigger" are never called.
                evt.OnDidTrigger();
                context.PopEvent(evt);
                s_Depth--;
                if (outer) ContextField.SetValue(rulebook, saved);
            }
        }

        // Called at the end of the innermost dry run, before its temporary modifiers are removed.
        internal static void AtEnd(Action cleanup)
        {
            if (s_Cleanups.Count > 0) s_Cleanups[s_Cleanups.Count - 1].Add(cleanup);
        }

        // The d20 modifiers (rerolls, "take the best of two", a fixed roll) that would apply to a roll for the rule,
        // read from a dry RuleRollD20 asked for while the rule is the current one, as Dice.D20 does.
        internal static List<D20Modifier> Rerolls(RulebookEvent rule)
        {
            D20Modifiers.Clear();
            var d20 = new RuleRollD20(rule.Initiator);
            Run(d20, null);
            return new List<D20Modifier>(D20Modifiers);
        }
    }

    // One d20 modifier as ModifyD20 sets it on a roll.
    class D20Modifier
    {
        public string Source;
        public bool Replace;
        public int Roll;
        public int Rerolls;
        public bool TakeBest;
        public bool OnlyIfFailed;
        // Added to every saving throw stat while the roll is made (AddSavingThrowBonus).
        public int SaveBonus;
        // 0..100, 100 when it always applies.
        public int Chance = 100;
        // WotR: "reroll only if succeeded", and a test of the first die against a value (ModifyD20.RollConditionType:
        // 1 more than, 2 less than, 3 less or equal, 4 more or equal, 5 equal).
        public bool OnlyIfSuccess;
        public int Compare;
        public int CompareValue;

        // Whether the modifier applies to a roll whose first die is f (ModifyD20.ShouldReroll).
        internal bool Applies(int f, Func<int, bool> failed)
        {
            if (OnlyIfFailed) return failed(f);
            if (OnlyIfSuccess) return !failed(f);
            switch (Compare)
            {
                case 1: return f > CompareValue;
                case 2: return f < CompareValue;
                case 3: return f <= CompareValue;
                case 4: return f >= CompareValue;
                case 5: return f == CompareValue;
                default: return true;
            }
        }
    }

    // Handlers that do something besides adding bonuses before a roll: while a rule runs dry they are not called.
    [HarmonyPatch]
    static class SilencedHandlers
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var m in Methods())
                if (m != null) yield return m;
                else Main.Log.Error("dry run: a handler to silence is missing in this version of the game");
        }

        static IEnumerable<MethodBase> Methods()
        {
            // Runs actions on the attacker and on itself when it is attacked.
#if WOTR
            yield return AccessTools.Method(typeof(AddTargetBeforeAttackRollTrigger), "OnEventAboutToTrigger", new[] { typeof(RuleAttackRoll) });
#else
            yield return AccessTools.Method(typeof(AddTargetBeforeAttackRollTrigger), "OnEventAboutToTrigger", new[] { typeof(RuleCalculateAttackBonus) });
#endif
            // AI of enemies with Power Attack: turns the ability on or off.
            yield return AccessTools.Method(typeof(PowerAttackWatcher), "OnEventAboutToTrigger", new[] { typeof(RuleAttackRoll) });
#if !WOTR
            // Metamagic for one spell: removes itself when the spell's parameters are calculated.
            yield return AccessTools.Method(typeof(AutoMetamagic), "OnEventAboutToTrigger", new[] { typeof(RuleCalculateAbilityParams) });
            yield return AccessTools.Method(typeof(MetamagicOnNextSpell), "OnEventAboutToTrigger", new[] { typeof(RuleCalculateAbilityParams) });
#endif
        }

        static bool Prefix() => !DryRun.Active;
    }

#if WOTR
    // Handlers that keep something for the attack in their "about to trigger" of the attack roll (a bonus for the armor
    // class or the attack bonus rule that follows, concealment ignored, an armor class modifier) and clear it in their
    // "did trigger": on a dry run they are cleared at its end the same way. Found by a search of every handler "about
    // to trigger" of the rules the mod runs dry for assignments and calls beyond bonuses; each "did trigger" only
    // clears.
    [HarmonyPatch]
    static class CleanedUpHandlers
    {
        static readonly Type[] Types =
        {
            typeof(ACBonusAgainstTarget), typeof(ACBonusAgainstBuffOwner), typeof(ACBonusAgainstFactOwner),
            typeof(ACBonusAgainstFactOwnerMultiple), typeof(ACBonusAgainstSize), typeof(ACBonusUnlessFactMultiple),
            typeof(ACContextBonusAgainstFactOwner), typeof(AttackBonusAgainstFactOwner), typeof(AttackBonusConditional),
            typeof(ShareFavoredEnemies), typeof(IgnoreConcealmentAgainstFactOwner), typeof(IgnorePartialConcealmentOnRangedAttacks),
            typeof(ACBonusAgainstSpellsWithDescriptor), typeof(ACBonusAgainstFactOwnerEquipment),
        };

        static IEnumerable<MethodBase> TargetMethods()
        {
            foreach (var type in Types)
            {
                var m = AccessTools.Method(type, "OnEventAboutToTrigger", new[] { typeof(RuleAttackRoll) });
                if (m != null) yield return m;
                else Main.Log.Error("dry run: no attack roll handler in " + type.Name);
            }
        }

        static readonly Dictionary<Type, PropertyInfo> s_Runtime = new Dictionary<Type, PropertyInfo>();

        // The handler keeps what it set in the runtime of its fact, which it reaches only while that runtime is the
        // context of the event: "did trigger" is called in it, as the event bus calls it (RulebookSubscribersList).
        static void Postfix(object __instance, RuleAttackRoll evt)
        {
            var handler = __instance as IRulebookHandler<RuleAttackRoll>;
            if (!DryRun.Active || handler == null) return;
            var runtime = Runtime(__instance);
            DryRun.AtEnd(() =>
            {
                using (runtime?.RequestEventContext()) handler.OnEventDidTrigger(evt);
            });
        }

        // The runtime of a fact's component, to call it later in its context.
        internal static ISubscriptionProxy Runtime(object component)
        {
            PropertyInfo property;
            var type = component.GetType();
            if (!s_Runtime.TryGetValue(type, out property)) s_Runtime[type] = property = AccessTools.Property(type, "Runtime");
            return property?.GetValue(component, null) as ISubscriptionProxy;
        }
    }

    // Metamagic for one spell adds its metamagic to the parameters and remembers the spell, to remove itself when that
    // spell is cast: on a dry run the metamagic counts and the spell it remembered before is put back.
    [HarmonyPatch]
    static class OneSpellMetamagic
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(AutoMetamagic), "OnEventAboutToTrigger", new[] { typeof(RuleCalculateAbilityParams) });
            yield return AccessTools.Method(typeof(MetamagicOnNextSpell), "OnEventAboutToTrigger", new[] { typeof(RuleCalculateAbilityParams) });
        }

        static void Prefix(object __instance, out object __state)
        {
            __state = DryRun.Active ? Traverse.Create(__instance).Property("Data").Field("Spell").GetValue() : null;
        }

        static void Postfix(object __instance, object __state)
        {
            if (!DryRun.Active) return;
            var runtime = CleanedUpHandlers.Runtime(__instance);
            DryRun.AtEnd(() =>
            {
                using (runtime?.RequestEventContext()) Traverse.Create(__instance).Property("Data").Field("Spell").SetValue(__state);
            });
        }
    }

#else
    // Handlers that change the game before a roll and undo it after the roll, in their "did trigger": on a dry run
    // what they did is undone at its end. Found by a search of every handler "about to trigger" of the rules the mod
    // runs dry for assignments and calls beyond bonuses (attack roll, attack bonus, armor class, weapon stats,
    // concealment, d20, flat-footed, saving throw, spell resistance, ability parameters).
    static class CleanedUpHandlers
    {
        static readonly FieldInfo IgnorePartialAttack = AccessTools.Field(typeof(IgnorePartialConcealmentOnRangedAttacks), "m_Attack");
        static readonly FieldInfo SpellsACModifier = AccessTools.Field(typeof(ACBonusAgainstSpellsWithDescriptor), "m_Mod");

        // Ignores partial concealment for the attacker until the attack ends.
        [HarmonyPatch(typeof(IgnorePartialConcealmentOnRangedAttacks), "OnEventAboutToTrigger", new[] { typeof(RuleAttackRoll) })]
        static class IgnorePartial
        {
            static void Postfix(IgnorePartialConcealmentOnRangedAttacks __instance, RuleAttackRoll evt)
            {
                if (DryRun.Active && IgnorePartialAttack.GetValue(__instance) == evt)
                    DryRun.AtEnd(() => __instance.OnEventDidTrigger(evt));
            }
        }

        // An armor class modifier against spells, removed when the attack ends.
        [HarmonyPatch(typeof(ACBonusAgainstSpellsWithDescriptor), "OnEventAboutToTrigger", new[] { typeof(RuleAttackRoll) })]
        static class SpellsAC
        {
            static void Prefix(ACBonusAgainstSpellsWithDescriptor __instance, out object __state)
            {
                __state = SpellsACModifier.GetValue(__instance);
            }

            static void Postfix(ACBonusAgainstSpellsWithDescriptor __instance, object __state)
            {
                var added = SpellsACModifier.GetValue(__instance) as ModifiableValue.Modifier;
                if (!DryRun.Active || added == null || added == __state) return;
                DryRun.AtEnd(() =>
                {
                    added.Remove();
                    SpellsACModifier.SetValue(__instance, __state);
                });
            }
        }
    }

#endif
#if WOTR
    // ModifyD20 on a dry roll: instead of setting the reroll (and pre-rolling the die for its conditions, rolling for
    // "with chance", running its actions) its parameters are written down, for the rule the roll is asked for.
    [HarmonyPatch(typeof(ModifyD20), "OnEventAboutToTrigger", new[] { typeof(RuleRollD20) })]
    static class D20ModifiersOnDryRoll
    {
        static readonly MethodInfo CheckRule = AccessTools.Method(typeof(ModifyD20), "CheckRule");
        static readonly MethodInfo FactGetter = AccessTools.PropertyGetter(typeof(UnitFactComponentDelegate<ModifyD20.ComponentData>), "Fact");
        static readonly MethodInfo ContextGetter = AccessTools.PropertyGetter(typeof(UnitFactComponentDelegate<ModifyD20.ComponentData>), "Context");

        static bool Prefix(ModifyD20 __instance, RuleRollD20 evt)
        {
            if (!DryRun.Active) return true;
            try
            {
                var rule = Rulebook.CurrentContext.PreviousEvent;
                if (evt.IsFake || rule == null || !(bool)CheckRule.Invoke(__instance, new object[] { rule })) return false;
                var fact = FactGetter.Invoke(__instance, null) as EntityFact;
                var context = ContextGetter.Invoke(__instance, null) as MechanicsContext;
                // The alignment condition as ModifyD20 tests it.
                if (__instance.AgainstAlignment && !rule.Initiator.Alignment.ValueRaw.HasComponent(__instance.Alignment)
                    && ((__instance.TargetAlignment && !rule.GetRuleTarget().Alignment.ValueRaw.HasComponent(__instance.Alignment))
                        || (!__instance.TargetAlignment && !rule.Initiator.Alignment.ValueRaw.HasComponent(__instance.Alignment))))
                    return false;
                bool successable = rule is ISuccessable;
                var m = new D20Modifier
                {
                    Source = fact?.Name,
                    Replace = __instance.Replace,
                    Roll = __instance.Replace ? __instance.RollResult.Calculate(context) : 0,
                    Rerolls = __instance.RollsAmount,
                    TakeBest = __instance.TakeBest,
                    OnlyIfFailed = successable && __instance.RerollOnlyIfFailed,
                    OnlyIfSuccess = successable && !__instance.RerollOnlyIfFailed && __instance.RerollOnlyIfSuccess,
                };
                if (!m.OnlyIfFailed && !m.OnlyIfSuccess && __instance.RollCondition != ModifyD20.RollConditionType.None)
                {
                    m.Compare = (int)__instance.RollCondition;
                    m.CompareValue = __instance.ValueToCompareRoll.Calculate(context);
                }
                // Both bonuses go on the stats while the rule is resolved: the saving throw reads its stat after the roll.
                if (__instance.AddSavingThrowBonus) m.SaveBonus += __instance.Value.Calculate(fact?.MaybeContext);
                if (__instance.AddBonus) m.SaveBonus += __instance.Bonus.Calculate(fact?.MaybeContext);
                if (__instance.WithChance)
                    m.Chance = Math.Max(0, Math.Min(100, __instance.Chance.Calculate(fact?.MaybeContext)));
                DryRun.D20Modifiers.Add(m);
            }
            catch (Exception e)
            {
                Main.Error("d20 modifier", e);
            }
            return false;
        }
    }
#else
    // ModifyD20 on a dry roll: instead of setting the reroll (and rolling dice for "only if failed" and "with chance")
    // its parameters are written down, for the rule the roll is asked for.
    [HarmonyPatch(typeof(ModifyD20), "OnEventAboutToTrigger", new[] { typeof(RuleRollD20) })]
    static class D20ModifiersOnDryRoll
    {
        static readonly MethodInfo CheckRule = AccessTools.Method(typeof(ModifyD20), "CheckRule");

        static bool Prefix(ModifyD20 __instance, RuleRollD20 evt)
        {
            if (!DryRun.Active) return true;
            try
            {
                var rule = Rulebook.CurrentContext.PreviousEvent;
                if (rule == null || !(bool)CheckRule.Invoke(__instance, new object[] { rule })) return false;
                var m = new D20Modifier
                {
                    Source = __instance.Fact?.Name,
                    Replace = __instance.Replace,
                    Roll = __instance.Roll,
                    Rerolls = __instance.RollsAmount,
                    TakeBest = __instance.TakeBest,
                    OnlyIfFailed = __instance.RerollOnlyIfFailed,
                };
                if (__instance.AddSavingThrowBonus) m.SaveBonus = __instance.Value.Calculate(__instance.Fact?.MaybeContext);
                if (__instance.WithChance)
                    m.Chance = Math.Max(0, Math.Min(100, __instance.Chance.Calculate(__instance.Fact?.MaybeContext)));
                DryRun.D20Modifiers.Add(m);
            }
            catch (Exception e)
            {
                Main.Error("d20 modifier", e);
            }
            return false;
        }
    }
#endif
}
