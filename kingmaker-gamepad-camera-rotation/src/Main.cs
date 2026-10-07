using System;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Assets.Console.GamepadInput;
using Kingmaker.GameModes;
using Kingmaker.UI;
using Kingmaker.UI._ConsoleUI.InputLayers.InGameLayer;
using Kingmaker.View;
using Rewired;
using UnityEngine;
using UnityModManagerNet;

namespace GamepadCameraRotation
{
    public class Settings : UnityModManager.ModSettings
    {
        public float RotationSpeed = 120f;
        public float ZoomSpeed = 0.8f;
        public bool InvertRotation;
        public bool InvertZoom;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    public static class Main
    {
        const int RightStickX = 2;
        const int RightStickY = 3;
        const int RightStickButton = 19;
        const float DeadZone = 0.2f;

        internal static UnityModManager.ModEntry.ModLogger Log;
        internal static Settings Settings;

        internal static bool RotateMode;
        internal static bool R3Armed;
        internal static bool Bypass;

        static readonly FieldInfo PlayerScroll = AccessTools.Field(typeof(CameraZoom), "m_PlayerScrollPosition");
        static readonly FieldInfo ZoomLength = AccessTools.Field(typeof(CameraZoom), "m_ZoomLenght");
        static readonly MethodInfo ToggleTurnBased =
            AccessTools.Method(typeof(InGameInputLayerView), "ChangeTurnBasedModeState");

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Log = modEntry.Logger;
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = e => Settings.Save(e);
            modEntry.OnUpdate = OnUpdate;
            new Harmony(modEntry.Info.Id).PatchAll(typeof(Main).Assembly);
            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.Label("Rotation speed, deg/s: " + Settings.RotationSpeed.ToString("0"));
            Settings.RotationSpeed = GUILayout.HorizontalSlider(Settings.RotationSpeed, 30f, 360f, GUILayout.Width(300));
            GUILayout.Label("Zoom speed: " + Settings.ZoomSpeed.ToString("0.00"));
            Settings.ZoomSpeed = GUILayout.HorizontalSlider(Settings.ZoomSpeed, 0.2f, 3f, GUILayout.Width(300));
            Settings.InvertRotation = GUILayout.Toggle(Settings.InvertRotation, "Invert rotation");
            Settings.InvertZoom = GUILayout.Toggle(Settings.InvertZoom, "Invert zoom");
        }

        static bool InGameLayerActive()
        {
            return GamePad.HasInstance && GamePad.Instance.CurrentInputLayer is InGameInputLayer;
        }

        static bool CameraControllable()
        {
            var game = Game.Instance;
            return game != null
                && (game.CurrentMode == GameModeType.Default || game.CurrentMode == GameModeType.Pause)
                && !game.CutsceneLock;
        }

        static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            try
            {
                if (!GamePad.HasInstance || Game.Instance == null || !Game.Instance.IsControllerGamepad) return;
                Rewired.Player player = GamePad.Instance.Player;
                if (player == null) return;

                if (R3Armed)
                {
                    if (player.GetButtonLongPress(RightStickButton))
                    {
                        R3Armed = false;
                        RotateMode = !RotateMode;
                        Game.Instance.UI.Common.UISound.Play(RotateMode ? UISoundType.ButtonClick : UISoundType.MapClose);
                    }
                    else if (!player.GetButton(RightStickButton))
                    {
                        R3Armed = false;
                        Bypass = true;
                        try { ToggleTurnBased.Invoke(null, new object[] { default(InputActionEventData) }); }
                        finally { Bypass = false; }
                    }
                }

                if (!RotateMode || !InGameLayerActive() || !CameraControllable()) return;
                CameraRig rig = Game.Instance.UI.GetCameraRig();
                if (rig == null) return;

                float x = player.GetAxis(RightStickX);
                float y = player.GetAxis(RightStickY);
                float step = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

                if (Mathf.Abs(x) > DeadZone)
                {
                    float yaw = x * Settings.RotationSpeed * step * (Settings.InvertRotation ? -1f : 1f);
                    rig.transform.rotation = Quaternion.Euler(0f, rig.transform.eulerAngles.y + yaw, 0f);
                }
                if (Mathf.Abs(y) > DeadZone && rig.CameraZoom != null)
                {
                    CameraZoom zoom = rig.CameraZoom;
                    float len = (float)ZoomLength.GetValue(zoom);
                    float delta = y * Settings.ZoomSpeed * len * step * (Settings.InvertZoom ? -1f : 1f);
                    PlayerScroll.SetValue(zoom, Mathf.Clamp((float)PlayerScroll.GetValue(zoom) + delta, 0f, len));
                }
            }
            catch (Exception e)
            {
                RotateMode = false;
                R3Armed = false;
                Log?.Error(e.ToString());
            }
        }
    }

    // R3 press is delivered here only while the in-game layer is on top; release toggles turn-based mode, hold toggles rotate mode.
    [HarmonyPatch(typeof(InGameInputLayerView), "ChangeTurnBasedModeState")]
    static class TurnBasedTogglePatch
    {
        static bool Prefix()
        {
            if (Main.Bypass) return true;
            Main.R3Armed = true;
            return false;
        }
    }

    // While rotate mode is on, the right stick rotates and zooms instead of panning.
    [HarmonyPatch(typeof(InGameInputLayer), "OnMoveCamera")]
    static class MoveCameraPatch
    {
        static bool Prefix()
        {
            return !Main.RotateMode;
        }
    }
}
