using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI.SettingsUI;
using Kingmaker.View;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityModManagerNet;
using PcLocalMap = Kingmaker.UI.ServiceWindow.LocalMap.LocalMap;

namespace GamepadCameraRotation
{
    // Keyboard and mouse mode, as in WotR: the middle mouse button rotates (Alt + middle keeps the Kingmaker pan),
    // Alt+A / Alt+D rotate, F1 turns back north; the compass sits to the right of the system buttons.
    static class PcMode
    {
        const string CompassName = "GCR_PcCompass";
        const string TipName = "GCR_PcCompassTip";
        const string MenuBlockName = "Menu_Buttons48px";
        const string BuffMenuName = "BI2TL_PadQuickMenu";
        const string PaperSpriteName = "dialogue_backsheet";
        const string FontName = "NexusSerif-Regular SDF";
        const float Gap = 6f;
        const string ClockName = "Clock";
        const string HourglassName = "Sand";
        const string RingName = "UI_HudAstrolabeBorder_Console";
        // The clock is 140 x 117 (TBM_frameClock at half size); its round window (pixels 63-214 by 22-172 of the
        // 280 x 234 sprite) is centred 69.5 from the left and 68.5 from the bottom. The arrow turns about DialCenter
        // of the compass block.
        static readonly Vector2 ClockSize = new Vector2(140f, 117f);
        static readonly Vector2 ClockWindow = new Vector2(69.5f, 68.5f);
        static readonly Vector2 DialCenter = new Vector2(75.5f, 68.6f);
        // The window's radius is 37.5. The dial (45 units in radius) fills it up to 1.5 units from the rim; the arrow
        // (61 units from the centre to the tail) is scaled on its own so that its tail stops just short of the rim.
        const float ClockDialScale = 0.8f;
        const float ClockArrowScale = 0.6f;
        const string ArrowName = "UI_HudAstrolabeArrow";
        const float ArrowLayerScale = 1.15f;

        static readonly FieldInfo BaseMousePoint = AccessTools.Field(typeof(CameraRig), "m_BaseMousePoint");
        static readonly PropertyInfo BindName = AccessTools.Property(typeof(SettingsEntityKeybind), "Name");
        // The hourglass, and the gears with their backing behind the clock's window.
        static readonly string[] ClockParts = { ClockName + "/" + HourglassName, "BackgroundClock" };

        static RectTransform s_Compass;
        static RectTransform s_Menu;
        static RectTransform s_Tip;
        static TextMeshProUGUI s_TipTitle;
        static TextMeshProUGUI s_TipText;
        static float s_NextSearch;
        static List<GameObject> s_ClockParts;
        static bool s_Dragging;
        static float s_LastMouseX;

        static bool Alt
        {
            get { return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt); }
        }

