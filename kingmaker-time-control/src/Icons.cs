using UnityEngine;

namespace TimeControl
{
    // Small pictograms drawn by code, white on transparent (tinted by the Image): the game's font has no such marks.
    static class Icons
    {
        static Sprite s_Eye;

        // The eye of a tracked deadline: an almond outline and the pupil.
        internal static Sprite Eye
        {
            get
            {
                if (s_Eye == null) s_Eye = DrawEye();
                return s_Eye;
            }
        }

        static Sprite DrawEye()
        {
            const int W = 56, H = 40, Sub = 4;
            const float Half = 25f, Lid = 15f, Stroke = 2.6f, Pupil = 7.5f, Iris = 11.5f;
            var pixels = new Color32[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    int hit = 0;
                    for (int sy = 0; sy < Sub; sy++)
                        for (int sx = 0; sx < Sub; sx++)
                        {
                            float px = x + (sx + 0.5f) / Sub - W / 2f, py = y + (sy + 0.5f) / Sub - H / 2f;
                            float u = px / Half;
                            // The lids: two arcs meeting at the corners.
                            float lid = Mathf.Abs(u) <= 1f ? Lid * (1f - u * u) : -1f;
                            bool outline = lid >= 0f && Mathf.Abs(Mathf.Abs(py) - lid) <= Stroke / 2f;
                            float r = Mathf.Sqrt(px * px + py * py);
                            // The iris as a ring inside the lids, the pupil full.
                            bool iris = r <= Pupil || (r >= Iris - Stroke / 2f && r <= Iris + Stroke / 2f && Mathf.Abs(py) < lid);
                            if (outline || iris) hit++;
                        }
                    pixels[y * W + x] = new Color32(255, 255, 255, (byte)(255 * hit / (Sub * Sub)));
                }
            var texture = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, W, H), new Vector2(0.5f, 0.5f));
        }
    }
}
