using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI._ConsoleUI.ActionBar;
using Kingmaker.UI._ConsoleUI.Utils.HintTool;
using Kingmaker.UI._ConsoleUI.Utils.MultiNavigationTool;
using Kingmaker.Assets.Console.GamepadInput;
using Kingmaker.Assets.UI.Utils;
using TMPro;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace ActionBarRows
{
    // The rows above the gamepad action bar. Each is a copy of the bar's slots showing the next page of the same
    // ten: the bottom row is the page the game shows, LB/RB turn all rows together. How many rows a character has is
    // the game's own UISettings.Phase (0..2), the state of the extra slots panel of the mouse interface; the game
    // sets it to 1 itself when it fills more than one page.
    //
    // The bar becomes two blocks, each on its own paper in its own frame: the slots with the rows (the frame grows
    // with them) and, a small gap to the right, the weapon set button with whatever other mods put next to it. The
    // whole panel moves left by what the gap and the second frame take. The windows that open from the buttons sit
    // on the top edge of the top row.
    sealed class Rows : MonoBehaviour
    {
        internal const int MaxRows = 3;
        const int PageSize = 14, Pages = 10;
        // Rows stand this far apart; the slots are 50 high, the rows of the paper keep the bar's own spacing.
        const float Pitch = 50f + Margin;
        // The paper around the slots: at the edges of the frame and between the rows alike.
        const float Margin = 6f;
        // The frame of the bar stands this much lower than the game's: its paper shows under the slots as above.
        const float FrameDrop = 3f;
        // Between the two frames, and between the top row and a window opened from the buttons.
        const float Gap = 4f;

        static readonly FieldInfo NavField = AccessTools.Field(typeof(ActionBarView), "m_NavigationCollection");
        static readonly FieldInfo SlotsField = AccessTools.Field(typeof(ActionBarView), "m_Slots");
        static readonly FieldInfo PageLabelField = AccessTools.Field(typeof(ActionBarView), "m_PageLabel");
        static readonly FieldInfo SizeContainerField = AccessTools.Field(typeof(ActionBarView), "ChangebleSizeContainer");
        static readonly FieldInfo BehaviorField = AccessTools.Field(typeof(ViewBase<ActionBarVM>), "m_InputBehavior");
        static readonly FieldInfo EntitiesField = AccessTools.Field(typeof(ConsoleMultiNavigationCollection), "m_Entities");
        static readonly FieldInfo MoveSlotField = AccessTools.Field(typeof(ActionBarVM), "m_MoveSlot");
        static readonly MethodInfo ManualClick = AccessTools.Method(typeof(ActionBarView), "OnSlotManualClick");

        internal static Rows Instance;
        static readonly HashSet<ActionBarSlotView> s_BarSlots = new HashSet<ActionBarSlotView>();

        sealed class Row
        {
            internal RectTransform Rect;
            internal readonly List<ActionBarSlotView> Views = new List<ActionBarSlotView>();
            internal readonly List<ActionBarSlotVM> Vms = new List<ActionBarSlotVM>();
            internal int Page = 1;
            // The first column is the auto-use slot's in the bottom row: the rows above keep it empty, so up and down
            // stay in the same column.
            internal readonly IConsoleMultiNavigationEntity Gap = new EmptyConsoleMultiNavigationEntity();
        }

        internal ActionBarView View;
        internal ActionBarVM Vm;
        RectTransform m_Root, m_Bar, m_Slots;
        List<ActionBarSlotView> m_MainSlots;
        ConsoleMultiNavigationCollection m_Nav;
        // Index 0 is the row right above the bar.
        readonly List<Row> m_Rows = new List<Row>();
        GenericHintView m_Hint;
        CompositeDisposable m_VmSubscriptions;
        int m_Count = 1;

        // The two blocks: papers and frames. The slots' frame is the game's own (the one for turn-based combat too),
        // the rest are copies.
        RectTransform m_SlotsPaper, m_RowsPaper, m_ButtonsPaper, m_SlotsFrame, m_SlotsFrameTbm, m_ButtonsFrame;
        readonly List<RectTransform> m_Buttons = new List<RectTransform>();
        readonly List<Patch> m_Patches = new List<Patch>();
        // The hints of the button closing the open bar and opening the closed one: under the buttons block, at its
        // right edge.
        readonly List<RectTransform> m_RightHints = new List<RectTransform>();
        // The game's layout, measured once in the panel's own coordinates: the paper of the bar, the frame around it
        // in both modes, and the margins between slots, paper and frame.
        Rect m_Paper0, m_Frame0, m_FrameTbm0;
        float m_PadX, m_FrameX, m_TbmShift;

        // Things above the bar, at their own places: those hanging from the top of the panel, and the variants
        // window of each slot.
        readonly Dictionary<RectTransform, Vector2> m_TopItems = new Dictionary<RectTransform, Vector2>();
        readonly Dictionary<RectTransform, Vector2> m_Variants = new Dictionary<RectTransform, Vector2>();
        readonly Dictionary<RectTransform, int> m_VariantsRow = new Dictionary<RectTransform, int>();
        // The windows of the weapon set button and the belt button, with the frame that shows where they end.
        readonly List<KeyValuePair<RectTransform, RectTransform>> m_Windows = new List<KeyValuePair<RectTransform, RectTransform>>();

        internal int Count { get { return m_Count; } }

        internal static bool IsBarSlot(ActionBarSlotView view)
        {
            return s_BarSlots.Contains(view);
        }

        internal static void OnBind(ActionBarView view, ActionBarVM vm)
        {
            var rows = view.GetComponent<Rows>();
            if (rows == null)
            {
                rows = view.gameObject.AddComponent<Rows>();
                if (!rows.Build(view))
                {
                    Destroy(rows);
                    return;
                }
            }
            Instance = rows;
            rows.BindVm(vm);
        }

        bool Build(ActionBarView view)
        {
            View = view;
            m_Root = (RectTransform)view.transform;
            m_Bar = m_Root.Find("ActionBar") as RectTransform;
            m_Slots = m_Bar != null ? m_Bar.Find("Slots") as RectTransform : null;
            var paper = m_Bar != null ? m_Bar.Find("PapperBack") as RectTransform : null;
            var weapon = m_Bar != null ? m_Bar.Find("CurrentWeaponSetView") as RectTransform : null;
            m_SlotsFrame = m_Root.Find("Background/Lather") as RectTransform;
            m_SlotsFrameTbm = m_Root.Find("Background/Lather_TBM") as RectTransform;
            m_Nav = NavField.GetValue(view) as ConsoleMultiNavigationCollection;
            m_MainSlots = SlotsField.GetValue(view) as List<ActionBarSlotView>;
            if (m_Slots == null || paper == null || weapon == null || m_SlotsFrame == null || m_Nav == null || m_MainSlots == null)
            {
                Main.Log("action bar layout not as expected, no rows");
                return false;
            }
            var sizeContainer = SizeContainerField.GetValue(view) as RectTransform;
            Main.Log("action bar: size container " + (sizeContainer != null ? sizeContainer.name : "none")
                + "; frame " + Describe(m_SlotsFrame) + "; paper " + Describe(paper));

            // The game's layout before anything moves.
            // The slots themselves: their container is wider than they are on one side.
            var slots0 = Union(m_MainSlots.Select(s => (RectTransform)s.transform).ToList());
            m_Paper0 = Box(paper);
            m_Frame0 = Box(m_SlotsFrame);
            m_FrameTbm0 = m_SlotsFrameTbm != null ? Box(m_SlotsFrameTbm) : m_Frame0;
            m_PadX = slots0.xMin - m_Paper0.xMin;
            m_FrameX = m_Paper0.xMin - m_Frame0.xMin;
            m_TbmShift = m_FrameTbm0.yMax - m_Frame0.yMax;

            // The buttons right of the slots: the weapon set button and whatever other mods put next to it.
            m_Buttons.Add(weapon);
            foreach (Transform child in m_Bar)
            {
                var rect = child as RectTransform;
                if (rect == null || rect == weapon || rect == paper || rect == m_Slots) continue;
                if (Box(rect).xMin > slots0.xMax) m_Buttons.Add(rect);
            }
            var buttons0 = Union(m_Buttons);
            float shift = slots0.xMax + m_PadX + m_FrameX + Gap + m_FrameX + m_PadX - buttons0.xMin;
            foreach (var button in m_Buttons) button.anchoredPosition += new Vector2(shift, 0f);
            m_Root.anchoredPosition -= new Vector2(shift, 0f);

            // The papers go before the rows and the bar, the rows before the bar with the top row first: what opens
            // from a lower row is drawn over the rows above it.
            int index = m_Bar.GetSiblingIndex();
            m_SlotsPaper = Copy(paper, m_Root, "ActionBarSlotsPaper", index++);
            m_RowsPaper = Copy(paper, m_Root, "ActionBarRowsPaper", index++);
            m_ButtonsPaper = Copy(paper, m_Root, "ActionBarButtonsPaper", index++);
            paper.gameObject.SetActive(false);
            m_ButtonsFrame = Copy(m_SlotsFrame, m_SlotsFrame.parent, "ActionBarButtonsFrame", m_SlotsFrame.GetSiblingIndex() + 1);
            // The frame: the end caps and the corners, the ornament in the middle of the top and bottom edges are
            // kept; the frame of the buttons is too short for the middle one. The turn-based frame grows at the top,
            // above the prediction panel. The paper fills the opening of the frame exactly with the middle of its
            // picture: its own edges are soft, meant to be hidden under the frame.
            var frameX = new[] { 0f, 0.035f, 0.47f, 0.53f, 0.965f, 1f };
            var frameY = new[] { 0f, 0.4f, 0.6f, 1f };
            var paperX = new[] { 0.15f, 0.85f };
            var paperY = new[] { 0.3f, 0.7f };
            foreach (var patch in new[]
            {
                Patch.Make(m_SlotsFrame, frameX, frameY),
                Patch.Make(m_SlotsFrameTbm, frameX, new[] { 0f, 0.78f, 0.82f, 1f }),
                Patch.Make(m_ButtonsFrame, new[] { 0f, 0.035f, 0.965f, 1f }, frameY),
                Patch.Make(m_SlotsPaper, paperX, paperY),
                Patch.Make(m_RowsPaper, paperX, paperY),
                Patch.Make(m_ButtonsPaper, paperX, paperY),
            })
                if (patch != null) m_Patches.Add(patch);
            foreach (string path in new[] { "Hints/HintClose", "Hints/HintToggle" })
            {
                var hint = m_Root.Find(path) as RectTransform;
                if (hint != null) m_RightHints.Add(hint);
            }

            for (int k = MaxRows - 1; k >= 1; k--)
            {
                var go = new GameObject("ActionBarRow" + k, typeof(RectTransform));
                var rect = (RectTransform)go.transform;
                rect.SetParent(m_Root, false);
                rect.SetSiblingIndex(m_Bar.GetSiblingIndex());
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                // The whole Slots copy: the slots' count texts live in its CountContainer, a copy of a single
                // slot would write into the bar's own.
                var copy = (RectTransform)Instantiate(m_Slots.gameObject, rect, false).transform;
                copy.anchorMin = copy.anchorMax = copy.pivot = new Vector2(0.5f, 0.5f);
                copy.anchoredPosition = Vector2.zero;
                var row = new Row { Rect = rect };
                foreach (Transform child in copy)
                {
                    var slot = child.GetComponent<ActionBarSlotView>();
                    if (slot == null) continue;
                    slot.Initialize();
                    slot.SetManualClickBehavior(s => ManualClick.Invoke(view, new object[] { s }));
                    row.Views.Add(slot);
                }
                if (row.Views.Count != PageSize)
                {
                    Main.Log("the copy of the bar has " + row.Views.Count + " slots, no rows");
                    return false;
                }
                m_Rows.Insert(0, row);
                go.SetActive(false);
            }
            m_Hint = MakeHint();

            s_BarSlots.Clear();
            foreach (var slot in m_MainSlots) s_BarSlots.Add(slot);
            foreach (var row in m_Rows)
                foreach (var slot in row.Views) s_BarSlots.Add(slot);

            foreach (string path in new[] { "SpellNamePanel", "InspectHint", "KineticistBurnView", "Hints/TopHints" })
            {
                var item = m_Root.Find(path) as RectTransform;
                if (item != null) m_TopItems[item] = item.anchoredPosition;
            }
            foreach (var slot in m_MainSlots) AddVariants(slot, 0);
            for (int k = 0; k < m_Rows.Count; k++)
                foreach (var slot in m_Rows[k].Views) AddVariants(slot, k + 1);
            foreach (var button in m_Buttons)
            {
                var window = button.Find("WeaponSetsView") as RectTransform;
                if (window == null) continue;
                var frame = window.Find("Frame") as RectTransform;
                m_Windows.Add(new KeyValuePair<RectTransform, RectTransform>(window, frame ?? window));
            }
            Main.Log("rows built: " + m_Rows.Count + " extra rows of " + PageSize + ", " + m_Buttons.Count + " buttons moved by " + shift
                + ", " + m_Windows.Count + " windows");
            return true;
        }

        static string Describe(RectTransform rect)
        {
            var image = rect.GetComponent<Image>();
            if (image == null || image.sprite == null) return rect.name + " (no sprite)";
            return rect.name + " " + image.sprite.name + " " + image.type + " border " + image.sprite.border;
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

        void AddVariants(ActionBarSlotView slot, int row)
        {
            var variants = slot.GetComponentInChildren<ActionBarConvertedView>(true);
            if (variants == null) return;
            var rect = (RectTransform)variants.transform;
            m_Variants[rect] = rect.anchoredPosition;
            m_VariantsRow[rect] = row;
        }

        GenericHintView MakeHint()
        {
            var source = m_Root.Find("Hints/HintTooltip") as RectTransform;
            if (source == null) return null;
            var go = Instantiate(source.gameObject, source.parent, false);
            go.name = "HintActionBarRows";
            var rect = (RectTransform)go.transform;
            rect.anchoredPosition = source.anchoredPosition + new Vector2(source.sizeDelta.x + 50f, 0f);
            return go.GetComponent<GenericHintView>();
        }

        // The rectangle of a transform in the panel's own coordinates.
        Rect Box(RectTransform rect)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 a = m_Root.InverseTransformPoint(corners[0]), b = m_Root.InverseTransformPoint(corners[2]);
            return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y));
        }

        Rect Union(List<RectTransform> rects)
        {
            var boxes = rects.Where(r => r.gameObject.activeSelf).Select(Box).ToList();
            if (boxes.Count == 0) boxes = rects.Select(Box).ToList();
            return Rect.MinMaxRect(boxes.Min(b => b.xMin), boxes.Min(b => b.yMin), boxes.Max(b => b.xMax), boxes.Max(b => b.yMax));
        }

        // What a transform shows: its pictures and texts, which may reach out of its own rectangle.
        Rect Visible(Transform root)
        {
            var boxes = root.GetComponentsInChildren<Graphic>().Where(g => g.enabled).Select(g => Box(g.rectTransform)).ToList();
            if (boxes.Count == 0) return Box((RectTransform)root);
            return Rect.MinMaxRect(boxes.Min(b => b.xMin), boxes.Min(b => b.yMin), boxes.Max(b => b.xMax), boxes.Max(b => b.yMax));
        }

        // Puts a transform on a rectangle given in the panel's coordinates, whatever its parent.
        void Place(RectTransform rect, Rect box)
        {
            var parent = (RectTransform)rect.parent;
            Vector2 a = parent.InverseTransformPoint(m_Root.TransformPoint(new Vector3(box.xMin, box.yMin, 0f)));
            Vector2 b = parent.InverseTransformPoint(m_Root.TransformPoint(new Vector3(box.xMax, box.yMax, 0f)));
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            var scale = rect.localScale;
            rect.sizeDelta = new Vector2(Mathf.Abs(b.x - a.x) / scale.x, Mathf.Abs(b.y - a.y) / scale.y);
            rect.anchoredPosition = (a + b) / 2f - parent.rect.center;
        }

        // Moves a transform by a distance in the panel's coordinates.
        void MoveY(RectTransform rect, float dy)
        {
            if (Mathf.Abs(dy) < 0.01f) return;
            rect.position += m_Root.TransformVector(new Vector3(0f, dy, 0f));
        }

        void MoveX(RectTransform rect, float dx)
        {
            if (Mathf.Abs(dx) < 0.01f) return;
            rect.position += m_Root.TransformVector(new Vector3(dx, 0f, 0f));
        }

        void BindVm(ActionBarVM vm)
        {
            m_VmSubscriptions?.Dispose();
            foreach (var row in m_Rows)
            {
                foreach (var old in row.Vms) old.Dispose();
                row.Vms.Clear();
            }
            Vm = vm;
            m_VmSubscriptions = new CompositeDisposable();
            foreach (var row in m_Rows)
            {
                foreach (var view in row.Views)
                {
                    var slotVm = new ActionBarSlotVM(s => Vm.CurrentSlot.Value = s, vm.Active);
                    // As the bar's own slots: using one closes the bar.
                    m_VmSubscriptions.Add(slotVm.OnClickCommand.Subscribe(ignore => { if (!ignore) Vm.Active.Value = false; }));
                    row.Vms.Add(slotVm);
                    // A slot needs a mechanic slot before it is shown.
                    slotVm.SetMechanicSlot(new IndexedMechanicSlot { Index = 0, Slot = new Kingmaker.UI.UnitSettings.MechanicActionBarSlotEmpty() });
                    view.Bind(slotVm);
                }
            }
            // The game unselects only the slots of its own page.
            m_VmSubscriptions.Add(vm.CurrentSlot.Subscribe(current =>
            {
                foreach (var row in m_Rows)
                    foreach (var slotVm in row.Vms)
                        if (slotVm != current)
                        {
                            slotVm.SetSelect(false);
                            slotVm.SetTooltip(false);
                        }
                UpdatePageLabel();
            }));
            var move = MoveSlotField.GetValue(vm) as ReactiveProperty<IndexedMechanicSlot>;
            if (move != null)
                m_VmSubscriptions.Add(move.Subscribe(moving =>
                {
                    foreach (var row in m_Rows)
                        foreach (var slotVm in row.Vms) slotVm.SetMove(moving);
                }));
            Refresh();
        }

        // The pages of the rows from the game's slots; called whenever the game reads them again.
        internal void Refresh()
        {
            try
            {
                UnitEntityData unit = Vm != null ? Vm.Unit.Value : null;
                if (unit == null) return;
                int count = Mathf.Clamp(unit.UISettings.Phase + 1, 1, MaxRows);
                int page = Vm.CurrentPageIndex.Value;
                for (int k = 0; k < m_Rows.Count; k++)
                {
                    var row = m_Rows[k];
                    row.Page = (page - 1 + k + 1) % Pages + 1;
                    // Hidden rows are kept filled too: the game selects and unselects their slots like any other.
                    for (int i = 0; i < PageSize; i++)
                    {
                        int index = PageSize * (row.Page - 1) + i;
                        row.Vms[i].SetMechanicSlot(new IndexedMechanicSlot { Index = index, Slot = unit.UISettings.GetSlot(index, unit) });
                    }
                    row.Rect.gameObject.SetActive(k + 1 < count);
                }
                if (count != m_Count)
                {
                    m_Count = count;
                    RebuildNavigation();
                }
                else if (!NavigationMatches()) RebuildNavigation();
                UpdatePageLabel();
            }
            catch (Exception e) { Main.Error(e, "rows refresh"); }
        }

        // L3: one row more, from three back to one.
        internal void Toggle()
        {
            try
            {
                UnitEntityData unit = Vm != null ? Vm.Unit.Value : null;
                if (unit == null) return;
                unit.UISettings.Phase = (Mathf.Clamp(unit.UISettings.Phase, 0, MaxRows - 1) + 1) % MaxRows;
                Game.Instance.UI.Common.UISound.Play(UISoundType.ActionBarSlotSelected);
                Refresh();
            }
            catch (Exception e) { Main.Error(e, "rows toggle"); }
        }

        bool NavigationMatches()
        {
            var entities = EntitiesField.GetValue(m_Nav) as List<List<IConsoleMultiNavigationEntity>>;
            return entities != null && entities.Count == m_Count;
        }

        // The game's bar is one line of entities (auto-use, slots, weapon sets and whatever other mods add). The rows
        // go above it; the entities other than the rows' stay in the bottom line in their order.
        void RebuildNavigation()
        {
            var entities = EntitiesField.GetValue(m_Nav) as List<List<IConsoleMultiNavigationEntity>>;
            if (entities == null) return;
            var ours = new HashSet<IConsoleMultiNavigationEntity>();
            foreach (var row in m_Rows)
            {
                ours.Add(row.Gap);
                foreach (var view in row.Views) ours.Add(view);
            }
            var bottom = entities.SelectMany(line => line).Where(e => !ours.Contains(e)).ToList();
            var current = m_Nav.CurrentEntity;
            var lines = new List<List<IConsoleMultiNavigationEntity>>();
            for (int k = m_Count - 2; k >= 0; k--)
            {
                var line = new List<IConsoleMultiNavigationEntity> { m_Rows[k].Gap };
                line.AddRange(m_Rows[k].Views.Cast<IConsoleMultiNavigationEntity>());
                lines.Add(line);
            }
            lines.Add(bottom);
            m_Nav.SetEntities(lines);
            if (current == null || !Vm.Active.Value) return;
            if (!lines.Any(line => line.Contains(current)))
            {
                // The cursor was in a row that is gone: the same column of the bar.
                int column = m_Rows.Select(r => r.Views.IndexOf(current as ActionBarSlotView)).FirstOrDefault(i => i >= 0);
                current = m_MainSlots[Mathf.Clamp(column, 0, m_MainSlots.Count - 1)];
            }
            m_Nav.SelectCurrentEntity(current);
        }

        // The page of the row with the cursor.
        internal void UpdatePageLabel()
        {
            if (Vm == null) return;
            var label = PageLabelField.GetValue(View) as TextMeshProUGUI;
            if (label == null) return;
            int page = Vm.CurrentPageIndex.Value;
            var current = Vm.CurrentSlot.Value;
            if (m_Count > 1)
                foreach (var row in m_Rows)
                    if (current != null && row.Vms.Contains(current)) page = row.Page;
            label.text = page + "/" + Vm.MaxPage.Value;
        }

        internal void OnBarActive(bool active)
        {
            foreach (var row in m_Rows)
                foreach (var view in row.Views)
                    if (view.GetViewModel() != null) view.SetTooltipEnabled(!active);
        }

        internal void AddInput(InputLayer layer)
        {
            var bind = layer.AddButton(e => Toggle(), 18);
            var behavior = BehaviorField.GetValue(View) as ConsoleInputBehavior;
            if (behavior != null && m_Hint != null && bind != null)
            {
                behavior.RegisteredHint(m_Hint, bind);
                m_Hint.SetLabel(Strings.Rows);
            }
        }

        // Positions follow the panel every frame: the turn-based panel comes and goes with a tween, the windows of the
        // buttons are moved by the game when they open.
        void LateUpdate()
        {
            try
            {
                bool tbm = Vm != null && Vm.TBMModeIsActive.Value;
                // In turn-based combat the prediction panel stands above the bar: the rows go above it.
                float shift = tbm ? m_TbmShift : 0f;
                float extra = (m_Count - 1) * Pitch;
                var slots = Box(m_Slots);
                for (int k = 0; k < m_Rows.Count; k++)
                    Place(m_Rows[k].Rect, new Rect(slots.x, slots.y + shift + (k + 1) * Pitch, slots.width, slots.height));

                // The slots block: one paper under all rows, the frame around it; in turn-based combat the paper of
                // the rows is apart from the bar's, above the prediction panel. The paper is the opening of the frame:
                // the same margin around the slots on every side.
                var content = Union(m_MainSlots.Select(s => (RectTransform)s.transform).ToList());
                float rowsTop = content.yMax + (m_Count > 1 ? shift + extra : 0f);
                float left = content.xMin - Margin, right = content.xMax + Margin;
                bool apart = tbm && m_Count > 1;
                Place(m_SlotsPaper, Rect.MinMaxRect(left, content.yMin - Margin, right, (apart ? content.yMax : rowsTop) + Margin));
                m_RowsPaper.gameObject.SetActive(apart);
                if (apart) Place(m_RowsPaper, Rect.MinMaxRect(left, content.yMin + shift + Pitch - Margin, right, rowsTop + Margin));
                float edge = m_PadX + m_FrameX;
                var frame = Rect.MinMaxRect(content.xMin - edge, m_Frame0.yMin - FrameDrop, content.xMax + edge, m_Frame0.yMax + extra);
                Place(m_SlotsFrame, frame);
                if (m_SlotsFrameTbm != null)
                    Place(m_SlotsFrameTbm, Rect.MinMaxRect(frame.xMin, m_FrameTbm0.yMin - FrameDrop, frame.xMax, m_FrameTbm0.yMax + extra));

                // The buttons block: the bar's height.
                var buttons = Union(m_Buttons);
                Place(m_ButtonsPaper, Rect.MinMaxRect(buttons.xMin - Margin, content.yMin - Margin, buttons.xMax + Margin, content.yMax + Margin));
                var buttonsFrame = Rect.MinMaxRect(buttons.xMin - edge, m_Frame0.yMin - FrameDrop, buttons.xMax + edge, m_Frame0.yMax);
                Place(m_ButtonsFrame, buttonsFrame);
                foreach (var patch in m_Patches) patch.Layout();
                foreach (var hint in m_RightHints)
                    if (hint.gameObject.activeInHierarchy) MoveX(hint, buttonsFrame.xMax - Visible(hint).xMax);
                float paperTop = m_Paper0.yMax + (m_Count > 1 ? shift + extra : 0f);

                // Above the top row stands what stands above the bar alone.
                foreach (var item in m_TopItems)
                    item.Key.anchoredPosition = item.Value + new Vector2(0f, extra);
                foreach (var item in m_Variants)
                {
                    int row = m_VariantsRow[item.Key];
                    // A window of a row opens above the top row: the rows above that row are lifted over.
                    float lift = row == 0 ? (m_Count > 1 ? shift + extra : 0f) : (m_Count - 1 - row) * Pitch;
                    item.Key.anchoredPosition = item.Value + new Vector2(0f, Mathf.Max(0f, lift));
                }
                // The windows of the buttons sit on the top edge of the top row (in turn-based combat with one row, on
                // the top of the prediction panel's frame).
                float top = m_Count > 1 ? paperTop : (tbm ? m_FrameTbm0.yMax : m_Paper0.yMax);
                foreach (var window in m_Windows)
                {
                    if (!window.Key.gameObject.activeInHierarchy) continue;
                    MoveY(window.Key, top + Gap - Box(window.Value).yMin);
                }
            }
            catch (Exception e)
            {
                Main.Error(e, "rows layout");
                enabled = false;
            }
        }

        void OnDestroy()
        {
            m_VmSubscriptions?.Dispose();
            foreach (var row in m_Rows)
                foreach (var slotVm in row.Vms) slotVm.Dispose();
            if (Instance == this) Instance = null;
        }
    }
}
