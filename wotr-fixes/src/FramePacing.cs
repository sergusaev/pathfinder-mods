using System;
using System.IO;
using System.Linq;
using HarmonyLib;
using Kingmaker.Settings;
using Kingmaker.Settings.Graphics;
using UnityEngine;
using UnityEngine.Scripting;

namespace WotrFixes
{
    // The game's vsync is a frame every refresh or none. A frame every N refreshes keeps every frame on the screen
    // for the same time when the game cannot draw one every refresh: on the 90 Hz Steam Deck OLED at about 35 fps the
    // frames took 22, 33 and 44 ms by turns. The frame limit of the Deck's quick access menu does not help: it limits
    // the frames shown, not those the game draws (the game kept drawing about 57 a second with 30 shown).
    static class FramePacing
    {
        internal static void Apply()
        {
            try
            {
                int interval = Main.Settings.FrameInterval;
                QualitySettings.vSyncCount = interval > 1 ? interval : (SettingsRoot.Graphics.VSync.GetValue() ? 1 : 0);
            }
            catch (Exception e)
            {
                Main.Log.Error("frame pacing: " + e);
            }
        }
    }

    // The game sets vsync whenever its graphics settings apply.
    [HarmonyPatch(typeof(GraphicsSettingsController), "ApplyGraphicsSettings")]
    static class ApplyGraphicsSettingsPatch
    {
        static void Postfix()
        {
            if (Main.Settings.FrameInterval > 1) FramePacing.Apply();
        }
    }

    // The engine reads gc-max-time-slice from boot.config before any mod is loaded: the setting takes effect at the
    // next start. Turned on, the line is written (again, if Steam put the game's own boot.config back); turned off in
    // the mod's settings, it is taken out. With the setting off the file is not touched otherwise.
    static class IncrementalGc
    {
        const string Key = "gc-max-time-slice";
        const string Line = Key + "=3";

        static string BootConfig { get { return Path.Combine(Application.dataPath, "boot.config"); } }

        internal static void Sync(bool changed)
        {
            try
            {
                var path = BootConfig;
                var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new System.Collections.Generic.List<string>();
                bool present = lines.Any(IsOurs);
                if (Main.Settings.IncrementalGc && !present)
                {
                    lines.Add(Line);
                    File.WriteAllText(path, string.Join("\n", lines) + "\n");
                    Main.Log.Log("incremental garbage collection written to boot.config, on at the next start");
                }
                else if (!Main.Settings.IncrementalGc && present && changed)
                {
                    File.WriteAllText(path, string.Join("\n", lines.Where(l => !IsOurs(l))) + "\n");
                    Main.Log.Log("incremental garbage collection taken out of boot.config, off at the next start");
                }
            }
            catch (Exception e)
            {
                Main.Log.Error("boot.config: " + e.Message);
            }
        }

        static bool IsOurs(string line)
        {
            return line.Trim().StartsWith(Key + "=", StringComparison.Ordinal);
        }

        internal static string Status()
        {
            bool on;
            try { on = GarbageCollector.isIncremental; }
            catch { return "unknown"; }
            bool wanted = Main.Settings.IncrementalGc;
            return (on ? "on" : "off") + (on != wanted ? ", " + (wanted ? "on" : "off") + " after the game is restarted" : "");
        }
    }
}
