using System;
using System.Collections;
using System.Linq;
using HarmonyLib;
using Kingmaker.Utility;

namespace KingmakerFixes
{
    // The game restarts itself (after the control scheme is changed) by starting its exe from the running process, so
    // the new process inherits its environment. Doorstop, which loads the mod manager, marks the environment of the
    // process it started in (DOORSTOP_INITIALIZED and others) and does nothing in a process that has the mark: the
    // restarted game ran without any mods, saves made with mods looked as needing a missing DLC, and mod content
    // (heads, hair) was gone. The marks are removed just before the restart; this process quits right after it.
    [HarmonyPatch(typeof(SystemUtil), nameof(SystemUtil.ApplicationRestart))]
    static class RestartWithMods
    {
        static void Prefix()
        {
            try
            {
                var names = Environment.GetEnvironmentVariables().Keys.Cast<object>().Select(k => k.ToString())
                    .Where(k => k.StartsWith("DOORSTOP_", StringComparison.OrdinalIgnoreCase)).ToArray();
                foreach (var name in names)
                    Environment.SetEnvironmentVariable(name, null);
                Main.Log.Log("restart: cleared " + (names.Length == 0 ? "nothing" : string.Join(", ", names)));
            }
            catch (Exception e)
            {
                Main.Log.Error("restart: " + e);
            }
        }
    }
}
