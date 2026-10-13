using System;
using System.Collections.Generic;
using System.Globalization;
using HarmonyLib;
using Kingmaker;
using UnityEngine;
using UnityModManagerNet;

namespace TimeControl
{
    public class Settings : UnityModManager.ModSettings
    {
        // Duration multipliers; 1 is the game's own duration.
        public float EventFactor = 1f;
        public float ProjectFactor = 1f;
        public float ClaimFactor = 1f;
        public float RankupFactor = 1f;
        public float RulerFactor = 1f;
        public float BuildingFactor = 1f;
        public float TravelFactor = 1f;
        public float RestFactor = 1f;

        // Button next to the buff bar in keyboard and mouse mode.
        public bool BarButton = true;
        public float BarOffsetX;
        public float BarOffsetY;

        public bool ShowHidden = true;
        // Deadlines after which something fails are tracked as soon as they appear.
        public bool AutoTrackCritical = true;
        public bool PauseOnAlert = true;
        // Hours before the deadline; 0 is the deadline itself.
        // Null until loaded: XmlSerializer adds to a list it finds already filled.
        public List<int> DefaultAlerts;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    public static class Main
    {
        internal static UnityModManager.ModEntry Mod;
        internal static Settings Settings;

        static readonly string[] s_FactorNames = { "EventFactor", "ProjectFactor", "ClaimFactor", "RankupFactor", "RulerFactor", "BuildingFactor", "TravelFactor", "RestFactor" };
        static readonly string[] s_Edit = new string[8];

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Mod = modEntry;
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            if (Settings.DefaultAlerts == null) Settings.DefaultAlerts = new List<int> { 168, 24, 0 };
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = e => Settings.Save(e);
            modEntry.OnUpdate = OnUpdate;
            MenuCards.Cards.Add(new MenuCard
            {
                Id = "TimeControl",
                Title = () => S.WindowTitle,
                Open = () => TimersWindow.Open(0),
                BigResource = "TimeControl.card_time.png",
                SmallResource = "TimeControl.card_time_small.png",
                Tint = new Color32(0x5B, 0x6B, 0xB8, 255),
                Menus = new[] { typeof(Kingmaker.UI._ConsoleUI.InGameMenu.InGameMenuView), typeof(Kingmaker.UI._ConsoleUI.InGameMenu.GlobalMapMenuView) }
            });
            new Harmony(modEntry.Info.Id).PatchAll(typeof(Main).Assembly);
            return true;
        }

        internal static void Log(string message)
        {
            Mod.Logger.Log(message);
        }

        internal static void Error(Exception e, string where)
        {
            Mod.Logger.Error(where + ": " + e);
        }

        internal static float GetFactor(int i)
        {
            return (float)typeof(Settings).GetField(s_FactorNames[i]).GetValue(Settings);
        }

        internal static void SetFactor(int i, float value)
        {
            typeof(Settings).GetField(s_FactorNames[i]).SetValue(Settings, Factors.Clamp(value));
        }

        internal static bool InGame
        {
            get
            {
                var game = Game.Instance;
                return game != null && game.Player != null && game.RootUiContext != null && !game.RootUiContext.IsMainMenu
                    && !Kingmaker.EntitySystem.Persistence.LoadingProcess.Instance.IsLoadingInProcess;
            }
        }

