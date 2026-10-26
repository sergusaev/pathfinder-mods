using System;
using System.Collections.Generic;
using System.IO;
using Kingmaker;
using Kingmaker.GameModes;
using ModBar;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
#if WOTR
using Kingmaker.UI.MVVM._VM.Tooltip.Templates;
using Kingmaker.UI.MVVM._VM.Tooltip.Utils;
#endif

namespace LearnScrolls
{
    // Keyboard and mouse mode: two buttons in the block of the mods' buttons over the game's menu buttons (ModBar: after
    // the buffs and the time, under one frame). The scroll learns everything for everyone (the list and the game's dialog
    // first); the scroll with a gear opens the window. In Kingmaker the buttons are drawn in the look of the game's menu
    // buttons with the tooltip on its parchment; in WotR in the look of the buff bar, with the game's own tooltip.
    static class Bar
    {
        static BarSegment s_Segment;

        internal static void Ensure()
        {
            if (s_Segment != null || !Main.Settings.BarButtons) return;
            var go = new GameObject("LearnScrolls_Bar");
            UnityEngine.Object.DontDestroyOnLoad(go);
            var view = go.AddComponent<BarView>();
            s_Segment = BarSegment.Add(Main.Id, BarSegment.ScrollsOrder, Shown, view.Fill);
        }

        // The mod's own condition; the block adds the game's (its menu buttons in view, mouse mode).
        static bool Shown()
        {
            var game = Game.Instance;
            if (!Main.Settings.BarButtons || !Main.InGame) return false;
#if KINGMAKER
            if (game.CutsceneLock) return false;
#else
            if (game.CutsceneLock.Active) return false;
#endif
            var mode = game.CurrentMode;
            return mode == GameModeType.Default || mode == GameModeType.Pause || mode == GameModeType.GlobalMap
                || mode == GameModeType.EscMode;
        }

        // A picture embedded in the DLL.
        internal static Texture2D Read(string name, bool mipmaps)
        {
            using (var stream = typeof(Bar).Assembly.GetManifestResourceStream("LearnScrolls." + name))
            {
                if (stream == null) return null;
                var memory = new MemoryStream();
                stream.CopyTo(memory);
                var texture = new Texture2D(2, 2, TextureFormat.ARGB32, mipmaps);
                texture.LoadImage(memory.ToArray());
                texture.name = name;
                return texture;
            }
        }

        internal static Sprite SpriteOf(Texture2D texture)
        {
            return texture == null ? null : Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
        }
    }

    class BarView : MonoBehaviour
    {
        class Spec
        {
            internal string Icon;
            internal Action Click;
            internal Func<string> Tip;
        }

        static readonly Spec[] Specs =
        {
            new Spec { Icon = "learn", Click = Window.LearnAll, Tip = () => Strings.LearnTip },
            new Spec { Icon = "window", Click = Window.Toggle, Tip = () => Strings.WindowTip },
        };

        RectTransform m_Segment;

        internal void Fill(RectTransform segment)
        {
            m_Segment = segment;
            for (int i = segment.childCount - 1; i >= 0; i--) DestroyImmediate(segment.GetChild(i).gameObject);
#if KINGMAKER
            m_Styled = true;
#endif
            foreach (var spec in Specs)
            {
                try { MakeButton(segment, spec); }
                catch (Exception e) { Main.Error(e, "bar button " + spec.Icon); }
            }
        }

#if KINGMAKER
        // The pictograms: the silhouettes scaled down by averaging so that their edges stay smooth, centred in the button
        // at the height of the game's pictograms (as the hourglass of Time Control).
        static readonly Dictionary<string, Texture2D> s_Pictograms = new Dictionary<string, Texture2D>();
        bool m_Styled;
        float m_NextStyle;
        GameObject m_Tip;
        bool m_TipPaper;
        TextMeshProUGUI m_TipTitle;
        TextMeshProUGUI m_TipText;

