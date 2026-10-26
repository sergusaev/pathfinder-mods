using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints.Console;
using Kingmaker.Kingdom;
using Kingmaker.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

namespace TimeControl
{
    // A message with up to two actions: the result of a coefficient change, the confirmation of a skip.
    class MessageWindow : Modal
    {
        const int VisibleLines = 18;

        List<string> m_Lines;
        int m_Offset;
        TextMeshProUGUI m_Body;
        ModBar.ModScrollbar m_Bar;
        Action m_Confirm;
        Action m_Alt;

        internal static void Show(string title, List<string> lines, string confirmLabel, Action confirm, string altLabel = null, Action alt = null)
        {
            var w = Create<MessageWindow>("Message", 980, title, 32300);
            w.m_Lines = lines;
            w.m_Confirm = confirm;
            w.m_Alt = alt;
            w.m_Body = Ui.Text(w.Panel, "", 21, Theme.Text);
            w.m_Body.lineSpacing = 4;
            w.m_Body.margin = new Vector4(0, 0, ModBar.ModScrollbar.Width + 4, 0);
            w.m_Bar = ModBar.ModScrollbar.Create(w.m_Body.rectTransform, null);
            w.m_Bar.Moved = first => w.Scroll(Mathf.RoundToInt(first) - w.m_Offset);
            var hints = new List<Hint>();
            if (confirm != null) hints.Add(new Hint(RewiredActionType.Confirm, confirmLabel));
            if (alt != null) hints.Add(new Hint(RewiredActionType.Func02, altLabel));
            hints.Add(new Hint(RewiredActionType.Decline, confirm != null ? S.HCancel : S.HOk));
            w.SetHints(hints.ToArray());
            w.Render();
            w.Show();
            Ui.Sound(UISoundType.NewInformation);
        }

        void Render()
        {
            var shown = m_Lines.Skip(m_Offset).Take(VisibleLines).ToList();
            string text = string.Join("\n", shown.ToArray());
            if (m_Offset > 0) text = "<color=" + Theme.Muted + ">· · ·</color>\n" + text;
            if (m_Offset + VisibleLines < m_Lines.Count) text += "\n<color=" + Theme.Muted + ">" + string.Format(S.More, m_Lines.Count - m_Offset - VisibleLines) + "</color>";
            m_Body.text = text;
            m_Bar.Set(m_Offset, VisibleLines, m_Lines.Count);
        }

        protected override void OnConfirm()
        {
            var action = m_Confirm;
            Close();
            if (action != null) action();
        }

        protected override void OnFuncY()
        {
            var action = m_Alt;
            if (action == null) return;
            Close();
            action();
        }

        protected override void OnMove(int direction)
        {
            Scroll(direction);
        }

        void Scroll(int direction)
        {
            int next = Mathf.Clamp(m_Offset + direction, 0, Math.Max(0, m_Lines.Count - VisibleLines));
            if (next == m_Offset) return;
            m_Offset = next;
            Render();
        }

        protected override void OnWheel(float y)
        {
            if (Mathf.Abs(y) > 0.01f) OnMove(y > 0 ? -3 : 3);
        }
    }

    // The alert of a tracked deadline: a sheet in the look of the game's notes, the game paused under it.
    class AlertWindow : Modal
    {
        static AlertWindow s_Instance;
        readonly List<Alert> m_Alerts = new List<Alert>();
        TextMeshProUGUI m_Body;

        internal static void Show(List<Alert> alerts)
        {
            if (s_Instance == null)
            {
                s_Instance = Create<AlertWindow>("Alert", 900, S.AlertTitle, 32200);
                s_Instance.m_Body = Ui.Text(s_Instance.Panel, "", 23, Theme.Text);
                s_Instance.m_Body.lineSpacing = 6;
                s_Instance.m_Alerts.AddRange(alerts);
                s_Instance.Render();
                s_Instance.Show();
            }
            else
            {
                s_Instance.m_Alerts.AddRange(alerts);
                s_Instance.Render();
            }
            Ui.Sound(UISoundType.NewInformation);
        }

        protected override bool PausesGame
        {
            get { return Main.Settings.PauseOnAlert; }
        }

        Alert Single
        {
            get { return m_Alerts.Count == 1 && !m_Alerts[0].Final && Tracker.IsTracked(m_Alerts[0].Key) ? m_Alerts[0] : null; }
        }

        void Render()
        {
            var shown = m_Alerts.Skip(Math.Max(0, m_Alerts.Count - 12)).Select(a => "• " + a.Text).ToList();
            shown.Insert(0, "<color=" + Theme.Muted + ">" + string.Format(S.Today, Timers.Date(Game.Instance.Player.GameTime)) + "</color>");
            m_Body.text = string.Join("\n", shown.ToArray());
            var hints = new List<Hint> { new Hint(RewiredActionType.Confirm, S.HOk), new Hint(RewiredActionType.Func01, S.HOpen) };
            if (Single != null) hints.Add(new Hint(RewiredActionType.Func02, S.HUntrack));
            SetHints(hints.ToArray());
        }

        protected override void OnConfirm()
        {
            Ui.Sound(UISoundType.ButtonClick);
            Close();
        }

