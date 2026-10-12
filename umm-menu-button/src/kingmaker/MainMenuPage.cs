using System;
using UnityEngine;
using UnityEngine.UI;

namespace UmmMenuButton
{
    // The main menu of the mouse interface lies on a page painted into the texture of the table, and the page has one
    // ornamental frame for each of the game's seven items. With the item added the item takes the place of Exit, and
    // Exit goes one row lower, where the last frame is painted into the texture once more. The column of frames and
    // the list of items are made smaller as a whole by the share of the added item, as the menus of Wrath of the
    // Righteous are, keeping their top left corner, and moved somewhat to the left; the place they leave is painted
    // over with the paper next to it. Painted into the texture, the frames take the light of the scene as the others do.
    class MainMenuPage
    {
        const string PlaneName = "MainMenuPlane_Full";
        // How far to the left the menu goes, in rows.
        const float Shift = 0.35f;
        static Texture2D s_Painted;

        RectTransform m_List;
        Vector3 m_First;
        Vector3 m_LeftMid;
        Vector3 m_Step;
        Vector3 m_Right;
        RectTransform m_FirstItem;
        float m_Row;
        int m_Count;

        static Vector3 Center(RectTransform rect)
        {
            return rect.TransformPoint(rect.rect.center);
        }

        // Before the item is added: where the game's items are.
        internal static MainMenuPage Measure(Component options, Component exit)
        {
            var list = options.transform.parent as RectTransform;
            if (list == null || list.Find(Main.ItemName) != null) return null;
            RectTransform first = null, second = null;
            int count = 0;
            foreach (Transform child in list)
            {
                if (!child.gameObject.activeSelf) continue;
                count++;
                if (first == null) first = (RectTransform)child;
                else if (second == null) second = (RectTransform)child;
            }
            if (first == null || second == null) return null;
            var page = new MainMenuPage { m_List = list, m_FirstItem = first, m_First = Center(first), m_Count = count };
            page.m_Step = Center(second) - page.m_First;
            page.m_Row = Mathf.Abs(list.InverseTransformVector(page.m_Step).y);
            // Not laid out yet: the items are all in one place.
            if (page.m_Row < 1) return null;
            var c = new Vector3[4]; // bottom left, top left, top right, bottom right
            ((RectTransform)exit.transform).GetWorldCorners(c);
            page.m_Right = (c[3] - c[0]).normalized * page.m_Step.magnitude;
            var corners = new Vector3[4];
            first.GetWorldCorners(corners);
            page.m_LeftMid = (corners[0] + corners[1]) / 2;
            return page;
        }

        // After the item is added: the list one row longer, its first row where it was, the new frame, and all of it
        // made smaller and moved to the left.
        internal void Lay()
        {
            var element = m_List.GetComponent<LayoutElement>() ?? m_List.gameObject.AddComponent<LayoutElement>();
            element.ignoreLayout = true;
            m_List.sizeDelta += new Vector2(0, m_Row);
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_List);
            m_List.position += m_First - Center(m_FirstItem);
            Main.Log.Log("main menu: row " + m_Row + ", list moved to keep the first row");

            float scale = (float)m_Count / (m_Count + 1);
            // The top left corner of the first frame: the frames begin where the items do and are one row high.
            var anchor = m_LeftMid - m_Step / 2;
            var shift = -m_Right * Shift;
            bool painted = false;
            try
            {
                painted = Paint(anchor, scale, shift);
            }
            catch (Exception e)
            {
                Main.Error(e, "MainMenuPage.Paint");
            }
            // Without the frames made smaller the items stay as they are, in the frames.
            if (!painted) return;
            m_List.localScale *= scale;
            m_List.position = anchor + (m_List.position - anchor) * scale + shift;
            Main.Log.Log("main menu: list made smaller to " + m_Count + "/" + (m_Count + 1) + " and moved to the left");
        }

