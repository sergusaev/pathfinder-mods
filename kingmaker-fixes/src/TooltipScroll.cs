using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI.Tooltip;
using UnityEngine;
using UnityEngine.UI;

namespace KingmakerFixes
{
    // A mouse tooltip taller than its limit (700) is cut at the bottom, so the rest of a long item description cannot
    // be read. While the cursor is on the item, the wheel now scrolls such a tooltip, and a copy of the game's scroll bar
    // on its right edge shows where in it one is; the lines end before the bar. While such a tooltip is shown the wheel
    // is its alone: the list under the cursor (the inventory) does not scroll. Tooltips that fit behave as before.
    [HarmonyPatch(typeof(TooltipsController), "Update")]
    static class TooltipScroll
    {
        const float WheelStep = 60f;
        const float BarInset = 3f;

        // Plain reflection: the mod's compiler does not take ref returns.
        static readonly System.Reflection.FieldInfo s_IsShowed = AccessTools.Field(typeof(TooltipsController), "m_IsShowed");

        // Per description body: how far it is scrolled, the padding the game gave it, its scroll bar.
        sealed class State
        {
            internal float Offset;
            internal int BaseTop;
            internal int BaseRight;
            internal RectTransform Track;
            internal RectTransform Thumb;
            internal Scrollbar Bar;
            internal float Width;
        }

        static readonly Dictionary<DescriptionBody, State> s_States = new Dictionary<DescriptionBody, State>();
        static bool s_Failed;

        // A long tooltip is shown with the mouse: the wheel scrolls it and nothing else.
        internal static bool LongShown;

        static void Postfix(TooltipsController __instance)
        {
            try { Wheel(__instance); }
            catch (Exception e)
            {
                if (!s_Failed) Main.Log.Error("tooltip scroll: " + e);
                s_Failed = true;
            }
        }

        // A shown tooltip that does not fit, while the cursor is on its item: the wheel scrolls it.
        static void Wheel(TooltipsController controller)
        {
            LongShown = false;
            var constructor = controller.TooltipConstructor;
            if (constructor == null || !(bool)s_IsShowed.GetValue(controller) || constructor.Bodies == null) return;
            if (Game.Instance.IsControllerGamepad) return;
            float wheel = Input.mouseScrollDelta.y;
            foreach (var body in constructor.Bodies)
            {
                if (body == null || body.TooltipBody == null || !body.TooltipBody.activeInHierarchy) continue;
                var state = StateOf(body, constructor);
                if (state == null) continue;
                if (body == constructor.Body) LongShown = true;
                // Only the main body scrolls with the wheel; the compared items beside it show their bar.
                if (wheel != 0f && body == constructor.Body) Scroll(body, state, state.Offset - wheel * WheelStep, MaxOffset(body, constructor));
                Place(body, state, constructor);
            }
        }

        // The scroll state of a body that does not fit, made on first need; null for one that fits.
        static State StateOf(DescriptionBody body, TooltipConstructor constructor)
        {
            var box = body.ContentBox;
            if (box == null) return null;
            var layout = box.GetComponent<VerticalLayoutGroup>();
            if (layout == null) return null;
            State state;
            s_States.TryGetValue(body, out state);
            float need = NeedHeight(body, layout) + layout.padding.bottom + (state != null ? state.BaseTop : layout.padding.top);
            if (need <= constructor.HeightLimit + 1f)
            {
                if (state != null)
                {
                    if (state.Track != null) state.Track.gameObject.SetActive(false);
                    Narrow(body, layout, state, false);
                }
                return null;
            }
            if (state == null)
            {
                state = new State { BaseTop = layout.padding.top, BaseRight = layout.padding.right };
                s_States[body] = state;
                if (box.GetComponent<RectMask2D>() == null && box.GetComponent<Mask>() == null) box.gameObject.AddComponent<RectMask2D>();
            }
            if (state.Track == null) MakeBar(body, state);
            state.Track.gameObject.SetActive(true);
            Narrow(body, layout, state, true);
            return state;
        }