        protected override void OnFuncX()
        {
            Close();
            TimersWindow.Open(1);
        }

        protected override void OnFuncY()
        {
            var single = Single;
            if (single == null) return;
            Tracker.Untrack(single.Key);
            Close();
        }

        protected override void OnClosed()
        {
            if (s_Instance == this) s_Instance = null;
        }
    }

    // The deadlines window: all countdowns, the tracked ones, the coefficients and the skip; and the tracking setup.
    class TimersWindow : Modal
    {
        enum Page { Timers, Tracked, Time, Help, Setup }
        const int Tabs = 4;

        class Item
        {
            public string Text;
            public string Right;
            public string Detail;
            public GameTimer Timer;
            public bool Header;
            // The eye before the date: a tracked deadline.
            public bool Eye;
            public Action Confirm;
            public Action<int> Change;
            // The control in the right column, in the look of the game's settings: a slider at Index of Count (Slide
            // sets it), a switch (On; Confirm turns it), a choice (Change steps it) or a button (Confirm).
            public ModBar.ModControl.Kind Control;
            public string Value;
            public int Index, Count;
            public bool On;
            public Action<int> Slide;
        }

        // The controls' place: as high as a row's text, so the sheet keeps its size.
        const float ControlWidth = 300f, ControlHeight = 26f;

        // The values of a coefficient as StepFactor goes through them: by 0.05 to 1, by 0.1 to 2, by 0.25 to 10.
        static readonly float[] FactorSteps = MakeFactorSteps();

        static float[] MakeFactorSteps()
        {
            var steps = new List<float>();
            for (int i = 2; i <= 20; i++) steps.Add(i * 0.05f);
            for (int i = 11; i <= 20; i++) steps.Add(i * 0.1f);
            for (int i = 9; i <= 40; i++) steps.Add(i * 0.25f);
            return steps.Select(Factors.Clamp).ToArray();
        }

        static int FactorIndex(float value)
        {
            int best = 0;
            for (int i = 1; i < FactorSteps.Length; i++)
                if (Mathf.Abs(FactorSteps[i] - value) < Mathf.Abs(FactorSteps[best] - value)) best = i;
            return best;
        }

        const int VisibleRows = 13;
        static TimersWindow s_Instance;

        Page m_Page;
        Page m_SetupBack;
        string m_SetupKey;
        int m_Cursor;
        int m_Top;
        List<Item> m_Items = new List<Item>();
        readonly List<Row> m_Rows = new List<Row>();
        TextMeshProUGUI m_Tabs;
        TextMeshProUGUI m_Date;
        TextMeshProUGUI m_Detail;
        TextMeshProUGUI m_Note;
        GameObject m_RowsBox;
        GameObject m_HelpBox;
        TextMeshProUGUI m_HelpText;
        float m_HelpOffset;
        ModBar.ModScrollbar m_RowsBar, m_HelpBar;
        // The label the A hint shows on the deadlines tab: Track or Stop tracking, after the selected row.
        bool m_HintUntrack;
        readonly float[] m_Pending = new float[8];
        int m_SpanDays = 1;
        int m_SpanHours;
        float m_NextRefresh;

        internal static bool IsOpen
        {
            get { return s_Instance != null; }
        }

        internal static void Toggle()
        {
            if (s_Instance != null) s_Instance.Close();
            else Open(0);
        }

        internal static void Open(int page)
        {
            if (!Main.InGame) return;
            if (s_Instance == null)
            {
                Tracker.Scan();
                var w = Create<TimersWindow>("Timers", 1280, S.WindowTitle, 32100);
                w.Build();
                s_Instance = w;
                for (int i = 0; i < 8; i++) w.m_Pending[i] = Main.GetFactor(i);
                w.Show();
                Ui.Sound(UISoundType.JournalOpen);
            }
            s_Instance.Go((Page)page);
        }

