using System;
using System.Collections.Generic;
using UnityEngine;

namespace LearnScrolls
{
    // Copied from Time Control and Deadlines (one sprite set per pictogram here).
    // Button sprites in the look of the Kingmaker menu buttons (as the buff bar makes them): the dark square in a bronze
    // frame, the pictogram in the pale olive of the game's ones; hovered, a red square in a gold frame with a gold pictogram.
    // The empty square is rebuilt from the game's menu buttons: at every pixel the median of the samples that look like the
    // background. Drawn from the loaded sprites once per session, nothing of the game is stored.
    static class ButtonStyle
    {
        const int Size = 106;
        const int Edge = 12;
        static readonly string[] MenuButtons = { "Inventory", "Character", "Journal", "Map", "Rest", "Esc", "Stop", "Hold", "SelectAll", "Spellbook", "Formation" };
        static readonly Color TopPlain = new Color32(160, 152, 96, 255);
        static readonly Color BottomPlain = new Color32(112, 106, 68, 255);
        static readonly Color TopGold = new Color32(238, 222, 111, 255);
        static readonly Color BottomGold = new Color32(146, 133, 69, 255);

        static bool s_Tried;
        static bool s_Missing;
        static Color32[] s_Normal;
        static Color32[] s_Hover;
        static readonly Dictionary<string, Sprite> s_Cache = new Dictionary<string, Sprite>();

        internal enum State { Normal, Hover, Down }

        internal static Sprite Get(Texture2D icon, State state)
        {
            Sprite sprite;
            string key = icon.name + ":" + state;
            if (s_Cache.TryGetValue(key, out sprite)) return sprite;
            sprite = null;
            try
            {
                if (Prepare())
                    sprite = state == State.Normal ? Compose(icon, s_Normal, TopPlain, BottomPlain, 1.25f)
                        : Compose(icon, s_Hover, TopGold, BottomGold, state == State.Hover ? 1.2f : 1f);
            }
            catch (Exception e)
            {
                Main.Error(e, "button style");
            }
            if (sprite != null) s_Cache[key] = sprite;
            return sprite;
        }

        internal static void Retry()
        {
            if (s_Normal != null) return;
            s_Tried = false;
            s_Cache.Clear();
        }

        static bool Prepare()
        {
            if (s_Tried) return s_Normal != null;
            s_Tried = true;
            var sprites = new Dictionary<string, Sprite>();
            foreach (var s in Resources.FindObjectsOfTypeAll<Sprite>())
                if (!sprites.ContainsKey(s.name)) sprites[s.name] = s;
            s_Normal = Square(sprites, "Normal");
            s_Hover = Square(sprites, "Hover");
            if (s_Normal == null || s_Hover == null) s_Normal = s_Hover = null;
            // Tried again every second until the sprites come: said once.
            if (s_Normal != null || !s_Missing) Main.Log("bar buttons: Kingmaker style " + (s_Normal != null ? "built" : "unavailable"));
            s_Missing = s_Normal == null;
            return s_Normal != null;
        }

        static Color32[] Square(Dictionary<string, Sprite> sprites, string state)
        {
            var samples = new List<Color32[]>();
            foreach (var name in MenuButtons)
            {
                Sprite sprite;
                if (sprites.TryGetValue("Ingame_Menu_" + name + "_" + state, out sprite)) samples.Add(Read(sprite));
            }
            if (samples.Count < 5) return null;
            var inner = new List<float>();
            foreach (var s in samples)
                for (int y = 30; y < 76; y++)
                    for (int x = 16; x < 22; x++)
                        inner.Add(Lum(s[y * Size + x]));
            inner.Sort();
            float plain = inner[inner.Count / 2];
            var result = new Color32[Size * Size];
            var pick = new List<Color32>(samples.Count);
            for (int i = 0; i < result.Length; i++)
            {
                int x = i % Size, y = i / Size;
                pick.Clear();
                bool frame = x < 10 || y < 10 || x >= Size - 10 || y >= Size - 10;
                foreach (var s in samples)
                {
                    float l = Lum(s[i]);
                    if (frame || (l > plain * 0.65f && l < plain * 1.5f)) pick.Add(s[i]);
                }
                if (pick.Count == 0) foreach (var s in samples) pick.Add(s[i]);
                pick.Sort((a, b) => Lum(a).CompareTo(Lum(b)));
                result[i] = pick[pick.Count / 2];
            }
            Smooth(result);
            return result;
        }