        bool Paint(Vector3 anchor, float scale, Vector3 shift)
        {
            var plane = GameObject.Find(PlaneName);
            var renderer = plane != null ? plane.GetComponent<MeshRenderer>() : null;
            var filter = plane != null ? plane.GetComponent<MeshFilter>() : null;
            if (renderer == null || filter == null || renderer.sharedMaterial == null || renderer.sharedMaterial.mainTexture == null)
            {
                Main.Log.Log("main menu: no " + PlaneName);
                return false;
            }
            var source = renderer.sharedMaterial.mainTexture;
            int w = source.width, h = source.height;

            // The frames are a little more than one row wide, the swirls on their left reaching the items' left edge.
            // With a margin of paper around them: the game's column of frames, the same with the row below it, the
            // point it is made smaller to, where it goes, one row down and where the plain paper is.
            var topLeft = Pixel(filter, anchor - m_Step * 0.15f - m_Right * 0.2f, w, h);
            var bottomRight = Pixel(filter, anchor + m_Step * (m_Count + 0.15f) + m_Right * 1.25f, w, h);
            var lower = Pixel(filter, anchor + m_Step * (m_Count + 1.15f) + m_Right * 1.25f, w, h);
            var at = Pixel(filter, anchor, w, h);
            var moved = Pixel(filter, anchor + shift, w, h);
            var down = Pixel(filter, anchor + m_Step, w, h);
            var paper = Pixel(filter, anchor + m_Right * 2.2f, w, h);
            if (topLeft == null || bottomRight == null || lower == null || at == null || moved == null || down == null || paper == null)
            {
                Main.Log.Log("main menu: the page is not where the items are");
                return false;
            }
            var column = Box(topLeft.Value, bottomRight.Value);
            var longer = Box(topLeft.Value, lower.Value);
            var row = down.Value - at.Value;
            Main.Log.Log("main menu: texture " + w + "x" + h + ", column " + column + ", one row " + row + ", moved by " + (moved.Value - at.Value));
            if (column.width < 1 || column.height < 1 || row.sqrMagnitude < 1) return false;

            var target = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var active = RenderTexture.active;
            Graphics.Blit(source, target);
            RenderTexture.active = target;
            var painted = new Texture2D(w, h, TextureFormat.RGBA32, true, false);
            painted.ReadPixels(new Rect(0, 0, w, h), 0, 0, false);
            RenderTexture.active = active;
            target.Release();
            UnityEngine.Object.Destroy(target);

            var pixels = painted.GetPixels32();
            // Diagnostics: the page around the column, before and after.
            float r = row.magnitude;
            var around = Rect.MinMaxRect(longer.xMin - 2 * r, longer.yMin - r, longer.xMax + 3 * r, longer.yMax + r);
            Save(pixels, w, h, around, "page-before.png");
            Shrink(pixels, w, h, column, longer, at.Value, row, m_Count, moved.Value - at.Value, scale, paper.Value - at.Value);
            Save(pixels, w, h, around, "page-after.png");
            painted.SetPixels32(pixels);
            painted.wrapMode = source.wrapMode;
            painted.filterMode = source.filterMode;
            painted.anisoLevel = source.anisoLevel;
            painted.Apply(true, true);
            if (s_Painted != null) UnityEngine.Object.Destroy(s_Painted);
            s_Painted = painted;
            renderer.material.mainTexture = painted;
            Main.Log.Log("main menu: frames painted");
            return true;
        }