        void Build()
        {
            m_Tabs = Ui.Text(Panel, "", 22, Theme.Text);
            m_Tabs.alignment = TextAlignmentOptions.Center;
            m_Tabs.raycastTarget = true;
            Ui.AddPointer(m_Tabs.gameObject).Click = ClickTabs;
            m_Date = Ui.Text(Panel, "", 19, Theme.Dim);
            m_Date.alignment = TextAlignmentOptions.Center;
            var list = Ui.Vertical(Panel, "Rows", 4);
            m_RowsBox = list;
            for (int i = 0; i < VisibleRows; i++)
            {
                int at = i;
                var row = MakeRow(list.transform, 20);
                row.Pointer.Click = e => ClickRow(at, e);
                row.Pointer.Live = () => m_Top + at < m_Items.Count && !m_Items[m_Top + at].Header;
                row.Control = ModBar.ModControl.Create((RectTransform)row.Bg.transform, row.Text, ControlWidth, ControlHeight);
                row.Control.Step = d => StepControl(at, d);
                row.Control.Slide = v => SlideControl(at, v);
                row.Control.Press = () => PressControl(at);
                m_Rows.Add(row);
            }
            list.GetComponent<UnityEngine.UI.VerticalLayoutGroup>().padding.right = (int)ModBar.ModScrollbar.Width + 4;
            m_RowsBar = ModBar.ModScrollbar.Create((RectTransform)list.transform, null);
            m_RowsBar.Moved = first =>
            {
                m_Top = Mathf.Clamp(Mathf.RoundToInt(first), 0, Math.Max(0, m_Items.Count - VisibleRows));
                m_Cursor = Mathf.Clamp(m_Cursor, m_Top, Math.Min(m_Items.Count - 1, m_Top + VisibleRows - 1));
                Render();
            };
            // The help in the rows' place, as high, scrolled inside it.
            m_HelpBox = new GameObject("Help", typeof(RectTransform));
            m_HelpBox.transform.SetParent(Panel, false);
            var helpSize = m_HelpBox.AddComponent<UnityEngine.UI.LayoutElement>();
            helpSize.preferredHeight = helpSize.minHeight = VisibleRows * 34 + (VisibleRows - 1) * 4 + 52 + 10;
            m_HelpBox.AddComponent<UnityEngine.UI.RectMask2D>();
            m_HelpText = Ui.Text(m_HelpBox.transform, "", 19, Theme.Text);
            var helpRect = m_HelpText.rectTransform;
            helpRect.anchorMin = new Vector2(0, 1);
            helpRect.anchorMax = new Vector2(1, 1);
            helpRect.pivot = new Vector2(0.5f, 1);
            helpRect.sizeDelta = Vector2.zero;
            helpRect.offsetMax = new Vector2(-ModBar.ModScrollbar.Width - 4, helpRect.offsetMax.y);
            m_HelpText.gameObject.AddComponent<UnityEngine.UI.ContentSizeFitter>().verticalFit = UnityEngine.UI.ContentSizeFitter.FitMode.PreferredSize;
            m_HelpBar = ModBar.ModScrollbar.Create((RectTransform)m_HelpBox.transform, null);
            m_HelpBar.Moved = first =>
            {
                m_HelpOffset = first;
                ScrollHelp(0);
            };
            m_HelpBox.SetActive(false);
            m_Detail = Ui.Text(Panel, "", 19, Theme.Dim);
            m_Detail.gameObject.AddComponent<UnityEngine.UI.LayoutElement>().minHeight = 52;
            m_Note = Ui.Text(Panel, "", 18, Theme.Dim);
        }

        protected override void OnClosed()
        {
            if (s_Instance == this) s_Instance = null;
        }

        void Go(Page page)
        {
            m_Page = page;
            m_Cursor = 0;
            m_Top = 0;
            bool help = page == Page.Help;
            m_RowsBox.SetActive(!help);
            m_Detail.gameObject.SetActive(!help);
            m_Note.gameObject.SetActive(!help);
            m_HelpBox.SetActive(help);
            if (help)
            {
                m_HelpText.spriteAsset = Help.IconAsset;
                m_HelpText.text = Help.Build();
                m_HelpOffset = 0;
                ScrollHelp(0);
            }
            Rebuild();
            PageHints();
            MoveTo(0, 1);
            Ui.Sound(UISoundType.BookPageTurn);
        }

        static string TabName(int page)
        {
            switch ((page % Tabs + Tabs) % Tabs)
            {
                case 0: return S.TabTimers;
                case 1: return S.TabTracked;
                case 2: return S.TabTime;
                default: return S.TabHelp;
            }
        }

        // LB and RB are named after the tabs they lead to.
        void PageHints()
        {
            int p = (int)m_Page;
            var prev = new Hint(RewiredActionType.LeftUp, TabName(p - 1));
            var next = new Hint(RewiredActionType.RightUp, TabName(p + 1));
            var close = new Hint(RewiredActionType.Decline, S.HClose);
            switch (m_Page)
            {
                case Page.Timers:
                    m_HintUntrack = SelectedTracked;
                    SetHints(new Hint(RewiredActionType.DPadVertical, S.HSelect), new Hint(RewiredActionType.Confirm, m_HintUntrack ? S.HUntrack : S.HTrack),
                        new Hint(RewiredActionType.Func01, S.HSetup),
                        new Hint(RewiredActionType.Func02, Main.Settings.ShowHidden ? S.HHiddenOff : S.HHiddenOn), prev, next, close);
                    break;
                case Page.Tracked:
                    SetHints(new Hint(RewiredActionType.DPadVertical, S.HSelect), new Hint(RewiredActionType.Confirm, S.HSetup),
                        new Hint(RewiredActionType.Func02, S.HUntrack), prev, next, close);
                    break;
                case Page.Time:
                    SetHints(new Hint(RewiredActionType.DPadVertical, S.HSelect), new Hint(RewiredActionType.DPadHorizontal, S.HChange),
                        new Hint(RewiredActionType.Confirm, S.HDo), prev, next, close);
                    break;
                case Page.Help:
                    SetHints(new Hint(RewiredActionType.DPadVertical, S.HScroll), prev, next, close);
                    break;
                case Page.Setup:
                    SetHints(new Hint(RewiredActionType.DPadVertical, S.HSelect), new Hint(RewiredActionType.DPadHorizontal, S.HChange),
                        new Hint(RewiredActionType.Confirm, S.HToggle), new Hint(RewiredActionType.Decline, S.HBack));
                    break;
            }
        }

