using System;
using System.Reflection;
using HarmonyLib;
using Kingmaker.UI.MVVM._ConsoleView.CharGen.Phases.Portrait;
using Owlcat.Runtime.UI.ConsoleTools;
using Owlcat.Runtime.UI.ConsoleTools.NavigationTool;
using Owlcat.Runtime.UI.VirtualListSystem;
using UnityEngine;
using UnityModManagerNet;

namespace PortraitScrollFix
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

    static class Access
    {
        internal static readonly FieldInfo Group =
            AccessTools.Field(typeof(CharGenPortraitPhaseDetailedConsoleView), "m_PortraitGroupConsoleView");
        internal static readonly FieldInfo List =
            AccessTools.Field(typeof(CharGenCustomPortraitGroupConsoleView), "m_VirtualList");
        internal static readonly FieldInfo Nav =
            AccessTools.Field(typeof(CharGenPortraitPhaseDetailedConsoleView), "m_Navigation");

        internal static VirtualListComponent ActiveList(CharGenPortraitPhaseDetailedConsoleView view)
        {
            var group = Group.GetValue(view) as Behaviour;
            if (group == null || !group.isActiveAndEnabled) return null;
            return List.GetValue(group) as VirtualListComponent;
        }

        // Walks nested navigation down to the grid that holds the focused virtual list element.
        internal static bool FindFocus(CharGenPortraitPhaseDetailedConsoleView view,
            out ConsoleNavigationBehaviour grid, out VirtualListElement element)
        {
            grid = null;
            element = null;
            var nav = Nav.GetValue(view) as ConsoleNavigationBehaviour;
            for (int depth = 0; nav != null && depth < 10; depth++)
            {
                IConsoleEntity focus = nav.Focus?.Value;
                element = focus as VirtualListElement;
                if (element != null)
                {
                    grid = nav;
                    return true;
                }
                nav = focus as ConsoleNavigationBehaviour;
            }
            return false;
        }
    }

    // Console chargen scrolls only the outer ScrollRect to a focused MonoBehaviour; custom portraits live in
    // a VirtualList of plain-object elements, and its ForceScrollToElement only works for already laid out rows.
    // This component keeps the focused element in view every frame via ScrollTowards, which also handles
    // far, not yet materialized rows, so d-pad, stick and held-button repeat all behave the same.
    class FocusFollower : MonoBehaviour
    {
        internal CharGenPortraitPhaseDetailedConsoleView View;

        const float NearSpeed = 45f;
        const float FarSpeed = 220f;

        void LateUpdate()
        {
            try
            {
                if (View == null) return;
                var list = Access.ActiveList(View);
                if (list == null) return;
                ConsoleNavigationBehaviour grid;
                VirtualListElement element;
                if (!Access.FindFocus(View, out grid, out element) || element.Data == null) return;
                float speed = element.HasView() ? NearSpeed : FarSpeed;
                list.ScrollController?.ScrollTowards(element.Data, speed);
            }
            catch (Exception e)
            {
                Main.Log?.Error(e.ToString());
                enabled = false;
            }
        }
    }

    [HarmonyPatch(typeof(CharGenPortraitPhaseDetailedConsoleView), "SetSelected")]
    static class SetSelectedPatch
    {
        static void Postfix(CharGenPortraitPhaseDetailedConsoleView __instance)
        {
            try
            {
                if (__instance.gameObject.GetComponent<FocusFollower>() == null)
                    __instance.gameObject.AddComponent<FocusFollower>().View = __instance;
            }
            catch (Exception e)
            {
                Main.Log?.Error(e.ToString());
            }
        }
    }

    // Right stick on the custom tab moves the cursor by rows (tap = one row, hold = repeat) instead of
    // scrolling the outer ScrollRect, which does not contain the virtual list.
    [HarmonyPatch(typeof(CharGenPortraitPhaseDetailedConsoleView), "Scroll")]
    static class StickScrollPatch
    {
        const float Threshold = 0.5f;
        const float FirstDelay = 0.35f;
        const float RepeatInterval = 0.09f;

        static int s_Direction;
        static float s_NextTime;
        static int s_LastFrame = -1;

        static bool Prefix(CharGenPortraitPhaseDetailedConsoleView __instance, float x)
        {
            try
            {
                if (Access.ActiveList(__instance) == null) return true;
                ConsoleNavigationBehaviour grid;
                VirtualListElement element;
                if (!Access.FindFocus(__instance, out grid, out element)) return true;

                int frame = Time.frameCount;
                if (s_LastFrame >= 0 && frame - s_LastFrame > 2) s_Direction = 0;
                s_LastFrame = frame;

                int direction = x > Threshold ? 1 : (x < -Threshold ? -1 : 0);
                float now = Time.unscaledTime;
                if (direction == 0)
                {
                    s_Direction = 0;
                    return false;
                }
                if (direction != s_Direction)
                {
                    s_Direction = direction;
                    s_NextTime = now + FirstDelay;
                    Step(grid, direction);
                }
                else if (now >= s_NextTime)
                {
                    s_NextTime = now + RepeatInterval;
                    Step(grid, direction);
                }
                return false;
            }
            catch (Exception e)
            {
                Main.Log?.Error(e.ToString());
                return true;
            }
        }

        static void Step(ConsoleNavigationBehaviour grid, int direction)
        {
            if (direction > 0) grid.HandleUp();
            else grid.HandleDown();
        }
    }
}
