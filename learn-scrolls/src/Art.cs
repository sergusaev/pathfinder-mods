using System;
using UnityEngine;

namespace LearnScrolls
{
    // The mod's pictures shipped with it (art/, see make_art.py): the scroll of the games' Journal card with its quill
    // taken away, and what is made of it.
    internal static class Art
    {
        // A picture with mipmaps, smooth when shown smaller; each pixel through paint when given.
        internal static Sprite Picture(string name, Func<Color32, Color32> paint = null)
        {
            var source = Bar.Read(name, false);
            if (source == null) return null;
            var sprite = Smooth(source, paint);
            UnityEngine.Object.Destroy(source);
            return sprite;
        }

        internal static Sprite Smooth(Texture2D texture, Func<Color32, Color32> paint = null)
        {
            if (texture == null) return null;
            var pixels = texture.GetPixels32();
            if (paint != null)
                for (int i = 0; i < pixels.Length; i++) pixels[i] = paint(pixels[i]);
            var copy = new Texture2D(texture.width, texture.height, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            copy.SetPixels32(pixels);
            copy.Apply(true, true);
            return Sprite.Create(copy, new Rect(0, 0, copy.width, copy.height), new Vector2(0.5f, 0.5f));
        }
    }
}
