using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kingmaker.UI.Common;
using Kingmaker.UI._ConsoleUI;
using UnityEngine;

namespace KingmakerFixes
{
    // The gamepad's right stick scrolls a tooltip or a list (the inventory, the combat log, the character's pages) by
    // as much as one step of the mouse wheel on every frame the stick is held, and its input can come twice a frame, so
    // a list flies to its end at once and faster the higher the frame rate. The scroll is made per second, once a
    // frame, as strong as the stick is tilted: a list at 6 wheel steps a second with the stick held fully; a tooltip at
    // 8, up to 16 the longer its text is against its window (twice the window and more: twice as fast).
    [HarmonyPatch]
    static class PadScrollSpeed
    {
        const float ListStepsPerSecond = 6f;
        const float TooltipStepsPerSecond = 8f;

        static readonly Dictionary<object, int> s_LastFrame = new Dictionary<object, int>();
        static readonly FieldInfo s_TooltipScroll = AccessTools.Field(typeof(TooltipSectionView), "m_ScrollRectExtended");

        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(TooltipSectionView), nameof(TooltipSectionView.Scroll));
            yield return AccessTools.Method(typeof(VirtualListController), nameof(VirtualListController.Scroll));
        }

        static bool Prefix(object __instance, ref float x)
        {
            try
            {
                int frame = Time.frameCount;
                int last;
                if (s_LastFrame.TryGetValue(__instance, out last) && last == frame) return false;
                if (s_LastFrame.Count > 64) s_LastFrame.Clear();
                s_LastFrame[__instance] = frame;
                float steps = __instance is TooltipSectionView ? TooltipStepsPerSecond * TooltipFactor((TooltipSectionView)__instance) : ListStepsPerSecond;
                x *= Mathf.Min(Time.unscaledDeltaTime, 0.1f) * steps;
            }
            catch (Exception e) { Main.Log.Error("pad scroll speed: " + e); }
            return true;
        }

        // 1 for a text as tall as the window, 2 for one twice as tall and more.
        static float TooltipFactor(TooltipSectionView view)
        {
            var scroll = s_TooltipScroll != null ? s_TooltipScroll.GetValue(view) as ScrollRectExtended : null;
            if (scroll == null || scroll.content == null || scroll.viewport == null) return 1f;
            float window = scroll.viewport.rect.height;
            if (window <= 0f) return 1f;
            return Mathf.Clamp(scroll.content.rect.height / window, 1f, 2f);
        }
    }
}
