using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Kingmaker.UI.MVVM._ConsoleView.IngameMenu;
using Kingmaker.UI.MVVM._ConsoleView.InGame;
using UnityEngine;

namespace WotrFixes
{
    // In the gamepad interface the hints around the compass (bottom left) lie on its ring and on each other: the
    // cursor and object highlighting icons cover the ring, the camera mode hint covers the tip of the arrow. As in
    // Kingmaker with Camera Rotation and Compass, the hints stand on one arc around the compass: the icons of the side
    // hints keep their heights and stand at one distance from the centre, clear of the arrow's reach; the camera mode
    // hint stands above the compass and the skip time hint, which the game puts at the screen's edge, right above it.
    class CompassHints : MonoBehaviour
    {
        const float Margin = 4f;

        static readonly string[] SideFields = { "m_PauseToggleHint", "m_CursorToggleHint", "m_MenuHint", "m_ChangeHighlightHint" };
        const string TopField = "m_ToggleModeHint";
        const string BottomField = "m_SkipTimeHint";

        static readonly FieldInfo ButtonField = AccessTools.Field(typeof(IngameMenuConsoleView), "m_CompassButton");
        static readonly FieldInfo ArrowField = AccessTools.Field(typeof(IngameMenuConsoleView), "m_Arrow");

        static readonly Vector3[] s_Corners = new Vector3[4];
        static bool s_Failed;
        // The layout is written to the log this long after the hints of a view are first placed: the first dumps can
        // fall on the loading screen, the later ones on the map.
        static readonly float[] DumpTimes = { 2f, 10f, 30f };
        static int s_Dumps;
        static readonly HashSet<string> s_MovedLogged = new HashSet<string>();

        readonly List<RectTransform> m_Side = new List<RectTransform>();
        RectTransform m_Top, m_Bottom;
        // The game's own place of each moved hint, and where this put it last, to see when the game moves it itself.
        readonly Dictionary<RectTransform, Vector3> m_Base = new Dictionary<RectTransform, Vector3>();
        readonly Dictionary<RectTransform, Vector3> m_Placed = new Dictionary<RectTransform, Vector3>();
        IngameMenuConsoleView m_Compass;
        float m_NextSearch;
        float m_FirstPlaced = -1f;
        int m_Dumps;

        internal static void Attach(InGameConsoleView view)
        {
            if (s_Failed || view.GetComponent<CompassHints>() != null) return;
            var hints = view.gameObject.AddComponent<CompassHints>();
            foreach (var name in SideFields)
            {
                var rt = Hint(view, name);
                if (rt != null) hints.m_Side.Add(rt);
            }
            hints.m_Top = Hint(view, TopField);
            hints.m_Bottom = Hint(view, BottomField);
        }

        static RectTransform Hint(InGameConsoleView view, string name)
        {
            var hint = AccessTools.Field(typeof(InGameConsoleView), name)?.GetValue(view) as Component;
            if (hint == null) Main.Log.Log("compass hints: " + name + " not found");
            return hint != null ? hint.transform as RectTransform : null;
        }

        // After the game's layout and its own moves of the hints, right before drawing.
        void OnEnable()
        {
            Canvas.willRenderCanvases += AfterLayout;
        }

        void OnDisable()
        {
            Canvas.willRenderCanvases -= AfterLayout;
            Restore();
        }

        void AfterLayout()
        {
            if (s_Failed || this == null) return;
            try
            {
                Place();
            }
            catch (Exception e)
            {
                s_Failed = true;
                Restore();
                Main.Log.Error("compass hints: " + e);
            }
        }

