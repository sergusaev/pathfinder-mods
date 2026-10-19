using System;
using HarmonyLib;
using Kingmaker.Assets.Console.GamepadInput;
using Kingmaker.UI._ConsoleUI.ActionBar;
using Rewired;

namespace ActionBarRows
{
    [HarmonyPatch(typeof(ActionBarView), nameof(ActionBarView.Bind))]
    static class BindPatch
    {
        static void Postfix(ActionBarView __instance, ActionBarVM viewModel)
        {
            try { Rows.OnBind(__instance, viewModel); }
            catch (Exception e) { Main.Error(e, "rows bind"); }
        }
    }

    // The game reads the slots of the unit again (unit change, new slot, move) and turns the page.
    [HarmonyPatch(typeof(ActionBarVM), "ReInitPages")]
    static class ReInitPagesPatch
    {
        static void Postfix(ActionBarVM __instance)
        {
            var rows = Rows.Instance;
            if (rows != null && rows.Vm == __instance) rows.Refresh();
        }
    }

    [HarmonyPatch(typeof(ActionBarVM), "OnCurrentPageChanges")]
    static class PagePatch
    {
        static void Postfix(ActionBarVM __instance)
        {
            var rows = Rows.Instance;
            if (rows != null && rows.Vm == __instance) rows.Refresh();
        }
    }

    [HarmonyPatch(typeof(ActionBarView), "SetActive")]
    static class ActivePatch
    {
        static void Postfix(ActionBarView __instance, bool value)
        {
            var rows = Rows.Instance;
            if (rows != null && rows.View == __instance) rows.OnBarActive(value);
        }
    }

    // L3 in the open bar: one row more.
    [HarmonyPatch(typeof(ActionBarView), "GetInputLayer")]
    static class InputPatch
    {
        static void Postfix(ActionBarView __instance, InputLayer __result)
        {
            try
            {
                var rows = Rows.Instance;
                if (rows != null && rows.View == __instance && __result != null) rows.AddInput(__result);
            }
            catch (Exception e) { Main.Error(e, "rows input"); }
        }
    }

    [HarmonyPatch(typeof(ActionBarView), "SetPageLabel")]
    static class PageLabelPatch
    {
        static void Postfix(ActionBarView __instance)
        {
            var rows = Rows.Instance;
            if (rows != null && rows.View == __instance) rows.UpdatePageLabel();
        }
    }

    // The stick down closes the window of the weapon sets as the D-pad does: the stick up opened it.
    [HarmonyPatch(typeof(ActionBarWeaponSetsView), "GetInputLayer")]
    static class WeaponSetsInputPatch
    {
        const int LeftStickY = 1;

        static void Postfix(ActionBarWeaponSetsView __instance, InputLayer __result)
        {
            if (__result == null) return;
            var view = __instance;
            __result.AddButton(data => Traverse.Create(view).Property("ViewModel").GetValue<ActionBarWeaponSetsVm>()?.Close(),
                LeftStickY, InputActionEventType.NegativeButtonJustPressed);
        }
    }

    // With rows above, up goes to the row above. D-pad up on a slot with variants still opens them, as it does on
    // the single bar; the stick always moves.
    [HarmonyPatch(typeof(ActionBarSlotView), "get_OnUp")]
    static class SlotUpPatch
    {
        const int DPadUp = 6;

        static void Postfix(ActionBarSlotView __instance, ref Func<InputActionEventData, bool> __result)
        {
            var rows = Rows.Instance;
            if (rows == null || rows.Count <= 1 || !Rows.IsBarSlot(__instance)) return;
            var view = __instance;
            __result = data =>
            {
                var vm = view.GetViewModel();
                if (data.actionId != DPadUp || vm == null || !vm.HasConvert.Value) return false;
                rows.Vm.OnShowConvertRequest();
                return true;
            };
        }
    }
}
