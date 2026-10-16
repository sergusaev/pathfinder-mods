using System;
using HarmonyLib;
using Kingmaker.UI.MVVM._ConsoleView.ActionBar;

namespace BeltBar
{
    [HarmonyPatch(typeof(ActionBarConsoleView), "BindViewImplementation")]
    static class BindPatch
    {
        static void Postfix(ActionBarConsoleView __instance)
        {
            try { BeltButton.Attach(__instance); }
            catch (Exception e) { Main.Error(e, "attach"); }
        }
    }

    // D-pad up on the weapon set button opens its window; on the belt button it opens the row.
    [HarmonyPatch(typeof(ActionBarConsoleView), "OnDPadUp")]
    static class DPadUpPatch
    {
        static bool Prefix(ActionBarConsoleView __instance)
        {
            try
            {
                var button = __instance.GetComponentInChildren<BeltButton>(true);
                if (button == null || !button.IsFocused) return true;
                button.Open();
                return false;
            }
            catch (Exception e)
            {
                Main.Error(e, "dpad up");
                return true;
            }
        }
    }

    // The bar knows what to show for each of its own entities; the belt button is treated like the weapon set button:
    // no slot is current, there is nothing to name or to open a context menu for, and up opens the row.
    [HarmonyPatch(typeof(ActionBarConsoleView), nameof(ActionBarConsoleView.OnFocusEntity))]
    static class FocusPatch
    {
        static bool Prefix(ActionBarConsoleView __instance, Owlcat.Runtime.UI.ConsoleTools.IConsoleEntity entity)
        {
            var button = entity as BeltButton;
            if (button == null) return true;
            try
            {
                if (button.BarVm != null) button.BarVm.SetCurrentSlot(null);
                var bar = Traverse.Create(__instance);
                bar.Field("m_IsVisibleContextMenuHint").GetValue<UniRx.BoolReactiveProperty>().Value = false;
                bar.Field("m_SpellNameGroup").GetValue<UnityEngine.GameObject>().SetActive(false);
                bar.Field("m_IsConvert").GetValue<UniRx.BoolReactiveProperty>().Value = true;
                Kingmaker.UI.MVVM._VM.Tooltip.Utils.TooltipHelper.HideTooltip();
            }
            catch (Exception e) { Main.Error(e, "focus"); }
            return false;
        }
    }
}
