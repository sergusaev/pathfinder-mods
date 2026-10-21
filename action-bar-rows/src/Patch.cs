using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace ActionBarRows
{
    // A picture of the bar (a frame, a paper) drawn in pieces, so that it can change size without stretching its
    // corners and ornaments. The game's sprites have no border to slice by: the pieces are cut here. The cuts are
    // fractions of the sprite, alternately a piece kept at its own size and a piece that takes up the change, the
    // first and the last kept. Two cuts make one piece: that part of the sprite stretched over the whole rectangle.
    sealed class Patch : MonoBehaviour
    {
        // The sides of a frame between its corners, drawn as its top edge turned on end: a plain stretch of the top
        // edge (between its ornaments, a whole number of repeats of its pattern) laid along the side again and again.
        // In pixels of the picture, counted from its top left corner.
        internal sealed class Side
        {
            // The plain stretch of the top edge, its outer edge at the top.
            internal Rect Bar;
            // Where the side stands: its outer and inner edges from the picture's left edge (the right side mirrors).
            internal float Outer, Inner;
            // A line of the inside of the frame, for what lies between the side and the middle of the frame.
            internal float InsideY;
        }

        Image m_Source;
        Vector2 m_Size0, m_Pixels;
        float[] m_X, m_Y;
        Image[,] m_Cells;
        Vector2 m_Laid = new Vector2(-1f, -1f);
        Side m_Side;
        Sprite m_Bar;
        Image[] m_Insides;
        readonly List<Image>[] m_Tiles = { new List<Image>(), new List<Image>() };

        internal static Patch Make(RectTransform rect, float[] x, float[] y, Side side = null)
        {
            var image = rect != null ? rect.GetComponent<Image>() : null;
            var sprite = image != null ? image.sprite : null;
            if (sprite == null) return null;
            if (sprite.packed && sprite.packingMode == SpritePackingMode.Tight)
            {
                Main.Log(rect.name + ": sprite " + sprite.name + " is tightly packed, stretched whole");
                return null;
            }
            var tex = sprite.packed ? sprite.textureRect : sprite.rect;
            var patch = rect.gameObject.AddComponent<Patch>();
            patch.m_Source = image;
            patch.m_Size0 = rect.rect.size;
            patch.m_Pixels = tex.size;
            patch.m_X = Snap(x, tex.width);
            patch.m_Y = Snap(y, tex.height);
            // The sides need the middle band of the height (four cuts of y) and their own picture.
            if (side != null && patch.m_Y.Length == 4 && side.Bar.xMax <= tex.width && side.Bar.yMax <= tex.height)
                patch.m_Side = side;
            patch.m_Cells = new Image[patch.m_X.Length - 1, patch.m_Y.Length - 1];
            int last = patch.m_X.Length - 2;
            for (int i = 0; i <= last; i++)
                for (int j = 0; j < patch.m_Y.Length - 1; j++)
                {
                    var part = Rect.MinMaxRect(tex.x + patch.m_X[i] * tex.width, tex.y + patch.m_Y[j] * tex.height,
                        tex.x + patch.m_X[i + 1] * tex.width, tex.y + patch.m_Y[j + 1] * tex.height);
                    patch.m_Cells[i, j] = patch.Piece(rect, rect.name + "Piece" + i + j, i * (patch.m_Y.Length - 1) + j, sprite, part);
                }
            if (patch.m_Side != null)
            {
                // The side's band of the two outer columns: the inside next to the side, then the side over it.
                patch.m_Cells[0, 1].gameObject.SetActive(false);
                patch.m_Cells[last, 1].gameObject.SetActive(false);
                float inside = tex.y + tex.height - side.InsideY - 1f;
                patch.m_Insides = new[]
                {
                    patch.Piece(rect, rect.name + "InsideLeft", rect.childCount, sprite,
                        Rect.MinMaxRect(tex.x + side.Inner, inside, tex.x + patch.m_X[1] * tex.width, inside + 1f)),
                    patch.Piece(rect, rect.name + "InsideRight", rect.childCount, sprite,
                        Rect.MinMaxRect(tex.x + patch.m_X[last] * tex.width, inside, tex.x + tex.width - side.Inner, inside + 1f)),
                };
                var bar = side.Bar;
                patch.m_Bar = Sprite.Create(sprite.texture, Rect.MinMaxRect(tex.x + bar.xMin, tex.y + tex.height - bar.yMax,
                    tex.x + bar.xMax, tex.y + tex.height - bar.yMin), new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            }
            image.enabled = false;
            patch.Layout();
            return patch;
        }

        Image Piece(RectTransform parent, string name, int index, Sprite sprite, Rect part)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var cell = (RectTransform)go.transform;
            cell.SetParent(parent, false);
            cell.SetSiblingIndex(index);
            cell.anchorMin = cell.anchorMax = cell.pivot = Vector2.zero;
            var piece = go.AddComponent<Image>();
            piece.sprite = sprite != null ? Sprite.Create(sprite.texture, part, new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect) : m_Bar;
            piece.raycastTarget = false;
            if (m_Source.material != m_Source.defaultMaterial) piece.material = m_Source.material;
            return piece;
        }

        // Cuts on whole pixels: a piece never takes a sliver of its neighbour.
        static float[] Snap(float[] cuts, float pixels)
        {
            var result = new float[cuts.Length];
            for (int i = 0; i < cuts.Length; i++)
            {
                result[i] = Mathf.Round(cuts[i] * pixels) / pixels;
                // A thin piece keeps at least one line of the picture.
                if (i > 0 && result[i] <= result[i - 1] && cuts[i] > cuts[i - 1]) result[i] = result[i - 1] + 1f / pixels;
            }
            return result;
        }

        // Where the cuts fall at a size: the kept pieces at their own size (all shrunk together if even they do not
        // fit), the rest share what is left in proportion.
        static float[] Positions(float[] cuts, float size0, float size)
        {
            // A single piece: a part of the picture stretched over all of it.
            if (cuts.Length == 2) return new[] { 0f, size };
            float kept = 0f;
            for (int i = 0; i < cuts.Length - 1; i += 2) kept += (cuts[i + 1] - cuts[i]) * size0;
            float stretched = size0 - kept;
            float keptScale = size >= kept ? 1f : size / kept;
            float stretchScale = size >= kept && stretched > 0.01f ? (size - kept) / stretched : 0f;
            // Pieces with no size of their own share what is added equally.
            int pieces = (cuts.Length - 1) / 2;
            float each = size >= kept && stretched <= 0.01f && pieces > 0 ? (size - kept) / pieces : 0f;
            var result = new float[cuts.Length];
            for (int i = 0; i < cuts.Length - 1; i++)
                result[i + 1] = result[i] + (i % 2 == 0 ? (cuts[i + 1] - cuts[i]) * size0 * keptScale
                    : stretchScale > 0f ? (cuts[i + 1] - cuts[i]) * size0 * stretchScale : each);
            return result;
        }

        internal void Layout()
        {
            var rect = (RectTransform)transform;
            var size = rect.rect.size;
            var color = m_Source.color;
            if (size != m_Laid)
            {
                m_Laid = size;
                var xs = Positions(m_X, m_Size0.x, size.x);
                var ys = Positions(m_Y, m_Size0.y, size.y);
                for (int i = 0; i < xs.Length - 1; i++)
                    for (int j = 0; j < ys.Length - 1; j++)
                    {
                        if (m_Side != null && j == 1 && (i == 0 || i == xs.Length - 2)) continue;
                        var cell = m_Cells[i, j].rectTransform;
                        // The piece's own rectangle starts at the bottom left of the picture.
                        cell.anchoredPosition = new Vector2(xs[i], ys[j]);
                        cell.sizeDelta = new Vector2(xs[i + 1] - xs[i], ys[j + 1] - ys[j]);
                        m_Cells[i, j].gameObject.SetActive(cell.sizeDelta.x > 0.01f && cell.sizeDelta.y > 0.01f);
                    }
                if (m_Side != null) LayoutSides(xs, ys);
            }
            foreach (var cell in m_Cells)
                if (cell.color != color) cell.color = color;
            if (m_Side == null) return;
            foreach (var inside in m_Insides)
                if (inside.color != color) inside.color = color;
            foreach (var tiles in m_Tiles)
                foreach (var tile in tiles)
                    if (tile.color != color) tile.color = color;
        }

        // The sides between the corners: the picture's pixels at the scale of the outer columns (kept at their own
        // size), the stretch of the top edge turned on end, its outer edge outwards, as many times as fit, all
        // stretched alike to fill the side exactly.
        void LayoutSides(float[] xs, float[] ys)
        {
            int last = xs.Length - 2;
            float scale = (xs[1] - xs[0]) / Mathf.Max(0.01f, (m_X[1] - m_X[0]) * m_Pixels.x);
            float outer = m_Side.Outer * scale, inner = m_Side.Inner * scale, width = xs[last + 1];
            float bottom = ys[1], height = ys[2] - ys[1];
            PlaceInside(m_Insides[0], inner, bottom, xs[1] - inner, height);
            PlaceInside(m_Insides[1], xs[last], bottom, width - inner - xs[last], height);
            float thickness = inner - outer;
            float length = m_Side.Bar.width * thickness / Mathf.Max(0.01f, m_Side.Bar.height);
            int count = height > 0.01f && length > 0.01f ? Mathf.Max(1, Mathf.RoundToInt(height / length)) : 0;
            for (int s = 0; s < 2; s++)
            {
                var tiles = m_Tiles[s];
                while (tiles.Count < count)
                {
                    var tile = Piece((RectTransform)transform, name + (s == 0 ? "SideLeft" : "SideRight") + tiles.Count, transform.childCount, null, default(Rect));
                    var r = tile.rectTransform;
                    r.pivot = new Vector2(0.5f, 0.5f);
                    // The top edge's outer edge (the picture's top) turned outwards: to the left on the left side.
                    r.localRotation = Quaternion.Euler(0f, 0f, s == 0 ? 90f : -90f);
                    tiles.Add(tile);
                }
                float x = s == 0 ? (outer + inner) / 2f : width - (outer + inner) / 2f;
                for (int k = 0; k < tiles.Count; k++)
                {
                    var r = tiles[k].rectTransform;
                    bool shown = k < count;
                    tiles[k].gameObject.SetActive(shown);
                    if (!shown) continue;
                    float each = height / count;
                    r.sizeDelta = new Vector2(each, thickness);
                    r.anchoredPosition = new Vector2(x, bottom + each * (k + 0.5f));
                }
            }
        }

        static void PlaceInside(Image inside, float x, float y, float width, float height)
        {
            var r = inside.rectTransform;
            r.anchoredPosition = new Vector2(x, y);
            r.sizeDelta = new Vector2(width, height);
            inside.gameObject.SetActive(width > 0.01f && height > 0.01f);
        }
    }
}
