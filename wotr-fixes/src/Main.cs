using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace WotrFixes
{
    public class Settings : UnityModManager.ModSettings
    {
        // With vsync on, a frame every this many refreshes of the screen; 0 leaves the game's own vsync.
        public int FrameInterval = 0;

        // The engine's incremental garbage collection, set in Wrath_Data/boot.config.
        public bool IncrementalGc = false;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    public static class Main
    {
        internal static UnityModManager.ModEntry.ModLogger Log;
        internal static Settings Settings;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Log = modEntry.Logger;
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = e => Settings.Save(e);
            new Harmony(modEntry.Info.Id).PatchAll(typeof(Main).Assembly);
            if (Settings.FrameInterval > 1) FramePacing.Apply();
            IncrementalGc.Sync(false);
            QuitWatchdog.Arm();
            return true;
        }

        static readonly string[] Intervals = { "Game's own", "Every 2nd", "Every 3rd", "Every 4th" };

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            int hz = Screen.currentResolution.refreshRate;
            GUILayout.Label("Frame pacing: a frame every N refreshes of the screen (" + hz + " Hz now), the same time for every frame. "
                + "On the Steam Deck use it instead of the frame limit of the quick access menu, which limits only what is shown: "
                + "the game keeps drawing faster and the picture judders.");
            int selected = Mathf.Clamp(Settings.FrameInterval - 1, 0, Intervals.Length - 1);
            int chosen = GUILayout.Toolbar(selected, Intervals);
            if (chosen != selected)
            {
                Settings.FrameInterval = chosen == 0 ? 0 : chosen + 1;
                FramePacing.Apply();
            }
            if (Settings.FrameInterval > 1 && hz > 0)
                GUILayout.Label("  " + hz + " Hz / " + Settings.FrameInterval + " = " + Mathf.RoundToInt((float)hz / Settings.FrameInterval) + " fps");

            GUILayout.Space(10);
            bool gc = GUILayout.Toggle(Settings.IncrementalGc, " Incremental garbage collection: the collector's pauses (about 0.3 s "
                + "every few minutes on the Steam Deck) are split into slices of 3 ms. Written to Wrath_Data/boot.config, takes "
                + "effect at the next start of the game; written back at start if a Steam update or file check removed it.");
            if (gc != Settings.IncrementalGc)
            {
                Settings.IncrementalGc = gc;
                IncrementalGc.Sync(true);
            }
            GUILayout.Label("  Now: " + IncrementalGc.Status());
        }
    }
}
