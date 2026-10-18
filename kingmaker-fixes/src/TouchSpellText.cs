using System;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.RuleSystem.Rules.Abilities;
using Kingmaker.UI.CombatText;
using Kingmaker.UI._ConsoleUI.Overtips;
using Kingmaker.UnitLogic.Abilities.Components;
using Kingmaker.UnitLogic.Parts;

namespace KingmakerFixes
{
    // A touch spell (Cure Light Wounds and the like, also as a potion) is cast twice: the spell itself, and then its
    // touch, a separate ability of the same name the caster holds in UnitPartTouch (AbilityEffectStickyTouch). Each cast
    // puts the spell name over the caster's head, so one potion or one touch spell shows the name twice. The text of the
    // touch is dropped, in the gamepad and the mouse interface alike; the combat log is not touched.
    static class TouchSpellText
    {
        static bool s_Failed;

        internal static bool IsTouchOfHeldSpell(RuleCastSpell evt)
        {
            try
            {
                var spell = evt.Spell;
                if (spell == null || spell.Blueprint.GetComponent<AbilityDeliverTouch>() == null) return false;
                var touch = evt.Initiator.Get<UnitPartTouch>();
                return touch != null && touch.Ability != null && touch.Ability.Blueprint == spell.Blueprint;
            }
            catch (Exception e)
            {
                if (!s_Failed) Main.Log.Error("touch spell text: " + e);
                s_Failed = true;
                return false;
            }
        }
    }

    [HarmonyPatch(typeof(OvertipsVM), nameof(OvertipsVM.OnEventDidTrigger), new Type[] { typeof(RuleCastSpell) })]
    static class TouchSpellTextGamepad
    {
        static bool Prefix(RuleCastSpell evt) => !TouchSpellText.IsTouchOfHeldSpell(evt);
    }

    [HarmonyPatch(typeof(CombatTextManager), nameof(CombatTextManager.OnEventDidTrigger), new Type[] { typeof(RuleCastSpell) })]
    static class TouchSpellTextMouse
    {
        static bool Prefix(RuleCastSpell evt) => !TouchSpellText.IsTouchOfHeldSpell(evt);
    }
}
