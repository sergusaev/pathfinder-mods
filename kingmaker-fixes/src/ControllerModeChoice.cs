using System;
using HarmonyLib;
using Kingmaker.UI.Common;
using Kingmaker.UI._ConsoleUI.ChoseControllerMode;
using UnityEngine;

namespace KingmakerFixes
{
    // The start window "press A or any key" now and then started the game in the keyboard and mouse interface after A
    // was pressed on a connected gamepad. The window picks the keyboard on any keyboard key, checked in LateUpdate after
    // the gamepad buttons; the suspicion is that while the game window is out of focus (it flickers while it goes full
    // screen) Steam Input sends A as Enter through its desktop layout. Nothing is changed here: the keys that made the
    // keyboard choice are written to the log, to see what came.
    [HarmonyPatch(typeof(ChoseControllerModeWindowView), "LateUpdate")]
    static class ControllerModeChoice
    {
        static bool s_Failed;

        static void Prefix()
        {
            try
            {
                if (!UIUtility.IsAnyKeyboardKeyDown) return;
                var keys = "";
                foreach (KeyCode key in Enum.GetValues(typeof(KeyCode)))
                {
                    if (key >= KeyCode.Mouse0 || !Input.GetKeyDown(key)) continue;
                    keys += (keys.Length == 0 ? "" : ", ") + key;
                }
                Main.Log.Log("controller mode window: the keyboard interface is chosen by " + keys);
            }
            catch (Exception e)
            {
                if (!s_Failed) Main.Log.Error("controller mode window: " + e);
                s_Failed = true;
            }
        }
    }
}
