using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        // Without the compass the Kingmaker hints keep their arc (positions from the clock prefab), moved right so the icons
        // clear the hourglass; the camera hint continues the arc above the clock.
        static readonly Vector2 ClockHintShift = new Vector2(24f, 0f);
        static readonly Vector2 ClockCameraHintBase = new Vector2(102f, 133f);

        internal static Vector2 MoveCameraHintPos
        {
            get { return (s_Sprites != null ? MoveCameraHintBase : ClockCameraHintBase) + new Vector2(0f, Main.Settings.CameraHintOffsetY); }
        }

        static readonly string[] SpriteNames =
        {
            "UI_HudAstrolabeBorder_Console", "UI_HudAstrolabe01", "UI_HudAstrolabe02", "UI_HudAstrolabeArrow",
            "UI_HudAstrolabeCenter01", "UI_HudAstrolabeCenter02", "UI_CircleHighliht"
        };

        static RectTransform s_Root;
        static Dictionary<string, Sprite> s_Sprites;
        // A failed search is not repeated on every area load, only after the WotR folder setting changes.
        static string s_FailedPath;

        // Every built compass: the console one on the clock and the PC one next to the menu buttons.
        sealed class Dial
        {
            public RectTransform Arrow, Astro01, Astro02;
        }

        static readonly List<Dial> s_Dials = new List<Dial>();

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
            s_Root = root;

            // Without WotR the Kingmaker clock and its hint layout stay as they are.
            if (!LoadSprites())
            {
                PlaceHints();
                return;
            }
            root.anchoredPosition = BlockPos;
            root.sizeDelta = BlockSize;
            PlaceHints();

            Transform clock = root.Find("BackgroundClock");
            if (clock != null) clock.gameObject.SetActive(false);

            var part = new GameObject(RootName, typeof(RectTransform)).GetComponent<RectTransform>();
            part.SetParent(root, false);
            part.SetAsFirstSibling();
            Set(part, Vector2.zero, Vector2.zero, BlockSize, Vector2.zero, 1f);
            BuildDial(part);
            Main.Log?.Log("Compass built");
        }

        internal static Vector2 Size
        {
            get { return BlockSize; }
        }

        // The WotR astrolabe layers inside part, a BlockSize rect; false without the sprites.
        internal static bool BuildDial(RectTransform part)
        {
            if (!LoadSprites()) return false;
            var dial = new Dial();
            dial.Astro02 = Layer(part, "UI_HudAstrolabe02", Vector2.zero, new Vector2(75.756f, 67.84f), new Vector2(78.5f, 81.5f), new Vector2(0.526f, 0.507f), 1.15f);
            var border = Layer(part, "UI_HudAstrolabeBorder_Console", Vector2.zero, new Vector2(75.51f, 71.24f), new Vector2(130f, 138f), new Vector2(0.5f, 0.5f), 1f);
            var button = Layer(border, "UI_CircleHighliht", Vector2.zero, new Vector2(0.456f, -2.068f), new Vector2(-6.389f, -15.088f), new Vector2(0.5f, 0.5f), 1f);
            button.anchorMax = Vector2.one;
            button.GetComponent<Image>().color = new Color(0.55f, 0.38f, 0.29f, 0.2f);
            dial.Astro01 = Layer(part, "UI_HudAstrolabe01", Vector2.zero, new Vector2(75.338f, 68.326f), new Vector2(63f, 63f), new Vector2(0.373f, 0.451f), 1.15f);
            Layer(part, "UI_HudAstrolabeCenter01", Vector2.zero, new Vector2(75.51f, 68.06f), new Vector2(17f, 17f), new Vector2(0.5f, 0.5f), 1.15f);
            dial.Arrow = Layer(part, "UI_HudAstrolabeArrow", new Vector2(0.5f, 0.5f), new Vector2(8.009f, 6.839f), new Vector2(31.5f, 120.5f), new Vector2(0.5f, 0.442f), 1.15f);
            Layer(dial.Arrow, "UI_HudAstrolabeCenter02", Vector2.zero, new Vector2(15.75f, 52.75f), new Vector2(11.5f, 11.5f), new Vector2(0.5f, 0.5f), 1f);
            s_Dials.Add(dial);
            return true;
        }

        // Hints keep the WotR layout, pushed outward by the configurable offset so their icons clear the compass ring.
        internal static void PlaceHints()
        {
            if (s_Root == null) return;
            PlaceHint(InputRemap.CameraHintName, MoveCameraHintPos);
            if (s_Sprites == null)
            {
                PlaceHint("HintMenu", new Vector2(108f, 95f) + ClockHintShift);
                PlaceHint("HintCursor", new Vector2(130f, 63f) + ClockHintShift);
                PlaceHint("HintPause", new Vector2(130f, 25f) + ClockHintShift);
                PlaceHint("HintHighlight", new Vector2(108f, -7f) + ClockHintShift);
                return;
            }
            var side = new Vector2(Main.Settings.HintsOffsetX, Main.Settings.HintsOffsetY);
            PlaceHint("HintPause", new Vector2(103.3f, 105.6f) + HintShift + side);
            PlaceHint("HintCursor", new Vector2(127f, 69f) + HintShift + side);
            PlaceHint("HintMenu", new Vector2(127f, 50.6f - 12.5f) + new Vector2(HintShift.x, 0f) + side);
            PlaceHint("HintHighlight", new Vector2(104.2f, -4.1f) + HintShift + side);
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
            img.sprite = s_Sprites[sprite];
            return rt;
        }

        static string CacheDir
        {
            get { return Path.Combine(Main.Dir, "Compass"); }
        }

        static bool LoadSprites()
        {
            if (s_Sprites != null) return true;
            string configured = Main.Settings.WotrPath ?? "";
            if (s_FailedPath == configured) return false;
            try
            {
                if (!SpriteNames.All(n => File.Exists(Path.Combine(CacheDir, n + ".png")))) Extract();
                var sprites = new Dictionary<string, Sprite>();
                foreach (string name in SpriteNames)
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    ImageConversion.LoadImage(tex, File.ReadAllBytes(Path.Combine(CacheDir, name + ".png")));
                    tex.wrapMode = TextureWrapMode.Clamp;
                    sprites[name] = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f));
                }
                s_Sprites = sprites;
                return true;
            }
            catch (Exception e)
            {
                Main.Log?.Warning("Compass sprites unavailable, keeping the Kingmaker clock: " + e.Message);
                s_FailedPath = configured;
                return false;
            }
        }

        // The sprites live in BC7 atlases of the WotR "ui" bundle; the GPU decodes them and each is cut out into a PNG once.
        static void Extract()
        {
            string bundle = WotrSprites.FindBundle(Main.Settings.WotrPath, Application.dataPath);
            if (bundle == null) throw new FileNotFoundException("Wrath of the Righteous not found, set its folder in the mod settings");
            Main.Log?.Log("Extracting compass sprites from " + bundle);
            List<WotrSprites.Entry> entries = WotrSprites.Load(bundle, SpriteNames);
            string missing = string.Join(", ", SpriteNames.Where(n => entries.All(e => e.Name != n)).ToArray());
            if (missing.Length > 0) throw new InvalidDataException("not found in the bundle: " + missing);

            Directory.CreateDirectory(CacheDir);
            var atlases = new Dictionary<WotrSprites.Atlas, Texture2D>();
            try
            {
                foreach (WotrSprites.Entry e in entries)
                {
                    Texture2D atlas;
                    if (!atlases.TryGetValue(e.Atlas, out atlas))
                    {
                        var format = (TextureFormat)e.Atlas.Format;
                        if (!SystemInfo.SupportsTextureFormat(format)) throw new NotSupportedException("the GPU cannot sample " + format);
                        atlas = new Texture2D(e.Atlas.Width, e.Atlas.Height, format, false, false) { filterMode = FilterMode.Point };
                        atlas.LoadRawTextureData(e.Atlas.Data);
                        atlas.Apply(false, false);
                        atlases[e.Atlas] = atlas;
                    }
                    File.WriteAllBytes(Path.Combine(CacheDir, e.Name + ".png"), Cut(atlas, e));
                }
            }
            finally
            {
                foreach (Texture2D t in atlases.Values) UnityEngine.Object.Destroy(t);
            }
        }

        static byte[] Cut(Texture2D atlas, WotrSprites.Entry e)
        {
            int w = Mathf.RoundToInt(e.Width), h = Mathf.RoundToInt(e.Height);
            RenderTexture rt = RenderTexture.GetTemporary(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = RenderTexture.active;
            var image = new Texture2D(w, h, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(atlas, rt, new Vector2((float)w / atlas.width, (float)h / atlas.height), new Vector2(e.X / atlas.width, e.Y / atlas.height));
                RenderTexture.active = rt;
                image.ReadPixels(new Rect(0, 0, w, h), 0, 0);
                image.Apply();
                return ImageConversion.EncodeToPNG(image);
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(rt);
                UnityEngine.Object.Destroy(image);
            }
        }

        internal static void Tick(float angle)
        {
            s_Dials.RemoveAll(d => d.Arrow == null);
            foreach (Dial d in s_Dials)
            {
                d.Arrow.localEulerAngles = new Vector3(0f, 0f, -angle);
                d.Astro01.localEulerAngles = new Vector3(0f, 0f, angle);
                d.Astro02.localEulerAngles = new Vector3(0f, 0f, -angle);
            }
        }
    }
}