        // The median keeps faint traces of the pictograms inside the square; blurred inside its frame (the frame itself
        // untouched, samples taken only inside it), the square keeps its shading without them.
        static void Smooth(Color32[] square)
        {
            const int Inner = 13, Radius = 6;
            var buffer = new Color[Size * Size];
            for (int pass = 0; pass < 4; pass++)
            {
                bool across = pass % 2 == 0;
                for (int y = Inner; y < Size - Inner; y++)
                    for (int x = Inner; x < Size - Inner; x++)
                    {
                        Color sum = Color.clear;
                        for (int d = -Radius; d <= Radius; d++)
                        {
                            int sx = across ? Mathf.Clamp(x + d, Inner, Size - Inner - 1) : x;
                            int sy = across ? y : Mathf.Clamp(y + d, Inner, Size - Inner - 1);
                            sum += square[sy * Size + sx];
                        }
                        buffer[y * Size + x] = sum / (2 * Radius + 1);
                    }
                for (int y = Inner; y < Size - Inner; y++)
                    for (int x = Inner; x < Size - Inner; x++)
                        square[y * Size + x] = buffer[y * Size + x];
            }
        }

        static Sprite Compose(Texture2D icon, Color32[] square, Color top, Color bottom, float bright)
        {
            var mask = new float[Size * Size];
            var lum = new float[Size * Size];
            for (int y = Edge; y < Size - Edge; y++)
                for (int x = Edge; x < Size - Edge; x++)
                {
                    var c = icon.GetPixelBilinear((x + 0.5f) / Size, (y + 0.5f) / Size);
                    float l = (c.r * 0.3f + c.g * 0.59f + c.b * 0.11f) * 255f;
                    lum[y * Size + x] = l;
                    mask[y * Size + x] = Mathf.Clamp01((l - 70f) / 60f) * c.a;
                }
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Size; y++)
            {
                var gold = Color.Lerp(bottom, top, (float)y / (Size - 1));
                for (int x = 0; x < Size; x++)
                {
                    int i = y * Size + x;
                    float shadow = 0f;
                    for (int dy = -2; dy <= 1; dy++)
                        for (int dx = -1; dx <= 2; dx++)
                        {
                            int sx = x - dx, sy = y - dy;
                            if (sx >= 0 && sy >= 0 && sx < Size && sy < Size) shadow = Mathf.Max(shadow, mask[sy * Size + sx]);
                        }
                    Color back = square[i];
                    back = new Color(back.r * (1 - 0.75f * shadow), back.g * (1 - 0.75f * shadow), back.b * (1 - 0.75f * shadow), back.a);
                    float shade = Mathf.Clamp(lum[i] / 200f, 0.55f, 1f) * bright;
                    var front = new Color(Mathf.Clamp01(gold.r * shade), Mathf.Clamp01(gold.g * shade), Mathf.Clamp01(gold.b * shade), 1f);
                    float m = mask[i];
                    pixels[i] = new Color(Mathf.Lerp(back.r, front.r, m), Mathf.Lerp(back.g, front.g, m), Mathf.Lerp(back.b, front.b, m), back.a);
                }
            }
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, Size, Size), new Vector2(0.5f, 0.5f));
        }

        // The game's sprites are not readable: the GPU copies the sprite's part of its texture, scaled to Size.
        static Color32[] Read(Sprite sprite)
        {
            var texture = sprite.texture;
            var rect = sprite.textureRect;
            var target = RenderTexture.GetTemporary(Size, Size, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var previous = RenderTexture.active;
            var image = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            try
            {
                Graphics.Blit(texture, target, new Vector2(rect.width / texture.width, rect.height / texture.height),
                    new Vector2(rect.x / texture.width, rect.y / texture.height));
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
                image.Apply();
                return image.GetPixels32();
            }
            finally
            {
                RenderTexture.active = previous;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(image);
            }
        }

        static float Lum(Color32 c)
        {
            return c.r * 0.3f + c.g * 0.59f + c.b * 0.11f;
        }
    }
}
