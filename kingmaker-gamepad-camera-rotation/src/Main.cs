using System;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Assets.Console.GamepadInput;
using Kingmaker.GameModes;
using Kingmaker.UI;
using Kingmaker.UI._ConsoleUI.InputLayers.InGameLayer;
using Kingmaker.UI._ConsoleUI.LocalMap;
using Kingmaker.View;
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
        public float HintsOffsetX = 40f;
        public float HintsOffsetY;
        public float CameraHintOffsetY = 8f;
        public string WotrPath = "";

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    public static class Main
    {
        internal const int RightStickX = 2;
        internal const int RightStickY = 3;
        internal const int DPadUp = 6;
        internal const int DPadDown = 7;
        internal const int RightStickButton = 19;
        const float DeadZone = 0.2f;
        const string LocalMapContext = "LocalMapInputContext";

        internal static UnityModManager.ModEntry.ModLogger Log;
        internal static Settings Settings;
        internal static string Dir;

        internal static bool RotateMode;
        internal static float MapYaw;

        static readonly FieldInfo PlayerScroll = AccessTools.Field(typeof(CameraZoom), "m_PlayerScrollPosition");
        static readonly FieldInfo ZoomLength = AccessTools.Field(typeof(CameraZoom), "m_ZoomLenght");

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Log = modEntry.Logger;
            Dir = modEntry.Path;
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
            GUILayout.Label("Compass hints offset X: " + Settings.HintsOffsetX.ToString("0"));
            float x = GUILayout.HorizontalSlider(Settings.HintsOffsetX, -40f, 120f, GUILayout.Width(300));
            GUILayout.Label("Compass hints offset Y: " + Settings.HintsOffsetY.ToString("0"));
            float y = GUILayout.HorizontalSlider(Settings.HintsOffsetY, -60f, 60f, GUILayout.Width(300));
            GUILayout.Label("Camera mode hint offset Y: " + Settings.CameraHintOffsetY.ToString("0"));
            float c = GUILayout.HorizontalSlider(Settings.CameraHintOffsetY, -40f, 80f, GUILayout.Width(300));
            GUILayout.Label("Wrath of the Righteous folder (empty = search Steam libraries), used once to extract the compass sprites:");
            Settings.WotrPath = GUILayout.TextField(Settings.WotrPath ?? "", GUILayout.Width(500));
            if (x != Settings.HintsOffsetX || y != Settings.HintsOffsetY || c != Settings.CameraHintOffsetY)
            {
                Settings.HintsOffsetX = Mathf.Round(x);
                Settings.HintsOffsetY = Mathf.Round(y);
                Settings.CameraHintOffsetY = Mathf.Round(c);
                Compass.PlaceHints();
            }
        }

        internal static void ToggleRotateMode()
        {
            RotateMode = !RotateMode;
            Game.Instance.UI.Common.UISound.Play(UISoundType.ButtonClick);
            InputRemap.UpdateCameraHintLabel();
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
                CameraRig rig = Game.Instance.UI.GetCameraRig();
                if (rig == null) return;
                Compass.Tick(Mathf.DeltaAngle(MapYaw, rig.transform.eulerAngles.y));

                if (!RotateMode) return;
                Rewired.Player player = GamePad.Instance.Player;
                InputLayer top = GamePad.Instance.CurrentInputLayer;
                if (player == null || top == null) return;
                bool inGame = top is InGameInputLayer && CameraControllable();
                bool onMap = top.ContextName == LocalMapContext;
                if (!inGame && !onMap) return;

                float x = player.GetAxis(RightStickX);
                float y = player.GetAxis(RightStickY);
                float step = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

                if (Mathf.Abs(x) > DeadZone)
                {
                    float yaw = x * Settings.RotationSpeed * step * (Settings.InvertRotation ? -1f : 1f);
                    rig.transform.rotation = Quaternion.Euler(0f, rig.transform.eulerAngles.y + yaw, 0f);
                }
                if (inGame && Mathf.Abs(y) > DeadZone && rig.CameraZoom != null)
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
                Log?.Error(e.ToString());
            }
        }
    }

    // Area entry passes the default yaw of the area part; it is north for the compass and the fixed yaw of the local map.
    [HarmonyPatch(typeof(CameraRig), "SetRotation")]
    static class SetRotationPatch
    {
        [HarmonyPriority(Priority.First)]
        static void Prefix(float cameraRotation)
        {
            Main.MapYaw = cameraRotation;
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

    [HarmonyPatch(typeof(LocalMapView), "OnMoveCamera")]
    static class LocalMapMoveCameraPatch
    {
        static bool Prefix()
        {
            return !Main.RotateMode;
        }
    }
}
