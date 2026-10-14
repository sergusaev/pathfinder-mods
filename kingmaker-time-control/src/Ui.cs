using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Assets.Console.GamepadInput;
using Kingmaker.Blueprints.Console;
using Kingmaker.Controllers.GlobalMap;
using Kingmaker.GameModes;
using Kingmaker.UI;
using Kingmaker.UI.Common;
using Kingmaker.UI._ConsoleUI;
using Kingmaker.UI.ServiceWindow;
using Kingmaker.PubSubSystem;
using ModWindows;
using TurnBased.Controllers;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace TimeControl
{
    // The look of the game's parchment: the dialogue sheet, its book font, the tooltip rule and the red initials
    // (the same look as the Buff It 2 The Limit menu of Kingmaker). Without the game's sprites the windows stay dark.
    static class Theme
    {
        const string SheetName = "dialogue_backsheet";
        const string SeparatorPrefix = "Separator_";
        const string FontName = "NexusSerif-Regular SDF";
        internal static readonly RectOffset PanelPadding = new RectOffset(58, 58, 48, 44);

        // The button hints under a window, as under the game's Formation window: one tiled plate under the sheet,
        // the hints far apart on it, the console icon and a white label in small capitals.
        const string HintPlateName = "backHintTile";
        internal const float HintPlateHeight = 90f;
        internal const float HintGap = 12f;
        internal const float HintSpacing = 50f;
        internal const float HintIconSize = 36f;
        internal const float HintLabelSize = 20f;
        internal const FontStyles HintLabelStyle = FontStyles.Bold | FontStyles.UpperCase | FontStyles.SmallCaps;
        internal static readonly Color HintLabel = Color.white;
        internal static readonly RectOffset HintPadding = new RectOffset(60, 60, 0, 0);
        static Sprite s_HintPlate;

        static bool s_Resolved;
        static float s_NextTry;
        static Sprite s_Sheet;
        static Sprite[] s_Separator;
        static TMP_FontAsset s_Font;

        internal static bool Paper
        {
            get { Resolve(); return s_Sheet != null; }
        }

        internal static TMP_FontAsset Font
        {
            get { Resolve(); return s_Font; }
        }

        internal static Color Text { get { return Paper ? new Color(0.17f, 0.09f, 0.05f) : new Color(0.92f, 0.9f, 0.85f); } }
        internal static Color Title { get { return Paper ? new Color(0.16f, 0.07f, 0.04f) : new Color(0.93f, 0.8f, 0.5f); } }
        internal static Color Dim { get { return Paper ? new Color(0.36f, 0.25f, 0.16f) : new Color(0.65f, 0.62f, 0.56f); } }
        internal static Color Row { get { return Paper ? new Color(0.36f, 0.22f, 0.1f, 0.07f) : new Color(1f, 1f, 1f, 0.04f); } }
        internal static Color RowSelected { get { return Paper ? new Color(0.55f, 0.3f, 0.1f, 0.3f) : new Color(0.62f, 0.47f, 0.2f, 0.55f); } }
        internal static Color RowHover { get { return Paper ? new Color(0.55f, 0.3f, 0.1f, 0.14f) : new Color(0.93f, 0.8f, 0.5f, 0.14f); } }
        internal static Color Line { get { return Paper ? new Color(0.3f, 0.18f, 0.08f, 0.3f) : new Color(1f, 1f, 1f, 0.12f); } }
        internal static string Accent { get { return Paper ? "#8C2F14" : "#E8C46A"; } }
        internal static string Bad { get { return Paper ? "#A3241A" : "#E08A7A"; } }
        internal static string Good { get { return Paper ? "#2F6420" : "#9AD08A"; } }
        internal static string Muted { get { return Paper ? "#6E5743" : "#8A847A"; } }
        internal static string Note { get { return Paper ? "#2E4A66" : "#9FC3D9"; } }

        // The plate under the hints; without the game's sprite a dark translucent panel.
        internal static void HintPlate(Image image)
        {
            image.raycastTarget = false;
            if (s_HintPlate == null)
            {
                try
                {
                    var sprite = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == HintPlateName);
                    // The game draws the tile 90 high whatever its pixels: a copy with that pixel density keeps the
                    // tiled borders the size the game shows them.
                    if (sprite != null)
                        s_HintPlate = Sprite.Create(sprite.texture, sprite.textureRect, new Vector2(0.5f, 0.5f),
                            100f * sprite.rect.height / HintPlateHeight, 0, SpriteMeshType.FullRect, sprite.border);
                }
                catch (Exception e)
                {
                    Main.Error(e, "hint plate");
                }
            }
            if (s_HintPlate != null)
            {
                image.sprite = s_HintPlate;
                image.type = Image.Type.Tiled;
                image.fillCenter = true;
                image.color = Color.white;
            }
            else
            {
                image.sprite = null;
                image.color = new Color(0.05f, 0.04f, 0.03f, 0.85f);
            }
        }

        internal static void Sheet(Image image)
        {
            Resolve();
            if (s_Sheet == null)
            {
                image.color = new Color(0.06f, 0.05f, 0.04f, 0.94f);
                return;
            }
            image.color = Color.clear;
            var go = new GameObject("Sheet", typeof(RectTransform));
            go.transform.SetParent(image.transform, false);
            go.transform.SetAsFirstSibling();
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            go.AddComponent<LayoutElement>().ignoreLayout = true;
            var sheet = go.AddComponent<Image>();
            sheet.sprite = s_Sheet;
            sheet.type = Image.Type.Sliced;
            sheet.raycastTarget = false;
        }

        // The tooltip rule: curled ends, a line each side and a dot in the middle.
        internal static void Separator(Transform parent)
        {
            Resolve();
            if (s_Separator == null)
            {
                Ui.Rule(parent);
                return;
            }
            var go = new GameObject("Separator", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            var element = go.AddComponent<LayoutElement>();
            element.preferredHeight = element.minHeight = 12f;
            element.flexibleWidth = 1;
            for (int i = 0; i < 3; i++)
            {
                var part = new GameObject("Part", typeof(RectTransform));
                part.transform.SetParent(go.transform, false);
                var image = part.AddComponent<Image>();
                image.sprite = s_Separator[i];
                image.type = i == 1 ? Image.Type.Simple : Image.Type.Sliced;
                image.raycastTarget = false;
                var size = part.AddComponent<LayoutElement>();
                if (i == 1) size.preferredWidth = size.minWidth = 12f * 50f / 23f;
                else
                {
                    size.flexibleWidth = 1;
                    size.minWidth = 32;
                }
            }
        }

        static readonly Regex LeadingTags = new Regex(@"^(\s*(<[^>]*>\s*)*)");

        // The book initial of the game: the first letter large, in the Saber font and the paper red.
        internal static string Initial(string text)
        {
            if (string.IsNullOrEmpty(text) || !Paper) return text;
            int start = LeadingTags.Match(text).Length;
            if (start >= text.Length || !char.IsLetter(text[start])) return text;
            try
            {
                return text.Substring(0, start) + UIUtility.GetSaberBookFormat(text.Substring(start));
            }
            catch (Exception)
            {
                return text;
            }
        }

        static void Resolve()
        {
            if (s_Resolved || Time.unscaledTime < s_NextTry) return;
            s_Resolved = true;
            try
            {
                var sprites = Resources.FindObjectsOfTypeAll<Sprite>();
                s_Sheet = sprites.FirstOrDefault(s => s.name == SheetName);
                var parts = new[] { "left", "middle", "right" }.Select(n => sprites.FirstOrDefault(s => s.name == SeparatorPrefix + n)).ToArray();
                if (parts.All(p => p != null))
                    s_Separator = parts.Select(p => Sprite.Create(p.texture, p.textureRect, new Vector2(0.5f, 0.5f), p.pixelsPerUnit * 2f, 0, SpriteMeshType.FullRect, p.border)).ToArray();
                s_Font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f.name == FontName)
                    ?? UnityEngine.Object.FindObjectsOfType<TextMeshProUGUI>().Select(t => t.font).FirstOrDefault(f => f != null);
                Main.Log(string.Format("theme: sheet={0}, separator={1}, font={2}", s_Sheet != null, s_Separator != null, s_Font != null ? s_Font.name : "none"));
            }
            catch (Exception e)
            {
                Main.Error(e, "theme");
            }
            // Before the HUD is loaded the sprites are not in memory yet: try again next time.
            if (s_Sheet == null)
            {
                s_Resolved = false;
                s_NextTry = Time.unscaledTime + 5f;
            }
        }
    }

    static class Ui
    {
        internal static bool Keyboard
        {
            get { return Game.Instance == null || !Game.Instance.IsControllerGamepad; }
        }

        internal static TextMeshProUGUI Text(Transform parent, string text, float size, Color color, FontStyles style = FontStyles.Normal)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var t = go.AddComponent<TextMeshProUGUI>();
            if (Theme.Font != null) t.font = Theme.Font;
            t.text = text;
            t.fontSize = size;
            t.fontStyle = style;
            t.color = color;
            t.richText = true;
            t.raycastTarget = false;
            t.enableWordWrapping = true;
            return t;
        }

        internal static GameObject Horizontal(Transform parent, string name, float spacing, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return go;
        }

        internal static GameObject Vertical(Transform parent, string name, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Configure(go.AddComponent<VerticalLayoutGroup>(), new RectOffset(0, 0, 0, 0), spacing);
            return go;
        }

        internal static void Configure(VerticalLayoutGroup layout, RectOffset padding, float spacing)
        {
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        internal static void Rule(Transform parent)
        {
            var go = new GameObject("Rule", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            go.AddComponent<Image>().color = Theme.Line;
            go.AddComponent<LayoutElement>().preferredHeight = 1;
        }

        internal static Pointer AddPointer(GameObject go)
        {
            var graphic = go.GetComponent<Graphic>();
            if (graphic == null)
            {
                var image = go.AddComponent<Image>();
                image.color = Color.clear;
                graphic = image;
            }
            graphic.raycastTarget = true;
            return go.AddComponent<Pointer>();
        }

        internal static void Sound(UISoundType sound)
        {
            try
            {
                Game.Instance.UI.Common.UISound.Play(sound);
            }
            catch (Exception) { }
        }
    }

    // Mouse over and clicks for a row or a button hint.
    class Pointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Action<PointerEventData> Click;
        // False on an empty row: no tint, no click.
        public Func<bool> Live;
        public Image Tint;
        public Color Normal;
        public bool Over;

        public void OnPointerEnter(PointerEventData e)
        {
            Over = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData e)
        {
            Over = false;
            Refresh();
        }

        public void OnPointerClick(PointerEventData e)
        {
            if (e.button != PointerEventData.InputButton.Left || Click == null || (Live != null && !Live())) return;
            try
            {
                Click(e);
            }
            catch (Exception ex)
            {
                Main.Error(ex, "click");
            }
        }

        public void Refresh()
        {
            if (Tint == null) return;
            bool lit = Over && Modal.MouseUsed && (Live == null || Live());
            Tint.color = lit ? (Normal.a > 0 ? Normal : Theme.RowHover) : Normal;
        }
    }

    class Wheel : MonoBehaviour, IScrollHandler
    {
        public Action<float> Scroll;

        public void OnScroll(PointerEventData e)
        {
            if (Scroll != null) Scroll(e.scrollDelta.y);
        }
    }

    class Row
    {
        public Image Bg;
        public TextMeshProUGUI Text;
        public TextMeshProUGUI Right;
        public Image Icon;
        public Pointer Pointer;
    }

    class Hint
    {
        public RewiredActionType Button;
        public string Label;

        public Hint(RewiredActionType button, string label)
        {
            Button = button;
            Label = label;
        }
    }

    // A window of the mod: a parchment sheet in the middle of the screen over a click blocker, with the game's button
    // hints at the bottom. Gamepad buttons come through an input layer of the game's console input (the world and the
    // HUD do not get them while the window is open), the keyboard keys are those of the buff menu, the mouse clicks rows
    // and hints, the right button goes back. Start and Esc open the game's menu over them, as over the game's own
    // windows: they hide under it and come back. The game is paused in real time and travel on the
    // global map stops while one is open; the camera and the game's triggers do nothing behind it (shared ModWindows).
    abstract class Modal : MonoBehaviour
    {
        static readonly List<Modal> s_Stack = new List<Modal>();
        static bool s_Paused;
        static KeyboardAccess s_Keyboard;
        // After the last window closes, its input layer and the keyboard lock stay until the buttons are let go:
        // the press that closed it (Start, Esc, B) does not reach the game as well, nor its release.
        static IDisposable s_HeldLayer;
        static int s_HeldFrame;

        internal static bool AnyOpen
        {
            get { return s_Stack.Count > 0; }
        }

        // The pointer lights rows up only once the real mouse moved over the open windows: a resting cursor
        // (the Deck's trackpad mouse) is not a second selection.
        internal static bool MouseUsed;
        static Vector3 s_MouseAt;

        protected GameObject Overlay;
        protected RectTransform Panel;
        protected TextMeshProUGUI TitleText;
        protected Transform HintHolder;
        IDisposable m_Layer;
        Canvas m_Canvas;
        readonly Dictionary<RewiredActionType, int> m_Fired = new Dictionary<RewiredActionType, int>();
        int m_HeldV, m_HeldH;
        float m_NextRepeat;
        int m_LayerDPadFrame = -10;
        RewiredActionType m_LayerDPad;
        int m_OpenFrame;

        const float RepeatDelay = 0.35f;
        const float RepeatInterval = 0.07f;

        static readonly RewiredActionType[] Buttons = {
            RewiredActionType.Confirm, RewiredActionType.Decline, RewiredActionType.Func01, RewiredActionType.Func02,
            RewiredActionType.LeftUp, RewiredActionType.RightUp, RewiredActionType.Options
        };
        static readonly RewiredActionType[] DPad = {
            RewiredActionType.DPadUp, RewiredActionType.DPadDown, RewiredActionType.DPadLeft, RewiredActionType.DPadRight
        };
        static readonly KeyCode[] KeysConfirm = { KeyCode.E, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Space };
        static readonly KeyCode[] KeysDecline = { KeyCode.Q, KeyCode.Backspace };
        static readonly KeyCode[] KeysClose = { KeyCode.Escape };
        static readonly KeyCode[] KeysX = { KeyCode.R };
        static readonly KeyCode[] KeysY = { KeyCode.F };
        static readonly KeyCode[] KeysLB = { KeyCode.X, KeyCode.PageUp };
        static readonly KeyCode[] KeysRB = { KeyCode.C, KeyCode.PageDown };

        static KeyCode[] Keys(RewiredActionType button)
        {
            switch (button)
            {
                case RewiredActionType.Confirm: return KeysConfirm;
                case RewiredActionType.Decline: return KeysDecline;
                case RewiredActionType.Func01: return KeysX;
                case RewiredActionType.Func02: return KeysY;
                case RewiredActionType.LeftUp: return KeysLB;
                case RewiredActionType.RightUp: return KeysRB;
                case RewiredActionType.Options: return KeysClose;
            }
            return new KeyCode[0];
        }

        static string KeyLabel(RewiredActionType button)
        {
            switch (button)
            {
                case RewiredActionType.DPadVertical: return "W/S";
                case RewiredActionType.DPadHorizontal: return "A/D";
                case RewiredActionType.Options: return "Esc";
                case RewiredActionType.Confirm: return "E";
            }
            var keys = Keys(button);
            return keys.Length > 0 ? keys[0].ToString() : button.ToString();
        }

        // The key or the pad button of an action, as the help names it.
        internal static string KeyName(RewiredActionType button)
        {
            switch (button)
            {
                case RewiredActionType.DPadUp: return "W";
                case RewiredActionType.DPadDown: return "S";
                case RewiredActionType.DPadLeft: return "A";
                case RewiredActionType.DPadRight: return "D";
            }
            return KeyLabel(button);
        }

        internal static string PadName(RewiredActionType button)
        {
            return PadLabel(button);
        }

        static string PadLabel(RewiredActionType button)
        {
            switch (button)
            {
                case RewiredActionType.Confirm: return "A";
                case RewiredActionType.Decline: return "B";
                case RewiredActionType.Func01: return "X";
                case RewiredActionType.Func02: return "Y";
                case RewiredActionType.LeftUp: return "LB";
                case RewiredActionType.RightUp: return "RB";
                case RewiredActionType.Options: return "Start";
            }
            return "D-pad";
        }

        // Builds the window: overlay canvas, sheet, title and rule; content goes into Panel, hints at the end.
        protected static T Create<T>(string name, float width, string title, int order) where T : Modal
        {
            var overlay = new GameObject("TimeControl_" + name, typeof(RectTransform));
            UnityEngine.Object.DontDestroyOnLoad(overlay);
            var canvas = overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = order;
            var scaler = overlay.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            overlay.AddComponent<GraphicRaycaster>();
            var blocker = new GameObject("Blocker", typeof(RectTransform));
            blocker.transform.SetParent(overlay.transform, false);
            var blockerRect = (RectTransform)blocker.transform;
            blockerRect.anchorMin = Vector2.zero;
            blockerRect.anchorMax = Vector2.one;
            blockerRect.sizeDelta = Vector2.zero;
            blocker.AddComponent<Image>().color = new Color(0, 0, 0, 0.25f);

            var root = new GameObject(name, typeof(RectTransform));
            root.transform.SetParent(overlay.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, 0);
            Theme.Sheet(root.AddComponent<Image>());
            Ui.Configure(root.AddComponent<VerticalLayoutGroup>(), Theme.Paper ? Theme.PanelPadding : new RectOffset(28, 28, 22, 22), 10);
            root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var modal = root.AddComponent<T>();
            overlay.AddComponent<Wheel>().Scroll = modal.OnWheel;
            modal.Overlay = overlay;
            modal.m_Canvas = canvas;
            modal.Panel = rect;
            modal.TitleText = Ui.Text(root.transform, Theme.Initial(title), 30, Theme.Title, FontStyles.Bold);
            modal.TitleText.alignment = TextAlignmentOptions.Center;
            Theme.Separator(root.transform);
            return modal;
        }

        protected void SetTitle(string title)
        {
            TitleText.text = Theme.Initial(title);
        }

        // The hint bar on a plate under the sheet, as under the game's Formation window: the console icon of each button
        // (or its key), then its label; a click does what the button does.
        protected void SetHints(params Hint[] hints)
        {
            if (HintHolder == null)
            {
                var row = Ui.Horizontal(Panel, "Hints", Theme.HintSpacing, TextAnchor.MiddleCenter);
                row.AddComponent<LayoutElement>().ignoreLayout = true;
                var rect = (RectTransform)row.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
                rect.pivot = new Vector2(0.5f, 1);
                rect.anchoredPosition = new Vector2(0, -Theme.HintGap);
                rect.sizeDelta = new Vector2(0, Theme.HintPlateHeight);
                Theme.HintPlate(row.AddComponent<Image>());
                row.GetComponent<HorizontalLayoutGroup>().padding = Theme.HintPadding;
                row.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
                HintHolder = row.transform;
                // The sheet and the plate under it are centred together.
                Panel.anchoredPosition += new Vector2(0, (Theme.HintGap + Theme.HintPlateHeight) / 2);
            }
            for (int i = HintHolder.childCount - 1; i >= 0; i--) Destroy(HintHolder.GetChild(i).gameObject);
            foreach (var hint in hints)
            {
                var item = Ui.Horizontal(HintHolder, "Hint", 6, TextAnchor.MiddleLeft);
                Sprite sprite = null;
                if (!Ui.Keyboard)
                {
                    try
                    {
                        sprite = GamePadIcons.Instance.GetIcon(hint.Button);
                    }
                    catch (Exception) { }
                }
                if (sprite != null)
                {
                    var icon = new GameObject("Icon", typeof(RectTransform));
                    icon.transform.SetParent(item.transform, false);
                    var image = icon.AddComponent<Image>();
                    image.sprite = sprite;
                    image.preserveAspect = true;
                    image.raycastTarget = false;
                    var size = icon.AddComponent<LayoutElement>();
                    size.preferredWidth = size.preferredHeight = Theme.HintIconSize;
                }
                else
                {
                    var key = Ui.Text(item.transform, "[" + (Ui.Keyboard ? KeyLabel(hint.Button) : PadLabel(hint.Button)) + "]", 19, Theme.HintLabel, FontStyles.Bold);
                    key.enableWordWrapping = false;
                }
                var label = Ui.Text(item.transform, hint.Label, Theme.HintLabelSize, Theme.HintLabel, Theme.HintLabelStyle);
                label.enableWordWrapping = false;
                var button = hint.Button;
                Ui.AddPointer(item).Click = e => Fire(button, "mouse");
            }
        }

        protected Row MakeRow(Transform parent, float size)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var bg = go.AddComponent<Image>();
            bg.color = Color.clear;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(12, 12, 4, 4);
            layout.spacing = 16;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            var element = go.AddComponent<LayoutElement>();
            element.minHeight = size + 14;
            var text = Ui.Text(go.transform, "", size, Theme.Text);
            text.enableWordWrapping = false;
            text.overflowMode = TextOverflowModes.Ellipsis;
            text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            // A mark before the right column (the eye of a tracked deadline), off by default.
            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(go.transform, false);
            var icon = iconGo.AddComponent<Image>();
            icon.raycastTarget = false;
            icon.preserveAspect = true;
            var iconSize = iconGo.AddComponent<LayoutElement>();
            iconSize.preferredWidth = iconSize.minWidth = size * 1.4f;
            iconSize.preferredHeight = size;
            iconGo.SetActive(false);
            var right = Ui.Text(go.transform, "", size, Theme.Dim);
            right.enableWordWrapping = false;
            right.alignment = TextAlignmentOptions.Right;
            var pointer = go.AddComponent<Pointer>();
            pointer.Tint = bg;
            pointer.Normal = Color.clear;
            return new Row { Bg = bg, Text = text, Right = right, Icon = icon, Pointer = pointer };
        }

        protected static void Paint(Row row, bool selected)
        {
            row.Pointer.Normal = selected ? Theme.RowSelected : Color.clear;
            row.Pointer.Refresh();
        }

        protected void Show()
        {
            s_Stack.Add(this);
            m_OpenFrame = Time.frameCount;
            if (s_Stack.Count == 1)
            {
                MouseUsed = false;
                s_MouseAt = Input.mousePosition;
            }
            if (s_Stack.Count == 1)
            {
                var game = Game.Instance;
                s_Paused = false;
                // Real time only: turn-based combat waits for the player anyway.
                if (PausesGame && game.CurrentMode == GameModeType.Default && !game.IsPaused && !CombatController.IsInTurnBasedCombat())
                {
                    game.IsPaused = true;
                    s_Paused = true;
                }
                // The lock still held from the last window carries over.
                DropHeldLayer();
                s_ShownBefore = null;
                LockKeyboard(true);
            }
            PushLayer();
            ModWindow.Opened(Main.Mod.Info.Id, Overlay);
        }

        internal void Close()
        {
            s_Stack.Remove(this);
            ModWindow.Closed(Overlay);
            if (s_Stack.Count == 0)
            {
                DropHeldLayer();
                s_HeldLayer = m_Layer;
                m_Layer = null;
                s_HeldFrame = Time.frameCount;
                if (s_Paused && Game.Instance != null && Game.Instance.IsPaused) Game.Instance.IsPaused = false;
                s_Paused = false;
                VirtualCursor.Restore();
            }
            else PopLayer();
            OnClosed();
            if (Overlay != null) Destroy(Overlay);
        }

        // Every window of the mod, from the top.
        static void CloseAll(bool sound = true)
        {
            if (sound) Ui.Sound(UISoundType.JournalClose);
            while (s_Stack.Count > 0) s_Stack[s_Stack.Count - 1].Close();
        }

        // The game's hotkeys, its Esc among them, are off while a window is up (s_Keyboard set) and under the game's menu on.
        static void LockKeyboard(bool on)
        {
            if (on == (s_Keyboard != null)) return;
            if (on)
            {
                s_Keyboard = Game.Instance != null ? Game.Instance.Keyboard : null;
                if (s_Keyboard != null) s_Keyboard.Disabled.Value = true;
            }
            else
            {
                s_Keyboard.Disabled.Value = false;
                s_Keyboard = null;
            }
        }

        // The game's menu over the mod's windows, as over the game's own: Start with the gamepad (the game's base layer
        // opens it), Esc with the keyboard (opened here). The windows hide under it and wait; with the keyboard the
        // game's keys are back meanwhile, for the menu and the windows it opens (options, saves).
        static int s_CoverFrame = -1;
        static bool s_Covered;
        static int s_MenuFrame = -10;
        static HashSet<UIWindow> s_ShownBefore;

        static void OpenGameMenu()
        {
            s_ShownBefore = new HashSet<UIWindow>(UnityEngine.Object.FindObjectsOfType<UIWindow>().Where(w => w.IsShow));
            s_MenuFrame = Time.frameCount;
            LockKeyboard(false);
            EventBus.RaiseEvent<IEscMenuWindowUIHandler>(h => h.HandleOpen());
        }

        static bool Covered
        {
            get
            {
                if (s_CoverFrame == Time.frameCount) return s_Covered;
                s_CoverFrame = Time.frameCount;
                if (!Ui.Keyboard)
                {
                    var layer = GamePad.HasInstance ? GamePad.Instance.CurrentInputLayer : null;
                    s_Covered = layer != null && (layer.ContextName == null || !layer.ContextName.StartsWith("TimeControl_"));
                    return s_Covered;
                }
                if (s_ShownBefore == null) return s_Covered = false;
                // The menu takes a frame or two to show.
                s_Covered = Time.frameCount <= s_MenuFrame + 2 || GameUiShown();
                if (!s_Covered)
                {
                    s_ShownBefore = null;
                    if (s_Stack.Count > 0) LockKeyboard(true);
                }
                return s_Covered;
            }
        }

        static readonly System.Reflection.FieldInfo s_SaveLoadShown = AccessTools.Field(typeof(Kingmaker.UI.SaveLoadWindow.SaveLoadWindow), "m_IsShown");

        // The menu and what it opens: the windows (options and the like), the list of saves and the confirmations,
        // which are not windows of the game's kind, and a load under way.
        static bool GameUiShown()
        {
            if (Kingmaker.EntitySystem.Persistence.LoadingProcess.Instance.IsLoadingInProcess) return true;
            if (UnityEngine.Object.FindObjectsOfType<UIWindow>().Any(w => w.IsShow && !s_ShownBefore.Contains(w))) return true;
            if (UnityEngine.Object.FindObjectsOfType<Kingmaker.UI.SaveLoadWindow.SaveLoadWindow>().Any(w => s_SaveLoadShown != null && (bool)s_SaveLoadShown.GetValue(w))) return true;
            return UnityEngine.Object.FindObjectsOfType<DialogMessageBox>().Any(w => w.IsShown);
        }

        // Each frame: the held layer and keyboard lock go once no button of the windows is down.
        internal static void TickHold()
        {
            if (s_Stack.Count > 0 || (s_HeldLayer == null && s_Keyboard == null)) return;
            if (Time.frameCount <= s_HeldFrame + 1 || Held()) return;
            DropHeldLayer();
            LockKeyboard(false);
        }

        static bool Held()
        {
            foreach (var b in Buttons)
                if (Keys(b).Any(Input.GetKey)) return true;
            try
            {
                var player = GamePad.HasInstance ? GamePad.Instance.Player : null;
                if (player != null)
                    foreach (var b in Buttons)
                        if (player.GetButton((int)b)) return true;
            }
            catch (Exception) { }
            return false;
        }

        static void DropHeldLayer()
        {
            if (s_HeldLayer != null) s_HeldLayer.Dispose();
            s_HeldLayer = null;
        }

        protected virtual void OnClosed() { }

        protected virtual bool PausesGame
        {
            get { return true; }
        }

        void OnDestroy()
        {
            if (s_Stack.Contains(this))
            {
                PopLayer();
                s_Stack.Remove(this);
                ModWindow.Closed(Overlay);
                if (s_Stack.Count == 0) LockKeyboard(false);
            }
        }

        void PushLayer()
        {
            if (Ui.Keyboard || !GamePad.HasInstance) return;
            var layer = new InputLayer { ContextName = "TimeControl_" + GetType().Name };
            // Start stays with the game's base layer: its menu.
            foreach (var b in Buttons.Where(b => b != RewiredActionType.Options))
            {
                var button = b;
                layer.AddButton(_ => Fire(button, "layer"), (int)button);
            }
            foreach (var d in DPad)
            {
                var dir = d;
                layer.AddButton(_ =>
                {
                    m_LayerDPad = dir;
                    m_LayerDPadFrame = Time.frameCount;
                }, (int)dir, Rewired.InputActionEventType.ButtonPressed);
            }
            m_Layer = GamePad.Instance.PushLayer(layer);
            VirtualCursor.Hide();
        }

        void PopLayer()
        {
            if (m_Layer != null) m_Layer.Dispose();
            m_Layer = null;
        }

        bool Top
        {
            get { return s_Stack.Count > 0 && s_Stack[s_Stack.Count - 1] == this; }
        }

        // From the layer, the polled gamepad, the keyboard and the hints; the first in a frame wins.
        protected void Fire(RewiredActionType button, string source)
        {
            if (!Top) return;
            int last;
            if (m_Fired.TryGetValue(button, out last) && Time.frameCount - last <= 2) return;
            m_Fired[button] = Time.frameCount;
            try
            {
                switch (button)
                {
                    case RewiredActionType.Confirm: OnConfirm(); break;
                    case RewiredActionType.Decline: OnDecline(); break;
                    case RewiredActionType.Func01: OnFuncX(); break;
                    case RewiredActionType.Func02: OnFuncY(); break;
                    case RewiredActionType.LeftUp: OnShoulder(-1); break;
                    case RewiredActionType.RightUp: OnShoulder(1); break;
                    case RewiredActionType.Options: if (Ui.Keyboard) OpenGameMenu(); break;
                    case RewiredActionType.DPadUp: OnMove(-1); break;
                    case RewiredActionType.DPadDown: OnMove(1); break;
                    case RewiredActionType.DPadLeft: OnChange(-1); break;
                    case RewiredActionType.DPadRight: OnChange(1); break;
                }
            }
            catch (Exception e)
            {
                Main.Error(e, GetType().Name + " " + button);
            }
        }

        protected virtual void OnConfirm() { }
        protected virtual void OnDecline() { Ui.Sound(UISoundType.JournalClose); Close(); }
        protected virtual void OnFuncX() { }
        protected virtual void OnFuncY() { }
        protected virtual void OnShoulder(int direction) { }
        protected virtual void OnMove(int direction) { }
        protected virtual void OnChange(int direction) { }
        protected virtual void OnWheel(float y) { }
        protected virtual void Tick() { }
        void Update()
        {
            // The game's menu loaded a save or went to the main menu: the windows have nothing to show.
            if (!Main.InGame)
            {
                if (Top) CloseAll(false);
                return;
            }
            bool covered = Covered;
            if (m_Canvas != null && m_Canvas.enabled == covered) m_Canvas.enabled = !covered;
            // The press that closes the game's menu is not also a press in the window.
            if (covered) m_OpenFrame = Time.frameCount;
            if (covered || !Top) return;
            if (!MouseUsed && (Input.mousePosition - s_MouseAt).sqrMagnitude > 4f)
            {
                MouseUsed = true;
                foreach (var p in Overlay.GetComponentsInChildren<Pointer>()) p.Refresh();
            }
            // The key that opened the window is not also a press in it.
            if (Time.frameCount <= m_OpenFrame + 1) return;
            try
            {
                Rewired.Player player = GamePad.HasInstance ? GamePad.Instance.Player : null;
                if (player != null)
                    foreach (var b in Buttons.Where(b => b != RewiredActionType.Options))
                    {
                        bool down = false;
                        try { down = player.GetButtonDown((int)b); } catch (Exception) { }
                        if (down) Fire(b, "poll");
                    }
                foreach (var b in Buttons)
                    if (Keys(b).Any(Input.GetKeyDown)) Fire(b, "key");
                if (Input.GetMouseButtonUp(1)) Fire(RewiredActionType.Decline, "mouse");
                Directions(player);
                Tick();
            }
            catch (Exception e)
            {
                Main.Error(e, GetType().Name + ".Update");
            }
        }

        // D-pad, left stick, arrows and WASD, repeated while held.
        void Directions(Rewired.Player player)
        {
            int v = 0, h = 0;
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W)) v = -1;
            else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S)) v = 1;
            if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A)) h = -1;
            else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D)) h = 1;
            if (Time.frameCount - m_LayerDPadFrame <= 1)
            {
                if (m_LayerDPad == RewiredActionType.DPadUp) v = -1;
                else if (m_LayerDPad == RewiredActionType.DPadDown) v = 1;
                else if (m_LayerDPad == RewiredActionType.DPadLeft) h = -1;
                else if (m_LayerDPad == RewiredActionType.DPadRight) h = 1;
            }
            if (player != null && v == 0 && h == 0)
            {
                try
                {
                    if (player.GetButton((int)RewiredActionType.DPadUp)) v = -1;
                    else if (player.GetButton((int)RewiredActionType.DPadDown)) v = 1;
                    else if (player.GetAxis((int)RewiredActionType.LeftStickY) > 0.5f) v = -1;
                    else if (player.GetAxis((int)RewiredActionType.LeftStickY) < -0.5f) v = 1;
                    if (player.GetButton((int)RewiredActionType.DPadLeft)) h = -1;
                    else if (player.GetButton((int)RewiredActionType.DPadRight)) h = 1;
                    else if (player.GetAxis((int)RewiredActionType.LeftStickX) < -0.5f) h = -1;
                    else if (player.GetAxis((int)RewiredActionType.LeftStickX) > 0.5f) h = 1;
                }
                catch (Exception) { }
            }
            if (v != 0 && h != 0) h = 0;
            if (v != m_HeldV || h != m_HeldH)
            {
                m_HeldV = v;
                m_HeldH = h;
                m_NextRepeat = Time.unscaledTime + RepeatDelay;
                Step(v, h);
            }
            else if ((v != 0 || h != 0) && Time.unscaledTime >= m_NextRepeat)
            {
                m_NextRepeat = Time.unscaledTime + RepeatInterval;
                Step(v, h);
            }
        }

        void Step(int v, int h)
        {
            try
            {
                if (v != 0) OnMove(v);
                if (h != 0) OnChange(h);
            }
            catch (Exception e)
            {
                Main.Error(e, GetType().Name + " move");
            }
        }
    }

    // The console interface has a virtual mouse moved by the left stick (RewiredCursorController, on the global map and in
    // cursor mode). It is on whenever the game's base layer wants it, whatever layer is on top, and its pointer lights up
    // whatever it is over in the mod's windows: a second, false highlight that the left stick moves. Off while a window is open.
    [HarmonyPatch(typeof(GamePad), "RestoreCursorState")]
    static class VirtualCursor
    {
        static void Postfix()
        {
            if (Modal.AnyOpen) Hide();
        }

        internal static void Hide()
        {
            var cursor = RewiredCursorController.Instance;
            if (cursor == null) return;
            cursor.Enabled = false;
            if (cursor.Cursor != null) cursor.Cursor.SetActive(false);
        }

        internal static void Restore()
        {
            if (GamePad.HasInstance && RewiredCursorController.Instance != null) GamePad.Instance.RestoreCursorState();
        }
    }

    // Travel on the global map does not go on under an open window.
    [HarmonyPatch(typeof(MapMovementController), "Tick")]
    static class FreezeTravelPatch
    {
        static bool Prefix()
        {
            return !Modal.AnyOpen;
        }
    }
}
