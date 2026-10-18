using HarmonyLib;
using UnityModManagerNet;

namespace WotrFixes
{
    public static class Main
    {
        internal static UnityModManager.ModEntry.ModLogger Log;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Log = modEntry.Logger;
            new Harmony(modEntry.Info.Id).PatchAll(typeof(Main).Assembly);
            return true;
        }
    }
}
