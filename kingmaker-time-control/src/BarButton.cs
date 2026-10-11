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
    // Keyboard and mouse mode: a button with an hourglass in the look of the game's menu buttons, on the same frame as the
    // quick cast bar of Buff It 2 The Limit and right next to it; without that bar, at the bar's place on top of the game's
    // menu buttons. A click opens the deadlines window.
    class BarButton : MonoBehaviour
    {
        const float ButtonSize = 48f;
        const float FrameSink = 4.5f;
        const int FrameRight = 18;
        const float FrameLeftBorder = 64f;
        static readonly Vector2 FallbackPos = new Vector2(12f, 132f);

        static BarButton s_Instance;
        RectTransform m_Row;
        RectTransform m_Host;
        GameObject m_Tip;
        TextMeshProUGUI m_TipText;
        float m_NextSearch;
        RectTransform m_BuffRow;
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
            layout.padding = new RectOffset(2, FrameRight, 18, 0);
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var fitter = rowGo.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            MakeButton();

            m_Tip = new GameObject("Tip", typeof(RectTransform));
            m_Tip.transform.SetParent(transform, false);
            var tipRect = (RectTransform)m_Tip.transform;
            tipRect.anchorMin = tipRect.anchorMax = tipRect.pivot = Vector2.zero;
            var bg = m_Tip.AddComponent<Image>();
            bg.raycastTarget = false;
            Theme.Sheet(bg);
            Ui.Configure(m_Tip.AddComponent<VerticalLayoutGroup>(), Theme.Paper ? new RectOffset(30, 30, 24, 22) : new RectOffset(14, 14, 10, 10), 6);
            var tipFit = m_Tip.AddComponent<ContentSizeFitter>();
            tipFit.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            tipFit.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            m_TipText = Ui.Text(m_Tip.transform, "", 18, Theme.Text);
            m_TipText.enableWordWrapping = false;
            m_Tip.SetActive(false);
        }

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
            var plate = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(sp => sp.name == "TBM_backButton");
            if (plate != null)
            {
                try
                {
                    var border = plate.border;
                    image.sprite = Sprite.Create(plate.texture, plate.textureRect, new Vector2(0.5f, 0.5f), plate.pixelsPerUnit, 0,
                        SpriteMeshType.FullRect, new Vector4(FrameLeftBorder, 0, border.z, border.w));
                    image.type = Image.Type.Tiled;
                    var element = parent.GetComponent<LayoutElement>() ?? parent.gameObject.AddComponent<LayoutElement>();
                    element.minWidth = (FrameLeftBorder + border.z) * 100f / plate.pixelsPerUnit;
                }
                catch (Exception)
                {
                    image.sprite = null;
                }
            }
            if (image.sprite == null) image.color = new Color(0.06f, 0.05f, 0.04f, 0.75f);
        }

        void MakeButton()
        {
            var go = new GameObject("Button", typeof(RectTransform));
            go.transform.SetParent(m_Row, false);
            ((RectTransform)go.transform).sizeDelta = new Vector2(ButtonSize, ButtonSize);
            var image = go.AddComponent<Image>();
            image.preserveAspect = true;
            var icon = Hourglass.Texture;
            var normal = ButtonStyle.Get(icon, ButtonStyle.State.Normal);
            m_Styled = normal != null;
            image.sprite = normal ?? Sprite.Create(icon, new Rect(0, 0, icon.width, icon.height), new Vector2(0.5f, 0.5f));
            var button = go.AddComponent<Button>();
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
            var lines = new List<string> { "<b>" + Theme.Initial(S.WindowTitle) + "</b>" };
            string key = Main.Settings.Key == KeyCode.None ? "" : (Main.Settings.KeyCtrl ? "Ctrl+" : "") + (Main.Settings.KeyAlt ? "Alt+" : "") + (Main.Settings.KeyShift ? "Shift+" : "") + Main.Settings.Key;
            if (key.Length > 0) lines[0] += "  <color=" + Theme.Muted + ">[" + key + "]</color>";
            foreach (var t in Tracker.Current.Where(x => Tracker.IsTracked(x.Key) && x.Remaining > TimeSpan.Zero).Take(5))
                lines.Add(t.Line);
            m_TipText.text = string.Join("\n", lines.ToArray());
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
                var game = Game.Instance;
                bool show = Main.Settings.BarButton && Main.InGame && !game.IsControllerGamepad && !game.CutsceneLock && !Modal.AnyOpen
                    && (game.CurrentMode == GameModeType.Default || game.CurrentMode == GameModeType.Pause || game.CurrentMode == GameModeType.GlobalMap);
                if (m_Row.gameObject.activeSelf != show)
                {
                    m_Row.gameObject.SetActive(show);
                    if (!show) m_Tip.SetActive(false);
                }
                if (show) m_Row.anchoredPosition = Position() + new Vector2(Main.Settings.BarOffsetX, Main.Settings.BarOffsetY);
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
            // The game's sprites came with the HUD: the frame and the button get their look now.
            var frame = m_Row.Find("Frame");
            if (!m_Styled || frame == null || frame.GetComponent<Image>().sprite == null)
            {
                ButtonStyle.Retry();
                for (int i = m_Row.childCount - 1; i >= 0; i--) DestroyImmediate(m_Row.GetChild(i).gameObject);
                MakeFrame(m_Row);
                MakeButton();
            }
        }

        // Right after the buff bar when it is shown, otherwise where it would be: on top of the game's menu buttons.
        Vector2 Position()
        {
            if (Time.unscaledTime >= m_NextSearch - 0.5f)
            {
                if (m_BuffRow == null)
                {
                    var host = GameObject.Find("BI2TL_PadBarHost");
                    m_BuffRow = host == null ? null : host.transform.Find("Row") as RectTransform;
                }
                if (m_Anchor == null)
                {
                    var anchor = GameObject.Find("Menu_Buttons48px");
                    m_Anchor = anchor == null ? null : anchor.transform as RectTransform;
                }
            }
            var root = (RectTransform)m_Row.parent;
            Vector2 local;
            if (m_BuffRow != null && m_BuffRow.gameObject.activeInHierarchy)
            {
                var corners = new Vector3[4];
                m_BuffRow.GetWorldCorners(corners);
                // Bottom right corner of the buff bar.
                if (ToLocal(root, m_BuffRow, corners[3], out local)) return local - root.rect.min;
            }
            if (m_Anchor == null || !m_Anchor.gameObject.activeInHierarchy) return FallbackPos;
            float left = float.MaxValue, top = float.MinValue;
            var c = new Vector3[4];
            var cam = Cam(m_Anchor);
            foreach (var rect in m_Anchor.GetComponentsInChildren<RectTransform>(false))
            {
                if (rect != m_Anchor && rect.parent != m_Anchor) continue;
                rect.GetWorldCorners(c);
                foreach (var corner in c)
                {
                    var screen = RectTransformUtility.WorldToScreenPoint(cam, corner);
                    left = Mathf.Min(left, screen.x);
                    top = Mathf.Max(top, screen.y);
                }
            }
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(root, new Vector2(Mathf.Max(left, 0f), top), Cam(root), out local)) return FallbackPos;
            local -= root.rect.min;
            return new Vector2(Mathf.Max(local.x, 0f), local.y - FrameSink);
        }

        static Camera Cam(Component c)
        {
            var canvas = c.GetComponentInParent<Canvas>();
            if (canvas == null) return null;
            canvas = canvas.rootCanvas;
            return canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        }

        static bool ToLocal(RectTransform root, Component from, Vector3 world, out Vector2 local)
        {
            var screen = RectTransformUtility.WorldToScreenPoint(Cam(from), world);
            return RectTransformUtility.ScreenPointToLocalPointInRectangle(root, screen, Cam(root), out local);
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
        }

        public void OnPointerEnter(PointerEventData e)
        {
            if (Enter != null) Enter();
        }

        public void OnPointerExit(PointerEventData e)
        {
            if (Exit != null) Exit();
        }
    }

    // The pictogram, drawn: an hourglass with sand below, light on transparent.
    static class Hourglass
    {
        const int Size = 106;
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
            var pixels = new Color32[Size * Size];
            const float cx = 52.5f;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float a = 0f;
                    // Rows go bottom up: y 14..20 the base, 86..92 the top plate.
                    bool plate = (y >= 14 && y <= 20 || y >= 86 && y <= 92) && x >= 22 && x <= 83;
                    if (plate) a = 1f;
                    if (y > 20 && y < 86)
                    {
                        float t = Mathf.Abs(y - 53f) / 33f;
                        float half = 4f + 25f * t;
                        float d = Mathf.Abs(x + 0.5f - cx);
                        // The glass: a 4 px outline.
                        if (d <= half && d >= half - 4.5f) a = 1f;
                        // Sand: a heap at the bottom, a little left at the top, the stream between.
                        bool inside = d < half - 4.5f;
                        if (inside && y < 40 && d < (40 - y) * 0.9f + 4f) a = 0.85f;
                        if (inside && y > 62 && y < 71) a = 0.85f;
                        if (y >= 40 && y <= 53 && d < 1.6f) a = 0.85f;
                    }
                    // The side posts.
                    if (y > 20 && y < 86 && (x >= 23 && x <= 26 || x >= 79 && x <= 82)) a = 1f;
                    byte v = (byte)(a > 0 ? 230 : 0);
                    pixels[y * Size + x] = new Color32(v, v, v, (byte)(a * 255));
                }
            }
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
            return result;
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
