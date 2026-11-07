using HarmonyLib;
using Kingmaker.UI._ConsoleUI;

namespace KingmakerFixes
{
    // The gamepad's inventory (and loot) the first time it is opened puts each item at its stored slot index, kept
    // from the mouse interface's grid of another width and counted over all the tabs, so the items of the tab shown
    // stand scattered with empty slots between them. Every later refresh lays them out one after another (the slot
    // indices made anew, SlotsGroupVM.UpdateVisibleCollection with force). The first one does the same now.
    [HarmonyPatch(typeof(SlotsGroupVM), nameof(SlotsGroupVM.UpdateVisibleCollection))]
    static class ConsoleStashOrder
    {
        static void Prefix(SlotsGroupVM __instance, ref bool force)
        {
            if (__instance.VisibleCollection == null) force = true;
        }
    }
}
