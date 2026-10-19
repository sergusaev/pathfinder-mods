using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.UI.MVVM._ConsoleView.ActionBar;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using Owlcat.Runtime.UI.ConsoleTools.ClickHandlers;
using Owlcat.Runtime.UI.ConsoleTools.GamepadInput;
using Owlcat.Runtime.UI.ConsoleTools.NavigationTool;

namespace ActionBarRows
{
    [HarmonyPatch(typeof(ActionBarConsoleView), "BindViewImplementation")]
    static class BindPatch
    {
        static void Postfix(ActionBarConsoleView __instance)
        {
            try { Rows.OnBind(__instance); }
            catch (Exception e) { Main.Error(e, "rows bind"); }
        }
    }

    // The game set up the slots of its page: on bind, page turn, unit change, after a move.
    [HarmonyPatch(typeof(ActionBarConsoleView), "SetupSlots")]
    static class SetupSlotsPatch
    {
        static void Postfix(ActionBarConsoleView __instance)
        {
            var rows = Rows.Instance;
            if (rows != null && rows.View == __instance) rows.Refresh();
        }
    }

    [HarmonyPatch(typeof(ActionBarVM), "OnLateUpdateHandler")]
    static class ResourcesPatch
    {
        static void Postfix(ActionBarVM __instance)
        {
            var rows = Rows.Instance;
            if (rows != null && rows.Vm == __instance) rows.UpdateResources();
        }
    }

    // L3 in the open bar: one row more. The game makes the layer inside its bind, before BindPatch.
    [HarmonyPatch(typeof(ActionBarConsoleView), "GetInputLayer")]
    static class InputPatch
    {
        static void Postfix(ActionBarConsoleView __instance, InputLayer __result)
        {
            try
            {
                if (__result != null) Rows.Attach(__instance).AddInput(__result);
            }
            catch (Exception e) { Main.Error(e, "rows input"); }
        }
    }

    [HarmonyPatch(typeof(ActionBarConsoleView), nameof(ActionBarConsoleView.OnFocusEntity))]
    static class FocusPatch
    {
        static void Postfix(ActionBarConsoleView __instance)
        {
            var rows = Rows.Instance;
            if (rows != null && rows.View == __instance) rows.UpdatePageLabel();
        }
    }

    [HarmonyPatch(typeof(ActionBarBaseView), "SetPageLabel")]
    static class PageLabelPatch
    {
        static void Postfix(ActionBarBaseView __instance)
        {
            var rows = Rows.Instance;
            if (rows != null && rows.View == __instance) rows.UpdatePageLabel();
        }
    }

    // Up on the bar. The stick and the D-pad set the same flags of the navigation, only the D-pad's own button tells
    // them apart.
    //  - On the weapon set button, up opens its window, with the stick as with the D-pad (Belt Bar's button does the
    //    same itself).
    //  - On a slot with variants, the D-pad opens them, as on the single bar; with rows above it would also move up.
    //    The stick moves.
    [HarmonyPatch(typeof(ConsoleNavigationBehaviour), "HandleUp")]
    static class UpPatch
    {
        const int DPadUp = 6;

        static bool Prefix(ConsoleNavigationBehaviour __instance, ref bool __result)
        {
            var rows = Rows.Instance;
            if (rows == null || rows.Nav != __instance) return true;
            if (__instance.CurrentEntity is ActionBarWeaponConsoleView weapon && weapon.CanConfirmClick())
            {
                weapon.OnConfirmClick();
                __result = true;
                return false;
            }
            if (rows.Count <= 1 || !rows.IsConvertFocused || !GamePad.Instance.Player.GetButton(DPadUp)) return true;
            __result = false;
            return false;
        }
    }

    // Down from the window of the weapon sets, with the stick as with the D-pad, closes it: up opened it.
    [HarmonyPatch(typeof(ActionBarWeaponConsoleView), "GetInputLayer")]
    static class WeaponSetsInputPatch
    {
        internal static readonly Dictionary<ConsoleNavigationBehaviour, ActionBarWeaponConsoleView> Windows =
            new Dictionary<ConsoleNavigationBehaviour, ActionBarWeaponConsoleView>();

        static void Postfix(ActionBarWeaponConsoleView __instance, ConsoleNavigationBehaviour navigationBehavior)
        {
            if (navigationBehavior != null) Windows[navigationBehavior] = __instance;
        }
    }

    [HarmonyPatch(typeof(ConsoleNavigationBehaviour), "HandleDown")]
    static class DownPatch
    {
        static void Postfix(ConsoleNavigationBehaviour __instance, bool __result)
        {
            ActionBarWeaponConsoleView view;
            if (__result || !WeaponSetsInputPatch.Windows.TryGetValue(__instance, out view) || view == null) return;
            if (view.IsVisibleWeaponSet.Value) view.OnWeaponSetVisibleChange(false);
        }
    }
}
