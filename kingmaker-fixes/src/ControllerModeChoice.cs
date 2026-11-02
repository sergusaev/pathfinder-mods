using System;
using HarmonyLib;
using Kingmaker.UI.Common;
using Kingmaker.UI._ConsoleUI.ChoseControllerMode;
using UnityEngine;

namespace KingmakerFixes
{
    // Unity starts the game in the screen mode it remembered on the last exit (Screenmanager Fullscreen mode), which
    // can differ from the game's own setting (Kingmaker.FullscreenMode); the game puts its setting back only in the
    // main menu, after the "press A or any key" window. Started in a window, that window took a keyboard key that was
    // never pressed and chose the keyboard; started full screen, it chose right. The game's own screen setting is
    // now applied as soon as the mod loads, before that window.
    static class StartScreenMode
    {
        internal static void Apply()
        {
            try
            {
                int pref = PlayerPrefs.GetInt("Kingmaker.FullscreenMode", 1);
                var mode = pref >= 0 && pref <= 3 ? (FullScreenMode)pref : FullScreenMode.FullScreenWindow;
                int w = PlayerPrefs.GetInt("Kingmaker.ScreenWidth", 0), h = PlayerPrefs.GetInt("Kingmaker.ScreenHeight", 0);
                if (w < 640 || h < 480) { w = Screen.currentResolution.width; h = Screen.currentResolution.height; }
                if (Screen.fullScreenMode == mode && Screen.width == w && Screen.height == h)
                {
                    Main.Log.Log(string.Format("start screen: {0} {1}x{2}, as set in the game", mode, w, h));
                    return;
                }
                Main.Log.Log(string.Format("start screen: {0} {1}x{2} -> {3} {4}x{5}, as set in the game",
                    Screen.fullScreenMode, Screen.width, Screen.height, mode, w, h));
                Screen.SetResolution(w, h, mode);
            }
            catch (Exception e)
            {
                Main.Log.Error("start screen: " + e);
            }
        }
    }

    // At start the game shows the "press A or any key" window only if Rewired sees a gamepad at that moment
    // (GamepadPluggedUnpluggedVM.GamepadIsConnected in GameStarter.StartGame); with none it silently takes the
    // keyboard interface. The user sees that only when the game starts in a window. Nothing is changed: what the game
    // sees when it decides, and when a gamepad appears afterwards, goes to the log.
    [HarmonyPatch(typeof(GamepadPluggedUnpluggedVM), nameof(GamepadPluggedUnpluggedVM.GamepadIsConnected), MethodType.Getter)]
    static class ControllerModeStart
    {
        static int s_Calls;
        static bool s_Watching;

        static void Postfix(bool __result)
        {
            try
            {
                if (s_Calls++ >= 3) return;
                string[] names = Input.GetJoystickNames();
                Main.Log.Log(string.Format("controller mode start: gamepad seen {0} (Rewired ready {1}, joysticks {2}); Unity joysticks [{3}]; "
                    + "window focused {4}, screen {5} {6}x{7}; {8:F1} s after start",
                    __result, Rewired.ReInput.isReady, Rewired.ReInput.isReady && Rewired.ReInput.controllers != null ? Rewired.ReInput.controllers.joystickCount : -1,
                    string.Join(", ", names), Application.isFocused, Screen.fullScreenMode, Screen.width, Screen.height, Time.realtimeSinceStartup));
                if (!s_Watching && Rewired.ReInput.isReady)
                {
                    s_Watching = true;
                    Rewired.ReInput.ControllerConnectedEvent += e =>
                        Main.Log.Log(string.Format("controller mode start: gamepad connected later: {0}, {1:F1} s after start, window focused {2}",
                            e.name, Time.realtimeSinceStartup, Application.isFocused));
                }
            }
            catch (Exception e)
            {
                Main.Log.Error("controller mode start: " + e);
            }
        }
    }

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
