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
        Image m_Source;
        Vector2 m_Size0;
        float[] m_X, m_Y;
        Image[,] m_Cells;
        Vector2 m_Laid = new Vector2(-1f, -1f);

        // yBand: where the picture of the stretched piece of y comes from, when not from between its own cuts. A side
        // that is ornaments from end to end grows by a plain line taken elsewhere, put between its two halves (the
        // cuts then give that piece no size of its own).
        internal static Patch Make(RectTransform rect, float[] x, float[] y, float[] yBand = null)
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
            patch.m_X = Snap(x, tex.width);
            patch.m_Y = Snap(y, tex.height);
            var band = yBand != null ? Snap(yBand, tex.height) : null;
            patch.m_Cells = new Image[patch.m_X.Length - 1, patch.m_Y.Length - 1];
            for (int i = 0; i < patch.m_X.Length - 1; i++)
                for (int j = 0; j < patch.m_Y.Length - 1; j++)
                {
                    float y0 = band != null && j == 1 ? band[0] : patch.m_Y[j], y1 = band != null && j == 1 ? band[1] : patch.m_Y[j + 1];
                    var part = Rect.MinMaxRect(tex.x + patch.m_X[i] * tex.width, tex.y + y0 * tex.height,
                        tex.x + patch.m_X[i + 1] * tex.width, tex.y + y1 * tex.height);
                    var go = new GameObject(rect.name + "Piece" + i + j, typeof(RectTransform));
                    var cell = (RectTransform)go.transform;
                    cell.SetParent(rect, false);
                    cell.SetSiblingIndex(i * (patch.m_Y.Length - 1) + j);
                    cell.anchorMin = cell.anchorMax = cell.pivot = Vector2.zero;
                    var piece = go.AddComponent<Image>();
                    piece.sprite = Sprite.Create(sprite.texture, part, new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
                    piece.raycastTarget = false;
                    if (image.material != image.defaultMaterial) piece.material = image.material;
                    patch.m_Cells[i, j] = piece;
                }
            image.enabled = false;
            patch.Layout();
            return patch;
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
                        var cell = m_Cells[i, j].rectTransform;
                        // The piece's own rectangle starts at the bottom left of the picture.
                        cell.anchoredPosition = new Vector2(xs[i], ys[j]);
                        cell.sizeDelta = new Vector2(xs[i + 1] - xs[i], ys[j + 1] - ys[j]);
                        m_Cells[i, j].gameObject.SetActive(cell.sizeDelta.x > 0.01f && cell.sizeDelta.y > 0.01f);
                    }
            }
            foreach (var cell in m_Cells)
                if (cell.color != color) cell.color = color;
        }
    }
}
