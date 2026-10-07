using System;
using System.IO;
using HarmonyLib;
using Kingmaker.Assets.UI._ConsoleUI.InGameClock;
using UnityEngine;
using UnityEngine.UI;

namespace GamepadCameraRotation
{
    // WotR console compass block (IngameMenuConsoleView) in place of the Kingmaker clock: same position, size and hint layout.
    [HarmonyPatch(typeof(InGameClockView), "Bind")]
    static class Compass
    {
        const string RootName = "GCR_Compass";

        static readonly Vector2 BlockPos = new Vector2(22.2f, 46f);
        static readonly Vector2 BlockSize = new Vector2(135f, 123.5f);

        // Kingmaker hints are 25 high with the icon 30 left of the rect; WotR hints are 36 high with the icon at the left edge.
        static readonly Vector2 HintShift = new Vector2(30f, 5.5f);
        static readonly Vector2 MoveCameraHintBase = new Vector2(57.3f, 150.9f) + HintShift;

        internal static Vector2 MoveCameraHintPos
        {
            get { return MoveCameraHintBase + new Vector2(0f, Main.Settings.CameraHintOffsetY); }
        }

        static RectTransform s_Root;

        static RectTransform s_Arrow;
        static RectTransform s_Astro01;
        static RectTransform s_Astro02;

        static void Postfix(InGameClockView __instance)
        {
            try
            {
                Build(__instance.transform as RectTransform);
            }
            catch (Exception e)
            {
                Main.Log?.Error(e.ToString());
            }
        }

        static void Build(RectTransform root)
        {
            if (root == null || root.Find(RootName) != null) return;
            root.anchoredPosition = BlockPos;
            root.sizeDelta = BlockSize;

            Transform clock = root.Find("BackgroundClock");
            if (clock != null) clock.gameObject.SetActive(false);

            s_Root = root;
            PlaceHints();

            var part = new GameObject(RootName, typeof(RectTransform)).GetComponent<RectTransform>();
            part.SetParent(root, false);
            part.SetAsFirstSibling();
            Set(part, Vector2.zero, Vector2.zero, BlockSize, Vector2.zero, 1f);

            s_Astro02 = Layer(part, "UI_HudAstrolabe02", Vector2.zero, new Vector2(75.756f, 67.84f), new Vector2(78.5f, 81.5f), new Vector2(0.526f, 0.507f), 1.15f);
            var border = Layer(part, "UI_HudAstrolabeBorder_Console", Vector2.zero, new Vector2(75.51f, 71.24f), new Vector2(130f, 138f), new Vector2(0.5f, 0.5f), 1f);
            var button = Layer(border, "UI_CircleHighliht", Vector2.zero, new Vector2(0.456f, -2.068f), new Vector2(-6.389f, -15.088f), new Vector2(0.5f, 0.5f), 1f);
            button.anchorMax = Vector2.one;
            button.GetComponent<Image>().color = new Color(0.55f, 0.38f, 0.29f, 0.2f);
            s_Astro01 = Layer(part, "UI_HudAstrolabe01", Vector2.zero, new Vector2(75.338f, 68.326f), new Vector2(63f, 63f), new Vector2(0.373f, 0.451f), 1.15f);
            Layer(part, "UI_HudAstrolabeCenter01", Vector2.zero, new Vector2(75.51f, 68.06f), new Vector2(17f, 17f), new Vector2(0.5f, 0.5f), 1.15f);
            s_Arrow = Layer(part, "UI_HudAstrolabeArrow", new Vector2(0.5f, 0.5f), new Vector2(8.009f, 6.839f), new Vector2(31.5f, 120.5f), new Vector2(0.5f, 0.442f), 1.15f);
            Layer(s_Arrow, "UI_HudAstrolabeCenter02", Vector2.zero, new Vector2(15.75f, 52.75f), new Vector2(11.5f, 11.5f), new Vector2(0.5f, 0.5f), 1f);
            Main.Log?.Log("Compass built");
        }

        // Hints keep the WotR layout, pushed outward by the configurable offset so their icons clear the compass ring.
        internal static void PlaceHints()
        {
            if (s_Root == null) return;
            var side = new Vector2(Main.Settings.HintsOffsetX, Main.Settings.HintsOffsetY);
            PlaceHint("HintPause", new Vector2(103.3f, 105.6f) + HintShift + side);
            PlaceHint("HintCursor", new Vector2(127f, 69f) + HintShift + side);
            PlaceHint("HintMenu", new Vector2(127f, 50.6f - 12.5f) + new Vector2(HintShift.x, 0f) + side);
            PlaceHint("HintHighlight", new Vector2(104.2f, -4.1f) + HintShift + side);
            PlaceHint(InputRemap.CameraHintName, MoveCameraHintPos);
        }

        static void PlaceHint(string name, Vector2 pos)
        {
            var hint = s_Root.Find(name) as RectTransform;
            if (hint != null) hint.anchoredPosition = pos;
        }

        static void Set(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot, float scale)
        {
            rt.anchorMin = rt.anchorMax = anchor;
            rt.pivot = pivot;
            rt.anchoredPosition = pos;
            rt.sizeDelta = size;
            rt.localScale = new Vector3(scale, scale, 1f);
        }

        static RectTransform Layer(RectTransform parent, string sprite, Vector2 anchor, Vector2 pos, Vector2 size, Vector2 pivot, float scale)
        {
            var go = new GameObject(sprite, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            Set(rt, anchor, pos, size, pivot, scale);
            var img = go.GetComponent<Image>();
            img.raycastTarget = false;
            img.sprite = LoadSprite(sprite);
            return rt;
        }

        static Sprite LoadSprite(string name)
        {
            string path = Path.Combine(Path.Combine(Main.Dir, "Compass"), name + ".png");
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            ImageConversion.LoadImage(tex, File.ReadAllBytes(path));
            tex.wrapMode = TextureWrapMode.Clamp;
            return Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
        }

        internal static void Tick(float angle)
        {
            if (s_Arrow == null) return;
            s_Arrow.localEulerAngles = new Vector3(0f, 0f, -angle);
            s_Astro01.localEulerAngles = new Vector3(0f, 0f, angle);
            s_Astro02.localEulerAngles = new Vector3(0f, 0f, -angle);
        }
    }
}
