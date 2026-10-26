using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
#if KINGMAKER
using Kingmaker.Assets.Console.GamepadInput;
using Kingmaker.Blueprints.Console;
#else
using Owlcat.Runtime.UI.ConsoleTools.GamepadInput;
#endif
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace LearnScrolls
{
    // The mod's window, in the look and with the controls of the buff menu (Buff It 2 The Limit, Groups): the game's
    // paper, its book font, button hints under the window as under the game's Formation window.
    //   Characters: every member of the party with its portrait and how many spells it can learn; a pick asks the game's
    //     dialog with the list and learns after the confirmation. With a gamepad also the whole party (in mouse mode that
    //     is the first button over the game's menu).
    //   Settings: the rules of the share-out, Defaults.
    //   Help.
    // Gamepad buttons go through the game's console input layer (the world and the HUD are blocked meanwhile), directions
    // are polled from the Rewired player; keys are polled and the game's own hotkeys are off. Start brings the game's menu
    // over the window: the window hides under it. Esc and the right mouse button close the window.
    internal class Window : MonoBehaviour
    {
        enum Page { Characters, Settings, Help }

        const string LayerName = "LearnScrolls_Window";
        const float Width = 920f;
        const float PortraitSize = 46f;
        const float HelpHeight = 560f;
        const float HelpStep = 40f;
        const float StickThreshold = 0.5f;
        static readonly string[] CriteriaFields =
        {
            "CanCastNow", "NotOpposition", "HigherCasterLevel", "SpecialistSchool", "SchoolFocus",
            "HigherCastingStat", "MagusTouch", "FewerKnown", "LowerSpellLevel", "MainCharacterFirst"
        };

        static Window s_Instance;

        GameObject m_Overlay;
        Canvas m_Canvas;
        TMP_FontAsset m_Font;
        bool m_BuiltForKeyboard;
        TextMeshProUGUI m_Tabs;
        GameObject[] m_Pages;
        Transform m_CharList;
        TextMeshProUGUI m_Status;
        Transform m_SetList;
        TextMeshProUGUI m_HelpText;
        ModBar.ModScrollbar m_HelpBar;
        float m_HelpOffset;
        readonly List<Row> m_CharRows = new List<Row>();
        readonly List<Row> m_SetRows = new List<Row>();
        readonly List<Entry> m_Entries = new List<Entry>();

        Page m_Page;
        int m_CharCursor;
        int m_SetCursor;

        IDisposable m_Layer;
        Kingmaker.UI.KeyboardAccess m_Keyboard;
        bool m_Covered;
        bool m_Asking;
        int m_IgnoreUntil;
        readonly Repeat m_Vertical = new Repeat();
        readonly Dictionary<RewiredActionType, int> m_Fired = new Dictionary<RewiredActionType, int>();
        RewiredActionType m_HeldDPad;
        int m_HeldDPadFrame = -10;
        Vector2 m_Stick;
        int m_StickFrame = -10;

        class Entry
        {
            internal UnitEntityData Unit;
            internal string Name;
            internal Sprite Icon;
            internal string Value;
        }

        class Row
        {
            internal GameObject Go;
            internal Image Bg;
            internal Image Icon;
            internal TextMeshProUGUI Text;
            internal TextMeshProUGUI Value;
            internal Pointer Pointer;
            internal ModBar.ModControl Control;
        }

        // The switches and the button of the settings, in the look of the game's own.
#if KINGMAKER
        const float ControlWidth = 230f, ControlHeight = 32f;
#else
        const float ControlWidth = 210f, ControlHeight = 34f;
#endif

        internal static bool IsOpen
        {
            get { return s_Instance != null && s_Instance.gameObject.activeSelf; }
        }

        internal static bool KeyboardMode
        {
            get { return Game.Instance != null && !Game.Instance.IsControllerGamepad; }
        }

        internal static void Toggle()
        {
            try
            {
                if (IsOpen) s_Instance.Close();
                else if (!ModWindows.ModWindow.OtherOpen(Main.Id)) Open();
            }
            catch (Exception e)
            {
                Main.Error(e, "Window.Toggle");
            }
        }

        static void Open()
        {
            if (s_Instance != null && s_Instance.m_BuiltForKeyboard != KeyboardMode)
            {
                var old = s_Instance;
                s_Instance = null;
                Destroy(old.m_Overlay);
            }
            if (s_Instance == null) s_Instance = Build();
            var w = s_Instance;
            w.m_Overlay.SetActive(true);
            w.gameObject.SetActive(true);
            w.m_Canvas.enabled = true;
            w.m_Covered = false;
            w.m_Asking = false;
            w.m_IgnoreUntil = Time.frameCount + 1;
            Pointer.ResetMouse();
            w.PushInput();
            w.BlockKeys();
            w.Status("");
            w.ShowPage(Page.Characters);
            ModWindows.ModWindow.Opened(Main.Id, w.m_Overlay);
        }

        void Close()
        {
            ModWindows.ModWindow.Closed(m_Overlay);
            PopInput();
            UnblockKeys();
            m_Asking = false;
            gameObject.SetActive(false);
            m_Overlay.SetActive(false);
#if KINGMAKER
            VirtualCursor.Restore();
#endif
        }

        void OnDestroy()
        {
            ModWindows.ModWindow.Closed(m_Overlay);
            PopInput();
            UnblockKeys();
            if (s_Instance == this) s_Instance = null;
            if (m_Overlay != null) Destroy(m_Overlay);
        }

        // ---------- learning ----------

        // The first button over the game's menu, and the whole party in the window: the party plan with the game's dialog.
        internal static void LearnAll()
        {
            try
            {
                var lessons = Planner.PartyPlan();
                if (lessons.Count == 0)
                {
                    Learner.Tell(Strings.NothingToLearn);
                    return;
                }
                Learner.Ask(lessons, null);
            }
            catch (Exception e)
            {
                Main.Error(e, "learn all");
            }
        }

        void Learn(Entry entry)
        {
            var lessons = entry.Unit == null ? Planner.PartyPlan() : Planner.UnitPlan(entry.Unit);
            if (lessons.Count == 0)
            {
                Status(Strings.NothingToLearn);
                return;
            }
            // The game's dialog is under the window's canvas: the window steps aside until it closes.
            m_Asking = true;
            m_Canvas.enabled = false;
            PopInput();
            UnblockKeys();
            Learner.Ask(lessons, learned =>
            {
                if (!IsOpen || s_Instance != this) return;
                m_Asking = false;
                m_Canvas.enabled = !m_Covered;
                m_IgnoreUntil = Time.frameCount + 2;
                PushInput();
                BlockKeys();
                RenderCharacters();
                Status(learned > 0 ? Strings.Learned(learned) : "");
            });
        }

        // The line under the list; without a message it takes no room.
        void Status(string text)
        {
            m_Status.text = text;
            m_Status.gameObject.SetActive(text.Length > 0);
        }

        // ---------- pages ----------

        void ShowPage(Page page)
        {
            m_Page = page;
            for (int i = 0; i < m_Pages.Length; i++) m_Pages[i].SetActive(i == (int)page);
            var names = new[] { Strings.TabCharacters, Strings.TabSettings, Strings.TabHelp };
            var text = new System.Text.StringBuilder();
            for (int i = 0; i < names.Length; i++)
            {
                if (i > 0) text.Append("<color=" + Theme.Faint + ">   ·   </color>");
                string name = i == (int)page ? "<b><color=" + Theme.Accent + ">" + names[i] + "</color></b>" : names[i];
                text.Append("<link=\"" + i + "\">" + name + "</link>");
            }
            m_Tabs.text = text.ToString();
            if (page == Page.Characters) RenderCharacters();
            else if (page == Page.Settings) RenderSettings();
            else
            {
                m_HelpText.spriteAsset = Help.IconAsset;
                m_HelpText.text = Help.Build();
                if (Help.FitIcons(m_HelpText)) m_HelpText.text = Help.Build();
                m_HelpOffset = 0;
                PlaceHelp();
                PlaceMarks();
            }
        }

        void SwitchTab(int delta)
        {
            ShowPage((Page)(((int)m_Page + delta + 3) % 3));
        }

        void RenderCharacters()
        {
            m_Entries.Clear();
            if (!KeyboardMode)
            {
                var party = Planner.PartyPlan();
                int spells = party.Count(l => l.Unit != null), recipes = party.Count(l => l.Recipe != null);
                m_Entries.Add(new Entry
                {
                    Name = Strings.WholeParty,
                    Icon = PartyIcon(),
                    Value = Strings.ToLearn(spells) + (recipes > 0 ? ", " + Strings.Recipes(recipes) : "")
                });
            }
            foreach (var unit in Planner.Party())
            {
                var plan = Planner.UnitPlan(unit);
                int other = plan.Count(l => l.Warning != null);
                Sprite portrait = null;
                try
                {
                    var data = unit.Descriptor.Portrait;
                    portrait = data != null ? data.SmallPortrait : null;
                }
                catch (Exception) { }
                m_Entries.Add(new Entry
                {
                    Unit = unit,
                    Name = unit.CharacterName,
                    Icon = portrait,
                    // The scrolls the party's plan gives this character: the counts add up to the party's. The copies the
                    // plan gives to others, which this character could learn as well, after it with a mark.
                    Value = Strings.ToLearn(plan.Count - other) + (other > 0 ? " <color=" + Theme.Partial + ">(" + other + " !)</color>" : "")
                });
            }
            while (m_CharRows.Count < m_Entries.Count)
            {
                int at = m_CharRows.Count;
                var row = MakeRow(m_CharList, 22, PortraitSize);
                row.Pointer.Click = () => ClickCharacter(at);
                m_CharRows.Add(row);
            }
            m_CharCursor = Mathf.Clamp(m_CharCursor, 0, Math.Max(0, m_Entries.Count - 1));
            var shown = m_Entries.FirstOrDefault(e => e.Unit != null && e.Icon != null);
            float portraitAspect = shown != null ? Mathf.Min(1f, shown.Icon.rect.width / shown.Icon.rect.height) : 0f;
            for (int i = 0; i < m_CharRows.Count; i++)
            {
                var row = m_CharRows[i];
                bool used = i < m_Entries.Count;
                row.Go.SetActive(used);
                if (!used) continue;
                var entry = m_Entries[i];
                row.Text.text = entry.Name;
                row.Value.text = entry.Value;
                row.Icon.sprite = entry.Icon;
                row.Icon.enabled = entry.Icon != null;
                row.Icon.color = Color.white;
                // The party pictogram as wide as the portraits under it.
                float scale = 1f;
                if (entry.Unit == null && entry.Icon != null && portraitAspect > 0f)
                {
                    float aspect = entry.Icon.rect.width / entry.Icon.rect.height;
                    scale = Mathf.Min(1f, portraitAspect / Mathf.Min(1f, aspect));
                }
                row.Icon.rectTransform.localScale = new Vector3(scale, scale, 1f);
                row.Bg.color = i == m_CharCursor ? Theme.RowSelected : Theme.Row;
            }
        }

        static Sprite s_PartyIcon;

        // The party's pictogram of the game's party card. Kingmaker's is shipped with the mod: the game loads its card only
        // in some menus. WotR's helmets made lighter, to stand out on the dark window.
        static Sprite PartyIcon()
        {
            if (s_PartyIcon != null) return s_PartyIcon;
#if KINGMAKER
            s_PartyIcon = Art.Picture("party_km.png");
#else
            var helmets = Theme.Find("UI_CardPartySmall");
            if (helmets != null)
            {
                var light = new Color32(240, 214, 170, 255);
                var copy = ModBar.BarLayout.Readable(helmets);
                s_PartyIcon = Art.Smooth(copy, c =>
                {
                    var lit = Color32.Lerp(c, light, 0.4f);
                    lit.a = c.a;
                    return lit;
                });
                UnityEngine.Object.Destroy(copy);
            }
#endif
            return s_PartyIcon;
        }

        void RenderSettings()
        {
            // The rules, the party order, the opposition schools left out, the defaults.
            int count = CriteriaFields.Length + 3;
            while (m_SetRows.Count < count)
            {
                int at = m_SetRows.Count;
                var row = MakeRow(m_SetList, 20, 0);
                row.Pointer.Click = () => ClickSetting(at);
                if (at == CriteriaFields.Length) row.Pointer.Live = () => false;
                row.Control = ModBar.ModControl.Create((RectTransform)row.Go.transform, row.Text, ControlWidth, ControlHeight);
                row.Control.Step = d => StepSetting(at, d);
                row.Control.Press = () => ClickSetting(at);
                m_SetRows.Add(row);
            }
            var labels = Strings.Criteria;
            for (int i = 0; i < count; i++)
            {
                var row = m_SetRows[i];
                row.Value.text = "";
                if (i < CriteriaFields.Length)
                {
                    bool on = Criterion(i);
                    row.Text.text = (i + 1) + ". " + labels[i];
                    row.Text.color = on ? Theme.Text : Theme.Dim;
                    row.Control.SetSwitch(on, Strings.SwitchOn, Strings.SwitchOff);
                }
                else if (i == CriteriaFields.Length)
                {
                    row.Text.text = (i + 1) + ". " + Strings.PartyOrder;
                    row.Text.color = Theme.Dim;
                    row.Control.Hide();
                }
                else if (i == CriteriaFields.Length + 1)
                {
                    bool on = Main.Settings.ExcludeOpposition;
                    row.Text.text = Strings.ExcludeOpposition;
                    row.Text.color = on ? Theme.Text : Theme.Dim;
                    row.Control.SetSwitch(on, Strings.SwitchOn, Strings.SwitchOff);
                }
                else
                {
                    row.Text.text = "<b>" + Strings.Defaults + "</b>";
                    row.Text.color = Theme.Text;
                    row.Control.SetButton(Strings.Restore);
                }
                row.Bg.color = i == m_SetCursor ? Theme.RowSelected : i == CriteriaFields.Length ? Theme.RowOutside : Theme.Row;
            }
        }

        static bool Criterion(int i)
        {
            return (bool)typeof(Settings).GetField(CriteriaFields[i]).GetValue(Main.Settings);
        }

        void ChangeSetting()
        {
            if (m_SetCursor < CriteriaFields.Length)
                typeof(Settings).GetField(CriteriaFields[m_SetCursor]).SetValue(Main.Settings, !Criterion(m_SetCursor));
            else if (m_SetCursor == CriteriaFields.Length + 1)
                Main.Settings.ExcludeOpposition = !Main.Settings.ExcludeOpposition;
            else if (m_SetCursor == CriteriaFields.Length + 2)
                Main.Settings.Reset();
            else return;
            Main.Save();
            RenderSettings();
        }

        // The marks of the lists over the room the help leaves for them: centred on its links, as tall as the line.
        void PlaceMarks()
        {
            var text = m_HelpText;
            for (int i = text.transform.childCount - 1; i >= 0; i--)
            {
                var child = text.transform.GetChild(i);
                if (child.name == "Mark") Destroy(child.gameObject);
            }
            text.ForceMeshUpdate();
            var info = text.textInfo;
            for (int l = 0; l < info.linkCount; l++)
            {
                var link = info.linkInfo[l];
                string id = link.GetLinkID();
                if (id != "LIKE" && id != "DISLIKE" || link.linkTextLength == 0) continue;
                var first = info.characterInfo[link.linkTextfirstCharacterIndex];
                var last = info.characterInfo[link.linkTextfirstCharacterIndex + link.linkTextLength - 1];
                float left = first.origin, right = last.xAdvance;
                float size = first.pointSize * 1.3f;
                float middle = first.baseLine + first.pointSize * 0.3f;
                var go = new GameObject("Mark", typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(text.transform, false);
                go.AddComponent<LayoutElement>().ignoreLayout = true;
                // The text's local space starts at its pivot.
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.anchorMin = rect.anchorMax = text.rectTransform.pivot;
                rect.anchoredPosition = new Vector2((left + right) / 2f, middle);
                rect.sizeDelta = new Vector2(size, size);
                var image = go.AddComponent<Image>();
                image.sprite = LessonList.Mark(id == "LIKE");
                image.preserveAspect = true;
                image.raycastTarget = false;
            }
        }

        void PlaceHelp()
        {
            var rect = (RectTransform)m_HelpText.transform;
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            float max = Mathf.Max(0, rect.rect.height - HelpHeight);
            m_HelpOffset = Mathf.Clamp(m_HelpOffset, 0, max);
            rect.anchoredPosition = new Vector2(rect.anchoredPosition.x, m_HelpOffset);
            m_HelpBar.Set(m_HelpOffset, HelpHeight, rect.rect.height);
        }

        // ---------- actions ----------

        void Fire(RewiredActionType action)
        {
            if (m_Covered || m_Asking || Time.frameCount <= m_IgnoreUntil) return;
            int last;
            if (m_Fired.TryGetValue(action, out last) && Time.frameCount - last <= 2) return;
            m_Fired[action] = Time.frameCount;
            if (action == RewiredActionType.Confirm) OnConfirm();
            else if (action == RewiredActionType.Decline) Close();
            else if (action == RewiredActionType.LeftUp) SwitchTab(-1);
            else if (action == RewiredActionType.RightUp) SwitchTab(1);
        }

        void OnConfirm()
        {
            if (m_Page == Page.Characters)
            {
                if (m_CharCursor < m_Entries.Count) Learn(m_Entries[m_CharCursor]);
            }
            else if (m_Page == Page.Settings) ChangeSetting();
        }

        void MoveVertical(int delta)
        {
            if (m_Page == Page.Characters)
            {
                if (m_Entries.Count == 0) return;
                m_CharCursor = Mathf.Clamp(m_CharCursor + delta, 0, m_Entries.Count - 1);
                RenderCharacters();
            }
            else if (m_Page == Page.Settings)
            {
                int next = m_SetCursor + delta;
                // The party order is always on: not a stop.
                if (next == CriteriaFields.Length) next += delta;
                m_SetCursor = Mathf.Clamp(next, 0, CriteriaFields.Length + 2);
                RenderSettings();
            }
            else
            {
                m_HelpOffset += delta * HelpStep;
                PlaceHelp();
            }
        }

        void ClickCharacter(int at)
        {
            if (at >= m_Entries.Count) return;
            m_CharCursor = at;
            RenderCharacters();
            Learn(m_Entries[at]);
        }

        // A side of a switch: on to the right, off to the left; nothing when it is so already.
        void StepSetting(int at, int direction)
        {
            bool on = direction > 0;
            if (at < CriteriaFields.Length ? Criterion(at) == on : at != CriteriaFields.Length + 1 || Main.Settings.ExcludeOpposition == on)
            {
                m_SetCursor = at;
                RenderSettings();
                return;
            }
            ClickSetting(at);
        }

        void ClickSetting(int at)
        {
            if (at == CriteriaFields.Length) return;
            m_SetCursor = at;
            ChangeSetting();
        }

        void ClickTabs(PointerEventData e)
        {
            int link = TMP_TextUtilities.FindIntersectingLink(m_Tabs, e.position, ModBar.BarLayout.Cam(m_Tabs));
            if (link >= 0) ShowPage((Page)int.Parse(m_Tabs.textInfo.linkInfo[link].GetLinkID()));
        }

        void OnWheel(float y)
        {
            if (Mathf.Abs(y) < 0.01f || m_Covered || m_Asking) return;
            if (m_Page == Page.Help)
            {
                m_HelpOffset -= y * HelpStep * 2;
                PlaceHelp();
            }
        }

        // ---------- every frame ----------

        void Update()
        {
            try
            {
                if (Kingmaker.EntitySystem.Persistence.LoadingProcess.Instance.IsLoadingInProcess || !Main.InGame)
                {
                    Close();
                    return;
                }
                if (m_Asking) return;
                bool over = GameMenuOver();
                if (over != m_Covered)
                {
                    m_Covered = over;
                    m_Canvas.enabled = !over;
                    if (!over) m_IgnoreUntil = Time.frameCount + 2;
                }
                if (m_Covered || Time.frameCount <= m_IgnoreUntil) return;
                Pointer.WatchMouse();
                PollButtons();
                PollKeys();
                PollDirections();
            }
            catch (Exception e)
            {
                Main.Error(e, "Window.Update");
            }
        }

        // Start opens the game's menu through its base layer, which stays bound under the window's layer: a layer over
        // the window's is the game's menu or one of its windows.
        bool GameMenuOver()
        {
            if (!Game.Instance.IsControllerGamepad || m_Layer == null) return false;
            var layer = GamePad.Instance != null ? GamePad.Instance.CurrentInputLayer : null;
            return layer != null && layer.ContextName != LayerName;
        }

        void PushInput()
        {
            if (m_Layer != null || !Game.Instance.IsControllerGamepad) return;
            var pad = GamePad.Instance;
            if (pad == null) return;
            var layer = new InputLayer { ContextName = LayerName };
            foreach (var action in Buttons)
            {
                var a = action;
                layer.AddButton(_ => Fire(a), (int)a);
            }
            foreach (var action in DPad)
            {
                var a = action;
                layer.AddButton(_ => { m_HeldDPad = a; m_HeldDPadFrame = Time.frameCount; }, (int)a, Rewired.InputActionEventType.ButtonPressed);
            }
            layer.AddAxis2D((_, v) => { m_Stick = v; m_StickFrame = Time.frameCount; }, (int)RewiredActionType.LeftStickX, (int)RewiredActionType.LeftStickY, false);
            m_Layer = pad.PushLayer(layer);
#if KINGMAKER
            VirtualCursor.Hide();
#endif
        }

        void PopInput()
        {
            if (m_Layer != null) m_Layer.Dispose();
            m_Layer = null;
        }

        static readonly RewiredActionType[] Buttons =
        {
            RewiredActionType.Confirm, RewiredActionType.Decline, RewiredActionType.LeftUp, RewiredActionType.RightUp
        };

        static readonly RewiredActionType[] DPad =
        {
            RewiredActionType.DPadUp, RewiredActionType.DPadDown, RewiredActionType.DPadLeft, RewiredActionType.DPadRight
        };

        // The game's own hotkeys (pause, windows on letters) stay off while the window is open.
        void BlockKeys()
        {
            if (m_Keyboard != null) return;
            var keyboard = Game.Instance != null ? Game.Instance.Keyboard : null;
            if (keyboard == null) return;
            keyboard.Disabled.Value = true;
            m_Keyboard = keyboard;
        }

        void UnblockKeys()
        {
            if (m_Keyboard == null) return;
            m_Keyboard.Disabled.Value = false;
            m_Keyboard = null;
        }

        void PollButtons()
        {
            var player = GamePad.Instance != null ? GamePad.Instance.Player : null;
            if (player == null) return;
            foreach (var action in Buttons)
            {
                bool down = false;
                try { down = player.GetButtonDown((int)action); }
                catch (Exception) { }
                if (down) Fire(action);
            }
        }

        static readonly KeyCode[] ConfirmKeys = { KeyCode.E, KeyCode.Return, KeyCode.KeypadEnter, KeyCode.Space };
        static readonly KeyCode[] DeclineKeys = { KeyCode.Q, KeyCode.Backspace, KeyCode.Escape };
        static readonly KeyCode[] PrevKeys = { KeyCode.X, KeyCode.PageUp };
        static readonly KeyCode[] NextKeys = { KeyCode.C, KeyCode.PageDown };
        static readonly KeyCode[] UpKeys = { KeyCode.UpArrow, KeyCode.W };
        static readonly KeyCode[] DownKeys = { KeyCode.DownArrow, KeyCode.S };

        static bool Pressed(KeyCode[] keys)
        {
            return keys.Any(Input.GetKeyDown);
        }

        static bool Held(KeyCode[] keys)
        {
            return keys.Any(Input.GetKey);
        }

        // On release: the press and the release both land on the window, so the game never sees half a click.
        void PollKeys()
        {
            if (Pressed(ConfirmKeys)) Fire(RewiredActionType.Confirm);
            if (Pressed(DeclineKeys) || Input.GetMouseButtonUp(1)) Fire(RewiredActionType.Decline);
            if (Pressed(PrevKeys)) Fire(RewiredActionType.LeftUp);
            if (Pressed(NextKeys)) Fire(RewiredActionType.RightUp);
        }

        void PollDirections()
        {
            int v = 0;
            int frame = Time.frameCount;
            if (frame - m_HeldDPadFrame <= 1)
            {
                if (m_HeldDPad == RewiredActionType.DPadUp) v = -1;
                else if (m_HeldDPad == RewiredActionType.DPadDown) v = 1;
            }
            if (v == 0 && frame - m_StickFrame <= 2)
            {
                if (m_Stick.y > StickThreshold) v = -1;
                else if (m_Stick.y < -StickThreshold) v = 1;
            }
            var player = GamePad.Instance != null ? GamePad.Instance.Player : null;
            if (v == 0 && player != null)
            {
                if (player.GetButton((int)RewiredActionType.DPadUp)) v = -1;
                else if (player.GetButton((int)RewiredActionType.DPadDown)) v = 1;
                else
                {
                    float y = player.GetAxis((int)RewiredActionType.LeftStickY);
                    if (y > StickThreshold) v = -1;
                    else if (y < -StickThreshold) v = 1;
                }
            }
            if (v == 0) v = Held(UpKeys) ? -1 : Held(DownKeys) ? 1 : 0;
            int step = m_Vertical.Step(v);
            if (step != 0) MoveVertical(step);
        }

        // A direction held: one step at once, then after a pause steps in a row.
        class Repeat
        {
            int m_Dir;
            float m_Next;

            internal int Step(int dir)
            {
                float now = Time.unscaledTime;
                if (dir == 0)
                {
                    m_Dir = 0;
                    return 0;
                }
                if (dir != m_Dir)
                {
                    m_Dir = dir;
                    m_Next = now + 0.35f;
                    return dir;
                }
                if (now >= m_Next)
                {
                    m_Next = now + 0.08f;
                    return dir;
                }
                return 0;
            }
        }

        // ---------- building ----------

        static Window Build()
        {
            var font = Theme.Font;
            var overlay = new GameObject("LearnScrolls_Overlay", typeof(RectTransform));
            DontDestroyOnLoad(overlay);
            var canvas = overlay.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 32000;
            ModBar.BarLayout.ThroughGameCamera(canvas);
            var scaler = overlay.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.matchWidthOrHeight = 0.5f;
            // Clicks land on the window instead of the world: the game skips the click when the pointer is over UI.
            overlay.AddComponent<GraphicRaycaster>();
            var blocker = new GameObject("ClickBlocker", typeof(RectTransform));
            blocker.transform.SetParent(overlay.transform, false);
            Stretch((RectTransform)blocker.transform, 0);
            blocker.AddComponent<Image>().color = Color.clear;
            blocker.AddComponent<Wheel>().Scroll = y => { if (s_Instance != null) s_Instance.OnWheel(y); };

            var root = new GameObject("LearnScrolls_Window", typeof(RectTransform));
            root.SetActive(false);
            root.transform.SetParent(overlay.transform, false);
            var rect = (RectTransform)root.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0, (Theme.HintGap + Theme.HintRowHeight) / 2);
            rect.sizeDelta = new Vector2(Width, 0);
            Theme.Panel(root.AddComponent<Image>());
            Vertical(root.AddComponent<VerticalLayoutGroup>(), Theme.Paper ? Theme.PanelPadding : new RectOffset(28, 28, 22, 22), 10);
            root.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            root.AddComponent<Wheel>().Scroll = y => { if (s_Instance != null) s_Instance.OnWheel(y); };

            var w = root.AddComponent<Window>();
            w.m_Overlay = overlay;
            w.m_Canvas = canvas;
            w.m_Font = font;
            w.m_BuiltForKeyboard = KeyboardMode;
            ModBar.KeyCapRoot.Attach(root);
            var title = w.Text(root.transform, Theme.Initial(Strings.Title), 30, FontStyles.Bold, Theme.Title);
            title.alignment = TextAlignmentOptions.Center;
            Theme.Separator(root.transform);

            var tabRow = Horizontal(root.transform, "Tabs", 16, TextAnchor.MiddleCenter);
            Hintable(w.Icon(tabRow.transform, RewiredActionType.LeftUp, 32, null), RewiredActionType.LeftUp, null, null);
            w.m_Tabs = w.Text(tabRow.transform, "", 22, FontStyles.Normal, Theme.Text);
            w.m_Tabs.enableWordWrapping = false;
            AddPointer(w.m_Tabs.gameObject, false).ClickEvent = e => { if (s_Instance != null) s_Instance.ClickTabs(e); };
            Hintable(w.Icon(tabRow.transform, RewiredActionType.RightUp, 32, null), RewiredActionType.RightUp, null, null);

            // characters
            var chars = Container(root.transform, "Characters", 8);
            w.m_CharList = Container(chars.transform, "Rows", 5).transform;
            w.m_Status = w.Text(chars.transform, "", 19, FontStyles.Normal, Theme.Title);
            w.HintBar(chars.transform,
                new[] { RewiredActionType.DPadVertical }, Strings.HintSelect,
                new[] { RewiredActionType.Confirm }, Strings.HintLearn,
                new[] { RewiredActionType.LeftUp, RewiredActionType.RightUp }, Strings.HintTab,
                new[] { RewiredActionType.Decline }, Strings.HintClose);

            // settings
            var set = Container(root.transform, "Settings", 8);
            w.Text(set.transform, Strings.CriteriaNote, 19, FontStyles.Normal, Theme.Dim);
            w.m_SetList = Container(set.transform, "Rows", 4).transform;
            w.HintBar(set.transform,
                new[] { RewiredActionType.DPadVertical }, Strings.HintSelect,
                new[] { RewiredActionType.Confirm }, Strings.HintChange,
                new[] { RewiredActionType.LeftUp, RewiredActionType.RightUp }, Strings.HintTab,
                new[] { RewiredActionType.Decline }, Strings.HintClose);

            // help
            var help = Container(root.transform, "Help", 8);
            var viewport = new GameObject("Viewport", typeof(RectTransform));
            viewport.transform.SetParent(help.transform, false);
            viewport.AddComponent<RectMask2D>();
            var viewportSize = viewport.AddComponent<LayoutElement>();
            viewportSize.preferredHeight = HelpHeight;
            viewportSize.flexibleWidth = 1;
            w.m_HelpText = w.Text(viewport.transform, "", 20, FontStyles.Normal, Theme.Text);
            w.m_HelpText.lineSpacing = 6;
            var helpRect = (RectTransform)w.m_HelpText.transform;
            helpRect.anchorMin = new Vector2(0, 1);
            helpRect.anchorMax = new Vector2(1, 1);
            helpRect.pivot = new Vector2(0.5f, 1);
            helpRect.offsetMin = new Vector2(4, 0);
            helpRect.offsetMax = new Vector2(-ModBar.ModScrollbar.Width - 4, 0);
            w.m_HelpText.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            w.m_HelpBar = ModBar.ModScrollbar.Create((RectTransform)viewport.transform, Theme.Find);
            w.m_HelpBar.Moved = offset =>
            {
                w.m_HelpOffset = offset;
                w.PlaceHelp();
            };
            w.HintBar(help.transform,
                new[] { RewiredActionType.DPadVertical }, Strings.HintScroll,
                new[] { RewiredActionType.LeftUp, RewiredActionType.RightUp }, Strings.HintTab,
                new[] { RewiredActionType.Decline }, Strings.HintClose);

            w.m_Pages = new[] { chars, set, help };
            foreach (var page in w.m_Pages) page.SetActive(false);
            return w;
        }

        Row MakeRow(Transform parent, float size, float icon)
        {
            var go = new GameObject("Row", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var bg = go.AddComponent<Image>();
            bg.color = Theme.Row;
            var layout = go.AddComponent<HorizontalLayoutGroup>();
#if KINGMAKER
            // Kingmaker's paper window is tall already: its rows closer.
            layout.padding = new RectOffset(14, 14, 3, 3);
#else
            layout.padding = new RectOffset(14, 14, 6, 6);
#endif
            layout.spacing = 14;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            var row = new Row { Go = go, Bg = bg, Pointer = AddPointer(go, true) };
            var slot = new GameObject("Icon", typeof(RectTransform));
            slot.transform.SetParent(go.transform, false);
            var element = slot.AddComponent<LayoutElement>();
            element.preferredWidth = element.minWidth = icon;
            element.preferredHeight = element.minHeight = icon;
            row.Icon = slot.AddComponent<Image>();
            row.Icon.preserveAspect = true;
            row.Icon.raycastTarget = false;
            row.Icon.enabled = false;
            slot.SetActive(icon > 0);
            row.Text = Text(go.transform, "", size, FontStyles.Normal, Theme.Text);
            row.Text.enableWordWrapping = false;
            row.Text.overflowMode = TextOverflowModes.Ellipsis;
            row.Text.gameObject.AddComponent<LayoutElement>().flexibleWidth = 1;
            row.Value = Text(go.transform, "", size - 2, FontStyles.Normal, Theme.Dim);
            row.Value.enableWordWrapping = false;
            row.Value.alignment = TextAlignmentOptions.MidlineRight;
            return row;
        }

        // Button hints under the window as under the game's Formation window: pairs of the buttons and their label.
        void HintBar(Transform page, params object[] items)
        {
            var panel = page.parent;
            var row = new GameObject("Hints", typeof(RectTransform));
            row.transform.SetParent(panel, false);
            row.AddComponent<LayoutElement>().ignoreLayout = true;
            var rect = (RectTransform)row.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0);
            rect.pivot = new Vector2(0.5f, 1);
            rect.anchoredPosition = new Vector2(0, -Theme.HintGap);
            rect.sizeDelta = new Vector2(0, Theme.HintRowHeight);
            ModBar.AbovePortraits.Attach((RectTransform)panel, rect, Theme.HintGap);
            if (Theme.HintPlate) Theme.HintBackground(row.AddComponent<Image>());
            var layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.padding = Theme.HintPadding;
            layout.spacing = Theme.HintSpacing;
            layout.childAlignment = TextAnchor.MiddleCenter;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            row.AddComponent<ContentSizeFitter>().horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
            page.gameObject.AddComponent<PageHints>().Row = row;
            row.SetActive(false);
            for (int i = 0; i + 1 < items.Length; i += 2)
            {
                var buttons = (RewiredActionType[])items[i];
                var label = (string)items[i + 1];
                var item = Horizontal(row.transform, "Hint", 6, TextAnchor.MiddleLeft);
                if (!Theme.HintPlate)
                {
                    // WotR: a dark strip behind every hint, longer than the hint on both sides, as the game draws it.
                    ((HorizontalLayoutGroup)item.GetComponent<LayoutGroup>()).padding = new RectOffset(4, 14, 0, 0);
                    var strip = new GameObject("Background", typeof(RectTransform));
                    strip.transform.SetParent(item.transform, false);
                    strip.AddComponent<LayoutElement>().ignoreLayout = true;
                    var stripRect = (RectTransform)strip.transform;
                    stripRect.anchorMin = new Vector2(0, 0.5f);
                    stripRect.anchorMax = new Vector2(1, 0.5f);
                    stripRect.sizeDelta = new Vector2(24, Theme.HintSpriteHeight);
                    stripRect.anchoredPosition = new Vector2(2, 0);
                    Theme.HintBackground(strip.AddComponent<Image>());
                }
                var icons = buttons.Select(b => Icon(item.transform, b, Theme.HintIconSize, Theme.HintLabel)).ToList();
                var text = Text(item.transform, label, Theme.HintLabelSize, Theme.HintLabelStyle, Theme.HintLabel);
                text.enableWordWrapping = false;
                if (buttons.Length == 1) Hintable(item, buttons[0], text, Theme.HintHover);
                else for (int b = 0; b < buttons.Length; b++) Hintable(icons[b], buttons[b], null, Theme.HintHover);
            }
        }

        // The console icon of the button, or its key in keyboard mode; a bracketed letter when the game has no icon.
        GameObject Icon(Transform parent, RewiredActionType button, float size, Color? color)
        {
            if (m_BuiltForKeyboard)
            {
                return ModBar.KeyCaps.Create(parent, KeyLabel(button), size, m_Font);
            }
            Sprite sprite = null;
            try { sprite = GamePadIcons.Instance != null ? GamePadIcons.Instance.GetIcon(button) : null; }
            catch (Exception) { }
            if (sprite == null)
            {
                var text = Text(parent, "[" + PadLabel(button) + "]", 19, FontStyles.Bold, color ?? Theme.Title);
                text.enableWordWrapping = false;
                return text.gameObject;
            }
            var go = new GameObject("Icon", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var image = go.AddComponent<Image>();
            image.sprite = sprite;
            image.preserveAspect = true;
            var element = go.AddComponent<LayoutElement>();
            element.preferredWidth = element.preferredHeight = size;
            return go;
        }

        internal static string KeyLabel(RewiredActionType button)
        {
            switch (button)
            {
                case RewiredActionType.Confirm: return "E";
                case RewiredActionType.Decline: return "Q";
                case RewiredActionType.LeftUp: return "X";
                case RewiredActionType.RightUp: return "C";
                case RewiredActionType.DPadVertical: return "W/S";
                case RewiredActionType.DPadUp: return "W";
                case RewiredActionType.DPadDown: return "S";
                default: return button.ToString();
            }
        }

        internal static string PadLabel(RewiredActionType button)
        {
            switch (button)
            {
                case RewiredActionType.Confirm: return "A";
                case RewiredActionType.Decline: return "B";
                case RewiredActionType.LeftUp: return "LB";
                case RewiredActionType.RightUp: return "RB";
                default: return "D-pad";
            }
        }

        static readonly Color IconHover = new Color(1f, 0.82f, 0.5f);

        // A hint or icon of a button that does something: a click does what the button does.
        static void Hintable(GameObject go, RewiredActionType button, Graphic label, Color? hover)
        {
            if (button != RewiredActionType.Confirm && button != RewiredActionType.Decline
                && button != RewiredActionType.LeftUp && button != RewiredActionType.RightUp) return;
            var pointer = AddPointer(go, false);
            var mark = label != null ? label : go.GetComponent<Graphic>();
            if (mark != null) pointer.Mark(mark, mark is Image ? IconHover : hover ?? Theme.Text);
            pointer.Click = () => { if (s_Instance != null) s_Instance.Fire(button); };
        }

        static Pointer AddPointer(GameObject go, bool tint)
        {
            var pointer = go.AddComponent<Pointer>();
            if (tint)
            {
                var hover = new GameObject("Hover", typeof(RectTransform));
                hover.transform.SetParent(go.transform, false);
                hover.transform.SetAsFirstSibling();
                hover.AddComponent<LayoutElement>().ignoreLayout = true;
                Stretch((RectTransform)hover.transform, 0);
                var image = hover.AddComponent<Image>();
                image.color = Theme.RowHover;
                image.raycastTarget = false;
                image.enabled = false;
                pointer.Tint = image;
            }
            return pointer;
        }

        TextMeshProUGUI Text(Transform parent, string value, float size, FontStyles style, Color color)
        {
            var go = new GameObject("Text", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var text = go.AddComponent<TextMeshProUGUI>();
            if (m_Font != null) text.font = m_Font;
            text.fontSize = size;
            text.fontStyle = style;
            text.color = color;
            text.richText = true;
            text.enableWordWrapping = true;
            text.text = value;
            return text;
        }

        static void Vertical(VerticalLayoutGroup layout, RectOffset padding, float spacing)
        {
            layout.padding = padding;
            layout.spacing = spacing;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
        }

        static GameObject Container(Transform parent, string name, float spacing)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            Vertical(go.AddComponent<VerticalLayoutGroup>(), new RectOffset(0, 0, 0, 0), spacing);
            return go;
        }

        static GameObject Horizontal(Transform parent, string name, float spacing, TextAnchor alignment)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = alignment;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = layout.childForceExpandHeight = false;
            return go;
        }

        static void Stretch(RectTransform rect, float inset)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(inset, inset);
            rect.offsetMax = new Vector2(-inset, -inset);
        }
    }

    // A part of the window the mouse works on. The hover tint is an image of its own over the part's colour.
    internal class Pointer : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        public Action Click;
        public Action<PointerEventData> ClickEvent;
        // False on a row that does nothing: no tint, and clicks do nothing.
        public Func<bool> Live;
        public Image Tint;
        Graphic m_Mark;
        Color m_MarkColor;
        Color m_MarkHover;
        bool m_Over;

        // Rows light up under the pointer only once the real mouse moved while the window is open: a cursor resting
        // over it (the Steam Deck's trackpad mouse) would otherwise show a second selection apart from the window's own.
        static bool s_MouseUsed;
        static Vector3 s_MouseAt;

        public static void ResetMouse()
        {
            s_MouseUsed = false;
            s_MouseAt = Input.mousePosition;
        }

        public static void WatchMouse()
        {
            if (s_MouseUsed || (Input.mousePosition - s_MouseAt).sqrMagnitude <= 4f) return;
            s_MouseUsed = true;
        }

        public void Mark(Graphic graphic, Color hover)
        {
            m_Mark = graphic;
            m_MarkColor = graphic.color;
            m_MarkHover = hover;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            m_Over = true;
            Refresh();
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            m_Over = false;
            Refresh();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button != PointerEventData.InputButton.Left || (Live != null && !Live())) return;
            try
            {
                if (Click != null) Click();
                if (ClickEvent != null) ClickEvent(eventData);
            }
            catch (Exception e)
            {
                Main.Error(e, "Pointer.Click");
            }
        }

        void Update()
        {
            if (m_Over) Refresh();
        }

        void Refresh()
        {
            bool lit = m_Over && s_MouseUsed && (Live == null || Live());
            if (Tint != null && Tint.enabled != lit) Tint.enabled = lit;
            if (m_Mark != null) m_Mark.color = lit ? m_MarkHover : m_MarkColor;
        }

        void OnDisable()
        {
            m_Over = false;
            Refresh();
        }
    }

    // Shows the hint row of a page while the page is shown; the row lives outside the page, under the panel.
    internal class PageHints : MonoBehaviour
    {
        public GameObject Row;

        void OnEnable()
        {
            if (Row != null) Row.SetActive(true);
        }

        void OnDisable()
        {
            if (Row != null) Row.SetActive(false);
        }
    }

    internal class Wheel : MonoBehaviour, IScrollHandler
    {
        public Action<float> Scroll;

        public void OnScroll(PointerEventData eventData)
        {
            if (Scroll != null) Scroll(eventData.scrollDelta.y);
        }
    }
}