        static bool Modified
        {
            get
            {
                return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)
                    || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            }
        }

        internal static void Update(CameraRig rig)
        {
            UpdateCompass();
            Settings s = Main.Settings;
            bool rotateMod = Alt && !Modified;
            bool left = rotateMod && Input.GetKey(s.RotateLeftKey);
            bool right = rotateMod && Input.GetKey(s.RotateRightKey);
            bool north = !Alt && !Modified && Input.GetKeyDown(s.NorthKey);
            if (!left && !right && !north) return;
            if (!InputAllowed()) return;

            if (north) North(rig);
            float dir = (right ? 1f : 0f) - (left ? 1f : 0f);
            if (dir != 0f)
                Main.Rotate(rig, dir * s.RotationSpeed * Mathf.Min(Time.unscaledDeltaTime, 0.1f));
        }

        // Runs after the game's own scroll tick: a drag without Alt turns the camera and cancels the pan the game started.
        internal static void TickDrag(CameraRig rig)
        {
            if (Game.Instance == null || Game.Instance.IsControllerGamepad)
            {
                s_Dragging = false;
                return;
            }
            if (Input.GetMouseButtonDown(2) && !Alt && InputAllowed())
            {
                s_Dragging = true;
                s_LastMouseX = Input.mousePosition.x;
            }
            if (!s_Dragging) return;
            if (!Input.GetMouseButton(2))
            {
                s_Dragging = false;
                return;
            }
            BaseMousePoint.SetValue(rig, null);
            float x = Input.mousePosition.x;
            float dx = x - s_LastMouseX;
            s_LastMouseX = x;
            if (dx != 0f)
                Main.Rotate(rig, dx * Main.Settings.MouseRotationSpeed);
        }

        static void North(CameraRig rig)
        {
            rig.transform.rotation = Quaternion.Euler(0f, Main.MapYaw, 0f);
        }

        // Not during cutscenes and dialogues, while typing, with the UMM window or the buff menu open.
        static bool InputAllowed()
        {
            if (!Main.CameraControllable()) return false;
            var ui = UnityModManager.UI.Instance;
            if (ui != null && ui.Opened) return false;
            if (GameObject.Find(BuffMenuName) != null) return false;
            GameObject selected = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (selected != null)
            {
                var tmp = selected.GetComponent<TMP_InputField>();
                if (tmp != null && tmp.isFocused) return false;
                var field = selected.GetComponent<InputField>();
                if (field != null && field.isFocused) return false;
            }
            return true;
        }

        // ---------- compass ----------

        static void UpdateCompass()
        {
            if (!Main.Settings.PcCompass)
            {
                SetClockParts(true);
                if (s_Compass != null) UnityEngine.Object.Destroy(s_Compass.gameObject);
                s_Compass = null;
                return;
            }
            if (s_Compass == null)
            {
                if (Time.unscaledTime < s_NextSearch) return;
                s_NextSearch = Time.unscaledTime + 1f;
                GameObject menu = GameObject.Find(MenuBlockName);
                if (menu == null) return;
                s_Menu = (RectTransform)menu.transform;
                s_ClockParts = null;
                Build((RectTransform)s_Menu.parent);
                if (s_Compass == null) return;
            }
            Place();
        }

        static void Build(RectTransform parent)
        {
            var go = new GameObject(CompassName, typeof(RectTransform), typeof(Image));
            var rt = go.GetComponent<RectTransform>();
            rt.SetParent(parent, false);
            rt.anchorMin = rt.anchorMax = rt.pivot = Vector2.zero;
            rt.sizeDelta = Compass.Size;
            var hit = go.GetComponent<Image>();
            hit.color = Color.clear;
            if (!Compass.BuildDial(rt))
            {
                UnityEngine.Object.Destroy(go);
                // Without WotR there is nothing to show; the search is repeated only after a settings change.
                s_NextSearch = float.MaxValue;
                return;
            }
            // The clock's frame is the ring: the WotR ring would cover it.
            Transform ring = rt.Find(RingName);
            if (ring != null) ring.gameObject.SetActive(false);
            var events = go.AddComponent<CompassEvents>();
            events.Click = () =>
            {
                CameraRig rig = Game.Instance != null ? Game.Instance.UI.GetCameraRig() : null;
                if (rig != null && Main.CameraControllable()) North(rig);
            };
            events.Enter = ShowTip;
            events.Exit = HideTip;
            s_Compass = rt;
            Main.Log?.Log("PC compass built next to " + MenuBlockName);
        }

        static void SetArrowScale(float scale)
        {
            var arrow = s_Compass.Find(ArrowName);
            if (arrow != null) arrow.localScale = new Vector3(scale, scale, 1f);
        }

        // The game shows the hourglass again on its own when combat mode is switched, so the parts are hidden on every
        // placement.
        static void SetClockParts(bool shown)
        {
            if (s_Menu == null) return;
            if (s_ClockParts == null)
            {
                s_ClockParts = new List<GameObject>();
                foreach (string path in ClockParts)
                {
                    Transform part = s_Menu.Find(path);
                    if (part != null) s_ClockParts.Add(part.gameObject);
                }
            }
            foreach (GameObject part in s_ClockParts)
                if (part != null && part.activeSelf != shown) part.SetActive(shown);
        }

        internal static void Reset()
        {
            SetClockParts(true);
            s_ClockParts = null;
            s_NextSearch = 0f;
            if (s_Compass != null) UnityEngine.Object.Destroy(s_Compass.gameObject);
            s_Compass = null;
        }

        // In the round window of the game's clock, in place of its hourglass; right of the system buttons when the clock
        // is not there.
        static void Place()
        {
            if (s_Menu == null) return;
            var parent = (RectTransform)s_Compass.parent;
            var clock = s_Menu.Find(ClockName) as RectTransform;
            if (clock != null && clock.gameObject.activeInHierarchy)
            {
                SetClockParts(false);
                SetArrowScale(ArrowLayerScale * ClockArrowScale / ClockDialScale);
                var frame = new Vector3[4];
                clock.GetWorldCorners(frame);
                Vector3 min = parent.InverseTransformPoint(frame[0]);
                Vector3 max = parent.InverseTransformPoint(frame[2]);
                float k = (max.x - min.x) / ClockSize.x;
                // The scale setting still works here, relative to its default.
                float dialScale = ClockDialScale * Main.Settings.PcCompassScale / 0.85f * k;
                s_Compass.localScale = new Vector3(dialScale, dialScale, 1f);
                Vector2 center = (Vector2)min + ClockWindow * k;
                s_Compass.localPosition = new Vector3(center.x - DialCenter.x * dialScale + Main.Settings.PcCompassOffsetX,
                    center.y - DialCenter.y * dialScale + Main.Settings.PcCompassOffsetY, 0f);
                return;
            }
            SetClockParts(true);
            SetArrowScale(ArrowLayerScale);
            float right = float.MinValue, bottom = float.MaxValue;
            var corners = new Vector3[4];
            foreach (RectTransform rect in s_Menu.GetComponentsInChildren<RectTransform>(false))
            {
                if (rect != s_Menu && rect.parent != s_Menu) continue;
                rect.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                {
                    Vector3 local = parent.InverseTransformPoint(corner);
                    right = Mathf.Max(right, local.x);
                    bottom = Mathf.Min(bottom, local.y);
                }
            }
            float scale = Main.Settings.PcCompassScale;
            s_Compass.localScale = new Vector3(scale, scale, 1f);
            s_Compass.localPosition = new Vector3(right + Gap + Main.Settings.PcCompassOffsetX, bottom + Main.Settings.PcCompassOffsetY, 0f);
        }

        // ---------- tooltip on the game's parchment ----------

        static void ShowTip()
        {
            if (s_Compass == null) return;
            if (s_Tip == null) BuildTip();
            s_TipTitle.text = TipTitle();
            s_TipText.text = TipText();
            s_Tip.SetAsLastSibling();
            s_Tip.gameObject.SetActive(true);
            Vector3 top = s_Compass.localPosition + new Vector3(0f, Compass.Size.y * s_Compass.localScale.y + Gap, 0f);
            s_Tip.localPosition = top;
        }

        static void HideTip()
        {
            if (s_Tip != null) s_Tip.gameObject.SetActive(false);
        }

        static void BuildTip()
        {
            var go = new GameObject(TipName, typeof(RectTransform), typeof(Image));
            s_Tip = go.GetComponent<RectTransform>();
            s_Tip.SetParent(s_Compass.parent, false);
            s_Tip.anchorMin = s_Tip.anchorMax = s_Tip.pivot = Vector2.zero;
            var bg = go.GetComponent<Image>();
            bg.raycastTarget = false;
            Sprite paper = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(sp => sp.name == PaperSpriteName);
            Color textColor;
            var layout = go.AddComponent<VerticalLayoutGroup>();
            if (paper != null)
            {
                // A child outside the layout: on the fitted object itself the sliced sprite would hold its minimum size.
                bg.color = Color.clear;
                var sheetGo = new GameObject("Sheet", typeof(RectTransform), typeof(LayoutElement), typeof(Image));
                var sheetRect = sheetGo.GetComponent<RectTransform>();
                sheetRect.SetParent(s_Tip, false);
                sheetRect.anchorMin = Vector2.zero;
                sheetRect.anchorMax = Vector2.one;
                sheetRect.offsetMin = sheetRect.offsetMax = Vector2.zero;
                sheetGo.GetComponent<LayoutElement>().ignoreLayout = true;
                var sheet = sheetGo.GetComponent<Image>();
                sheet.sprite = paper;
                sheet.type = Image.Type.Sliced;
                sheet.raycastTarget = false;
                layout.padding = new RectOffset(30, 30, 24, 22);
                textColor = new Color(0.17f, 0.09f, 0.05f);
            }
            else
            {
                bg.color = new Color(0.06f, 0.05f, 0.04f, 0.94f);
                layout.padding = new RectOffset(14, 14, 10, 10);
                textColor = new Color(0.92f, 0.9f, 0.85f);
            }
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            layout.spacing = 6;
            var fitter = go.AddComponent<ContentSizeFitter>();
            fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // As the game's tooltips: the title centred, the ornamental rule, then the text.
            TMP_FontAsset font = Resources.FindObjectsOfTypeAll<TMP_FontAsset>().FirstOrDefault(f => f.name == FontName);
            s_TipTitle = TipLabel("Title", font, 22, FontStyles.Bold, TextAlignmentOptions.Center,
                paper != null ? new Color(0.16f, 0.07f, 0.04f) : new Color(0.93f, 0.8f, 0.5f));
            if (paper != null) Rule(s_Tip);
            s_TipText = TipLabel("Text", font, 18, FontStyles.Normal, TextAlignmentOptions.TopLeft, textColor);
        }

        static TextMeshProUGUI TipLabel(string name, TMP_FontAsset font, float size, FontStyles style, TextAlignmentOptions alignment, Color color)
        {
            var textGo = new GameObject(name, typeof(RectTransform));
            textGo.transform.SetParent(s_Tip, false);
            var text = textGo.AddComponent<TextMeshProUGUI>();
            if (font != null) text.font = font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = alignment;
            text.color = color;
            text.richText = true;
            text.enableWordWrapping = false;
            text.raycastTarget = false;
            return text;
        }

        // The rule under the game's tooltip titles: Separator_left / _middle / _right at half size.
        static void Rule(Transform parent)
        {
            var all = Resources.FindObjectsOfTypeAll<Sprite>();
            Sprite[] parts = new[] { "Separator_left", "Separator_middle", "Separator_right" }
                .Select(n => all.FirstOrDefault(sp => sp.name == n)).ToArray();
            if (parts.Any(p => p == null)) return;
            const float height = 12f;
            var go = new GameObject("Separator", typeof(RectTransform), typeof(HorizontalLayoutGroup), typeof(LayoutElement));
            go.transform.SetParent(parent, false);
            var row = go.GetComponent<HorizontalLayoutGroup>();
            row.childControlWidth = true;
            row.childControlHeight = true;
            row.childForceExpandWidth = false;
            row.childForceExpandHeight = true;
            var size = go.GetComponent<LayoutElement>();
            size.preferredHeight = size.minHeight = height;
            size.flexibleWidth = 1f;
            for (int i = 0; i < 3; i++)
            {
                var part = new GameObject("Part", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
                part.transform.SetParent(go.transform, false);
                Sprite sp = parts[i];
                var image = part.GetComponent<Image>();
                image.sprite = Sprite.Create(sp.texture, sp.textureRect, new Vector2(0.5f, 0.5f), sp.pixelsPerUnit * 2f, 0, SpriteMeshType.FullRect, sp.border);
                image.type = i == 1 ? Image.Type.Simple : Image.Type.Sliced;
                image.raycastTarget = false;
                var element = part.GetComponent<LayoutElement>();
                if (i == 1)
                {
                    element.preferredWidth = element.minWidth = height * 50f / 23f;
                }
                else
                {
                    element.flexibleWidth = 1f;
                    element.minWidth = 32f;
                }
            }
        }

        static string TipTitle()
        {
            try
            {
                return Kingmaker.UI.Common.UIUtility.GetSaberBookFormat(Strings.CameraTitle);
            }
            catch (Exception)
            {
                return Strings.CameraTitle;
            }
        }

        static string TipText()
        {
            Settings s = Main.Settings;
            return string.Format(Strings.PcRotateTip, "Alt+" + KeyName(s.RotateLeftKey), "Alt+" + KeyName(s.RotateRightKey)) + "\n"
                + string.Format(Strings.PcNorthTip, KeyName(s.NorthKey)) + "\n"
                + Strings.PcPanTip;
        }

        internal static string KeyName(KeyCode key)
        {
            string name = key.ToString();
            return name.StartsWith("Alpha") ? name.Substring(5) : name;
        }

        // ---------- conflicts with the game's key bindings ----------

        // Names of the game actions bound to the key (with Alt when alt is set, without Ctrl and Shift).
        internal static List<string> Conflicts(KeyCode key, bool alt)
        {
            var found = new List<string>();
            try
            {
                object root = SettingsRoot.Instance;
                if (root == null) return found;
                foreach (FieldInfo field in root.GetType().GetFields(BindingFlags.Public | BindingFlags.Instance))
                {
                    var bind = field.GetValue(root) as SettingsEntityKeybind;
                    if (bind == null) continue;
                    for (int i = 0; i < 2; i++)
                    {
                        BindingKeysData data = bind.GetBinding(i);
                        if (data == null || data.Key != key || data.IsAltDown != alt || data.IsCtrlDown || data.IsShiftDown) continue;
                        string name = null;
                        try
                        {
                            name = BindName != null ? BindName.GetValue(bind, null) as string : null;
                        }
                        catch (Exception) { }
                        found.Add(string.IsNullOrEmpty(name) ? field.Name : name);
                        break;
                    }
                }
            }
            catch (Exception e)
            {
                Main.Log?.Warning("Key conflict check failed: " + e.Message);
            }
            return found;
        }
    }

    sealed class CompassEvents : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public Action Click;
        public Action Enter;
        public Action Exit;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Left && Click != null) Click();
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (Enter != null) Enter();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (Exit != null) Exit();
        }

        void OnDisable()
        {
            if (Exit != null) Exit();
        }
    }

    [HarmonyPatch(typeof(CameraRig), "TickScroll")]
    static class PcDragPatch
    {
        static void Postfix(CameraRig __instance)
        {
            try
            {
                PcMode.TickDrag(__instance);
            }
            catch (Exception e)
            {
                Main.Log?.Error(e.ToString());
            }
        }
    }

    // The PC local map draws an axis-aligned frame; it is replaced by the same rotated outline as on the console map.
    [HarmonyPatch(typeof(PcLocalMap), "OnShow")]
    static class PcLocalMapFramePatch
    {
        static void Postfix(PcLocalMap __instance)
        {
            try
            {
                if (__instance.Frame == null || __instance.Image == null) return;
                LocalMapFramePatch.AddOutline(__instance.Frame, __instance.Image, (RectTransform)__instance.Image.transform);
            }
            catch (Exception e)
            {
                Main.Log?.Error(e.ToString());
            }
        }
    }
}
