using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.MVVM._ConsoleView.ActionBar;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using Owlcat.Runtime.UI.ConsoleTools;
using Owlcat.Runtime.UI.ConsoleTools.GamepadInput;
using Owlcat.Runtime.UI.ConsoleTools.HintTool;
using Owlcat.Runtime.UI.ConsoleTools.NavigationTool;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ActionBarRows
{
    // The rows above the gamepad action bar of Wrath of the Righteous. The game's view model holds the slots of all
    // ten pages; each row is a copy of the bar's slots bound to the slots of the next page. The bottom row is the
    // page the game shows, LB/RB turn all rows together. How many rows a character has is the game's own
    // UISettings.Phase (0..2): the mouse interface shows as many rows of its own bar.
    //
    // As in Kingmaker, the bar becomes two blocks, each on its own paper in its own frame: the slots with the rows
    // (the frame grows with them) and, a small gap to the right, the weapon set button with whatever other mods put
    // next to it. The whole bar moves left by what the gap and the second frame take.
    sealed class Rows : MonoBehaviour
    {
        internal const int MaxRows = 3;
        const int PageSize = 12, Pages = 10;
        // Between the two frames.
        const float BlockGap = 4f;

        static readonly FieldInfo NavField = AccessTools.Field(typeof(ActionBarConsoleView), "m_NavigationBehaviour");
        static readonly FieldInfo IsConvertField = AccessTools.Field(typeof(ActionBarConsoleView), "m_IsConvert");
        static readonly FieldInfo StickHintField = AccessTools.Field(typeof(ActionBarConsoleView), "m_RightStickButtonHint");
        static readonly FieldInfo SlotsContainerField = AccessTools.Field(typeof(ActionBarBaseView), "m_MainSlotsContainer");
        static readonly FieldInfo SlotsField = AccessTools.Field(typeof(ActionBarBaseView), "m_Slots");
        static readonly FieldInfo PageLabelField = AccessTools.Field(typeof(ActionBarBaseView), "m_PageLabel");
        static readonly FieldInfo EntitiesField = AccessTools.Field(typeof(GridConsoleNavigationBehaviour), "m_Entities");

        internal static Rows Instance;

        sealed class Gap : IConsoleNavigationEntity
        {
            public void SetFocus(bool value) { }
            public bool IsValid() { return false; }
        }

        sealed class Row
        {
            internal RectTransform Rect, Copy;
            internal readonly List<ActionBarBaseSlotConsoleView> Views = new List<ActionBarBaseSlotConsoleView>();
            internal readonly List<IConsoleEntity> Entities = new List<IConsoleEntity>();
            internal int Page = 1;
            // The bottom row starts with the auto-use slot: the rows above keep its column empty, so up and down
            // stay in the same column.
            internal readonly IConsoleEntity Gap = new Gap();
        }

        internal ActionBarConsoleView View;
        internal ActionBarVM Vm;
        internal GridConsoleNavigationBehaviour Nav;
        RectTransform m_Main, m_Bar, m_Slots;
        List<ActionBarBaseSlotView> m_MainSlots;
        readonly List<Row> m_Rows = new List<Row>();
        ConsoleHint m_Hint;
        IDisposable m_HintBind;
        InputBindStruct m_Bind;
        bool m_Built, m_Failed;
        int m_Count = 1;

        // The two blocks: papers and frames. The slots' paper and frames are the game's own, the rest are copies.
        RectTransform m_SlotsPaper, m_RowsPaper, m_ButtonsPaper, m_SlotsFrame, m_SlotsFrameTbm, m_ButtonsFrame;
        readonly List<RectTransform> m_Buttons = new List<RectTransform>();
        readonly List<Patch> m_Patches = new List<Patch>();
        // The game's layout, measured once: the space between slots (kept around them on every side and between the
        // rows), the frame's edges outside the paper, how much higher the turn-based frame is.
        float m_Margin, m_Pitch, m_EdgeX, m_EdgeBottom, m_EdgeTop, m_TbmShift;

        // Above the rows stands what stands above the bar alone; the variants window of each slot opens above the
        // top row.
        readonly Dictionary<RectTransform, float> m_TopItems = new Dictionary<RectTransform, float>();
        // The name of the slot under the cursor with the hints above it, and the windows of the buttons: each a small
        // gap above the frame.
        RectTransform m_SpellPanel, m_SpellName;
        readonly List<RectTransform> m_Windows = new List<RectTransform>();
        const string ButtonPath = "ActionBarWeaponConsoleView";
        readonly Dictionary<RectTransform, float> m_Variants = new Dictionary<RectTransform, float>();
        readonly Dictionary<RectTransform, int> m_VariantsRow = new Dictionary<RectTransform, int>();
        // The hints under the bar: the rows' one after the stick's, those of opening and closing the bar at the right
        // edge of the buttons block.
        RectTransform m_StickHint;
        readonly List<RectTransform> m_RightHints = new List<RectTransform>();

        internal int Count { get { return m_Count; } }

        internal bool IsConvertFocused
        {
            get
            {
                var convert = IsConvertField.GetValue(View) as UniRx.BoolReactiveProperty;
                return convert != null && convert.Value;
            }
        }

        internal static Rows Attach(ActionBarConsoleView view)
        {
            var rows = view.GetComponent<Rows>() ?? view.gameObject.AddComponent<Rows>();
            rows.View = view;
            Instance = rows;
            return rows;
        }

        internal static void OnBind(ActionBarConsoleView view)
        {
            var rows = Attach(view);
            rows.Vm = Traverse.Create(view).Property("ViewModel").GetValue<ActionBarVM>();
            rows.Nav = NavField.GetValue(view) as GridConsoleNavigationBehaviour;
            // Built once the game laid the slots out: Belt Bar adds its button in the same bind.
            if (rows.m_Built) rows.Refresh();
        }

        bool Build()
        {
            m_Slots = SlotsContainerField.GetValue(View) as RectTransform;
            m_Bar = m_Main.Find("ActionBarContainer") as RectTransform;
            var paper = m_Bar != null ? m_Bar.Find("Background") as RectTransform : null;
            m_SlotsFrame = m_Main.Find("Background/ActionBack") as RectTransform;
            m_SlotsFrameTbm = m_Main.Find("Background/TBMBack") as RectTransform;
            if (m_Slots == null || m_Bar == null || paper == null || m_SlotsFrame == null || Nav == null || m_MainSlots.Count < 2)
            {
                Main.Log("action bar layout not as expected, no rows");
                return false;
            }

            // The game's layout before anything moves.
            var content0 = SlotsBox();
            var paper0 = Box(paper);
            var frame0 = Box(m_SlotsFrame);
            m_Margin = Box((RectTransform)m_MainSlots[1].transform).xMin - Box((RectTransform)m_MainSlots[0].transform).xMax;
            m_Pitch = content0.height + m_Margin;
            m_EdgeX = paper0.xMin - frame0.xMin;
            m_EdgeBottom = paper0.yMin - frame0.yMin;
            m_EdgeTop = frame0.yMax - paper0.yMax;
            m_TbmShift = m_SlotsFrameTbm != null ? Box(m_SlotsFrameTbm).yMax - frame0.yMax : 0f;

            // The buttons right of the slots: the weapon set button and Belt Bar's copy of it (whose view is not the
            // game's any more, only its name is). Whatever else stands right of the slots moves with them.
            var moved = new List<RectTransform>();
            foreach (Transform child in m_Bar)
            {
                var rect = child as RectTransform;
                if (rect == null || rect == paper || rect == m_Slots || Box(rect).center.x <= content0.xMax) continue;
                moved.Add(rect);
                if (rect.Find(ButtonPath) != null) m_Buttons.Add(rect);
            }
            if (m_Buttons.Count == 0)
            {
                Main.Log("no weapon set button right of the slots, no rows");
                return false;
            }
            var buttons0 = ButtonsBox();
            float shift = content0.xMax + m_Margin + m_EdgeX + BlockGap + m_EdgeX + m_Margin - buttons0.xMin;
            foreach (var rect in moved) rect.anchoredPosition += new Vector2(shift, 0f);
            var control = m_Main.Find("ControlCharacterView") as RectTransform;
            if (control != null) control.anchoredPosition += new Vector2(shift, 0f);
            m_Main.anchoredPosition -= new Vector2(shift, 0f);

            // The papers and the rows go into the bar's own container, right after its paper: the rows are drawn on
            // the paper, under the bar's slots, the top row first (what opens from a lower row is drawn over it).
            int index = paper.GetSiblingIndex() + 1;
            m_SlotsPaper = paper;
            m_RowsPaper = Copy(paper, m_Bar, "ActionBarRowsPaper", index++);
            m_ButtonsPaper = Copy(paper, m_Bar, "ActionBarButtonsPaper", index++);
            m_ButtonsFrame = Copy(m_SlotsFrame, m_SlotsFrame.parent, "ActionBarButtonsFrame", m_SlotsFrame.GetSiblingIndex() + 1);

            // The frames keep their corners and ornaments: the pieces left and right at their own size, the middle
            // cut out (both blocks are narrower than the game's bar). They grow in height in the middle, the
            // turn-based one at the top, above the prediction panel.
            float slotsWidth = content0.width + 2f * (m_Margin + m_EdgeX);
            float buttonsWidth = buttons0.width + 2f * (m_Margin + m_EdgeX);
            // The sides are the ornaments of the two corners meeting in the middle; the slots frame grows by a plain
            // stretch of its side put between them, one line of the picture taken right under the top corner's gem,
            // where the side is a plain bar.
            var frameY = new[] { 0f, 0.4f, 0.6f, 1f };
            var slotsFrameY = new[] { 0f, 0.5f, 0.5f, 1f };
            var plainSide = new[] { 0.835f, 0.84f };
            var paperX = new[] { 0f, 0.03f, 0.97f, 1f };
            var paperY = new[] { 0f, 0.3f, 0.7f, 1f };
            foreach (var patch in new[]
            {
                Patch.Make(m_SlotsFrame, Sides(slotsWidth, frame0.width), slotsFrameY, plainSide),
                Patch.Make(m_SlotsFrameTbm, Sides(slotsWidth, frame0.width), new[] { 0f, 0.8f, 0.85f, 1f }),
                Patch.Make(m_ButtonsFrame, Sides(buttonsWidth, frame0.width), frameY),
                Patch.Make(m_SlotsPaper, paperX, paperY),
                Patch.Make(m_RowsPaper, paperX, paperY),
                Patch.Make(m_ButtonsPaper, paperX, paperY),
            })
                if (patch != null) m_Patches.Add(patch);

            for (int k = MaxRows - 1; k >= 1; k--)
            {
                var go = new GameObject("ActionBarRow" + k, typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(m_Bar, false);
                rect.SetSiblingIndex(index);
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                var copy = (RectTransform)Instantiate(m_Slots.gameObject, rect, false).transform;
                copy.anchorMin = copy.anchorMax = new Vector2(0.5f, 0.5f);
                var row = new Row { Rect = rect, Copy = copy };
                foreach (var slot in copy.GetComponentsInChildren<ActionBarBaseSlotConsoleView>(true))
                {
                    row.Views.Add(slot);
                    row.Entities.Add(slot.ConsoleEntityProxy);
                }
                if (row.Views.Count != PageSize)
                {
                    Main.Log("the copy of the bar has " + row.Views.Count + " slots, no rows");
                    return false;
                }
                foreach (var slot in row.Views) slot.Initialize();
                m_Rows.Insert(0, row);
                go.SetActive(false);
            }
            m_Hint = MakeHint();
            BindHint();

            foreach (string path in new[] { "AbilitiesGroupsContainer", "PredictionPanelConsoleView/PredictionHints" })
                AddTop(m_Main.Find(path) as RectTransform);
            m_SpellPanel = m_Main.Find("SpellPanelContainer") as RectTransform;
            m_SpellName = m_Main.Find("SpellPanelContainer/SpellNamePanel") as RectTransform;
            foreach (var button in m_Buttons)
            {
                var window = button.Find(ButtonPath + "/WeaponSets") as RectTransform;
                if (window != null) m_Windows.Add(window);
            }
            foreach (var slot in m_MainSlots) AddVariants(slot, 0);
            for (int k = 0; k < m_Rows.Count; k++)
                foreach (var slot in m_Rows[k].Views) AddVariants(slot, k + 1);

            m_StickHint = m_Main.Find("RightStickButtonHint") as RectTransform;
            foreach (string path in new[] { "ActionbarHint", "DeactivateHint" })
            {
                var hint = m_Main.Find(path) as RectTransform;
                if (hint != null) m_RightHints.Add(hint);
            }
            Main.Log("rows built: " + m_Rows.Count + " extra rows of " + PageSize + ", pitch " + m_Pitch + ", margin " + m_Margin
                + ", " + m_Buttons.Count + " buttons moved by " + shift + ", " + m_Patches.Count + " pictures cut");
            return true;
        }

        // A frame cut for a width: the left and right parts at their own size, as wide as the frame is to be, and a
        // sliver between them that takes up any difference.
        static float[] Sides(float width, float width0)
        {
            float side = Mathf.Clamp01((width / 2f - 2f) / width0);
            return new[] { 0f, side, 1f - side, 1f };
        }

        static RectTransform Copy(RectTransform source, Transform parent, string name, int index)
        {
            var copy = (RectTransform)Instantiate(source.gameObject, parent, false).transform;
            copy.name = name;
            copy.SetSiblingIndex(index);
            foreach (Transform child in copy) Destroy(child.gameObject);
            copy.gameObject.SetActive(true);
            return copy;
        }

        void AddTop(RectTransform rect)
        {
            if (rect != null) m_TopItems[rect] = rect.anchoredPosition.y;
        }

        void AddVariants(ActionBarBaseSlotView slot, int row)
        {
            var variants = slot != null ? slot.transform.Find("SubGroup") as RectTransform : null;
            if (variants == null) return;
            m_Variants[variants] = variants.anchoredPosition.y;
            m_VariantsRow[variants] = row;
        }

        // The slots themselves: their container has no size of its own.
        Rect SlotsBox()
        {
            return Union(m_MainSlots.Where(s => s != null).Select(s => Box((RectTransform)s.transform)));
        }

        // The buttons themselves: their views are wider than they are.
        Rect ButtonsBox()
        {
            return Union(m_Buttons.Where(b => b.gameObject.activeSelf).Select(b => Box((RectTransform)b.Find(ButtonPath))));
        }

        static Rect Union(IEnumerable<Rect> rects)
        {
            var boxes = rects.ToList();
            if (boxes.Count == 0) return Rect.zero;
            return Rect.MinMaxRect(boxes.Min(b => b.xMin), boxes.Min(b => b.yMin), boxes.Max(b => b.xMax), boxes.Max(b => b.yMax));
        }

        // The rectangle of a transform in the bar's coordinates.
        Rect Box(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 a = m_Main.InverseTransformPoint(corners[0]), b = m_Main.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        // What a transform shows: its pictures and texts, which may reach out of its own rectangle.
        Rect Visible(Transform root)
        {
            var boxes = root.GetComponentsInChildren<Graphic>().Where(g => g.enabled).Select(g => Box(g.rectTransform)).ToList();
            return boxes.Count == 0 ? Box((RectTransform)root) : Union(boxes);
        }

        // Puts a transform on a rectangle given in the bar's coordinates, whatever its parent.
        void Place(RectTransform rect, Rect box)
        {
            var parent = (RectTransform)rect.parent;
            Vector2 a = parent.InverseTransformPoint(m_Main.TransformPoint(new Vector3(box.xMin, box.yMin, 0f)));
            Vector2 b = parent.InverseTransformPoint(m_Main.TransformPoint(new Vector3(box.xMax, box.yMax, 0f)));
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var scale = rect.localScale;
            rect.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x) / scale.x, Mathf.Abs(b.y - a.y) / scale.y);
            rect.anchoredPosition = (a + b) / 2f - parent.rect.center;
        }

        void MoveX(RectTransform rect, float dx)
        {
            if (Mathf.Abs(dx) < 0.01f) return;
            rect.position += m_Main.TransformVector(new Vector3(dx, 0f, 0f));
        }

        ConsoleHint MakeHint()
        {
            var source = StickHintField.GetValue(View) as ConsoleHint;
            if (source == null) return null;
            var go = Instantiate(source.gameObject, source.transform.parent, false);
            go.name = "HintActionBarRows";
            return go.GetComponent<ConsoleHint>();
        }

        void LateUpdate()
        {
            if (m_Failed || View == null) return;
            try
            {
                if (!m_Built)
                {
                    // Not before the game laid the slots out.
                    m_MainSlots = SlotsField.GetValue(View) as List<ActionBarBaseSlotView>;
                    m_Main = View.transform.Find("ActionBarMainContainer") as RectTransform;
                    if (m_MainSlots == null || m_Main == null)
                    {
                        Main.Log("action bar layout not as expected, no rows");
                        m_Failed = true;
                        return;
                    }
                    if (SlotsBox().height < 1f) return;
                    if (!Build())
                    {
                        m_Failed = true;
                        return;
                    }
                    m_Built = true;
                    Refresh();
                }
                Layout();
            }
            catch (Exception e)
            {
                Main.Error(e, "rows");
                m_Failed = true;
            }
        }

        // The game set up the slots of its page (bind, page turn, unit change): the rows follow.
        internal void Refresh()
        {
            if (!m_Built || Vm == null) return;
            try
            {
                UnitEntityData unit = Vm.SelectedUnit.Value;
                int count = unit != null ? Mathf.Clamp(unit.UISettings.Phase + 1, 1, MaxRows) : 1;
                int page = Vm.CurrentPageIndex.Value;
                for (int k = 0; k < m_Rows.Count; k++)
                {
                    var row = m_Rows[k];
                    bool shown = k + 1 < count;
                    row.Page = (page - 1 + k + 1) % Pages + 1;
                    if (shown)
                        for (int i = 0; i < PageSize; i++)
                            row.Views[i].Bind(Vm.Slots[PageSize * (row.Page - 1) + i]);
                    row.Rect.gameObject.SetActive(shown);
                }
                if (count != m_Count || !NavigationMatches(count))
                {
                    m_Count = count;
                    RebuildNavigation();
                }
                UpdatePageLabel();
            }
            catch (Exception e) { Main.Error(e, "rows refresh"); }
        }

        // L3: one row more, from three back to one.
        internal void Toggle()
        {
            try
            {
                UnitEntityData unit = Vm != null ? Vm.SelectedUnit.Value : null;
                if (unit == null) return;
                unit.UISettings.Phase = (Mathf.Clamp(unit.UISettings.Phase, 0, MaxRows - 1) + 1) % MaxRows;
                Refresh();
            }
            catch (Exception e) { Main.Error(e, "rows toggle"); }
        }

        bool NavigationMatches(int count)
        {
            var entities = EntitiesField.GetValue(Nav) as List<List<IConsoleEntity>>;
            return entities != null && entities.Count == count;
        }

        // The game's bar is one line of entities (auto-use, slots, weapon sets and what other mods add). The rows go
        // above it; the entities other than the rows' stay in the bottom line in their order.
        void RebuildNavigation()
        {
            var entities = EntitiesField.GetValue(Nav) as List<List<IConsoleEntity>>;
            if (entities == null) return;
            var ours = new HashSet<IConsoleEntity>();
            foreach (var row in m_Rows)
            {
                ours.Add(row.Gap);
                foreach (var entity in row.Entities) ours.Add(entity);
            }
            var bottom = entities.SelectMany(line => line).Where(e => !ours.Contains(e)).ToList();
            var current = Nav.CurrentEntity;
            var lines = new List<List<IConsoleEntity>>();
            for (int k = m_Count - 2; k >= 0; k--)
            {
                var line = new List<IConsoleEntity> { m_Rows[k].Gap };
                line.AddRange(m_Rows[k].Entities);
                lines.Add(line);
            }
            lines.Add(bottom);
            Nav.SetEntities(lines);
            if (current == null || !Vm.IsActive.Value) return;
            if (!lines.Any(line => line.Contains(current)))
            {
                // The cursor was in a row that is gone: the same column of the bar.
                int column = m_Rows.Select(r => r.Entities.IndexOf(current)).FirstOrDefault(i => i >= 0);
                var proxy = m_MainSlots[Mathf.Clamp(column, 0, m_MainSlots.Count - 1)] as IConsoleEntityProxy;
                current = proxy != null ? proxy.ConsoleEntityProxy : null;
            }
            if (current != null) Nav.FocusOnEntityManual(current, true);
        }

        // The page of the row with the cursor.
        internal void UpdatePageLabel()
        {
            if (!m_Built || Vm == null) return;
            var label = PageLabelField.GetValue(View) as TextMeshProUGUI;
            if (label == null) return;
            int page = Vm.CurrentPageIndex.Value;
            var focus = Nav != null ? Nav.CurrentEntity : null;
            if (m_Count > 1 && focus != null)
                foreach (var row in m_Rows)
                    if (row.Entities.Contains(focus)) page = row.Page;
            label.text = page + "/" + Pages;
        }

        internal void AddInput(InputLayer layer)
        {
            m_Bind = layer.AddButton(e => Toggle(), 18, Rewired.InputActionEventType.ButtonJustPressed, true, false);
            BindHint();
        }

        // The hint is made with the rows, after the first bind made the layer.
        void BindHint()
        {
            if (m_Hint == null || m_Bind == null) return;
            m_HintBind?.Dispose();
            m_HintBind = m_Hint.Bind(m_Bind);
            m_Hint.SetLabel(Strings.Rows);
        }

        // The slots of the pages in the rows keep their counts up to date, as the game does for its own page.
        internal void UpdateResources()
        {
            if (!m_Built || Vm == null || m_Count <= 1 || Game.Instance.IsControllerMouse) return;
            for (int k = 0; k + 1 < m_Count; k++)
            {
                int first = PageSize * (m_Rows[k].Page - 1);
                for (int i = first; i < first + PageSize; i++) Vm.Slots[i].UpdateResource();
            }
        }

        // Positions follow the bar every frame: it slides up when opened, the prediction panel of turn-based combat
        // comes and goes.
        void Layout()
        {
            bool tbm = Vm != null && Vm.PredictionPanelVm.Value != null;
            // In turn-based combat the prediction panel stands above the bar: the rows go above it.
            float shift = tbm ? m_TbmShift : 0f;
            float extra = (m_Count - 1) * m_Pitch;
            float M = m_Margin;
            var content = SlotsBox();
            var container = Box(m_Slots).center;
            for (int k = 0; k < m_Rows.Count; k++)
            {
                Place(m_Rows[k].Rect, new Rect(content.x, content.y + shift + (k + 1) * m_Pitch, content.width, content.height));
                m_Rows[k].Copy.anchoredPosition = container - content.center;
            }

            // The slots block: one paper under all rows, the frame around it; in turn-based combat the paper of the
            // rows is apart from the bar's, above the prediction panel.
            float rowsTop = content.yMax + (m_Count > 1 ? shift + extra : 0f);
            bool apart = tbm && m_Count > 1;
            var paper = Rect.MinMaxRect(content.xMin - M, content.yMin - M, content.xMax + M, (apart ? content.yMax : rowsTop) + M);
            Place(m_SlotsPaper, paper);
            m_RowsPaper.gameObject.SetActive(apart);
            if (apart) Place(m_RowsPaper, Rect.MinMaxRect(paper.xMin, content.yMin + shift + m_Pitch - M, paper.xMax, rowsTop + M));
            var frame = Rect.MinMaxRect(paper.xMin - m_EdgeX, content.yMin - M - m_EdgeBottom, paper.xMax + m_EdgeX, content.yMax + M + m_EdgeTop + extra);
            Place(m_SlotsFrame, frame);
            if (m_SlotsFrameTbm != null) Place(m_SlotsFrameTbm, Rect.MinMaxRect(frame.xMin, frame.yMin, frame.xMax, frame.yMax + m_TbmShift));

            // The buttons block: the bar's height.
            var buttons = ButtonsBox();
            Place(m_ButtonsPaper, Rect.MinMaxRect(buttons.xMin - M, content.yMin - M, buttons.xMax + M, content.yMax + M));
            var buttonsFrame = Rect.MinMaxRect(buttons.xMin - M - m_EdgeX, content.yMin - M - m_EdgeBottom, buttons.xMax + M + m_EdgeX, content.yMax + M + m_EdgeTop);
            Place(m_ButtonsFrame, buttonsFrame);
            foreach (var patch in m_Patches) patch.Layout();

            // The hints under the bar.
            if (m_Hint != null && m_StickHint != null && m_Hint.gameObject.activeInHierarchy)
            {
                var hint = (RectTransform)m_Hint.transform;
                float left = m_StickHint.gameObject.activeInHierarchy ? Visible(m_StickHint).xMax + 0.5f * M : Visible(m_StickHint).xMin;
                MoveX(hint, left - Visible(hint).xMin);
            }
            foreach (var hint in m_RightHints)
                if (hint.gameObject.activeInHierarchy) MoveX(hint, buttonsFrame.xMax - Visible(hint).xMax);

            // Above the top row stands what stands above the bar alone.
            foreach (var item in m_TopItems)
                item.Key.anchoredPosition = new Vector2(item.Key.anchoredPosition.x, item.Value + extra);
            foreach (var item in m_Variants)
            {
                int row = m_VariantsRow[item.Key];
                // A window of a row opens above the top row: the rows above that row are lifted over.
                float lift = row == 0 ? (m_Count > 1 ? shift + extra : 0f) : (m_Count - 1 - row) * m_Pitch;
                item.Key.anchoredPosition = new Vector2(item.Key.anchoredPosition.x, item.Value + Mathf.Max(0f, lift));
            }
            // The name of the slot and the windows of the buttons stand a small gap above the slots frame (in
            // turn-based combat, above its frame with the prediction panel).
            float top = tbm && m_SlotsFrameTbm != null ? frame.yMax + m_TbmShift : frame.yMax;
            if (m_SpellPanel != null && m_SpellName != null) MoveY(m_SpellPanel, top + BlockGap - Box(m_SpellName).yMin);
            foreach (var window in m_Windows)
                if (window.gameObject.activeInHierarchy) MoveY(window, top + BlockGap - Visible(window).yMin);
        }

        void MoveY(RectTransform rect, float dy)
        {
            if (Mathf.Abs(dy) < 0.01f) return;
            rect.position += m_Main.TransformVector(new Vector3(0f, dy, 0f));
        }

        void OnDestroy()
        {
            m_HintBind?.Dispose();
            if (Instance == this) Instance = null;
        }
    }
}
