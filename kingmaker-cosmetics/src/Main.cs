using System;
using HarmonyLib;
using Kingmaker.Blueprints;
using UnityEngine.SceneManagement;
using UnityModManagerNet;

namespace KingmakerCosmetics
{
    public static class Main
    {
        internal static UnityModManager.ModEntry.ModLogger Log;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Log = modEntry.Logger;
            new Harmony(modEntry.Info.Id).PatchAll(typeof(Main).Assembly);
            modEntry.OnUpdate = OnUpdate;
            return true;
        }

        // The styles are made once, in the main menu: the library and the common bundles are loaded by then, and every
        // way to character creation or to a save goes through it. The scene is named as in Kingmaker.SceneName,
        // the console interface has its own.
        static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            if (ResourcesLibrary.LibraryObject == null || ResourcesLibrary.Preloading
                || !SceneManager.GetSceneByName("MainMenu").isLoaded && !SceneManager.GetSceneByName("UI_MainMenu_Scene_Console").isLoaded)
                return;
            modEntry.OnUpdate = (entry, time) => Styles.Tick();
            try
            {
                Styles.Build();
            }
            catch (Exception ex)
            {
                Log.Error("styles not made: " + ex);
            }
        }
    }
}
