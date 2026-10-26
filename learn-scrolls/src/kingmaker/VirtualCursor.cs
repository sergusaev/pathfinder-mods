using HarmonyLib;
using Kingmaker.Assets.Console.GamepadInput;
using Kingmaker.UI._ConsoleUI;

namespace LearnScrolls
{
    // The console interface has a virtual mouse moved by the left stick (RewiredCursorController). It is on whenever the
    // game's base layer wants it (the global map, cursor mode), whatever layer is on top, and its hidden pointer lit up
    // rows of the window: a second highlight, apart from the window's own, that the left stick moved. Off while the window
    // is open (as in the buff menu of Buff It 2 The Limit).
    [HarmonyPatch(typeof(GamePad), "RestoreCursorState")]
    static class VirtualCursor
    {
        static void Postfix()
        {
            if (Window.IsOpen) Hide();
        }

        internal static void Hide()
        {
            var cursor = RewiredCursorController.Instance;
            if (cursor == null) return;
            cursor.Enabled = false;
            if (cursor.Cursor != null) cursor.Cursor.SetActive(false);
        }

        internal static void Restore()
        {
            if (GamePad.HasInstance && RewiredCursorController.Instance != null) GamePad.Instance.RestoreCursorState();
        }
    }
}
