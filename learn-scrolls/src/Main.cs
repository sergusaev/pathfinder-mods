using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker;
using UnityEngine;
using UnityModManagerNet;

namespace LearnScrolls
{
    public class Settings : UnityModManager.ModSettings
    {
        // Who gets a scroll there are too few copies of: the criteria in this order, each next one only between the
        // characters the ones before left equal. Off criteria are skipped; the order in the party decides the rest.
        public bool CanCastNow = true;
        public bool NotOpposition = true;
        public bool HigherCasterLevel = true;
        public bool SpecialistSchool = true;
        public bool SchoolFocus = true;
        public bool HigherCastingStat = true;
        public bool MagusTouch;
        public bool FewerKnown = true;
        public bool LowerSpellLevel;
        public bool MainCharacterFirst;

        // Spells of a book's opposition schools are not learnt at all: left out of the lists, the counts and the learning.
        public bool ExcludeOpposition;

        // The two buttons in the block of the mods' buttons over the game's menu buttons (mouse mode).
        public bool BarButtons = true;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }

        internal void Reset()
        {
            var d = new Settings();
            CanCastNow = d.CanCastNow;
            NotOpposition = d.NotOpposition;
            HigherCasterLevel = d.HigherCasterLevel;
            SpecialistSchool = d.SpecialistSchool;
            SchoolFocus = d.SchoolFocus;
            HigherCastingStat = d.HigherCastingStat;
            MagusTouch = d.MagusTouch;
            FewerKnown = d.FewerKnown;
            LowerSpellLevel = d.LowerSpellLevel;
            MainCharacterFirst = d.MainCharacterFirst;
            ExcludeOpposition = d.ExcludeOpposition;
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
            Cards.Register();
            new Harmony(modEntry.Info.Id).PatchAll(typeof(Main).Assembly);
            return true;
        }

        internal static string Id
        {
            get { return Mod.Info.Id; }
        }

        internal static void Save()
        {
            Settings.Save(Mod);
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
                LessonList.Tick();
                Cards.Tick();
                Bar.Ensure();
            }
            catch (Exception e)
            {
                Error(e, "OnUpdate");
            }
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            var s = Settings;
            s.BarButtons = GUILayout.Toggle(s.BarButtons, " Buttons in the block of the mods' buttons over the game's menu buttons, after the buffs and the time (keyboard and mouse mode)");
            s.ExcludeOpposition = GUILayout.Toggle(s.ExcludeOpposition, " Do not learn spells of opposition schools");
            GUILayout.Label("The criteria and the help are in the mod's window: its button over the game's menu buttons, or its card in the gamepad menu.");
            if (GUILayout.Button("Open the window", GUILayout.Width(200)) && InGame) Window.Toggle();
        }

        internal static void Log(string message)
        {
            Mod.Logger.Log(message);
        }

        // Each kind of error once to the UMM log.
        internal static void Error(Exception e, string what)
        {
            if (s_Errors.Add(what + e.GetType().Name)) Mod.Logger.Error(what + ": " + e);
        }

        internal static void Error(string what, Exception e)
        {
            Error(e, what);
        }
    }
}