        void Place()
        {
            if (m_Compass == null)
            {
                if (Time.unscaledTime < m_NextSearch) return;
                m_NextSearch = Time.unscaledTime + 1f;
                m_Compass = FindObjectOfType<IngameMenuConsoleView>();
                if (m_Compass == null) return;
            }
            TakeGameMoves();
            Restore();
            var button = ButtonField.GetValue(m_Compass) as Component;
            if (!m_Compass.isActiveAndEnabled || button == null) return;

            // The ring: the compass button's circle; the arrow turns about the centre and reaches past it.
            Rect ring = WorldBox((RectTransform)button.transform);
            Vector2 centre = ring.center;
            float radius = Mathf.Max(ring.width, ring.height) * 0.5f;
            var arrow = ArrowField.GetValue(m_Compass) as RectTransform;
            if (arrow != null)
            {
                float length = arrow.rect.height * arrow.lossyScale.y;
                radius = Mathf.Max(radius, length * Mathf.Max(arrow.pivot.y, 1f - arrow.pivot.y));
            }
            float scale = ((RectTransform)button.transform).lossyScale.x;
            float margin = Margin * scale;

            // The side hints keep their heights, their icons go to one distance from the centre.
            float top = float.MinValue;
            foreach (var hint in m_Side)
            {
                if (!hint.gameObject.activeInHierarchy || !Icon(hint, out var icon, out float r)) continue;
                float need = radius + margin + r;
                float dy = icon.y - centre.y;
                Move(hint, new Vector3(centre.x + Mathf.Sqrt(Mathf.Max(0f, need * need - dy * dy)) - icon.x, 0f, 0f));
                if (Shown(hint, out var box)) top = Mathf.Max(top, box.yMax);
            }

            // The camera mode hint stands above the compass, clear of it and of the side hints next to it; the skip
            // time hint, which below the compass reaches the screen's edge, stands right above it, icon over icon.
            bool hasTop = m_Top != null && m_Top.gameObject.activeInHierarchy && Icon(m_Top, out var t, out float tr);
            bool hasBottom = m_Bottom != null && m_Bottom.gameObject.activeInHierarchy && Icon(m_Bottom, out var b, out float br);
            float rowTop = float.MinValue;
            if (hasTop)
            {
                float dist = Reach(radius + margin + tr, t.x - centre.x);
                if (top > float.MinValue && Shown(m_Top, out var box)) dist = Mathf.Max(dist, top + margin + (t.y - box.yMin) - centre.y);
                Move(m_Top, new Vector3(0f, centre.y + dist - t.y, 0f));
                if (Shown(m_Top, out box)) rowTop = box.yMax;
            }
            if (hasBottom && hasTop && rowTop > float.MinValue && Shown(m_Bottom, out var shown))
                Move(m_Bottom, new Vector3(t.x - b.x, rowTop + margin + (b.y - shown.yMin) - b.y, 0f));

            if (m_FirstPlaced < 0f) m_FirstPlaced = Time.unscaledTime;
            if (m_Dumps < DumpTimes.Length && s_Dumps < 9 && Time.unscaledTime - m_FirstPlaced > DumpTimes[m_Dumps])
            {
                s_Dumps++;
                Dump("after " + DumpTimes[m_Dumps++] + " s", centre, radius);
            }
        }

        // A hint the game put elsewhere since the last frame keeps the game's new place as its own.
        void TakeGameMoves()
        {
            foreach (var pair in m_Placed)
            {
                var hint = pair.Key;
                if (hint == null || (hint.anchoredPosition3D - pair.Value).sqrMagnitude < 0.01f) continue;
                m_Base[hint] = hint.anchoredPosition3D;
                if (s_MovedLogged.Add(hint.name))
                    Main.Log.Log("compass hints: the game moved " + hint.name + " to " + hint.anchoredPosition3D);
            }
        }

        // The icon of a hint: its largest square picture.
        static bool Icon(RectTransform hint, out Vector2 centre, out float radius)
        {
            centre = Vector2.zero;
            radius = 0f;
            float best = 0f;
            foreach (var graphic in hint.GetComponentsInChildren<UnityEngine.UI.Graphic>())
            {
                if (!graphic.enabled || graphic is TMPro.TMP_Text) continue;
                Rect box = WorldBox(graphic.rectTransform);
                if (box.width <= 0f || box.height <= 0f || Mathf.Abs(box.width - box.height) > 0.25f * Mathf.Max(box.width, box.height)) continue;
                if (box.width * box.height <= best) continue;
                best = box.width * box.height;
                centre = box.center;
                radius = Mathf.Max(box.width, box.height) * 0.5f;
            }
            return best > 0f;
        }