        // The tracking of a timer in short: its alerts and the own deadline.
        static string AlertsShort(GameTimer t)
        {
            var tracked = Tracker.Get(t.Key);
            if (tracked == null) return "";
            string alerts = tracked.Alerts.Count == 0 ? S.AlertsNone
                : string.Format(S.AlertsList, string.Join(", ", tracked.Alerts.OrderByDescending(a => a).Select(a => S.Offset(a)).ToArray()));
            if (tracked.DeadlineHours > 0) alerts += "; " + string.Format(S.SetupDeadline, S.Span(tracked.DeadlineHours)).ToLowerInvariant();
            return alerts;
        }

        // ---------- items ----------

        void Rebuild()
        {
            m_Items = new List<Item>();
            switch (m_Page)
            {
                case Page.Timers: TimerItems(false); break;
                case Page.Tracked: TimerItems(true); break;
                case Page.Time: TimeItems(); break;
                case Page.Setup: SetupItems(); break;
                case Page.Help: m_Note.text = ""; break;
            }
            Render();
        }

        string Tags(GameTimer t)
        {
            var tags = new List<string>();
            if (t.Critical) tags.Add("<color=" + Theme.Bad + ">" + S.Critical + "</color>");
            if (t.Hidden) tags.Add("<color=" + Theme.Note + ">" + S.Hidden + "</color>");
            if (Tracker.IsTracked(t.Key)) tags.Add("<color=" + Theme.Good + ">" + S.TrackedMark + "</color>");
            return tags.Count == 0 ? "" : "  <size=17>(" + string.Join(", ", tags.ToArray()) + ")</size>";
        }

        string TimerDetail(GameTimer t)
        {
            var lines = new List<string> { "<color=" + (t.Critical ? Theme.Bad : Theme.Accent) + ">" + t.Name + "</color>. " + string.Format(S.DueDate, Timers.Date(t.Due)) };
            var tracked = Tracker.Get(t.Key);
            if (tracked != null)
            {
                string deadline = tracked.DeadlineHours > 0
                    ? string.Format(S.Deadline, S.Span(tracked.DeadlineHours), Timers.Date(t.Due - TimeSpan.FromHours(tracked.DeadlineHours)))
                    : S.DeadlineSame;
                string alerts = tracked.Alerts.Count == 0 ? S.AlertsNone
                    : string.Format(S.AlertsList, string.Join(", ", tracked.Alerts.OrderByDescending(a => a).Select(a => S.Offset(a)).ToArray()));
                lines.Add(deadline + ". " + alerts + ".");
            }
            var notes = new List<string>();
            if (t.Critical) notes.Add(S.CriticalNote);
            if (t.Hidden) notes.Add(S.HiddenNote);
            if (t.Kingdom) notes.Add(S.AffectedNote);
            if (notes.Count > 0) lines.Add(string.Join(" ", notes.ToArray()));
            return string.Join("\n", lines.ToArray());
        }

        void TimerItems(bool trackedOnly)
        {
            foreach (var t in Tracker.Current)
            {
                if (t.Remaining <= TimeSpan.Zero) continue;
                bool tracked = Tracker.IsTracked(t.Key);
                if (trackedOnly ? !tracked : (t.Hidden && !Main.Settings.ShowHidden && !tracked)) continue;
                var timer = t;
                m_Items.Add(new Item
                {
                    Text = t.Line + (t.Hidden ? "  <size=17><color=" + Theme.Note + ">(" + S.Hidden + ")</color></size>" : ""),
                    Right = Timers.Date(t.Due),
                    Eye = tracked,
                    Detail = AlertsShort(t),
                    Timer = t,
                    Confirm = trackedOnly ? (Action)(() => OpenSetup(timer)) : () => ToggleTrack(timer)
                });
            }
            m_Note.text = "";
            if (m_Items.Count == 0)
                m_Items.Add(new Item { Text = "<color=" + Theme.Muted + ">" + (trackedOnly ? S.EmptyTracked : S.Empty) + "</color>", Header = true });
        }

