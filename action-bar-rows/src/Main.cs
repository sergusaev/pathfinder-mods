using System;
using HarmonyLib;
using UnityModManagerNet;

namespace ActionBarRows
{
    // Up to three rows on the gamepad action bar: above the page the game shows, the next pages of the same
    // ten. The code for each game is in src/kingmaker and src/wotr.
    public static class Main
    {
        internal static UnityModManager.ModEntry Mod;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Mod = modEntry;
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
    }
}
