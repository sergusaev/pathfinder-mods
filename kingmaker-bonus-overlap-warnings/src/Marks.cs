using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.Items;
using Kingmaker.UI.ActionBar;
using Kingmaker.UI._ConsoleUI.Inventory;
using UnityEngine;
using UnityEngine.UI;
using PcEquipSlot = Kingmaker.UI.ServiceWindow.EquipSlot;

namespace BonusOverlapWarnings
{
    // The warning in the upper left corner of an equipped item's icon on the doll: red when no bonus of the item has
    // effect, yellow when some have none, blue when the item's bonus leaves another source's without use. The mouse
    // interface and the gamepad's; the pet's doll too, it is made of the same slots.
    internal static class Marks
    {
        const string MarkName = "BonusOverlapWarnings_Mark";
        static readonly Color32 Red = new Color32(222, 70, 52, 255);
        static readonly Color32 Yellow = new Color32(236, 170, 48, 255);
        static readonly Color32 Blue = new Color32(96, 156, 222, 255);
        static readonly Color32 Green = new Color32(76, 178, 88, 255);
        static readonly Dictionary<int, Sprite> s_Sprites = new Dictionary<int, Sprite>();
        // The marks of the doll's slots showing an item now, with their item.
        static readonly Dictionary<Image, ItemEntity> s_Live = new Dictionary<Image, ItemEntity>();
        static float s_Next;

        // The slot's icon shows this item now (null when empty). host: the gamepad's slot view, whose layers (the
        // magic circle, the selection frame) are drawn over the icon: the mark is put on top of them all there.
        internal static void Show(Image icon, ItemEntity item, Transform host = null)
        {
            if (icon == null) return;
            var mark = Find(host != null ? host : icon.transform);
            if (item == null || item.Wielder == null)
            {
                if (mark != null)
                {
                    mark.gameObject.SetActive(false);
                    s_Live.Remove(mark);
                }
                return;
            }
            bool green;
            var verdict = VerdictOf(item, out green);
            if (mark == null)
            {
                if (verdict == Verdict.None) return;
                mark = Create(icon, host);
                if (mark == null) return;
            }
            if (host != null) PlaceOnTop(mark, icon, host);
            s_Live[mark] = item;
            Apply(mark, verdict, green);
            // An item put on or taken off changes the other slots' marks too: all of them next frame.
            s_Next = Time.unscaledTime;
        }

        static Verdict VerdictOf(ItemEntity item, out bool green)
        {
            green = false;
            var report = Overlaps.Of(item);
            if (report == null) return Verdict.None;
            // The plain blue mark can be turned off; the blue and green one (equal items, both kept) stays.
            if (report.Verdict == Verdict.Overrides && !report.Green && !Main.Settings.MarkOverrides) return Verdict.None;
            green = report.Green;
            return report.Verdict;
        }

        static void Apply(Image mark, Verdict verdict, bool green)
        {
            bool on = verdict != Verdict.None;
            if (on)
            {
                var sprite = SpriteOf(verdict, green);
                if (mark.sprite != sprite) mark.sprite = sprite;
            }
            if (mark.gameObject.activeSelf != on) mark.gameObject.SetActive(on);
        }