        // How far from the centre along one axis a point at offset across it is at distance need.
        static float Reach(float need, float across)
        {
            return Mathf.Sqrt(Mathf.Max(0f, need * need - across * across));
        }

        // The box of everything a hint draws: its icon, plate and label.
        static bool Shown(RectTransform hint, out Rect box)
        {
            box = default(Rect);
            bool any = false;
            foreach (var graphic in hint.GetComponentsInChildren<UnityEngine.UI.Graphic>())
            {
                if (!graphic.enabled || graphic.rectTransform.rect.width <= 0f || graphic.rectTransform.rect.height <= 0f) continue;
                Rect at = WorldBox(graphic.rectTransform);
                box = any ? Rect.MinMaxRect(Mathf.Min(box.xMin, at.xMin), Mathf.Min(box.yMin, at.yMin),
                    Mathf.Max(box.xMax, at.xMax), Mathf.Max(box.yMax, at.yMax)) : at;
                any = true;
            }
            return any;
        }

        // The interface is drawn by a perspective camera on a tilted canvas, so a move along the world's axes also moves
        // the hint in depth, and the screen shows it nearer the screen's centre. The move stays in the canvas's plane.
        void Move(RectTransform hint, Vector3 delta)
        {
            if (!m_Base.ContainsKey(hint)) m_Base[hint] = hint.anchoredPosition3D;
            Vector3 local = hint.parent.InverseTransformVector(delta);
            local.z = 0f;
            hint.localPosition += local;
            m_Placed[hint] = hint.anchoredPosition3D;
        }

        void Restore()
        {
            foreach (var pair in m_Base)
                if (pair.Key != null) pair.Key.anchoredPosition3D = pair.Value;
        }

