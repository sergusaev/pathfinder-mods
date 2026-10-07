using System;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI._ConsoleUI.LocalMap;
using Kingmaker.View;
using Kingmaker.Visual.LocalMap;
using UnityEngine;
using UnityEngine.UI;

namespace GamepadCameraRotation
{
    // As in WotR the local map keeps a fixed yaw (the area default) instead of following the camera, so the image never goes stale.
    [HarmonyPatch(typeof(LocalMapRenderer), "UpdateCamera")]
    static class LocalMapCameraPatch
    {
        static readonly FieldInfo CameraField = AccessTools.Field(typeof(LocalMapRenderer), "m_Camera");
        static readonly Vector3[] Signs =
        {
            new Vector3(1f, 1f, -1f), new Vector3(-1f, 1f, -1f), new Vector3(1f, -1f, -1f), new Vector3(-1f, -1f, -1f),
            new Vector3(1f, 1f, 1f), new Vector3(-1f, 1f, 1f), new Vector3(1f, -1f, 1f), new Vector3(-1f, -1f, 1f)
        };

        static void Postfix(LocalMapRenderer __instance)
        {
            if (__instance.CurrentArea == null) return;
            var cam = CameraField.GetValue(__instance) as Camera;
            if (cam == null) return;

            Bounds bounds = __instance.CurrentArea.Bounds;
            float num = Vector3.Distance(bounds.min, bounds.max);
            cam.transform.rotation = Quaternion.Euler(__instance.ViewAngle, Main.MapYaw, 0f);
            cam.transform.position = bounds.center - cam.transform.forward * num;
            Matrix4x4 toLocal = cam.transform.worldToLocalMatrix;
            Vector3 min = Vector3.one * float.MaxValue;
            Vector3 max = Vector3.one * float.MinValue;
            foreach (Vector3 s in Signs)
            {
                Vector3 p = toLocal.MultiplyPoint(bounds.center + Vector3.Scale(bounds.extents, s));
                min = Vector3.Min(min, p);
                max = Vector3.Max(max, p);
            }
            var local = new Bounds();
            local.SetMinMax(min, max);
            cam.transform.position = cam.transform.TransformPoint(local.center - Vector3.forward * num);
            cam.farClipPlane = num * 2f;
            cam.orthographicSize = local.extents.y;
            cam.aspect = local.size.x / local.size.y;
        }
    }

    [HarmonyPatch(typeof(LocalMapRenderer), "IsDirty")]
    static class LocalMapDirtyPatch
    {
        static float s_RenderedYaw = float.NaN;

        static void Postfix(ref bool __result)
        {
            if (!__result && !Mathf.Approximately(s_RenderedYaw, Main.MapYaw)) __result = true;
            if (__result) s_RenderedYaw = Main.MapYaw;
        }
    }

    // The axis-aligned screen frame is replaced by the exact ground footprint of the camera, which turns and tilts with it.
    [HarmonyPatch(typeof(LocalMapView), "Bind")]
    static class LocalMapFramePatch
    {
        const string OutlineName = "GCR_ViewOutline";
        static readonly FieldInfo FrameField = AccessTools.Field(typeof(LocalMapView), "m_Frame");
        static readonly FieldInfo ImageField = AccessTools.Field(typeof(LocalMapView), "m_Image");

        static void Postfix(LocalMapView __instance)
        {
            try
            {
                var frame = FrameField.GetValue(__instance) as RectTransform;
                var image = ImageField.GetValue(__instance) as RawImage;
                if (frame == null || image == null) return;
                var parent = (RectTransform)frame.parent;
                if (parent.Find(OutlineName) != null) return;

                Color color = Color.white;
                var frameImage = frame.GetComponentInChildren<Graphic>(true);
                if (frameImage != null) color = frameImage.color;

                var go = new GameObject(OutlineName, typeof(RectTransform), typeof(ViewOutline));
                var rt = go.GetComponent<RectTransform>();
                rt.SetParent(parent, false);
                rt.SetSiblingIndex(frame.GetSiblingIndex());
                rt.anchorMin = frame.anchorMin;
                rt.anchorMax = frame.anchorMax;
                rt.pivot = Vector2.zero;
                rt.sizeDelta = Vector2.zero;
                rt.localPosition = Vector3.zero;
                var outline = go.GetComponent<ViewOutline>();
                outline.MapImage = image.rectTransform;
                outline.color = color;
                outline.raycastTarget = false;
                frame.gameObject.SetActive(false);
                Main.Log?.Log("Map outline added, frame pivot " + frame.pivot + " anchors " + frame.anchorMin + " parent " + parent.name);
            }
            catch (Exception e)
            {
                Main.Log?.Error(e.ToString());
            }
        }
    }

    public class ViewOutline : MaskableGraphic
    {
        const float Width = 2.5f;

        public RectTransform MapImage;
        readonly Vector2[] m_Points = new Vector2[4];
        static readonly Vector3[] Corners = { new Vector3(0f, 0f, 1f), new Vector3(1f, 0f, 1f), new Vector3(1f, 1f, 1f), new Vector3(0f, 1f, 1f) };

        void LateUpdate()
        {
            Camera cam = Game.GetCamera();
            LocalMapRenderer renderer = LocalMapRenderer.Instance;
            CameraRig rig = Game.Instance?.UI?.GetCameraRig();
            if (cam == null || renderer == null || rig == null || MapImage == null) return;
            var plane = new Plane(Vector3.up, rig.transform.position);
            Vector2 size = MapImage.sizeDelta;
            for (int i = 0; i < 4; i++)
            {
                Ray ray = cam.ViewportPointToRay(Corners[i]);
                float enter;
                plane.Raycast(ray, out enter);
                Vector3 v = renderer.WorldToViewportPoint(ray.origin + ray.direction * enter);
                m_Points[i] = new Vector2(v.x * size.x, v.y * size.y);
            }
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            for (int i = 0; i < 4; i++)
            {
                Vector2 a = m_Points[i];
                Vector2 b = m_Points[(i + 1) % 4];
                Vector2 dir = b - a;
                if (dir.sqrMagnitude < 0.0001f) continue;
                Vector2 n = new Vector2(-dir.y, dir.x).normalized * (Width * 0.5f);
                Vector2 ext = dir.normalized * (Width * 0.5f);
                int start = vh.currentVertCount;
                vh.AddVert(a - n - ext, color, Vector2.zero);
                vh.AddVert(a + n - ext, color, Vector2.zero);
                vh.AddVert(b + n + ext, color, Vector2.zero);
                vh.AddVert(b - n + ext, color, Vector2.zero);
                vh.AddTriangle(start, start + 1, start + 2);
                vh.AddTriangle(start, start + 2, start + 3);
            }
        }
    }
}