        // The lines of a tooltip with the bar end before it.
        static void Narrow(DescriptionBody body, VerticalLayoutGroup layout, State state, bool on)
        {
            int right = on ? state.BaseRight + Mathf.CeilToInt(state.Width + BarInset + 6f) : state.BaseRight;
            if (layout.padding.right == right) return;
            layout.padding.right = right;
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)body.ContentBox.transform);
        }

        // The height of the content's bricks, as the game counts it.
        static float NeedHeight(DescriptionBody body, VerticalLayoutGroup layout)
        {
            float need = 0f;
            foreach (Transform child in body.ContentBox.transform)
                if (child.gameObject.activeSelf) need += ((RectTransform)child).rect.height + layout.spacing;
            return need;
        }

        static float MaxOffset(DescriptionBody body, TooltipConstructor constructor)
        {
            var layout = body.ContentBox.GetComponent<VerticalLayoutGroup>();
            State state;
            s_States.TryGetValue(body, out state);
            int top = state != null ? state.BaseTop : layout.padding.top;
            return Mathf.Max(0f, NeedHeight(body, layout) + top + layout.padding.bottom - constructor.HeightLimit);
        }

        static void Scroll(DescriptionBody body, State state, float offset, float max)
        {
            offset = Mathf.Clamp(offset, 0f, max);
            if (Mathf.Approximately(offset, state.Offset)) return;
            state.Offset = offset;
            var layout = body.ContentBox.GetComponent<VerticalLayoutGroup>();
            layout.padding.top = state.BaseTop - Mathf.RoundToInt(offset);
            LayoutRebuilder.MarkLayoutForRebuild((RectTransform)body.ContentBox.transform);
        }

        // The bar sits on the tooltip body, not in the content: the content's children are the game's bricks. It is a
        // copy of a vertical scroll bar of the game's interface (the inventory's) at its own width, shown only: it
        // takes no clicks. Without one found, a plain bar.
        static void MakeBar(DescriptionBody body, State state)
        {
            var parent = body.TooltipBody.transform;
            var source = GameBar();
            if (source != null)
            {
                var copy = UnityEngine.Object.Instantiate(source.gameObject, parent, false);
                copy.name = "KingmakerFixes_ScrollBar";
                copy.SetActive(true);
                var bar = copy.GetComponent<Scrollbar>();
                bar.onValueChanged = new Scrollbar.ScrollEvent();
                // In the tooltip the copy counts as not interactable (the tooltip's canvas group) and would be drawn in
                // the faded tint of a disabled control: no tint, the handle as the original shows it.
                bar.transition = Selectable.Transition.None;
                if (bar.targetGraphic != null) bar.targetGraphic.canvasRenderer.SetColor(Color.white);
                var sourceHandle = source.handleRect != null ? source.handleRect.GetComponent<Image>() : null;
                var handle = bar.handleRect != null ? bar.handleRect.GetComponent<Image>() : null;
                if (sourceHandle != null && handle != null)
                {
                    handle.canvasRenderer.SetColor(Color.white);
                    var tint = source.transition == Selectable.Transition.ColorTint && source.targetGraphic == sourceHandle
                        ? source.colors.normalColor * source.colors.colorMultiplier : Color.white;
                    handle.color = sourceHandle.color * tint;
                }
                var group = copy.GetComponent<CanvasGroup>();
                if (group == null) group = copy.AddComponent<CanvasGroup>();
                group.blocksRaycasts = false;
                group.alpha = 1f;
                var layout = copy.GetComponent<LayoutElement>();
                if (layout == null) layout = copy.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;
                float width = ((RectTransform)source.transform).rect.width * source.transform.lossyScale.x / Mathf.Max(1e-5f, parent.lossyScale.x);
                state.Width = width >= 6f && width <= 60f ? width : 20f;
                state.Bar = bar;
                state.Track = (RectTransform)copy.transform;
            }
            else
            {
                var track = new GameObject("KingmakerFixes_ScrollTrack", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                track.GetComponent<LayoutElement>().ignoreLayout = true;
                track.transform.SetParent(parent, false);
                var trackImage = track.GetComponent<Image>();
                trackImage.color = new Color(0.25f, 0.17f, 0.1f, 0.25f);
                trackImage.raycastTarget = false;
                state.Track = (RectTransform)track.transform;
                var thumb = new GameObject("KingmakerFixes_ScrollThumb", typeof(RectTransform), typeof(Image));
                thumb.transform.SetParent(track.transform, false);
                var thumbImage = thumb.GetComponent<Image>();
                thumbImage.color = new Color(0.45f, 0.29f, 0.16f, 0.9f);
                thumbImage.raycastTarget = false;
                state.Thumb = (RectTransform)thumb.transform;
                state.Thumb.anchorMin = new Vector2(0f, 1f);
                state.Thumb.anchorMax = new Vector2(1f, 1f);
                state.Thumb.pivot = new Vector2(0.5f, 1f);
                state.Width = 7f;
            }
            state.Track.anchorMin = state.Track.anchorMax = new Vector2(0.5f, 0.5f);
            state.Track.pivot = new Vector2(1f, 1f);
        }

        // The scroll bar of the inventory's list (the game's ScrollRectExtended on screen), as it is: its track with
        // the frame and its handle. Else a vertical bar of the game with a track that is seen.
        static Scrollbar GameBar()
        {
            foreach (var list in Resources.FindObjectsOfTypeAll<Kingmaker.UI.Common.ScrollRectExtended>())
            {
                if (list == null || !list.gameObject.scene.IsValid() || !list.gameObject.activeInHierarchy || !Visible(list.transform)) continue;
                var bar = list.verticalScrollbar;
                if (bar != null && Seen(bar)) return bar;
            }
            foreach (var bar in Resources.FindObjectsOfTypeAll<Scrollbar>())
            {
                if (bar == null || !bar.gameObject.scene.IsValid() || bar.name.StartsWith("KingmakerFixes")) continue;
                if (bar.direction != Scrollbar.Direction.BottomToTop && bar.direction != Scrollbar.Direction.TopToBottom) continue;
                if (Seen(bar)) return bar;
            }
            return null;
        }

        // A bar with a track and a handle that are drawn.
        static bool Seen(Scrollbar bar)
        {
            var track = bar.GetComponent<Image>();
            var handle = bar.handleRect != null ? bar.handleRect.GetComponent<Image>() : null;
            return track != null && track.sprite != null && track.color.a > 0.5f && handle != null && handle.sprite != null;
        }

        // Not faded out by a canvas group above it.
        static bool Visible(Transform transform)
        {
            foreach (var group in transform.GetComponentsInParent<CanvasGroup>())
                if (group.alpha <= 0.01f) return false;
            return true;
        }

        // The bar along the right edge of the content, its handle as long as the part seen and where it is.
        static void Place(DescriptionBody body, State state, TooltipConstructor constructor)
        {
            var content = (RectTransform)body.ContentBox.transform;
            var parent = (RectTransform)state.Track.parent;
            var corners = new Vector3[4];
            content.GetWorldCorners(corners);
            Vector2 bottomRight = parent.InverseTransformPoint(corners[3]);
            Vector2 topRight = parent.InverseTransformPoint(corners[2]);
            // Local position is from the parent's pivot, as InverseTransformPoint counts it; the bar's pivot is its
            // upper right corner.
            state.Track.localPosition = new Vector3(topRight.x - BarInset, topRight.y, 0f);
            float height = topRight.y - bottomRight.y;
            state.Track.sizeDelta = new Vector2(state.Width, height);

            float max = MaxOffset(body, constructor);
            float seen = height / (height + max);
            float t = max > 0f ? state.Offset / max : 0f;
            if (state.Bar != null)
            {
                state.Bar.size = Mathf.Clamp(seen, 0.1f, 1f);
                state.Bar.value = state.Bar.direction == Scrollbar.Direction.BottomToTop ? 1f - t : t;
                return;
            }
            float thumb = Mathf.Max(24f, height * seen);
            state.Thumb.sizeDelta = new Vector2(0f, thumb);
            state.Thumb.anchoredPosition = new Vector2(0f, -(height - thumb) * t);
        }

        // A tooltip shown anew starts from its top.
        internal static void Reset()
        {
            foreach (var pair in s_States)
            {
                if (pair.Key == null) continue;
                pair.Value.Offset = 0f;
                var layout = pair.Key.ContentBox != null ? pair.Key.ContentBox.GetComponent<VerticalLayoutGroup>() : null;
                if (layout != null)
                {
                    layout.padding.top = pair.Value.BaseTop;
                    layout.padding.right = pair.Value.BaseRight;
                }
                if (pair.Value.Track != null) pair.Value.Track.gameObject.SetActive(false);
            }
        }
    }

    // The scroll of a list under a long tooltip is not done: the wheel scrolls the tooltip. The inventory's list is
    // the game's own ScrollRectExtended, other lists Unity's ScrollRect.
    [HarmonyPatch(typeof(ScrollRect), nameof(ScrollRect.OnScroll))]
    static class TooltipScrollKeepsWheel
    {
        static bool Prefix() => !TooltipScroll.LongShown;
    }

    [HarmonyPatch(typeof(Kingmaker.UI.Common.ScrollRectExtended), nameof(Kingmaker.UI.Common.ScrollRectExtended.OnScroll))]
    static class TooltipScrollKeepsWheelExtended
    {
        static bool Prefix() => !TooltipScroll.LongShown;
    }

    [HarmonyPatch(typeof(TooltipConstructor), "SetContent")]
    static class TooltipScrollReset
    {
        static void Prefix()
        {
            try { TooltipScroll.Reset(); }
            catch (Exception e) { Main.Log.Error("tooltip scroll reset: " + e); }
        }
    }
}