        void TimeItems()
        {
            m_Note.text = "";
            m_Items.Add(new Item { Text = "<color=" + Theme.Accent + ">" + string.Format(S.Section, S.SecCoef) + "</color>", Header = true });
            string[] names = { S.CoefEvents, S.CoefProjects, S.CoefClaim, S.CoefRankup, S.CoefRuler, S.CoefBuilding, S.CoefTravel, S.CoefRest };
            for (int i = 0; i < 8; i++)
            {
                int at = i;
                float now = Main.GetFactor(i);
                string text = names[i];
                if (!Mathf.Approximately(now, m_Pending[i])) text += "  <color=" + Theme.Muted + ">" + string.Format(S.Now, Factors.Text(now)) + "</color>";
                m_Items.Add(new Item
                {
                    Text = text,
                    Change = d => StepFactor(at, d),
                    Control = ModBar.ModControl.Kind.Slider,
                    Value = Factors.Text(m_Pending[i]).TrimStart('×'),
                    Index = FactorIndex(m_Pending[i]),
                    Count = FactorSteps.Length,
                    Slide = v =>
                    {
                        m_Pending[at] = FactorSteps[v];
                        Rebuild();
                    }
                });
            }
            bool pending = Enumerable.Range(0, 8).Any(i => !Mathf.Approximately(Main.GetFactor(i), m_Pending[i]));
            m_Items.Add(new Item
            {
                Text = (pending ? "<b>" : "") + S.Apply + (pending ? "</b>" : ""),
                Confirm = ApplyFactors,
                Control = ModBar.ModControl.Kind.Button,
                Value = S.ButtonApply
            });
            m_Items.Add(new Item
            {
                Text = S.Reset,
                Control = ModBar.ModControl.Kind.Button,
                Value = S.ButtonReset,
                Confirm = () =>
                {
                    for (int i = 0; i < 8; i++) m_Pending[i] = 1f;
                    Rebuild();
                }
            });
            m_Items.Add(new Item { Text = "<color=" + Theme.Accent + ">" + string.Format(S.Section, S.SecSpan) + "</color>", Header = true });
            int hours = m_SpanDays * 24 + m_SpanHours;
            string after = Timers.Date(Game.Instance.Player.GameTime + TimeSpan.FromHours(hours));
            m_Items.Add(new Item
            {
                Text = S.SpanDays,
                Detail = after,
                Change = d => { m_SpanDays = Mathf.Clamp(m_SpanDays + d, 0, 365); Rebuild(); },
                Control = ModBar.ModControl.Kind.Slider,
                Value = m_SpanDays.ToString(),
                Index = m_SpanDays,
                Count = 366,
                Slide = v => { m_SpanDays = v; Rebuild(); }
            });
            m_Items.Add(new Item
            {
                Text = S.SpanHours,
                Detail = after,
                Change = d => { m_SpanHours = (m_SpanHours + d + 24) % 24; Rebuild(); },
                Control = ModBar.ModControl.Kind.Slider,
                Value = m_SpanHours.ToString(),
                Index = m_SpanHours,
                Count = 24,
                Slide = v => { m_SpanHours = v; Rebuild(); }
            });
            m_Items.Add(new Item { Text = S.SkipRow + " (" + S.Span(hours) + ")", Detail = after, Confirm = AskSkip, Control = ModBar.ModControl.Kind.Button, Value = S.ButtonSkip });
            string before = Timers.Date(Game.Instance.Player.GameTime - TimeSpan.FromHours(Math.Min(hours, Rewinder.MaxHours)));
            m_Items.Add(new Item { Text = S.RewindRow + " (" + S.Span(hours) + ")", Detail = before, Confirm = AskRewind, Control = ModBar.ModControl.Kind.Button, Value = S.ButtonRewind });
        }

        void SetupItems()
        {
            m_Note.text = "";
            var tracked = Tracker.Get(m_SetupKey);
            var timer = Tracker.Current.FirstOrDefault(t => t.Key == m_SetupKey);
            if (tracked == null || timer == null)
            {
                m_Items.Add(new Item { Text = "<color=" + Theme.Muted + ">" + S.EmptyTracked + "</color>", Header = true });
                return;
            }
            m_Items.Add(new Item { Text = "<b>" + timer.Line + "</b>", Detail = TimerDetail(timer), Header = true });
            m_Items.Add(new Item
            {
                Text = S.SetupDeadlineRow,
                Control = ModBar.ModControl.Kind.Choice,
                Value = tracked.DeadlineHours > 0 ? S.Span(tracked.DeadlineHours) : S.DeadlineAtOnce,
                Detail = TimerDetail(timer),
                Change = d =>
                {
                    int i = Array.IndexOf(Tracker.DeadlinePresets, tracked.DeadlineHours);
                    if (i < 0) i = 0;
                    i = Mathf.Clamp(i + d, 0, Tracker.DeadlinePresets.Length - 1);
                    tracked.DeadlineHours = Tracker.DeadlinePresets[i];
                    Tracker.Changed(tracked);
                    Rebuild();
                }
            });
            foreach (int preset in Tracker.AlertPresets)
            {
                int a = preset;
                bool on = tracked.Alerts.Contains(a);
                m_Items.Add(new Item
                {
                    Text = string.Format(S.SetupAlert, S.Offset(a)),
                    Control = ModBar.ModControl.Kind.Switch,
                    On = on,
                    Detail = TimerDetail(timer),
                    Confirm = () =>
                    {
                        if (tracked.Alerts.Contains(a)) tracked.Alerts.Remove(a);
                        else tracked.Alerts.Add(a);
                        Tracker.Changed(tracked);
                        Rebuild();
                    }
                });
            }
            m_Items.Add(new Item
            {
                Text = "<color=" + Theme.Bad + ">" + S.SetupStop + "</color>",
                Control = ModBar.ModControl.Kind.Button,
                Value = S.ButtonStop,
                Confirm = () =>
                {
                    Tracker.Untrack(m_SetupKey);
                    Go(m_SetupBack);
                }
            });
        }

        // ---------- actions ----------

        void ToggleTrack(GameTimer t)
        {
            if (Tracker.IsTracked(t.Key)) Tracker.Untrack(t.Key);
            else Tracker.Add(t, false);
            Ui.Sound(UISoundType.ButtonClick);
            Rebuild();
        }