        static Rect WorldBox(RectTransform rt)
        {
            rt.GetWorldCorners(s_Corners);
            Vector2 min = s_Corners[0], max = s_Corners[0];
            for (int i = 1; i < 4; i++)
            {
                min = Vector2.Min(min, s_Corners[i]);
                max = Vector2.Max(max, s_Corners[i]);
            }
            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        // Twice per session, as the game laid the hints out and after this placed them, for checking against the screen.
        void Dump(string when, Vector2 centre, float radius)
        {
            var sb = new StringBuilder("compass hints ").Append(when).Append(": centre ").Append(centre)
                .Append(", radius ").Append(radius);
            foreach (var hint in m_Side) Describe(sb, hint);
            if (m_Top != null) Describe(sb, m_Top);
            if (m_Bottom != null)
            {
                Describe(sb, m_Bottom);
                // Every text on screen that reads as the bottom hint, with its path, to tell which one is shown.
                var label = m_Bottom.GetComponentInChildren<TMPro.TextMeshProUGUI>(true);
                if (label != null && !string.IsNullOrEmpty(label.text))
                    foreach (var text in FindObjectsOfType<TMPro.TextMeshProUGUI>())
                        if (text.text == label.text)
                            sb.Append("\n  text '").Append(text.text).Append("' at ").Append(WorldBox(text.rectTransform))
                                .Append(" path ").Append(Path(text.transform)).Append(text == label ? " (this hint)" : "");
            }
            // Each picture of the two hints across the centre as the game draws it: whether it is seen at all, and
            // where it is in the world and on the screen, to check the measured boxes against what the screen shows.
            Camera camera = null;
            var canvas = m_Compass.GetComponentInParent<Canvas>();
            if (canvas != null)
            {
                canvas = canvas.rootCanvas;
                camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                sb.Append("\n  canvas ").Append(canvas.name).Append(" mode ").Append(canvas.renderMode)
                    .Append(" scale ").Append(canvas.scaleFactor).Append(" root ").Append(WorldBox((RectTransform)canvas.transform))
                    .Append(" screen ").Append(Screen.width).Append('x').Append(Screen.height);
                if (camera != null)
                    sb.Append(" camera ").Append(camera.name).Append(camera.orthographic ? " ortho " + camera.orthographicSize : " fov " + camera.fieldOfView)
                        .Append(" at ").Append(camera.transform.position.ToString("F1")).Append(" rot ").Append(camera.transform.eulerAngles.ToString("F1"));
            }
            foreach (var hint in new List<RectTransform>(m_Side) { m_Top, m_Bottom })
            {
                if (hint == null) continue;
                sb.Append("\n  ").Append(hint.name).Append(" local ").Append(hint.localPosition.ToString("F1"))
                    .Append(" z ").Append(hint.position.z.ToString("F1")).Append(" rot ").Append(hint.eulerAngles.ToString("F1"))
                    .Append(" scale ").Append(hint.lossyScale.ToString("F3"));
                // Anything that can move or draw the hint on its own, from the hint up to the compass.
                for (var t = hint.transform; t != null && t != m_Compass.transform.parent; t = t.parent)
                    foreach (var c in t.GetComponents<Component>())
                        if (c is Canvas || c is UnityEngine.UI.LayoutGroup || c is UnityEngine.UI.ContentSizeFitter || c is Animator
                            || c is Animation || c is CanvasGroup || c.GetType().Name.Contains("Tween"))
                            sb.Append("\n    has ").Append(t.name).Append(':').Append(c.GetType().Name)
                                .Append(c is Behaviour be && !be.enabled ? " off" : "");
                foreach (var graphic in hint.GetComponentsInChildren<UnityEngine.UI.Graphic>(true))
                {
                    if (!graphic.isActiveAndEnabled) continue;
                    var rt = graphic.rectTransform;
                    rt.GetWorldCorners(s_Corners);
                    Vector2 lo = RectTransformUtility.WorldToScreenPoint(camera, s_Corners[0]);
                    Vector2 hi = RectTransformUtility.WorldToScreenPoint(camera, s_Corners[2]);
                    float alpha = graphic.color.a * graphic.canvasRenderer.GetAlpha();
                    foreach (var group in graphic.GetComponentsInParent<CanvasGroup>()) alpha *= group.alpha;
                    sb.Append("\n    ").Append(Path(rt).Substring(Path(hint).Length)).Append(' ').Append(graphic.GetType().Name)
                        .Append(graphic.isActiveAndEnabled ? "" : " off").Append(" alpha ").Append(alpha.ToString("F2"))
                        .Append(" world ").Append(WorldBox(rt)).Append(" z ").Append(rt.position.z.ToString("F1"))
                        .Append(" screen ").Append(lo.ToString("F0")).Append('-').Append(hi.ToString("F0"));
                }
            }
            Main.Log.Log(sb.ToString());
        }

        static string Path(Transform t)
        {
            var sb = new StringBuilder(t.name);
            for (t = t.parent; t != null; t = t.parent) sb.Insert(0, t.name + "/");
            return sb.ToString();
        }

        void Describe(StringBuilder sb, RectTransform hint)
        {
            sb.Append("\n  ").Append(hint.name).Append(" active ").Append(hint.gameObject.activeInHierarchy)
                .Append(" anchored ").Append(hint.anchoredPosition)
                .Append(" base ").Append(m_Base.TryGetValue(hint, out var at) ? at.ToString() : "-");
            var plate = hint.Find("Image") as RectTransform;
            if (plate != null) sb.Append(" plate ").Append(WorldBox(plate));
            if (Shown(hint, out var shown)) sb.Append(" shown ").Append(shown);
        }
    }

    [HarmonyPatch(typeof(InGameConsoleView), "BindViewImplementation")]
    static class CompassHintsAttach
    {
        static void Postfix(InGameConsoleView __instance) => CompassHints.Attach(__instance);
    }
}
