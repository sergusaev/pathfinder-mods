using System;
using UnityEngine;
using UnityEngine.UI;

namespace GamepadCameraRotation
{
    // The hints around the console compass stand on one arc: the icons of the side hints (pause, cursor, menu,
    // highlight) keep their heights and stand at one distance from the compass centre, as far as the reach of the arrow
    // plus the gap from the settings; the camera mode hint stands on the same arc above the centre, clear of the pause
    // hint. Measured on screen each frame, after the game's layout.
    class CompassArc : MonoBehaviour
    {
        static readonly string[] SideHints = { "HintPause", "HintCursor", "HintMenu", "HintHighlight" };
        static readonly Vector3[] s_Corners = new Vector3[4];
        static bool s_Failed;
        static bool s_Logged;

        void OnEnable()
        {
            Canvas.willRenderCanvases += AfterLayout;
        }

        void OnDisable()
        {
            Canvas.willRenderCanvases -= AfterLayout;
        }

        void AfterLayout()
        {
            if (s_Failed || this == null) return;
            try
            {
                Compass.PlaceHints();
                Place();
            }
            catch (Exception e)
            {
                s_Failed = true;
                Compass.PlaceHints();
                Main.Log?.Error("compass hints: " + e);
            }
        }

        void Place()
        {
            RectTransform ring = Compass.Ring, arrow = Compass.Arrow;
            if (ring == null || !ring.gameObject.activeInHierarchy) return;
            Rect box = WorldBox(ring);
            Vector2 centre = box.center;
            float radius = Mathf.Max(box.width, box.height) * 0.5f;
            if (arrow != null)
                radius = Mathf.Max(radius, arrow.rect.height * arrow.lossyScale.y * Mathf.Max(arrow.pivot.y, 1f - arrow.pivot.y));
            float scale = ring.lossyScale.x;
            float gap = Main.Settings.HintsGap * scale;

            float top = float.MinValue;
            foreach (string name in SideHints)
            {
                var hint = Compass.Hint(name);
                if (hint == null || !hint.gameObject.activeInHierarchy || !Icon(hint, out var icon, out float r)) continue;
                float need = radius + gap + r;
                float dy = icon.y - centre.y;
                float x = centre.x + Mathf.Sqrt(Mathf.Max(0f, need * need - dy * dy));
                Move(hint, new Vector3(x - icon.x, 0f, 0f));
                top = Mathf.Max(top, Shown(hint).yMax);
            }

            var camera = Compass.Hint(InputRemap.CameraHintName);
            if (camera != null && camera.gameObject.activeInHierarchy && Icon(camera, out var at, out float cr))
            {
                float need = radius + gap + cr;
                float dx = at.x - centre.x;
                float y = centre.y + Mathf.Sqrt(Mathf.Max(0f, need * need - dx * dx));
                // Its icon and plate stay clear of the pause hint below.
                if (top > float.MinValue) y = Mathf.Max(y, top + gap + (at.y - Shown(camera).yMin));
                Move(camera, new Vector3(0f, y - at.y, 0f));
            }

            if (!s_Logged)
            {
                s_Logged = true;
                Main.Log?.Log("Compass hints on the arc: centre " + centre + ", radius " + radius + ", gap " + gap);
            }
        }

        // A move along the world's axes stays in the canvas's plane: the game puts back only the hint's place in the
        // plane every frame, so on a tilted canvas any move in depth would pile up from frame to frame.
        static void Move(RectTransform hint, Vector3 delta)
        {
            Vector3 local = hint.parent.InverseTransformVector(delta);
            local.z = 0f;
            hint.localPosition += local;
        }

        // The icon of a hint: its largest square picture.
        static bool Icon(RectTransform hint, out Vector2 centre, out float radius)
        {
            centre = Vector2.zero;
            radius = 0f;
            float best = 0f;
            foreach (var graphic in hint.GetComponentsInChildren<Graphic>())
            {
                if (!graphic.enabled || graphic.GetType().Name.Contains("Text")) continue;
                Rect box = WorldBox(graphic.rectTransform);
                if (box.width <= 0f || box.height <= 0f || Mathf.Abs(box.width - box.height) > 0.25f * Mathf.Max(box.width, box.height)) continue;
                if (box.width * box.height <= best) continue;
                best = box.width * box.height;
                centre = box.center;
                radius = Mathf.Max(box.width, box.height) * 0.5f;
            }
            return best > 0f;
        }

        // The box of everything a hint draws.
        static Rect Shown(RectTransform hint)
        {
            Rect shown = WorldBox(hint);
            bool any = false;
            foreach (var graphic in hint.GetComponentsInChildren<Graphic>())
            {
                if (!graphic.enabled) continue;
                Rect box = WorldBox(graphic.rectTransform);
                if (box.width <= 0f || box.height <= 0f) continue;
                shown = any ? Rect.MinMaxRect(Mathf.Min(shown.xMin, box.xMin), Mathf.Min(shown.yMin, box.yMin),
                    Mathf.Max(shown.xMax, box.xMax), Mathf.Max(shown.yMax, box.yMax)) : box;
                any = true;
            }
            return shown;
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
    }
}
