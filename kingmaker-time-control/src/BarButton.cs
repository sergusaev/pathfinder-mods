using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.GameModes;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TimeControl
{
    // Keyboard and mouse mode: a button with an hourglass in the look of the game's menu buttons, in the frame of the game's
    // button sheet cut to this one button. It stands right of the quick cast bar of Buff It 2 The Limit, level with it;
    // without that bar (or that mod), alone at the left edge of the screen on top of the game's menu buttons. A click opens
    // the deadlines window.
    class BarButton : MonoBehaviour
    {
        const float ButtonSize = 48f;
        // TBM_backButton as the buff bar uses it: its top 4.5 units are empty, so the frame goes that much down onto the
        // game's sheet; the inner edge of its right side is 17.5 units in; its left end needs only its small ornament.
        const float FrameSink = 4.5f;
        const int FrameLeft = 2;
        const int FrameRight = 18;
        const int FrameTop = 18;
        const float FrameLeftBorder = 64f;
        static readonly Vector2 FallbackPos = new Vector2(0f, 132f);

        static BarButton s_Instance;
        static Sprite s_Frame;
        static bool s_FrameFromHud;
        float m_NextStyle;
        RectTransform m_Row;
        RectTransform m_Host;
        GameObject m_Tip;
        bool m_TipPaper;
        TextMeshProUGUI m_TipTitle;
        TextMeshProUGUI m_TipText;
        CanvasGroup m_Group;
        float m_NextSearch;
        RectTransform m_BuffRow;
        float m_NextBuffSearch;
        float m_NextAnchorSearch;
        float m_Size = ButtonSize;
        RectTransform m_Anchor;
        bool m_Styled;

        internal static void Ensure()
        {
            if (s_Instance != null || !Main.Settings.BarButton) return;
            var go = new GameObject("TimeControl_Bar", typeof(RectTransform));
            DontDestroyOnLoad(go);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 31000;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            go.AddComponent<GraphicRaycaster>();
            s_Instance = go.AddComponent<BarButton>();
            s_Instance.Build();
        }

        void Build()
        {
            var rowGo = new GameObject("Row", typeof(RectTransform));
            rowGo.transform.SetParent(transform, false);
            m_Row = (RectTransform)rowGo.transform;
            m_Row.anchorMin = m_Row.anchorMax = m_Row.pivot = Vector2.zero;
            MakeFrame(m_Row);
            var layout = rowGo.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(FrameLeft, FrameRight, FrameTop, 0);
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = rowGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            m_Group = rowGo.AddComponent<CanvasGroup>();
            MakeButton();
            MakeTip(transform);
        }

        // The tooltip on the game's parchment; made again once the parchment is loaded, if it was made before.
        void MakeTip(Transform parent)
        {
            m_TipPaper = Theme.Paper;
            m_Tip = new GameObject("Tip", typeof(RectTransform));
            m_Tip.transform.SetParent(parent, false);
            var tipRect = (RectTransform)m_Tip.transform;
            tipRect.anchorMin = tipRect.anchorMax = tipRect.pivot = Vector2.zero;
            var bg = m_Tip.AddComponent<Image>();
            bg.raycastTarget = false;
            Theme.Sheet(bg);
            Ui.Configure(m_Tip.AddComponent<VerticalLayoutGroup>(), Theme.Paper ? new RectOffset(30, 30, 24, 22) : new RectOffset(14, 14, 10, 10), 6);
            var tipFit = m_Tip.AddComponent<ContentSizeFitter>();
            tipFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            tipFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            // As the game's tooltips and the buff bar's: the title centred, the rule, then the deadlines.
            m_TipTitle = Ui.Text(m_Tip.transform, "", 22, Theme.Title, FontStyles.Bold);
            m_TipTitle.alignment = TextAlignmentOptions.Center;
            m_TipTitle.enableWordWrapping = false;
            Theme.Separator(m_Tip.transform);
            m_TipText = Ui.Text(m_Tip.transform, "", 18, Theme.Text);
            m_TipText.enableWordWrapping = false;
            m_Tip.SetActive(false);
        }

        // The frame of the game's menu buttons cut to one button: the left end with its small ornament, the right end with its
        // ornament and side, as narrow as the button between them allows (canvas units are pixels at 100 per unit).
        static void MakeFrame(RectTransform parent)
        {
            var go = new GameObject("Frame", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.transform.SetAsFirstSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var image = go.AddComponent<Image>();
            image.raycastTarget = false;
            image.sprite = FrameSprite();
            if (image.sprite != null) image.type = Image.Type.Tiled;
            else image.color = new Color(0.06f, 0.05f, 0.04f, 0.75f);
        }

        // The frame sprite, kept for the session: the local map's HUD brings TBM_backButton and takes it away with it, the
        // global map has only its own sheet (globalmap_leftbackmeny, the same frame), which serves until the HUD is seen.
        static Sprite FrameSprite()
        {
            if (s_Frame != null && s_FrameFromHud) return s_Frame;
            var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
            var plate = sprites.FirstOrDefault(sp => sp.name == "TBM_backButton");
            bool hud = plate != null;
            if (!hud && s_Frame != null) return s_Frame;
            if (!hud) plate = sprites.FirstOrDefault(sp => sp.name == "globalmap_leftbackmeny");
            if (plate == null) return null;
            try
            {
                var border = plate.border;
                float width = (FrameLeft + ButtonSize + FrameRight) * plate.pixelsPerUnit / 100f;
                float left = Mathf.Min(FrameLeftBorder, width / 2f);
                float right = Mathf.Min(border.z, width - left);
                s_Frame = Sprite.Create(plate.texture, plate.textureRect, new Vector2(0.5f, 0.5f), plate.pixelsPerUnit, 0,
                    SpriteMeshType.FullRect, new Vector4(left, 0, right, border.w));
                s_FrameFromHud = hud;
                Main.Log("bar button: frame from " + plate.name + ", " + plate.textureRect.size + " border " + border + " ppu " + plate.pixelsPerUnit);
            }
            catch (Exception e)
            {
                Main.Error(e, "bar button frame");
            }
            return s_Frame;
        }

        // The game's sprites come with its screens, not with the mod: until the button and the frame have their look, try
        // again every second, on whichever map; a frame from the global map's sheet gives way to the HUD's when it comes.
        void Restyle()
        {
            if (Time.unscaledTime < m_NextStyle) return;
            m_NextStyle = Time.unscaledTime + (m_Styled ? 5f : 1f);
            var frame = m_Row.Find("Frame");
            var current = frame == null ? null : frame.GetComponent<Image>().sprite;
            if (m_Styled && current != null && s_FrameFromHud && current == s_Frame) return;
            ButtonStyle.Retry();
            bool styled = ButtonStyle.Get(Hourglass.Texture, ButtonStyle.State.Normal) != null;
            var sprite = FrameSprite();
            if (styled == m_Styled && sprite == current) return;
            for (int i = m_Row.childCount - 1; i >= 0; i--) DestroyImmediate(m_Row.GetChild(i).gameObject);
            MakeFrame(m_Row);
            MakeButton();
        }

        void MakeButton()
        {
            var go = new GameObject("TimeControl_Button", typeof(RectTransform));
            go.transform.SetParent(m_Row, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(ButtonSize, ButtonSize);
            var image = go.AddComponent<Image>();
            image.preserveAspect = true;
            var icon = Hourglass.Texture;
            var normal = ButtonStyle.Get(icon, ButtonStyle.State.Normal);
            m_Styled = normal != null;
            image.sprite = normal ?? Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height), new Vector2(0.5f, 0.5f));
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
            events.Click = () => TimersWindow.Toggle();
            events.Enter = () => ShowTip((RectTransform)go.transform);
            events.Exit = () => m_Tip.SetActive(false);
        }

        void ShowTip(RectTransform over)
        {
            if (m_TipPaper != Theme.Paper)
            {
                var parent = m_Tip.transform.parent;
                DestroyImmediate(m_Tip);
                MakeTip(parent);
            }
            string title = Theme.Initial(S.WindowTitle);
            string key = Main.Settings.Key == KeyCode.None ? "" : (Main.Settings.KeyCtrl ? "Ctrl+" : "") + (Main.Settings.KeyAlt ? "Alt+" : "") + (Main.Settings.KeyShift ? "Shift+" : "") + Main.Settings.Key;
            if (key.Length > 0) title += "  <size=18><color=" + Theme.Muted + ">[" + key + "]</color></size>";
            m_TipTitle.text = title;
            var lines = Tracker.Current.Where(x => Tracker.IsTracked(x.Key) && x.Remaining > TimeSpan.Zero).Take(5).Select(t => t.Line).ToArray();
            m_TipText.text = string.Join("\n", lines);
            // Without deadlines to show, only the title.
            m_TipText.gameObject.SetActive(lines.Length > 0);
            foreach (Transform child in m_Tip.transform)
                if (child.name == "Separator" || child.name == "Rule") child.gameObject.SetActive(lines.Length > 0);
            m_Tip.SetActive(true);
            var tipRect = (RectTransform)m_Tip.transform;
            tipRect.anchoredPosition = new Vector2(m_Row.anchoredPosition.x + over.anchoredPosition.x - ButtonSize / 2, m_Row.anchoredPosition.y + m_Row.rect.height + 8f);
        }

        void Update()
        {
            try
            {
                if (m_Row == null)
                {
                    m_Host = null;
                    Build();
                }
                Attach();
                Restyle();
                var game = Game.Instance;
                bool show = Main.Settings.BarButton && Main.InGame && !game.IsControllerGamepad && !game.CutsceneLock
                    && (game.CurrentMode == GameModeType.Default || game.CurrentMode == GameModeType.Pause || game.CurrentMode == GameModeType.GlobalMap
                        || game.CurrentMode == GameModeType.EscMode);
                if (m_Row.gameObject.activeSelf != show)
                {
                    m_Row.gameObject.SetActive(show);
                    if (!show) m_Tip.SetActive(false);
                }
                // Under the Esc menu the button is seen, as the game's, but not pressed.
                bool live = game.CurrentMode != GameModeType.EscMode;
                if (m_Group.blocksRaycasts != live)
                {
                    m_Group.blocksRaycasts = m_Group.interactable = live;
                    if (!live) m_Tip.SetActive(false);
                }
                if (show)
                {
                    m_Row.anchoredPosition = Position() + new Vector2(Main.Settings.BarOffsetX, Main.Settings.BarOffsetY);
                    // As large as the game's buttons next to it: 48 on the local map, 56 on the global map.
                    var button = m_Row.Find("TimeControl_Button") as RectTransform;
                    if (button != null && button.sizeDelta.x != m_Size) button.sizeDelta = new Vector2(m_Size, m_Size);
                }
            }
            catch (Exception e)
            {
                Main.Error(e, "bar button");
                enabled = false;
            }
        }

        // Into the game's HUD canvas just before its tooltips, as the buff bar; until the HUD exists it stays on its own canvas.
        void Attach()
        {
            if (m_Host != null || Time.unscaledTime < m_NextSearch) return;
            m_NextSearch = Time.unscaledTime + 1f;
            var staticCanvas = GameObject.Find("StaticCanvas");
            var tooltips = staticCanvas == null ? null : staticCanvas.transform.Find("Tooltips");
            if (tooltips == null) return;
            m_Host = (RectTransform)new GameObject("TimeControl_BarHost", typeof(RectTransform)).transform;
            m_Host.SetParent(tooltips.parent, false);
            m_Host.anchorMin = Vector2.zero;
            m_Host.anchorMax = Vector2.one;
            m_Host.offsetMin = m_Host.offsetMax = Vector2.zero;
            m_Host.SetSiblingIndex(tooltips.GetSiblingIndex());
            m_Row.SetParent(m_Host, false);
            m_Tip.transform.SetParent(m_Host, false);
        }

        // The row of Buff It 2 The Limit's quick cast bar when the bar is shown.
        RectTransform BuffRow()
        {
            if (m_BuffRow == null && Time.unscaledTime >= m_NextBuffSearch)
            {
                m_NextBuffSearch = Time.unscaledTime + 1f;
                var host = GameObject.Find("BI2TL_PadBarHost");
                m_BuffRow = host == null ? null : host.transform.Find("Row") as RectTransform;
            }
            return m_BuffRow != null && m_BuffRow.gameObject.activeSelf && m_BuffRow.parent.gameObject.activeInHierarchy ? m_BuffRow : null;
        }

        // Right of the buff bar, frame to frame and level with it; without the bar, at the left edge of the screen on top of
        // the game's menu buttons, as the bar would be.
        Vector2 Position()
        {
            var root = (RectTransform)m_Row.parent;
            Vector2 local;
            m_Size = ButtonSize;
            var buffRow = BuffRow();
            if (buffRow != null)
            {
                var corners = new Vector3[4];
                buffRow.GetWorldCorners(corners);
                var screen = RectTransformUtility.WorldToScreenPoint(Cam(buffRow), corners[3]);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, Cam(root), out local)) return local - root.rect.min;
            }
            // The game's menu buttons: on the local map the HUD's block of buttons, on the global map the sheet under its row of
            // buttons, whose top edge is its frame.
            if ((m_Anchor == null || !m_Anchor.gameObject.activeInHierarchy) && Time.unscaledTime >= m_NextAnchorSearch)
            {
                m_NextAnchorSearch = Time.unscaledTime + 0.5f;
                var anchor = GameObject.Find("Menu_Buttons48px") ?? GameObject.Find("GlobalMapCanvas/GameMenu/Background");
                m_Anchor = anchor == null ? null : anchor.transform as RectTransform;
            }
            if (m_Anchor == null || !m_Anchor.gameObject.activeInHierarchy) return FallbackPos;
            float top = float.MinValue;
            var c = new Vector3[4];
            var cam = Cam(m_Anchor);
            // The top of the buttons themselves, the block's own rectangle may reach higher; the sheet is measured as it is.
            bool sheet = m_Anchor.name == "Background";
            var row = sheet ? m_Anchor.parent.Find("SystemButtons") : null;
            m_Size = row != null && row.childCount > 0 ? ((RectTransform)row.GetChild(0)).rect.height : ButtonSize;
            foreach (var rect in m_Anchor.GetComponentsInChildren<RectTransform>(false))
            {
                if (sheet ? rect != m_Anchor : rect.parent != m_Anchor) continue;
                rect.GetWorldCorners(c);
                foreach (var corner in c)
                {
                    var screen = RectTransformUtility.WorldToScreenPoint(cam, corner);
                    top = Mathf.Max(top, screen.y);
                }
            }
            if (top == float.MinValue || !RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(0f, top), Cam(root), out local)) return FallbackPos;
            local -= root.rect.min;
            return new Vector2(0f, local.y - FrameSink);
        }

        static Camera Cam(Component c)
        {
            var canvas = c.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }
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

    // The pictogram: the silhouette of the menu card, white on transparent, scaled down by averaging so that its edges stay
    // smooth, centred in the button at the height of the game's pictograms.
    static class Hourglass
    {
        const int Size = 106;
        const int Height = 72;
        static Texture2D s_Texture;

        internal static Texture2D Texture
        {
            get
            {
                if (s_Texture == null) s_Texture = Draw();
                return s_Texture;
            }
        }

        static Texture2D Draw()
        {
            var source = new Texture2D(2, 2, TextureFormat.ARGB32, false);
            using (var stream = typeof(Hourglass).Assembly.GetManifestResourceStream("TimeControl.card_time.png"))
            {
                var memory = new System.IO.MemoryStream();
                stream.CopyTo(memory);
                source.LoadImage(memory.ToArray());
            }
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
            UnityEngine.Object.Destroy(source);
            var texture = new Texture2D(Size, Size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            texture.SetPixels32(pixels);
            texture.Apply();
            return texture;
        }
    }

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
        static Color32[] s_Normal;
        static Color32[] s_Hover;
        static readonly Dictionary<State, Sprite> s_Cache = new Dictionary<State, Sprite>();

        internal enum State { Normal, Hover, Down }

        internal static Sprite Get(Texture2D icon, State state)
        {
            Sprite sprite;
            if (s_Cache.TryGetValue(state, out sprite)) return sprite;
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
            if (sprite != null) s_Cache[state] = sprite;
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
            Main.Log("bar button: Kingmaker style " + (s_Normal != null ? "built" : "unavailable"));
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