        void OpenSetup(GameTimer t)
        {
            if (!Tracker.IsTracked(t.Key)) Tracker.Add(t, false);
            m_SetupBack = m_Page == Page.Setup ? m_SetupBack : m_Page;
            m_SetupKey = t.Key;
            SetTitle(S.TabSetup);
            Go(Page.Setup);
            MoveTo(1, 1);
        }

        void StepFactor(int i, int d)
        {
            float v = m_Pending[i];
            float step = (d > 0 ? v >= 2f : v > 2f) ? 0.25f : (d > 0 ? v >= 1f : v > 1f) ? 0.1f : 0.05f;
            m_Pending[i] = Factors.Clamp((float)Math.Round((v + d * step) / step) * step);
            Rebuild();
        }

        void ApplyFactors()
        {
            Factors.ApplyAndReport((float[])m_Pending.Clone());
            Tracker.Scan();
            Rebuild();
        }

        int SpanHours
        {
            get { return m_SpanDays * 24 + m_SpanHours; }
        }

        void AskSkip()
        {
            int hours = SpanHours;
            if (hours <= 0)
            {
                MessageWindow.Show(S.SkipTitle, new List<string> { S.SkipZero }, null, null);
                return;
            }
            if (!Skipper.CanSkipNow)
            {
                MessageWindow.Show(S.SkipTitle, new List<string> { S.SkipNotNow }, null, null);
                return;
            }
            var plan = Skipper.Check(hours);
            var lines = new List<string>();
            if (!plan.Allowed)
            {
                lines.Add(string.Format(S.SkipBlocked, S.Span(hours)));
                lines.Add("");
                lines.AddRange(plan.Blockers.Select(b => "<color=" + Theme.Bad + ">• " + b + "</color>"));
                lines.Add("");
                lines.Add(plan.MaxSafeHours > 0 ? string.Format(S.SkipMax, S.Span(plan.MaxSafeHours)) : S.SkipNoMax);
                int safe = plan.MaxSafeHours;
                if (safe > 0) MessageWindow.Show(S.SkipTitle, lines, string.Format(S.HSkipMax, S.Span(safe)), () => DoSkip(safe));
                else MessageWindow.Show(S.SkipTitle, lines, null, null);
                return;
            }
            lines.Add(string.Format(S.SkipAsk, S.Span(hours), Timers.Date(Game.Instance.Player.GameTime + TimeSpan.FromHours(hours))));
            lines.Add("");
            if (plan.Passed.Count == 0) lines.Add(S.SkipNothing);
            else
            {
                lines.Add(S.SkipWillHappen);
                lines.AddRange(plan.Passed.Select(t => "• " + t.Name + " — " + Timers.Date(t.Due) + Tags(t)));
            }
            MessageWindow.Show(S.SkipTitle, lines, S.HSkip, () => DoSkip(hours));
        }

        void AskRewind()
        {
            int hours = SpanHours;
            if (hours <= 0)
            {
                MessageWindow.Show(S.RewindTitle, new List<string> { S.SkipZero }, null, null);
                return;
            }
            if (!Skipper.CanSkipNow)
            {
                MessageWindow.Show(S.RewindTitle, new List<string> { S.SkipNotNow }, null, null);
                return;
            }
            var lines = new List<string>();
            int max = Rewinder.MaxHours;
            if (hours > max)
            {
                lines.Add(string.Format(S.RewindLimit, S.Span(max)));
                lines.Add("");
                hours = max;
                if (hours <= 0)
                {
                    MessageWindow.Show(S.RewindTitle, lines, null, null);
                    return;
                }
            }
            TimeSpan now = Game.Instance.Player.GameTime;
            TimeSpan then = now - TimeSpan.FromHours(hours);
            TimeSpan delta = TimeSpan.FromHours(hours);
            lines.Add(string.Format(S.RewindAsk, S.Span(hours), Timers.Date(then)));
            lines.Add("");
            var shown = Tracker.Current.Where(t => t.Remaining > TimeSpan.Zero)
                .OrderByDescending(t => Tracker.IsTracked(t.Key) || t.Critical).ThenBy(t => t.Due).Take(12).ToList();
            if (shown.Count > 0)
            {
                lines.Add(string.Format(S.RewindLonger, S.Span(hours)));
                foreach (var t in shown)
                    lines.Add(string.Format(S.RewindLine, t.Short, Timers.Period(t.Remaining), Timers.Period(t.Remaining + delta)));
                lines.Add("");
            }
            int waits = Rewinder.KingdomWaitsUntil(then);
            if (waits >= 0)
            {
                lines.Add(string.Format(S.RewindKingdom, Timers.Date(TimeSpan.FromDays(KingdomState.Instance.StartDay + waits))));
                lines.Add("");
            }
            lines.Add(S.RewindPast);
            lines.Add(S.RewindUnits);
            int span = hours;
            MessageWindow.Show(S.RewindTitle, lines, S.HRewind, () => DoRewind(span));
        }

        void DoRewind(int hours)
        {
            Rewinder.Run(hours);
            Rebuild();
            MessageWindow.Show(S.RewindTitle, new List<string> { string.Format(S.RewindDone, S.Span(hours), Timers.Date(Game.Instance.Player.GameTime)) }, null, null);
        }