        static Texture2D Pictogram(string name)
        {
            Texture2D texture;
            if (s_Pictograms.TryGetValue(name, out texture)) return texture;
            const int Size = 106, Height = 72;
            var source = Bar.Read("km_" + name + ".png", false);
            var from = source.GetPixels32();
            int sw = source.width, sh = source.height;
            float scale = (float)sh / Height;
            int width = Mathf.RoundToInt(sw / scale);
            int left = (Size - width) / 2, bottom = (Size - Height) / 2;
            var pixels = new Color32[Size * Size];
            for (int y = 0; y < Height; y++)
            {
                int y0 = Mathf.FloorToInt(y * scale), y1 = Mathf.Min(sh, Mathf.CeilToInt((y + 1) * scale));
                for (int x = 0; x < width; x++)
                {
                    int x0 = Mathf.FloorToInt(x * scale), x1 = Mathf.Min(sw, Mathf.CeilToInt((x + 1) * scale));
                    int sum = 0, count = 0;
                    for (int sy = y0; sy < y1; sy++)
                        for (int sx = x0; sx < x1; sx++)
                        {
                            sum += from[sy * sw + sx].a;
                            count++;
                        }
                    pixels[(bottom + y) * Size + left + x] = new Color32(255, 255, 255, (byte)(count == 0 ? 0 : sum / count));
                }
            }
            Destroy(source);
            texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = name };
            texture.SetPixels32(pixels);
            texture.Apply();
            s_Pictograms[name] = texture;
            return texture;
        }

        void MakeButton(RectTransform segment, Spec spec)
        {
            var rect = BarSegment.Button(segment, "LearnScrolls_" + spec.Icon);
            var go = rect.gameObject;
            var image = go.AddComponent<Image>();
            image.preserveAspect = true;
            var icon = Pictogram(spec.Icon);
            var normal = ButtonStyle.Get(icon, ButtonStyle.State.Normal);
            if (normal == null) m_Styled = false;
            image.sprite = normal ?? Bar.SpriteOf(icon);
            var button = go.AddComponent<Button>();
            // Not a keyboard target: a clicked button would stay selected, lit, and move with the arrow keys.
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            if (normal != null)
            {
                button.transition = Selectable.Transition.SpriteSwap;
                button.spriteState = new SpriteState
                {
                    highlightedSprite = ButtonStyle.Get(icon, ButtonStyle.State.Hover),
                    pressedSprite = ButtonStyle.Get(icon, ButtonStyle.State.Down),
                    disabledSprite = normal
                };
            }
            var events = go.AddComponent<BarEvents>();
            events.Click = spec.Click;
            events.Enter = () => ShowTip(rect, spec.Tip());
            events.Exit = HideTip;
        }

        // The tooltip on the game's parchment, over the block; made again once the parchment is loaded.
        void MakeTip(Transform parent)
        {
            m_TipPaper = Theme.Paper;
            m_Tip = new GameObject("LearnScrolls_Tip", typeof(RectTransform));
            m_Tip.transform.SetParent(parent, false);
            var tipRect = (RectTransform)m_Tip.transform;
            tipRect.anchorMin = tipRect.anchorMax = tipRect.pivot = Vector2.zero;
            var bg = m_Tip.AddComponent<Image>();
            bg.raycastTarget = false;
            Theme.Tip(bg);
            var layout = m_Tip.AddComponent<VerticalLayoutGroup>();
            layout.padding = Theme.Paper ? Theme.TipPadding : new RectOffset(14, 14, 10, 10);
            layout.spacing = 6;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fit = m_Tip.AddComponent<ContentSizeFitter>();
            fit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // As the game's tooltips and the buff bar's: the title centred, the rule, then the text.
            m_TipTitle = TipText("Title", 22, FontStyles.Bold, Theme.Title);
            m_TipTitle.alignment = TextAlignmentOptions.Center;
            Theme.Separator(m_Tip.transform);
            m_TipText = TipText("Text", 18, FontStyles.Normal, Theme.Text);
            var wrap = m_TipText.gameObject.AddComponent<LayoutElement>();
            wrap.preferredWidth = 420;
            m_TipText.enableWordWrapping = true;
            m_Tip.SetActive(false);
        }

