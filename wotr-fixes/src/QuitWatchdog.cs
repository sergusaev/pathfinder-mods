using System;
using System.Runtime.InteropServices;
using HarmonyLib;
using Kingmaker;
using UnityEngine;

namespace WotrFixes
{
    // On the Steam Deck the game does not end after quitting: Player.log is written to its last line and the window is
    // gone, but the main thread is over while 25 threads of the engine (DXVK, sound, input) wait on forever, and Steam
    // shows the game running until it is started again. The game's own work at quitting is Game.HandleQuit (prefs,
    // saves, the area stash, logs), called by the Runner of a loaded game, so a process still alive a few seconds after
    // it is ended; a process that may never get there, a little later. Without a Runner (the main menu) there is nothing
    // to wait for: the saves are written before the game asks to quit (SystemUtil.ApplicationQuit). The timer is the
    // system's own and calls TerminateProcess itself: threads of Mono stop while the engine shuts down.
    static class QuitWatchdog
    {
        internal const uint AfterHandleQuitMs = 3000;
        const uint AfterQuittingMs = 15000, WT_EXECUTEONLYONCE = 8;

        [DllImport("kernel32", CharSet = CharSet.Ansi)]
        static extern IntPtr GetModuleHandle(string name);

        [DllImport("kernel32", CharSet = CharSet.Ansi)]
        static extern IntPtr GetProcAddress(IntPtr module, string name);

        [DllImport("kernel32", SetLastError = true)]
        static extern bool CreateTimerQueueTimer(out IntPtr timer, IntPtr queue, IntPtr callback, IntPtr parameter,
            uint dueTime, uint period, uint flags);

        internal static void Arm()
        {
            Application.quitting += OnQuitting;
        }

        static void OnQuitting()
        {
            bool runner;
            try { runner = UnityEngine.Object.FindObjectOfType<Runner>() != null; }
            catch { runner = true; }
            EndIn(runner ? AfterQuittingMs : AfterHandleQuitMs);
        }

        internal static void EndIn(uint delayMs)
        {
            try
            {
                // A timer callback gets (parameter, TRUE): TerminateProcess(the current process, 1).
                var terminate = GetProcAddress(GetModuleHandle("kernel32.dll"), "TerminateProcess");
                IntPtr timer;
                if (terminate == IntPtr.Zero
                    || !CreateTimerQueueTimer(out timer, IntPtr.Zero, terminate, new IntPtr(-1), delayMs, 0, WT_EXECUTEONLYONCE))
                    Main.Log.Error("quit: no timer to end the process, error " + Marshal.GetLastWin32Error());
                else
                    Main.Log.Log("quit: the process is ended in " + delayMs / 1000 + " s if it is still running");
            }
            catch (Exception e)
            {
                Main.Log.Error("quit: " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(Game), nameof(Game.HandleQuit))]
    static class HandleQuitPatch
    {
        // A finalizer runs after the method even when it throws.
        static void Finalizer()
        {
            QuitWatchdog.EndIn(QuitWatchdog.AfterHandleQuitMs);
        }
    }
}