        static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            try
            {
                Modal.TickHold();
                if (!InGame) return;
                MenuCards.Tick();
                Tracker.Tick();
                BarButton.Ensure();
            }
            catch (Exception e)
            {
                Error(e, "OnUpdate");
            }
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            GUILayout.Label("<b>Duration coefficients</b> (×0.5 = twice as fast, ×2 = twice as long; fractions allowed, 0.1 … 10)");
            string[] labels = {
                "Kingdom event resolution", "Kingdom projects", "Claiming and upgrading regions", "Advisor rank-ups",
                "Ruler's time spent on projects", "Building construction (new buildings)", "Travel time on the global map", "Camping and sleeping time"
            };
            bool pending = false;
            for (int i = 0; i < 8; i++)
            {
                if (s_Edit[i] == null) s_Edit[i] = GetFactor(i).ToString("0.###", CultureInfo.InvariantCulture);
                GUILayout.BeginHorizontal();
                GUILayout.Label(labels[i], GUILayout.Width(300));
                s_Edit[i] = GUILayout.TextField(s_Edit[i], GUILayout.Width(80));
                float value;
                bool ok = TryParse(s_Edit[i], out value);
                if (!ok) GUILayout.Label("<color=orange>not a number</color>");
                else if (Math.Abs(Factors.Clamp(value) - GetFactor(i)) > 0.0001f)
                {
                    pending = true;
                    GUILayout.Label("now ×" + GetFactor(i).ToString("0.###", CultureInfo.InvariantCulture));
                }
                GUILayout.EndHorizontal();
            }
            GUILayout.BeginHorizontal();
            if (GUILayout.Button(pending ? "<b>Apply and check deadlines</b>" : "Apply and check deadlines", GUILayout.Width(260)))
            {
                var values = new float[8];
                for (int i = 0; i < 8; i++)
                {
                    float value;
                    values[i] = TryParse(s_Edit[i], out value) ? Factors.Clamp(value) : GetFactor(i);
                }
                Factors.ApplyAndReport(values);
                for (int i = 0; i < 8; i++) s_Edit[i] = null;
            }
            if (GUILayout.Button("Reset to ×1", GUILayout.Width(120)))
                for (int i = 0; i < 8; i++) s_Edit[i] = "1";
            GUILayout.EndHorizontal();

            GUILayout.Space(10);
            GUILayout.Label("<b>Deadlines window</b>");
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Open the deadlines window", GUILayout.Width(260)) && InGame)
                TimersWindow.Open(0);
            GUILayout.EndHorizontal();
            Settings.BarButton = GUILayout.Toggle(Settings.BarButton, " Button to the right of the buff bar (keyboard and mouse mode)");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Button offset X: " + Settings.BarOffsetX.ToString("0"), GUILayout.Width(150));
            Settings.BarOffsetX = Mathf.Round(GUILayout.HorizontalSlider(Settings.BarOffsetX, -300f, 600f, GUILayout.Width(250)));
            GUILayout.Label("  Y: " + Settings.BarOffsetY.ToString("0"), GUILayout.Width(60));
            Settings.BarOffsetY = Mathf.Round(GUILayout.HorizontalSlider(Settings.BarOffsetY, -200f, 400f, GUILayout.Width(250)));
            GUILayout.EndHorizontal();
            Settings.ShowHidden = GUILayout.Toggle(Settings.ShowHidden, " Show hidden timers (ones the game does not show)");
            Settings.AutoTrackCritical = GUILayout.Toggle(Settings.AutoTrackCritical, " Track deadlines after which something fails as soon as they appear");
            Settings.PauseOnAlert = GUILayout.Toggle(Settings.PauseOnAlert, " Pause the game when an alert pops up");

            GUILayout.Label("Default alerts for a newly tracked deadline:");
            GUILayout.BeginHorizontal();
            foreach (int hours in Tracker.AlertPresets)
            {
                bool on = Settings.DefaultAlerts.Contains(hours);
                bool next = GUILayout.Toggle(on, " " + PresetName(hours), GUILayout.Width(95));
                if (next && !on) Settings.DefaultAlerts.Add(hours);
                if (!next && on) Settings.DefaultAlerts.Remove(hours);
            }
            GUILayout.EndHorizontal();
        }

        static string PresetName(int hours)
        {
            if (hours == 0) return "when due";
            if (hours % 24 == 0) return hours / 24 + " d";
            return hours + " h";
        }

        static bool TryParse(string text, out float value)
        {
            return float.TryParse((text ?? "").Trim().Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out value);
        }
    }
}
