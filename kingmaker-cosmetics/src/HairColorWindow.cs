using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI.LevelUp;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace KingmakerCosmetics
{
    // With the colours of all races there are too many hair colours for one row: five are shown at a time, between
    // two arrows like those of the hair style, each choosing the next or the previous colour, the row moving on
    // when the choice goes past its end. It opens on the colour chosen.
    class HairColorWindow : MonoBehaviour
    {
        internal const int Shown = 5;

        static readonly FieldInfo s_Toggles = AccessTools.Field(typeof(CharBColorSelector), "m_ColorToggles");
        static readonly FieldInfo s_Ramps = AccessTools.Field(typeof(CharBColorSelector), "m_CurrentRamps");
        static readonly FieldInfo s_Group = AccessTools.Field(typeof(CharBColorSelector), "m_ColorToggleGroup");
        static readonly FieldInfo s_Ignore = AccessTools.Field(typeof(CharBColorSelector), "m_IgnoreAction");
        static readonly FieldInfo s_Back = AccessTools.Field(typeof(CharBSequentialSelector), "m_BackButton");
        static readonly FieldInfo s_Next = AccessTools.Field(typeof(CharBSequentialSelector), "m_NextButton");

        CharBColorSelector m_Selector;
        Button m_BackArrow, m_NextArrow;
        int m_Start;
        bool m_Placed;

        internal static void Attach(CharBColorSelector selector, CharBSequentialSelector style)
        {
            if (selector == null || selector.GetComponent<HairColorWindow>() != null)
                return;
            var window = selector.gameObject.AddComponent<HairColorWindow>();
            window.m_Selector = selector;
            var group = (ToggleGroup)s_Group.GetValue(selector);
            if (style != null && group != null)
            {
                window.m_BackArrow = window.Arrow((Button)s_Back.GetValue(style), group.transform, -1);
                window.m_NextArrow = window.Arrow((Button)s_Next.GetValue(style), group.transform, 1);
            }
        }

        Button Arrow(Button template, Transform parent, int by)
        {
            if (template == null)
                return null;
            var copy = Instantiate(template.gameObject, parent, false);
            copy.name = "KingmakerCosmetics_" + (by < 0 ? "Back" : "Next");
            var layout = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
            layout.ignoreLayout = true;
            var rect = (RectTransform)copy.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var button = copy.GetComponent<Button>();
            button.onClick = new Button.ButtonClickedEvent();
            // The copy's own handlers took the clicks and the button got none: the click is taken here, the button
            // only shows hover and press.
            string handlers = string.Join(", ", copy.GetComponentsInChildren<MonoBehaviour>(true)
                .Where(m => m is IEventSystemHandler && !(m is Button)).Select(m => $"{m.name}:{m.GetType().Name}").ToArray());
            foreach (var handler in copy.GetComponentsInChildren<MonoBehaviour>(true).Where(m => m is IEventSystemHandler && m != button).ToList())
                DestroyImmediate(handler);
            Main.Log.Log($"hair colour arrow {copy.name}: button enabled {button.enabled}, handlers taken off: {(handlers.Length > 0 ? handlers : "none")}");
            button.enabled = true;
            // Out of the row the arrows lay under other parts of the panel: each is drawn and hit on a canvas of its
            // own above them.
            copy.AddComponent<ArrowOnTop>().Click = () =>
            {
                if (button.interactable)
                    Move(by);
            };
            copy.SetActive(false);
            return button;
        }

        class ArrowOnTop : MonoBehaviour, IPointerEnterHandler, IPointerClickHandler
        {
            internal Action Click;
            bool m_Logged;

            public void OnPointerClick(PointerEventData data)
            {
                if (data.button == PointerEventData.InputButton.Left && Click != null)
                    Click();
            }

            void OnEnable()
            {
                if (GetComponent<Canvas>() != null)
                    return;
                var parent = transform.parent == null ? null : transform.parent.GetComponentInParent<Canvas>();
                var canvas = gameObject.AddComponent<Canvas>();
                canvas.overrideSorting = true;
                canvas.sortingLayerID = parent == null ? 0 : parent.sortingLayerID;
                canvas.sortingOrder = (parent == null ? 0 : parent.sortingOrder) + 10;
                gameObject.AddComponent<GraphicRaycaster>();
            }

            public void OnPointerEnter(PointerEventData data)
            {
                if (m_Logged)
                    return;
                m_Logged = true;
                Main.Log.Log($"hair colour arrow {name}: pointer over it, interactable {GetComponent<Button>()?.interactable}");
            }
        }

        internal static HairColorWindow Of(CharBColorSelector selector)
        {
            return selector == null ? null : selector.GetComponent<HairColorWindow>();
        }

        // The colour of the doll, chosen in the selector without a sound or an event, in the middle of the row.
        internal void Open()
        {
            var toggles = (List<Toggle>)s_Toggles.GetValue(m_Selector);
            int count = Count(toggles);
            var doll = Game.Instance.UI.CharacterBuildController?.LevelUpController?.Doll;
            if (doll != null && doll.HairRampIndex >= 0 && doll.HairRampIndex < count && m_Selector.CurrentElementIndex != doll.HairRampIndex)
            {
                Main.Log.Log($"hair colour window: colour {m_Selector.CurrentElementIndex} -> {doll.HairRampIndex} of the doll");
                m_Selector.CurrentElementIndex = doll.HairRampIndex;
            }
            int current = m_Selector.CurrentElementIndex;
            if (current >= 0 && current < count && !toggles[current].isOn)
            {
                s_Ignore.SetValue(m_Selector, true);
                try
                {
                    toggles[current].isOn = true;
                }
                finally
                {
                    s_Ignore.SetValue(m_Selector, false);
                }
            }
            m_Start = current - Shown / 2;
            Refresh();
        }

        // The next or the previous colour chosen as by a click on it.
        void Move(int by)
        {
            var toggles = (List<Toggle>)s_Toggles.GetValue(m_Selector);
            int count = Count(toggles), before = m_Selector.CurrentElementIndex, next = before + by;
            if (next < 0 || next >= count)
            {
                Main.Log.Log($"hair colour arrow {by:+0;-0}: colour {before} of {count}, nothing next");
                return;
            }
            if (next < m_Start)
                m_Start = next;
            if (next >= m_Start + Shown)
                m_Start = next - Shown + 1;
            // The swatch is shown before it is chosen: the selector takes only shown ones.
            Refresh();
            toggles[next].isOn = true;
            var doll = Game.Instance.UI.CharacterBuildController?.LevelUpController?.Doll;
            Main.Log.Log($"hair colour arrow {by:+0;-0}: colour {before} -> {next}, now {m_Selector.CurrentElementIndex}, swatch on {toggles[next].isOn}, "
                + $"active {toggles[next].gameObject.activeInHierarchy}, doll colour {(doll == null ? "none" : doll.HairRampIndex.ToString())}");
        }

        int Count(List<Toggle> toggles)
        {
            var ramps = (List<Texture2D>)s_Ramps.GetValue(m_Selector);
            return ramps == null || toggles == null ? 0 : Math.Min(ramps.Count, toggles.Count);
        }

        internal void Refresh()
        {
            var toggles = (List<Toggle>)s_Toggles.GetValue(m_Selector);
            int count = Count(toggles);
            bool window = count > Shown;
            m_Start = Mathf.Clamp(m_Start, 0, Math.Max(0, count - Shown));
            for (int i = 0; i < count; i++)
            {
                bool shown = !window || i >= m_Start && i < m_Start + Shown;
                if (toggles[i].gameObject.activeSelf != shown)
                    toggles[i].gameObject.SetActive(shown);
                // A colour chosen before and scrolled away stays chosen with no group to turn it off.
                if (toggles[i].isOn && i != m_Selector.CurrentElementIndex)
                    toggles[i].isOn = false;
            }
            if (m_BackArrow == null || m_NextArrow == null)
                return;
            m_BackArrow.gameObject.SetActive(window);
            m_NextArrow.gameObject.SetActive(window);
            if (!window)
                return;
            m_BackArrow.interactable = m_Selector.CurrentElementIndex > 0;
            m_NextArrow.interactable = m_Selector.CurrentElementIndex < count - 1;
            var group = (RectTransform)m_BackArrow.transform.parent;
            LayoutRebuilder.ForceRebuildLayoutImmediate(group);
            Place(m_BackArrow, (RectTransform)toggles[m_Start].transform, group, -1);
            Place(m_NextArrow, (RectTransform)toggles[m_Start + Shown - 1].transform, group, 1);
            if (!m_Placed)
            {
                m_Placed = true;
                var size = ((RectTransform)toggles[m_Start].transform).rect.size;
                Main.Log.Log($"hair colours: {count} in a window of {Shown}, swatch {size.x}x{size.y}, group {group.GetComponent<LayoutGroup>()?.GetType().Name ?? "no layout"}");
            }
        }

        // The arrow beside the swatch, as high as it, a quarter of it away.
        static void Place(Button arrow, RectTransform swatch, RectTransform group, int side)
        {
            var rect = (RectTransform)arrow.transform;
            var corners = new Vector3[4];
            swatch.GetWorldCorners(corners);
            Vector3 low = group.InverseTransformPoint(corners[0]), high = group.InverseTransformPoint(corners[2]);
            float height = high.y - low.y;
            var size = rect.rect.size;
            float scale = size.y > 0 ? height / size.y : 1;
            rect.localScale = new Vector3(scale, scale, 1);
            float x = side < 0 ? low.x - height / 4 - size.x * scale / 2 : high.x + height / 4 + size.x * scale / 2;
            rect.localPosition = new Vector3(x, (low.y + high.y) / 2, 0);
        }
    }

    [HarmonyPatch(typeof(CharBDollStyler), nameof(CharBDollStyler.FillData))]
    static class HairColorWindowAttach
    {
        static readonly FieldInfo s_HairColor = AccessTools.Field(typeof(CharBDollStyler), "m_HairColor");
        static readonly FieldInfo s_HairStyle = AccessTools.Field(typeof(CharBDollStyler), "m_HairStyle");

        static void Prefix(CharBDollStyler __instance)
        {
            try
            {
                HairColorWindow.Attach((CharBColorSelector)s_HairColor.GetValue(__instance), (CharBSequentialSelector)s_HairStyle.GetValue(__instance));
            }
            catch (Exception ex)
            {
                Main.Log.Error("hair colour window: " + ex);
            }
        }
    }

    [HarmonyPatch]
    static class HairColorWindowOpen
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CharBColorSelector), nameof(CharBColorSelector.SetData));
            yield return AccessTools.Method(typeof(CharBColorSelector), nameof(CharBColorSelector.TryApplyCurrentElement));
        }

        static void Postfix(CharBColorSelector __instance)
        {
            try
            {
                var window = HairColorWindow.Of(__instance);
                if (window != null)
                    window.Open();
            }
            catch (Exception ex)
            {
                Main.Log.Error("hair colour window: " + ex);
            }
        }
    }

    [HarmonyPatch(typeof(CharBColorSelector), nameof(CharBColorSelector.HandleDecorationChanged))]
    static class HairColorWindowChosen
    {
        static void Postfix(CharBColorSelector __instance)
        {
            try
            {
                var window = HairColorWindow.Of(__instance);
                if (window != null)
                    window.Refresh();
            }
            catch (Exception ex)
            {
                Main.Log.Error("hair colour window: " + ex);
            }
        }
    }
}
