using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace PortraitGallery
{
    // One scrolling list of portraits in character creation, in both interfaces, split into collapsible categories:
    // Kingmaker / Wrath of the Righteous / WotR characters / Custom. Portraits worn by any character of the game
    // are not offered (Gallery.cs). The log lines start with "gallery:" and "ban:".
    // The WotR categories come from portrait folders the mod writes itself from an installed WotR (Export/), when the
    // game starts and when the mod is switched on; "wotr export:" in the log.
    public static class Main
    {
        internal static UnityModManager.ModEntry.ModLogger Log;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Log = modEntry.Logger;
            new Harmony(modEntry.Info.Id).PatchAll(typeof(Main).Assembly);
            modEntry.OnUpdate = (m, dt) => Flush();
            modEntry.OnToggle = (m, on) => { if (on) StartExport(); return true; };
            StartExport();
            return true;
        }

        static readonly Queue<string> s_Lines = new Queue<string>();
        static volatile bool s_Exporting, s_Exported;

        // The WotR portraits are brought up to date on a background thread: reading the bundle and writing the
        // pictures takes seconds, and the menu need not wait for it.
        static void StartExport()
        {
            if (s_Exporting) return;
            s_Exporting = true;
            string data = Application.dataPath;
            string portraits = Path.Combine(Application.persistentDataPath, "Portraits");
            var thread = new Thread(() =>
            {
                try
                {
                    Directory.CreateDirectory(portraits);
                    Export.WotrExport.Run(data, portraits, Line);
                }
                catch (Exception e) { Line("wotr export: failed: " + e); }
                finally { s_Exported = true; s_Exporting = false; }
            });
            thread.IsBackground = true;
            thread.Priority = System.Threading.ThreadPriority.BelowNormal;
            thread.Start();
        }

        static void Line(string text) { lock (s_Lines) s_Lines.Enqueue(text); }

        // Log lines of the export thread go out on the main thread; when it ends, the gallery reads the folders anew.
        static void Flush()
        {
            lock (s_Lines)
                while (s_Lines.Count > 0) Log.Log(s_Lines.Dequeue());
            if (s_Exported) { s_Exported = false; Gallery.Forget(); }
        }

        internal static void Safe(string what, Action action)
        {
            try { action(); }
            catch (Exception e) { Log.Error(what + ": " + e); }
        }
    }
}