        void DoSkip(int hours)
        {
            Skipper.Run(hours);
            Rebuild();
            MessageWindow.Show(S.SkipTitle, new List<string> { string.Format(S.SkipDone, S.Span(hours), Timers.Date(Game.Instance.Player.GameTime)) }, null, null);
        }

        // ---------- input ----------

        Item Selected
        {
            get { return m_Cursor >= 0 && m_Cursor < m_Items.Count ? m_Items[m_Cursor] : null; }
        }

        protected override void OnConfirm()
        {
            var item = Selected;
            if (item == null || item.Header) return;
            if (item.Confirm != null) item.Confirm();
            else if (item.Change != null) item.Change(1);
        }

        protected override void OnDecline()
        {
            if (m_Page == Page.Setup)
            {
                SetTitle(S.WindowTitle);
                Go(m_SetupBack);
                return;
            }
            base.OnDecline();
        }

        protected override void OnFuncX()
        {
            var item = Selected;
            if (m_Page == Page.Timers && item != null && item.Timer != null) OpenSetup(item.Timer);
        }

        protected override void OnFuncY()
        {
            var item = Selected;
            if (m_Page == Page.Timers)
            {
                Main.Settings.ShowHidden = !Main.Settings.ShowHidden;
                Main.Settings.Save(Main.Mod);
                Rebuild();
                PageHints();
            }
            else if (m_Page == Page.Tracked && item != null && item.Timer != null)
            {
                Tracker.Untrack(item.Timer.Key);
                Rebuild();
                MoveTo(m_Cursor, 1);
            }
        }

        protected override void OnShoulder(int direction)
        {
            if (m_Page == Page.Setup) return;
            int next = ((int)m_Page + direction + Tabs) % Tabs;
            Go((Page)next);
        }

        protected override void OnMove(int direction)
        {
            if (m_Page == Page.Help) ScrollHelp(direction * 60f);
            else MoveTo(m_Cursor + direction, direction);
        }

        bool SelectedTracked
        {
            get
            {
                var item = Selected;
                return item != null && item.Timer != null && Tracker.IsTracked(item.Timer.Key);
            }
        }

        void ScrollHelp(float delta)
        {
            Canvas.ForceUpdateCanvases();
            float box = ((RectTransform)m_HelpBox.transform).rect.height;
            float max = Mathf.Max(0, m_HelpText.preferredHeight - box);
            m_HelpOffset = Mathf.Clamp(m_HelpOffset + delta, 0, max);
            m_HelpText.rectTransform.anchoredPosition = new Vector2(m_HelpText.rectTransform.anchoredPosition.x, m_HelpOffset);
            m_HelpBar.Set(m_HelpOffset, box, m_HelpText.preferredHeight);
        }

        protected override void OnChange(int direction)
        {
            var item = Selected;
            if (item != null && item.Change != null) item.Change(direction);
        }

        protected override void OnWheel(float y)
        {
            if (Mathf.Abs(y) < 0.01f) return;
            if (m_Page == Page.Help)
            {
                ScrollHelp(y > 0 ? -90f : 90f);
                return;
            }
            int delta = y > 0 ? -3 : 3;
            m_Top = Mathf.Clamp(m_Top + delta, 0, Math.Max(0, m_Items.Count - VisibleRows));
            m_Cursor = Mathf.Clamp(m_Cursor, m_Top, Math.Min(m_Items.Count - 1, m_Top + VisibleRows - 1));
            Render();
        }

        // To the nearest row that is not a header, in the given direction.
        void MoveTo(int index, int direction)
        {
            if (m_Items.Count == 0) return;
            index = Mathf.Clamp(index, 0, m_Items.Count - 1);
            int start = index;
            while (index >= 0 && index < m_Items.Count && m_Items[index].Header) index += direction >= 0 ? 1 : -1;
            if (index < 0 || index >= m_Items.Count)
            {
                index = start;
                while (index >= 0 && index < m_Items.Count && m_Items[index].Header) index += direction >= 0 ? -1 : 1;
                if (index < 0 || index >= m_Items.Count) index = start;
            }
            m_Cursor = index;
            if (m_Cursor < m_Top) m_Top = m_Cursor;
            if (m_Cursor >= m_Top + VisibleRows) m_Top = m_Cursor - VisibleRows + 1;
            // Headers above the first row stay in view.
            if (m_Top > 0 && m_Items.Take(m_Top).All(i => i.Header)) m_Top = 0;
            Render();
        }

        void ClickRow(int at, PointerEventData e)
        {
            int index = m_Top + at;
            if (index >= m_Items.Count || m_Items[index].Header) return;
            var item = m_Items[index];
            // The control takes its own clicks; the rest of its row only selects it.
            if (item.Control != ModBar.ModControl.Kind.None && item.Control != ModBar.ModControl.Kind.Button)
            {
                if (item.Control == ModBar.ModControl.Kind.Switch && index == m_Cursor) OnConfirm();
                m_Cursor = index;
                Render();
                return;
            }
            if (item.Change != null)
            {
                m_Cursor = index;
                Vector2 local;
                var rect = (RectTransform)m_Rows[at].Bg.transform;
                RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, null, out local);
                item.Change(local.x < rect.rect.center.x ? -1 : 1);
                return;
            }
            if (index == m_Cursor) OnConfirm();
            else
            {
                m_Cursor = index;
                Render();
            }
        }

