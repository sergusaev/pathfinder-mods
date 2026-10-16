using System;
using HarmonyLib;
using Kingmaker.UI._ConsoleUI.ActionBar;
using UnityModManagerNet;

namespace BeltBar
{
    [HarmonyPatch(typeof(ActionBarView), nameof(ActionBarView.Initialize))]
    static class InitializePatch
    {
        static void Postfix(ActionBarView __instance)
        {
            try { BeltButton.Attach(__instance); }
            catch (Exception e) { Main.Error(e, "attach"); }
        }
    }

    [HarmonyPatch(typeof(ActionBarView), nameof(ActionBarView.Bind))]
    static class BindPatch
    {
        static void Postfix(ActionBarView __instance, ActionBarVM viewModel)
        {
            try
            {
                var button = __instance.GetComponentInChildren<BeltButton>(true);
                if (button != null) button.BarVm = viewModel;
            }
            catch (Exception e) { Main.Error(e, "bind"); }
        }
    }

    // The accept button on the belt button opens the row; the game's handler knows only its own entities.
    [HarmonyPatch(typeof(ActionBarView), "OnAcceptHandler")]
    static class AcceptPatch
    {
        static bool Prefix(ActionBarView __instance)
        {
            try
            {
                var button = __instance.GetComponentInChildren<BeltButton>(true);
                if (button == null || button.Nav == null || !ReferenceEquals(button.Nav.CurrentEntity, button)) return true;
                // The first thing the game does: a slot being moved is put down.
                if (button.BarVm != null && button.BarVm.OnClick()) return false;
                button.Open();
                return false;
            }
            catch (Exception e)
            {
                Main.Error(e, "accept");
                return true;
            }
        }
    }
}