        TextMeshProUGUI TipText(string name, float size, FontStyles style, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(m_Tip.transform, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (Theme.Font != null) text.font = Theme.Font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.richText = true;
            text.raycastTarget = false;
            text.enableWordWrapping = false;
            return text;
        }

        void ShowTip(RectTransform over, string text)
        {
            var parent = BarSegment.TipParent as RectTransform;
            if (parent == null || !BarSegment.Live) return;
            if (m_Tip == null || m_TipPaper != Theme.Paper || m_Tip.transform.parent != parent)
            {
                if (m_Tip != null) DestroyImmediate(m_Tip);
                MakeTip(parent);
            }
            m_Tip.transform.SetAsLastSibling();
            m_TipTitle.text = Theme.Initial(Strings.Title);
            m_TipText.text = text;
            m_Tip.SetActive(true);
            ((RectTransform)m_Tip.transform).anchoredPosition = BarSegment.Above(over, parent) + new Vector2(0f, 8f);
        }

        void HideTip()
        {
            if (m_Tip != null) m_Tip.SetActive(false);
        }

        void Update()
        {
            try
            {
                bool seen = m_Segment != null && m_Segment.gameObject.activeInHierarchy;
                // The game's sprites come with its screens, not with the mod: until the buttons have their look, try
                // again every second.
                if (seen && !m_Styled && Time.unscaledTime >= m_NextStyle)
                {
                    m_NextStyle = Time.unscaledTime + 1f;
                    ButtonStyle.Retry();
                    if (ButtonStyle.Get(Pictogram("learn"), ButtonStyle.State.Normal) != null) Fill(m_Segment);
                }
                // The block faded or went away under the pointer: the tooltip goes with it.
                if (m_Tip != null && m_Tip.activeSelf && (!seen || !BarSegment.Live)) HideTip();
            }
            catch (Exception e)
            {
                Main.Error(e, "bar buttons");
                enabled = false;
            }
        }
#else
        static readonly Dictionary<string, Sprite> s_Sprites = new Dictionary<string, Sprite>();
        static Sprite Load(string icon, string state)
        {
            Sprite sprite;
            string name = "wotr_" + icon + "_" + state;
            if (!s_Sprites.TryGetValue(name, out sprite))
            {
                sprite = Bar.SpriteOf(Bar.Read(name + ".png", false));
                s_Sprites[name] = sprite;
            }
            return sprite;
        }

        // The look of the buff bar's buttons: a dark square in the game's frame, the pictogram in pale grey; the hover and
        // the press in its purple.
        void MakeButton(RectTransform segment, Spec spec)
        {
            var rect = BarSegment.Button(segment, "LearnScrolls_" + spec.Icon);
            var go = rect.gameObject;
            var image = go.AddComponent<Image>();
            image.preserveAspect = true;
            image.sprite = Load(spec.Icon, "normal");
            var button = go.AddComponent<Button>();
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = Load(spec.Icon, "hover"),
                pressedSprite = Load(spec.Icon, "down"),
                disabledSprite = image.sprite
            };
            var events = go.AddComponent<BarEvents>();
            events.Click = spec.Click;
            button.SetTooltip(new TooltipTemplateSimple(Strings.Title, spec.Tip()));
        }
#endif
    }

    class BarEvents : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Action Click;
        public Action Enter;
        public Action Exit;

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button == PointerEventData.InputButton.Left && Click != null) Click();
            Deselect();
        }

        void Deselect()
        {
            var events = EventSystem.current;
            if (events != null && events.currentSelectedGameObject == gameObject) events.SetSelectedGameObject(null);
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (Enter != null) Enter();
        }

        public void OnPointerExit(PointerEventData e)
        {
            Deselect();
            if (Exit != null) Exit();
        }
    }
}