        Item ControlItem(int at)
        {
            int index = m_Top + at;
            if (index >= m_Items.Count) return null;
            m_Cursor = index;
            return m_Items[index];
        }

        // An arrow of a choice, or a side of a switch: the switch turns only when the side is not its state.
        void StepControl(int at, int direction)
        {
            var item = ControlItem(at);
            if (item == null) return;
            if (item.Control == ModBar.ModControl.Kind.Switch)
            {
                if (item.On != direction > 0 && item.Confirm != null) item.Confirm();
                else Render();
            }
            else if (item.Change != null) item.Change(direction);
        }

        void SlideControl(int at, int value)
        {
            var item = ControlItem(at);
            if (item != null && item.Slide != null) item.Slide(value);
        }

        void PressControl(int at)
        {
            var item = ControlItem(at);
            if (item == null || item.Confirm == null) return;
            Render();
            item.Confirm();
        }

        void ClickTabs(PointerEventData e)
        {
            if (m_Page == Page.Setup) return;
            Vector2 local;
            var rect = m_Tabs.rectTransform;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(rect, e.position, null, out local);
            // The names are centred and of different lengths: the tab whose name is nearest to the click.
            string[] names = { S.TabTimers, S.TabTracked, S.TabTime, S.TabHelp };
            var info = m_Tabs.textInfo;
            int start = 0, best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < Tabs; i++)
            {
                int end = start + names[i].Length - 1;
                if (end < info.characterCount)
                {
                    float left = info.characterInfo[start].bottomLeft.x, right = info.characterInfo[end].topRight.x;
                    float distance = local.x < left ? left - local.x : local.x > right ? local.x - right : 0f;
                    if (distance < bestDistance)
                    {
                        bestDistance = distance;
                        best = i;
                    }
                }
                start += names[i].Length + TabSeparator.Length;
            }
            if (best >= 0) Go((Page)best);
        }

        const string TabSeparator = "   ·   ";

        protected override void Tick()
        {
            // Time goes on while the window is open on the global map in a cutscene or after a skip: refresh the countdowns.
            if (Time.unscaledTime < m_NextRefresh) return;
            m_NextRefresh = Time.unscaledTime + 1f;
            m_Date.text = string.Format(S.Today, Timers.Date(Game.Instance.Player.GameTime));
        }

        void Render()
        {
            string[] names = { S.TabTimers, S.TabTracked, S.TabTime, S.TabHelp };
            if (m_Page == Page.Setup) m_Tabs.text = "<b>" + S.TabSetup + "</b>";
            else
            {
                var parts = new List<string>();
                for (int i = 0; i < Tabs; i++)
                    parts.Add(i == (int)m_Page ? "<b><color=" + Theme.Accent + ">" + names[i] + "</color></b>" : "<color=" + Theme.Muted + ">" + names[i] + "</color>");
                m_Tabs.text = string.Join(TabSeparator, parts.ToArray());
            }
            m_Date.text = string.Format(S.Today, Timers.Date(Game.Instance.Player.GameTime));
            m_RowsBar.Set(m_Top, VisibleRows, m_Items.Count);
            for (int i = 0; i < VisibleRows; i++)
            {
                int index = m_Top + i;
                var row = m_Rows[i];
                // Empty rows stay in place, so the sheet keeps its size from page to page.
                if (index >= m_Items.Count)
                {
                    row.Text.text = "";
                    row.Right.text = "";
                    row.Control.Hide();
                    row.Icon.gameObject.SetActive(false);
                    row.Pointer.Normal = Color.clear;
                    row.Pointer.Refresh();
                    continue;
                }
                var item = m_Items[index];
                row.Text.text = item.Text;
                row.Right.text = item.Right ?? "";
                switch (item.Control)
                {
                    case ModBar.ModControl.Kind.Slider: row.Control.SetSlider(item.Index, item.Count, item.Value); break;
                    case ModBar.ModControl.Kind.Switch: row.Control.SetSwitch(item.On, S.SwitchOn, S.SwitchOff); break;
                    case ModBar.ModControl.Kind.Choice: row.Control.SetChoice(item.Value); break;
                    case ModBar.ModControl.Kind.Button: row.Control.SetButton(item.Value); break;
                    default: row.Control.Hide(); break;
                }
                if (item.Eye)
                {
                    row.Icon.sprite = Icons.Eye;
                    row.Icon.color = Theme.Dim;
                }
                row.Icon.gameObject.SetActive(item.Eye);
                row.Text.color = item.Header ? Theme.Dim : Theme.Text;
                Paint(row, index == m_Cursor && !item.Header);
            }
            var selected = Selected;
            m_Detail.text = selected != null && selected.Detail != null ? selected.Detail : "";
            if (m_Page == Page.Timers && m_HintUntrack != SelectedTracked) PageHints();
            if (m_Items.Count > VisibleRows && m_Page == Page.Setup)
                m_Detail.text += (m_Detail.text.Length > 0 ? "\n" : "") + "<color=" + Theme.Muted + ">" + (m_Top + 1) + "–" + Math.Min(m_Items.Count, m_Top + VisibleRows) + " / " + m_Items.Count + "</color>";
        }
    }
}