        static Rect Box(Vector2 a, Vector2 b)
        {
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        // The column of frames one row longer, the last frame once more under it, made smaller towards the anchor and
        // moved; where the game's column was, the plain paper found at the given offset. Everything fades into the
        // texture as it was at the edges, on the paper around the frames.
        static void Shrink(Color32[] pixels, int w, int h, Rect column, Rect longer, Vector2 anchor, Vector2 row, int rows, Vector2 shift, float scale, Vector2 paper)
        {
            var source = (Color32[])pixels.Clone();
            var to = Rect.MinMaxRect(anchor.x + shift.x + (longer.xMin - anchor.x) * scale, anchor.y + shift.y + (longer.yMin - anchor.y) * scale,
                anchor.x + shift.x + (longer.xMax - anchor.x) * scale, anchor.y + shift.y + (longer.yMax - anchor.y) * scale);
            var all = Rect.MinMaxRect(Mathf.Min(column.xMin, to.xMin), Mathf.Min(column.yMin, to.yMin), Mathf.Max(column.xMax, to.xMax), Mathf.Max(column.yMax, to.yMax));
            float fade = Mathf.Max(2f, row.magnitude * 0.08f);
            int px = Mathf.RoundToInt(paper.x), py = Mathf.RoundToInt(paper.y);
            int left = Mathf.Max(0, (int)all.xMin), right = Mathf.Min(w - 1, (int)all.xMax);
            int top = Mathf.Max(0, (int)all.yMin), bottom = Mathf.Min(h - 1, (int)all.yMax);
            // The page as dark as it is right left and right of the place, every row of pixels.
            var pageLeft = new Color[bottom - top + 1];
            var pageRight = new Color[bottom - top + 1];
            for (int y = top; y <= bottom; y++)
            {
                pageLeft[y - top] = Mean(source, w, h, left - 10, y);
                pageRight[y - top] = Mean(source, w, h, right + 10, y);
            }
            Func<float, float, Color> page = (x, y) =>
            {
                int i = Mathf.Clamp(Mathf.RoundToInt(y), top, bottom) - top;
                return Color.Lerp(pageLeft[i], pageRight[i], (x - left) / Mathf.Max(1, right - left));
            };
            for (int y = top; y <= bottom; y++)
            {
                // The page darkens towards its edges: the paper taken from aside made as dark as the page is right
                // left and right of the place, and in between as the page goes from one to the other.
                var toLeft = Tone(source, w, h, left - 10, y, px, py);
                var toRight = Tone(source, w, h, right + 10, y, px, py);
                for (int x = left; x <= right; x++)
                {
                    float cx = x + 0.5f, cy = y + 0.5f;
                    float keep = Edge(all, cx, cy, fade);
                    if (keep <= 0) continue;
                    var plain = (Color)source[Mathf.Clamp(y + py, 0, h - 1) * w + Mathf.Clamp(x + px, 0, w - 1)];
                    var tone = Color.Lerp(toLeft, toRight, (x - left) / (float)Mathf.Max(1, right - left));
                    Color32 result = new Color(Mathf.Clamp01(plain.r + tone.r), Mathf.Clamp01(plain.g + tone.g), Mathf.Clamp01(plain.b + tone.b), plain.a);
                    float inside = Edge(to, cx, cy, fade);
                    if (inside > 0)
                    {
                        var from = anchor + (new Vector2(cx, cy) - anchor - shift) / scale;
                        // Below the game's last frame: the last frame once more.
                        if (Vector2.Dot(from - anchor, row) / row.sqrMagnitude >= rows) from -= row;
                        // Taken from elsewhere on the page, the frames made as dark as the page is where they go.
                        var frames = (Color)Sample(source, w, h, from.x, from.y);
                        var darker = page(cx, cy) - page(from.x, from.y);
                        Color32 moved = new Color(Mathf.Clamp01(frames.r + darker.r), Mathf.Clamp01(frames.g + darker.g), Mathf.Clamp01(frames.b + darker.b), frames.a);
                        result = Color32.Lerp(result, moved, inside);
                    }
                    pixels[y * w + x] = Color32.Lerp(source[y * w + x], result, keep);
                }
            }
        }

        static void Save(Color32[] pixels, int w, int h, Rect area, string name)
        {
            int x0 = Mathf.Clamp((int)area.xMin, 0, w - 1), y0 = Mathf.Clamp((int)area.yMin, 0, h - 1);
            int x1 = Mathf.Clamp((int)area.xMax, x0 + 1, w), y1 = Mathf.Clamp((int)area.yMax, y0 + 1, h);
            var crop = new Texture2D(x1 - x0, y1 - y0, TextureFormat.RGBA32, false);
            var part = new Color32[(x1 - x0) * (y1 - y0)];
            for (int y = y0; y < y1; y++)
                Array.Copy(pixels, y * w + x0, part, (y - y0) * (x1 - x0), x1 - x0);
            crop.SetPixels32(part);
            crop.Apply();
            System.IO.File.WriteAllBytes(System.IO.Path.Combine(Main.Path, name), crop.EncodeToPNG());
            UnityEngine.Object.Destroy(crop);
            Main.Log.Log("main menu: saved " + name + " of " + x0 + ".." + x1 + " x " + y0 + ".." + y1);
        }

        // How much darker the page is around the given pixel than the paper at the offset: the difference of their
        // averages, which may be negative and so is not a colour.
        static Color Tone(Color32[] pixels, int w, int h, int x, int y, int px, int py)
        {
            float r = 0, g = 0, b = 0;
            int n = 0;
            for (int j = y - 8; j <= y + 8; j++)
            {
                for (int i = x - 6; i <= x + 6; i++)
                {
                    if (i < 0 || j < 0 || i >= w || j >= h || i + px < 0 || j + py < 0 || i + px >= w || j + py >= h) continue;
                    Color here = pixels[j * w + i], there = pixels[(j + py) * w + i + px];
                    r += here.r - there.r;
                    g += here.g - there.g;
                    b += here.b - there.b;
                    n++;
                }
            }
            return n == 0 ? new Color(0, 0, 0, 0) : new Color(r / n, g / n, b / n, 0);
        }

        static Color Mean(Color32[] pixels, int w, int h, int x, int y)
        {
            float r = 0, g = 0, b = 0;
            int n = 0;
            for (int j = y - 8; j <= y + 8; j++)
            {
                for (int i = x - 6; i <= x + 6; i++)
                {
                    if (i < 0 || j < 0 || i >= w || j >= h) continue;
                    Color c = pixels[j * w + i];
                    r += c.r;
                    g += c.g;
                    b += c.b;
                    n++;
                }
            }
            return n == 0 ? Color.clear : new Color(r / n, g / n, b / n, 0);
        }

        static float Edge(Rect r, float x, float y, float fade)
        {
            return Mathf.Clamp01(Mathf.Min(Mathf.Min(x - r.xMin, r.xMax - x), Mathf.Min(y - r.yMin, r.yMax - y)) / fade);
        }

        // The texture between its pixels.
        static Color32 Sample(Color32[] pixels, int w, int h, float x, float y)
        {
            x -= 0.5f;
            y -= 0.5f;
            int x0 = Mathf.Clamp(Mathf.FloorToInt(x), 0, w - 1), y0 = Mathf.Clamp(Mathf.FloorToInt(y), 0, h - 1);
            int x1 = Mathf.Min(x0 + 1, w - 1), y1 = Mathf.Min(y0 + 1, h - 1);
            float fx = Mathf.Clamp01(x - x0), fy = Mathf.Clamp01(y - y0);
            var lower = Color32.Lerp(pixels[y0 * w + x0], pixels[y0 * w + x1], fx);
            var upper = Color32.Lerp(pixels[y1 * w + x0], pixels[y1 * w + x1], fx);
            return Color32.Lerp(lower, upper, fy);
        }

        // The texture pixel under a point of the scene: the point taken into the plane's mesh, its texture coordinates
        // interpolated in the triangle it falls into (seen along the plane's normal).
        static Vector2? Pixel(MeshFilter filter, Vector3 world, int w, int h)
        {
            var mesh = filter.sharedMesh;
            if (mesh == null || !mesh.isReadable) return null;
            var local = filter.transform.InverseTransformPoint(world);
            var vertices = mesh.vertices;
            var uv = mesh.uv;
            var triangles = mesh.triangles;
            for (int i = 0; i + 2 < triangles.Length; i += 3)
            {
                Vector3 p = vertices[triangles[i]], q = vertices[triangles[i + 1]], r = vertices[triangles[i + 2]];
                float d = (q.z - r.z) * (p.x - r.x) + (r.x - q.x) * (p.z - r.z);
                if (Mathf.Abs(d) < 1e-9f) continue;
                float l1 = ((q.z - r.z) * (local.x - r.x) + (r.x - q.x) * (local.z - r.z)) / d;
                float l2 = ((r.z - p.z) * (local.x - r.x) + (p.x - r.x) * (local.z - r.z)) / d;
                float l3 = 1 - l1 - l2;
                if (l1 < -1e-4f || l2 < -1e-4f || l3 < -1e-4f) continue;
                var t = uv[triangles[i]] * l1 + uv[triangles[i + 1]] * l2 + uv[triangles[i + 2]] * l3;
                return new Vector2(t.x * w, t.y * h);
            }
            return null;
        }
    }
}
