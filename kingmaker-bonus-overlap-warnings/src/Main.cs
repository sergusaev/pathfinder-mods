using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker;
using UnityEngine;
using UnityModManagerNet;

namespace BonusOverlapWarnings
{
    public class Settings : UnityModManager.ModSettings
    {
        // The blue mark of an item whose bonus works and leaves another one without use.
        public bool MarkOverrides = true;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    public static class Main
    {
        internal static UnityModManager.ModEntry Mod;
        internal static Settings Settings;
        static readonly HashSet<string> s_Errors = new HashSet<string>();

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Mod = modEntry;
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = e => Settings.Save(e);
            modEntry.OnUpdate = OnUpdate;
            new Harmony(modEntry.Info.Id).PatchAll(typeof(Main).Assembly);
            return true;
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
                if (!InGame) return;
                Marks.Tick();
            }
            catch (Exception e)
            {
                Error(e, "OnUpdate");
            }
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            var s = Settings;
            bool overrides = GUILayout.Toggle(s.MarkOverrides, " " + Strings.SettingOverrides);
            if (overrides != s.MarkOverrides)
            {
                s.MarkOverrides = overrides;
                Marks.Refresh();
            }
            GUILayout.Label(Strings.SettingsNote);
        }

        // Each kind of error once to the UMM log.
        internal static void Error(Exception e, string what)
        {
            if (s_Errors.Add(what + e.GetType().Name)) Mod.Logger.Error(what + ": " + e);
        }
    }
}
