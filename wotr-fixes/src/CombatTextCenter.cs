using System;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Kingmaker.UI.CombatText;
using Kingmaker.UI._ConsoleUI.Overtips;
using TMPro;
using UnityEngine;

namespace WotrFixes
{
    // Combat text (damage, misses, saves, spell names) floats to the right of the unit's head instead of over its
    // middle. Kingmaker keeps each text inside the unit's overtip; Wrath moves it to the shared CombatTextContainer
    // and puts the text's pivot on the overtip part every frame (OvertipViewPartCombatText.GetCombatTextPosition),
    // and it hides an absent spell icon by alpha only, so the text starts at the head and runs to the right. Here the
    // visible text with its icon is centred over the overtip's anchor, the point over the head; the height above the
    // name and the HP bar stays the game's own (UnitOvertipView.TweenComponentsToPlace), and the stacked texts are
    // spaced in canvas units, not in screen pixels.
    static class CombatTextCenter
    {
        const float LineStep = 25f;

        static readonly FieldInfo GetPosition = AccessTools.Field(typeof(CombatTextController), "m_GetCombatTextPosition");

        static readonly Vector3[] s_Corners = new Vector3[4];
        static bool s_Failed;
        static bool s_Dumped;

        internal static void Center(CombatTextController text, bool rebuild)
        {
            if (s_Failed) return;
            try
            {
                var gamePosition = GetPosition.GetValue(text) as Func<CombatTextController, Vector3>;
                if (gamePosition == null) return;
                var part = ((Delegate)gamePosition).Target as OvertipViewPartCombatText;
                if (part == null) return;
                var root = part.GetComponentInParent<OvertipViewBase>();
                if (root == null) return;
                var tmp = text.GetComponent<TextMeshProUGUI>();
                if (tmp == null) return;
                if (rebuild) tmp.ForceMeshUpdate();
                if (!s_Dumped)
                {
                    s_Dumped = true;
                    Dump(part, root, text);
                }

                float min = float.MaxValue, max = float.MinValue;
                Bounds b = tmp.textBounds;
                if (b.size.x > 0f)
                {
                    Widen(tmp.transform.TransformPoint(b.min).x, ref min, ref max);
                    Widen(tmp.transform.TransformPoint(b.max).x, ref min, ref max);
                }
                else
                {
                    WidenRect(tmp.rectTransform, ref min, ref max);
                }
                if (text.Icon != null && text.Icon.sprite != null && (text.IconCanvasGroup == null || text.IconCanvasGroup.alpha > 0f))
                    WidenRect(text.Icon.rectTransform, ref min, ref max);

                // X is moved by what the text is off the head now, Y is set from the game's position: both stay put
                // when Initialize runs this again after its own SetPosition.
                var p = text.transform.position;
                var game = gamePosition(text);
                p.x += HeadX(root) - (min + max) * 0.5f;
                p.y = game.y + StackShift(part, game);
                text.transform.position = p;
            }
            catch (Exception e)
            {
                s_Failed = true;
                Main.Log.Error("combat text centre: " + e);
            }
        }

        // The overtip is anchored to the point over the head (OvertipViewBase.UpdatePosition sets both anchors to it).
        static float HeadX(OvertipViewBase root)
        {
            var rt = (RectTransform)root.transform;
            var parent = rt.parent as RectTransform;
            if (parent == null) return rt.position.x;
            Rect r = parent.rect;
            return parent.TransformPoint(new Vector3(Mathf.LerpUnclamped(r.xMin, r.xMax, rt.anchorMin.x), 0f, 0f)).x;
        }

        // The game lifts each older text by 25 screen pixels; make it 25 canvas units, as the rest of the overtip.
        static float StackShift(OvertipViewPartCombatText part, Vector3 game)
        {
            float scale = part.transform.lossyScale.y;
            if (Mathf.Approximately(scale, 1f)) return 0f;
            float lines = (game.y - part.transform.position.y) / LineStep;
            return lines * LineStep * (scale - 1f);
        }

        static void Widen(float x, ref float min, ref float max)
        {
            if (x < min) min = x;
            if (x > max) max = x;
        }

        static void WidenRect(RectTransform rt, ref float min, ref float max)
        {
            rt.GetWorldCorners(s_Corners);
            for (int i = 0; i < 4; i++) Widen(s_Corners[i].x, ref min, ref max);
        }

        // Once per session: the layout the fix works with, for checking against the screen.
        static void Dump(OvertipViewPartCombatText part, OvertipViewBase root, CombatTextController text)
        {
            var sb = new StringBuilder("combat text layout:");
            for (Transform t = part.transform; t != null && t != root.transform.parent; t = t.parent)
                Describe(sb, "part chain", t);
            Describe(sb, "text", text.transform);
            foreach (Transform child in text.transform) Describe(sb, "text child", child);
            var tmp = text.GetComponent<TextMeshProUGUI>();
            if (tmp != null) sb.Append("\n  tmp alignment ").Append(tmp.alignment).Append(" bounds ").Append(tmp.textBounds);
            sb.Append("\n  head x ").Append(HeadX(root)).Append(", text pos ").Append(text.transform.position);
            Main.Log.Log(sb.ToString());
        }

        static void Describe(StringBuilder sb, string what, Transform t)
        {
            sb.Append("\n  ").Append(what).Append(' ').Append(t.name).Append(" pos ").Append(t.position)
                .Append(" scale ").Append(t.lossyScale);
            if (t is RectTransform rt)
                sb.Append(" anchors ").Append(rt.anchorMin).Append('-').Append(rt.anchorMax)
                    .Append(" pivot ").Append(rt.pivot).Append(" anchoredPos ").Append(rt.anchoredPosition)
                    .Append(" size ").Append(rt.rect.size);
            foreach (var c in t.GetComponents<Component>())
                if (!(c is Transform)) sb.Append(' ').Append(c.GetType().Name);
        }
    }

    [HarmonyPatch(typeof(CombatTextController), "SetPosition")]
    static class CombatTextCenterMove
    {
        static void Postfix(CombatTextController __instance) => CombatTextCenter.Center(__instance, false);
    }

    [HarmonyPatch(typeof(CombatTextController), nameof(CombatTextController.Initialize))]
    static class CombatTextCenterInit
    {
        static void Postfix(CombatTextController __instance) => CombatTextCenter.Center(__instance, true);
    }
}
