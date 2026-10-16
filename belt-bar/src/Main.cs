using System;
using HarmonyLib;
using UnityModManagerNet;

namespace BeltBar
{
    // A belt button on the gamepad action bar, next to the weapon set button: it opens a row with the belt slots.
    // The two games have their own gamepad interfaces: the code for each is in src/kingmaker and src/wotr.
    public static class Main
    {
        internal static UnityModManager.ModEntry Mod;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Mod = modEntry;
            modEntry.OnUpdate = (e, dt) => BeltButton.Tick();
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