        static Image Find(Transform parent)
        {
            var child = parent.Find(MarkName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        static Image Create(Image icon, Transform host)
        {
            var go = new GameObject(MarkName, typeof(RectTransform), typeof(LayoutElement));
            go.GetComponent<LayoutElement>().ignoreLayout = true;
            go.layer = icon.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(host != null ? host : icon.transform, false);
            rect.anchorMin = new Vector2(-0.04f, 0.6f);
            rect.anchorMax = new Vector2(0.4f, 1.04f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
            var image = go.AddComponent<Image>();
            image.preserveAspect = true;
            // The slot's clicks, drags and tooltips stay the slot's.
            image.raycastTarget = false;
            return image;
        }

        // On the gamepad: over every layer of the slot, at the icon's upper left corner, smaller than with the mouse,
        // its edges along the icon's, so it does not go under the selection frame.
        static void PlaceOnTop(Image mark, Image icon, Transform host)
        {
            var hostRect = host as RectTransform;
            if (hostRect == null) return;
            var corners = new Vector3[4];
            ((RectTransform)icon.transform).GetWorldCorners(corners);
            Vector2 min = hostRect.InverseTransformPoint(corners[0]);
            Vector2 max = hostRect.InverseTransformPoint(corners[2]);
            var r = hostRect.rect;
            if (r.width <= 0f || r.height <= 0f) return;
            var lo = new Vector2((min.x - r.xMin) / r.width, (min.y - r.yMin) / r.height);
            var hi = new Vector2((max.x - r.xMin) / r.width, (max.y - r.yMin) / r.height);
            var size = hi - lo;
            var rect = (RectTransform)mark.transform;
            rect.anchorMin = new Vector2(lo.x, hi.y - size.y * 0.36f);
            rect.anchorMax = new Vector2(lo.x + size.x * 0.36f, hi.y);
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
        }

        // Once a second everything is counted anew: a buff cast or ended with the inventory open shows at once.
        internal static void Tick()
        {
            if (s_Live.Count == 0 || Time.unscaledTime < s_Next) return;
            Refresh();
        }

        internal static void Refresh()
        {
            s_Next = Time.unscaledTime + 1f;
            Overlaps.Clear();
            if (s_Live.Count == 0) return;
            var gone = new List<Image>();
            foreach (var pair in s_Live)
            {
                // A destroyed window's marks.
                if (pair.Key == null) { gone.Add(pair.Key); continue; }
                if (pair.Value.Wielder == null) { pair.Key.gameObject.SetActive(false); gone.Add(pair.Key); continue; }
                bool green;
                var verdict = VerdictOf(pair.Value, out green);
                Apply(pair.Key, verdict, green);
            }
            foreach (var mark in gone) s_Live.Remove(mark);
        }

        static Sprite SpriteOf(Verdict verdict, bool green)
        {
            Sprite sprite;
            int key = (int)verdict * 2 + (green ? 1 : 0);
            if (s_Sprites.TryGetValue(key, out sprite) && sprite != null) return sprite;
            var color = verdict == Verdict.Lost ? Red : verdict == Verdict.Reduced ? Yellow : Blue;
            sprite = Draw(color, green);
            s_Sprites[key] = sprite;
            return sprite;
        }

        // A diamond of the colour with a dark rim and a dark exclamation mark, smoothed at the edges. Split: cut in
        // two along the line from the middle of the lower left edge to the middle of the upper right one, the colour
        // in the upper left half and green in the lower right one, with no line between them.
        static Sprite Draw(Color32 fill, bool split)
        {
            const int size = 64;
            var dark = new Color(0.08f, 0.06f, 0.05f, 1f);
            var paint = (Color)fill;
            var pixels = new Color[size * size];
            float c = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float px = x - c, py = y - c;
                    // Distance to the diamond's edge in pixels, inside negative.
                    float d = (Mathf.Abs(px) + Mathf.Abs(py) - 30f) * 0.7071f;
                    float alpha = Mathf.Clamp01(0.5f - d);
                    // The rim: the outer 3.5 pixels.
                    float rim = Mathf.Clamp01(d + 4f);
                    var body = paint;
                    if (split) body = Color.Lerp(paint, (Color)Green, Mathf.Clamp01((px - py) * 0.7071f + 0.5f));
                    var col = Color.Lerp(body, dark, rim);
                    // The exclamation mark: a bar narrowing downwards and a dot, y up.
                    float bar = Mathf.Max(Mathf.Abs(px) - Mathf.Lerp(3.2f, 5f, Mathf.InverseLerp(-4f, 20f, py)), Mathf.Max(-4f - py, py - 20f));
                    float dot = Mathf.Sqrt(px * px + (py + 12.5f) * (py + 12.5f)) - 4.4f;
                    float glyph = Mathf.Clamp01(0.5f - Mathf.Min(bar, dot));
                    col = Color.Lerp(col, dark, glyph);
                    col.a = alpha;
                    pixels[y * size + x] = col;
                }
            }
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, true)
            {
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 4
            };
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f));
        }
    }

    // The mouse interface: the doll's slots (the inventory, the vendor's window), not the action bar's weapon slots.
    [HarmonyPatch(typeof(PcEquipSlot), nameof(PcEquipSlot.SetItem))]
    static class PcEquipSlotSetItem
    {
        static void Postfix(PcEquipSlot __instance, ItemEntity item)
        {
            try
            {
                if (__instance is ActionBarEquipSlot) return;
                Marks.Show(__instance.ItemImage, item);
            }
            catch (Exception e) { Main.Error(e, "mark (mouse)"); }
        }
    }

    // The gamepad's interface: the doll's slots of the inventory.
    [HarmonyPatch(typeof(InventoryEquipSlotView), "SetupIcon")]
    static class ConsoleEquipSlotIcon
    {
        static void Postfix(InventoryEquipSlotView __instance)
        {
            try
            {
                var vm = __instance.EquipSlotVM;
                Marks.Show(__instance.Icon, vm != null && vm.Item != null ? vm.Item.Value : null, __instance.transform);
            }
            catch (Exception e) { Main.Error(e, "mark (gamepad)"); }
        }
    }
}
