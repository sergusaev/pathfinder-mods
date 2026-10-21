using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI._ConsoleUI.ActionBar;
using Kingmaker.UI._ConsoleUI.InputLayers.InGameLayer;
using Kingmaker.UI._ConsoleUI.TurnBasedMode;
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
    // on the top edge of the top row. In turn-based combat the prediction panel is a third block, on its own paper in
    // its own frame above the slots block and as wide; what stands above the bar then stands above it.
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
        static readonly FieldInfo PredictionField = AccessTools.Field(typeof(ActionBarView), "m_PredictionPanelView");
        static readonly FieldInfo ChangeHintField = AccessTools.Field(typeof(PredictionPanelView), "m_ChangeAction");
        static readonly FieldInfo ContextHintField = AccessTools.Field(typeof(ActionBarView), "m_ContextMenuHint");
        static readonly FieldInfo InspectViewField = AccessTools.Field(typeof(ActionBarView), "m_InspectView");
        static readonly FieldInfo InspectHintField = AccessTools.Field(typeof(InGameInputLayerView), "m_InspectHint");
        static readonly FieldInfo TerrainHintField = AccessTools.Field(typeof(PredictionPanelView), "m_PointerTerrainMode");
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

        // The blocks: papers and frames. The slots' frame is the game's own (its turn-based one is not shown), the
        // rest are copies.
        RectTransform m_SlotsPaper, m_ButtonsPaper, m_SlotsFrame, m_ButtonsFrame;
        RectTransform m_Prediction, m_PredictionPaper, m_PredictionFrame;
        // The panel's actions laid out by the rows, side by side over the whole width of the slots block: five-foot
        // step, move, standard, swift, free; the full round one over move and standard. The move and the standard
        // share what the others leave.
        static readonly string[] ActionFields =
            { "m_FiveFootAction", "m_MoveAction", "m_StandardAction", "m_SwiftAction", "m_FreeAction", "m_FullRoundActionView" };
        RectTransform[] m_Actions;
        float[] m_ActionWidths, m_ActionHeights;
        float m_ActionGap;
        // The panel's hints, as the game makes them: changing the action at the left edge of the prediction block,
        // the pointer's terrain mode at its right edge, a small gap above it (m_HintsLeft > m_HintsRight: no block).
        RectTransform m_ChangeHint, m_TerrainHint;
        float m_HintsLeft = 1f, m_HintsRight, m_HintsBottom;
        readonly List<RectTransform> m_Buttons = new List<RectTransform>();
        readonly List<Patch> m_Patches = new List<Patch>();
        // The hints of the button closing the open bar and opening the closed one: under the buttons block, at its
        // right edge.
        readonly List<RectTransform> m_RightHints = new List<RectTransform>();
        // The game's layout, measured once in the panel's own coordinates: the paper of the bar, the frame around it
        // in both modes, and the margins between slots, paper and frame.
        Rect m_Paper0, m_Frame0;
        float m_PadX, m_FrameX;

        // Things above the bar, at their own places: those hanging from the top of the panel (the bottom of each as
        // the game puts it out of combat), and the variants window of each slot. The name of the slot stands in the
        // middle over the slots block in turn-based combat, where the game puts it otherwise.
        readonly Dictionary<RectTransform, float> m_TopItems = new Dictionary<RectTransform, float>();
        // The names over the bar (of the slot, of the enemy under the pointer) with the middle of each out of combat,
        // and their hints.
        readonly Dictionary<RectTransform, float> m_Names = new Dictionary<RectTransform, float>();
        readonly List<RectTransform> m_NameHints = new List<RectTransform>();
        readonly Dictionary<RectTransform, Vector2> m_NameHintsAt = new Dictionary<RectTransform, Vector2>();
        bool m_InspectHintSought;
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
                if (!rows.Build(view, vm))
                {
                    Destroy(rows);
                    return;
                }
            }
            Instance = rows;
            rows.BindVm(vm);
        }

        bool Build(ActionBarView view, ActionBarVM vm)
        {
            View = view;
            m_Root = (RectTransform)view.transform;
            m_Bar = m_Root.Find("ActionBar") as RectTransform;
            m_Slots = m_Bar != null ? m_Bar.Find("Slots") as RectTransform : null;
            var paper = m_Bar != null ? m_Bar.Find("PapperBack") as RectTransform : null;
            var weapon = m_Bar != null ? m_Bar.Find("CurrentWeaponSetView") as RectTransform : null;
            m_SlotsFrame = m_Root.Find("Background/Lather") as RectTransform;
            var frameTbm = m_Root.Find("Background/Lather_TBM") as RectTransform;
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
            m_PadX = slots0.xMin - m_Paper0.xMin;
            m_FrameX = m_Paper0.xMin - m_Frame0.xMin;

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
            m_ButtonsPaper = Copy(paper, m_Root, "ActionBarButtonsPaper", index++);
            paper.gameObject.SetActive(false);
            m_ButtonsFrame = Copy(m_SlotsFrame, m_SlotsFrame.parent, "ActionBarButtonsFrame", m_SlotsFrame.GetSiblingIndex() + 1);
            // The frame: the end caps and the corners, the ornament in the middle of the top and bottom edges are
            // kept; the frame of the buttons is too short for the middle one. The paper fills the opening of the frame
            // exactly with the middle of its picture: its own edges are soft, meant to be hidden under the frame.
            var frameX = new[] { 0f, 0.035f, 0.47f, 0.53f, 0.965f, 1f };
            // The corners keep their size; the sides between them are the top edge turned on end
            // (Tbm_BackgroundActionM, 817 x 86: corners 32 pixels high, the top edge from 2 to 17 pixels down, its
            // stitches every 10 pixels).
            var frameY = new[] { 0f, 32f / 86f, 1f - 32f / 86f, 1f };
            var side = new Patch.Side { Bar = new Rect(150f, 2f, 100f, 15f), Outer = 3f, Inner = 18f, InsideY = 43f };
            var paperX = new[] { 0.15f, 0.85f };
            var paperY = new[] { 0.3f, 0.7f };
            foreach (var patch in new[]
            {
                Patch.Make(m_SlotsFrame, frameX, frameY, side),
                Patch.Make(m_ButtonsFrame, new[] { 0f, 0.035f, 0.965f, 1f }, frameY, side),
                Patch.Make(m_SlotsPaper, paperX, paperY),
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
                if (item != null) m_TopItems[item] = Box(item).yMin;
            }
            var inspect = InspectViewField.GetValue(view) as Component;
            foreach (var name in new[] { m_Root.Find("SpellNamePanel") as RectTransform, inspect != null ? inspect.transform as RectTransform : null })
            {
                if (name == null || m_Names.ContainsKey(name)) continue;
                m_Names[name] = Box(name).center.x;
                if (!m_TopItems.ContainsKey(name) && name.IsChildOf(m_Root)) m_TopItems[name] = Box(name).yMin;
            }
            var context = ContextHintField.GetValue(view) as Component;
            if (context != null) m_NameHints.Add((RectTransform)context.transform);
            Main.Log("names over the bar: " + string.Join(", ", m_Names.Keys.Select(n => Path(n)).ToArray())
                + "; context menu hint " + (context != null ? Path(context.transform) : "none"));
            if (vm.TBMModeIsActive.Value) Main.Log("rows built in turn-based combat: what stands above the bar may stand too high out of it");

            // The prediction panel of turn-based combat: its own paper and frame, drawn right under it. The game's
            // turn-based frame is not shown, the slots frame stays as it is.
            if (frameTbm != null)
                foreach (var graphic in frameTbm.GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
            var prediction = PredictionField.GetValue(view) as PredictionPanelView;
            m_Prediction = prediction != null ? prediction.transform as RectTransform : null;
            if (m_Prediction != null && m_Prediction != m_Root && m_Prediction.IsChildOf(m_Root) && !m_Slots.IsChildOf(m_Prediction))
            {
                var change = ChangeHintField.GetValue(prediction) as Component;
                var terrain = TerrainHintField.GetValue(prediction) as Component;
                m_ChangeHint = change != null ? change.transform as RectTransform : null;
                m_TerrainHint = terrain != null ? terrain.transform as RectTransform : null;
                Transform under = m_Prediction;
                while (under.parent != m_Root) under = under.parent;
                int at = under.GetSiblingIndex();
                m_PredictionPaper = Copy(paper, m_Root, "ActionBarPredictionPaper", at);
                m_PredictionFrame = Copy(m_SlotsFrame, m_Root, "ActionBarPredictionFrame", at + 1);
                foreach (var patch in new[] { Patch.Make(m_PredictionPaper, paperX, paperY), Patch.Make(m_PredictionFrame, frameX, frameY, side) })
                    if (patch != null) m_Patches.Add(patch);
                m_PredictionPaper.gameObject.SetActive(false);
                m_PredictionFrame.gameObject.SetActive(false);

                var actions = ActionFields
                    .Select(name => AccessTools.Field(typeof(PredictionPanelView), name).GetValue(prediction) as Component)
                    .Select(action => action != null ? action.transform as RectTransform : null).ToArray();
                if (actions.All(action => action != null))
                {
                    m_Actions = actions;
                    m_ActionWidths = actions.Select(action => Box(action).width).ToArray();
                    m_ActionHeights = actions.Select(action => Box(action).height).ToArray();
                    m_ActionGap = Mathf.Clamp(Box(actions[1]).xMin - Box(actions[0]).xMax, 0f, Margin);
                    // The panel's own backdrop, around all its actions: the block's paper takes its place.
                    var row = Rect.MinMaxRect(actions.Min(a => Box(a).xMin), actions.Min(a => Box(a).yMin),
                        actions.Max(a => Box(a).xMax), actions.Max(a => Box(a).yMax));
                    foreach (var graphic in m_Prediction.GetComponentsInChildren<Graphic>(true))
                    {
                        var box = Box(graphic.rectTransform);
                        if (actions.Any(a => graphic.transform.IsChildOf(a)) || box.xMin > row.xMin || box.xMax < row.xMax
                            || box.yMin > row.yMin || box.yMax < row.yMax) continue;
                        graphic.enabled = false;
                        Main.Log("prediction panel backdrop " + graphic.name + " hidden");
                    }
                    foreach (var action in actions)
                    {
                        for (var parent = action.parent; parent != null && parent != m_Root; parent = parent.parent)
                        {
                            foreach (var group in parent.GetComponents<LayoutGroup>()) group.enabled = false;
                            foreach (var fitter in parent.GetComponents<ContentSizeFitter>()) fitter.enabled = false;
                        }
                        Stretch(action);
                    }
                }
                else Main.Log("prediction panel actions not as expected, the panel is moved whole");
            }
            else
            {
                m_Prediction = null;
                Main.Log("no prediction panel apart from the slots, it stays over the bar");
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
        // The corners are taken into one array for all calls: measured every frame.
        static readonly Vector3[] s_Corners = new Vector3[4];

        Rect Box(RectTransform rect)
        {
            var corners = s_Corners;
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

        // What the prediction panel shows, its hints left out: they are placed apart.
        Rect PredictionBox()
        {
            var boxes = m_Prediction.GetComponentsInChildren<Graphic>()
                .Where(g => g.enabled && g.color.a > 0.01f && !(g is TMP_Text && string.IsNullOrEmpty(((TMP_Text)g).text))
                    && (m_ChangeHint == null || !g.transform.IsChildOf(m_ChangeHint))
                    && (m_TerrainHint == null || !g.transform.IsChildOf(m_TerrainHint)))
                .Select(g => Box(g.rectTransform)).ToList();
            if (boxes.Count == 0) return Box(m_Prediction);
            return Rect.MinMaxRect(boxes.Min(b => b.xMin), boxes.Min(b => b.yMin), boxes.Max(b => b.xMax), boxes.Max(b => b.yMax));
        }

        // An action made wider: what spans it (its back, its name) spans it at any width, the rest stays in the middle.
        static void Stretch(RectTransform action)
        {
            var stretched = new HashSet<Transform> { action };
            foreach (var rect in action.GetComponentsInChildren<RectTransform>(true))
            {
                if (rect == action || !stretched.Contains(rect.parent) || Mathf.Abs(rect.localScale.x - 1f) > 0.01f) continue;
                var parent = (RectTransform)rect.parent;
                if (rect.rect.width < 0.9f * parent.rect.width) continue;
                float left = rect.localPosition.x + rect.rect.xMin - parent.rect.xMin;
                float right = parent.rect.xMax - rect.localPosition.x - rect.rect.xMax;
                rect.anchorMin = new Vector2(0f, rect.anchorMin.y);
                rect.anchorMax = new Vector2(1f, rect.anchorMax.y);
                rect.offsetMin = new Vector2(left, rect.offsetMin.y);
                rect.offsetMax = new Vector2(-right, rect.offsetMax.y);
                stretched.Add(rect);
            }
        }

        // The actions side by side between two edges, each at the height the game gives it.
        void PlaceActions(float left, float right)
        {
            float wide = Mathf.Max(0f, (right - left - m_ActionWidths[0] - m_ActionWidths[3] - m_ActionWidths[4] - 4f * m_ActionGap) / 2f);
            var widths = new[] { m_ActionWidths[0], wide, wide, m_ActionWidths[3], m_ActionWidths[4] };
            var places = new Rect[widths.Length];
            float x = left;
            // The heights are the game's own, measured once: what is placed is never measured back (an action caught
            // at no size would keep it).
            for (int i = 0; i < widths.Length; i++)
            {
                float y = Box(m_Actions[i]).center.y;
                places[i] = new Rect(x, y - m_ActionHeights[i] / 2f, widths[i], m_ActionHeights[i]);
                x += widths[i] + m_ActionGap;
            }
            for (int i = 0; i < widths.Length; i++) Place(m_Actions[i], places[i]);
            float fullY = Box(m_Actions[5]).center.y;
            Place(m_Actions[5], Rect.MinMaxRect(places[1].xMin, fullY - m_ActionHeights[5] / 2f, places[2].xMax, fullY + m_ActionHeights[5] / 2f));
        }

        // Done again after the game's layout, which may put the hints back whenever they change. Returns the top of
        // what shows, or the bottom when nothing does. The hints of the names above (the slot's context menu, the
        // enemy's inspection) join them in the middle, under the names; out of combat they are back where the game
        // put them.
        float PlaceHints()
        {
            float top = m_HintsBottom - Gap;
            if (m_HintsLeft > m_HintsRight)
            {
                foreach (var hint in m_NameHintsAt) hint.Key.anchoredPosition = hint.Value;
                m_NameHintsAt.Clear();
                return top;
            }
            foreach (var hint in new[] { m_ChangeHint, m_TerrainHint })
            {
                if (hint == null || !hint.gameObject.activeInHierarchy) continue;
                var shown = Visible(hint);
                float x = hint == m_ChangeHint ? m_HintsLeft : m_HintsRight - shown.width;
                var moved = new Vector2(x - shown.xMin, m_HintsBottom - shown.yMin);
                if (moved.sqrMagnitude > 0.0001f) hint.position += m_Root.TransformVector(moved);
                top = Mathf.Max(top, shown.yMax + moved.y);
            }
            // Side by side, together in the middle.
            var shownHints = m_NameHints.Where(hint => hint != null && hint.gameObject.activeInHierarchy).ToList();
            var boxes = shownHints.Select(hint => Visible(hint)).ToList();
            float at = (m_HintsLeft + m_HintsRight - boxes.Sum(box => box.width) - 2f * Margin * Mathf.Max(0, boxes.Count - 1)) / 2f;
            for (int i = 0; i < shownHints.Count; i++)
            {
                var hint = shownHints[i];
                if (!m_NameHintsAt.ContainsKey(hint)) m_NameHintsAt[hint] = hint.anchoredPosition;
                var moved = new Vector2(at - boxes[i].xMin, m_HintsBottom - boxes[i].yMin);
                if (moved.sqrMagnitude > 0.0001f) hint.position += m_Root.TransformVector(moved);
                top = Mathf.Max(top, boxes[i].yMax + moved.y);
                at += boxes[i].width + 2f * Margin;
            }
            return top;
        }

        // The hint of inspecting the enemy under the pointer belongs to the game's input layer, made apart from the bar:
        // looked for once, in the first turn-based combat.
        void FindInspectHint()
        {
            m_InspectHintSought = true;
            var layer = FindObjectOfType<InGameInputLayerView>();
            var hint = layer != null ? InspectHintField.GetValue(layer) as Component : null;
            if (hint != null) m_NameHints.Add((RectTransform)hint.transform);
            Main.Log("inspect hint: " + (hint != null ? Path(hint.transform) : "none"));
        }

        static string Path(Transform t)
        {
            return t.parent == null ? t.name : Path(t.parent) + "/" + t.name;
        }

        // What a name above the bar shows, its hints left out.
        Rect? NameBox(RectTransform name)
        {
            var boxes = name.GetComponentsInChildren<Graphic>()
                .Where(g => g.enabled && !m_NameHints.Any(hint => hint != null && g.transform.IsChildOf(hint)))
                .Select(g => Box(g.rectTransform)).ToList();
            if (boxes.Count == 0) return null;
            return Rect.MinMaxRect(boxes.Min(b => b.xMin), boxes.Min(b => b.yMin), boxes.Max(b => b.xMax), boxes.Max(b => b.yMax));
        }

        void AfterLayout()
        {
            try
            {
                if (enabled && m_Prediction != null) PlaceHints();
            }
            catch (Exception e)
            {
                Main.Error(e, "prediction hints");
                Canvas.willRenderCanvases -= AfterLayout;
            }
        }

        void OnEnable() { Canvas.willRenderCanvases += AfterLayout; }

        void OnDisable() { Canvas.willRenderCanvases -= AfterLayout; }

        // Puts a transform on a rectangle given in the panel's coordinates, whatever its parent.
        void Place(RectTransform rect, Rect box)
        {
            var parent = (RectTransform)rect.parent;
            Vector2 a = parent.InverseTransformPoint(m_Root.TransformPoint(new Vector3(box.xMin, box.yMin, 0f)));
            Vector2 b = parent.InverseTransformPoint(m_Root.TransformPoint(new Vector3(box.xMax, box.yMax, 0f)));
            var scale = rect.localScale;
            // Shrunk to nothing (an animation): no size to give it.
            if (Mathf.Abs(scale.x) < 0.001f || Mathf.Abs(scale.y) < 0.001f) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
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
                bool tbm = Vm != null && Vm.TBMModeIsActive.Value && m_Prediction != null && m_Prediction.gameObject.activeInHierarchy;
                float extra = (m_Count - 1) * Pitch;
                var slots = Box(m_Slots);
                for (int k = 0; k < m_Rows.Count; k++)
                    Place(m_Rows[k].Rect, new Rect(slots.x, slots.y + (k + 1) * Pitch, slots.width, slots.height));

                // The slots block: one paper under all rows, the frame around it. The paper is the opening of the
                // frame: the same margin around the slots on every side.
                var content = Union(m_MainSlots.Select(s => (RectTransform)s.transform).ToList());
                float left = content.xMin - Margin, right = content.xMax + Margin;
                var paper = Rect.MinMaxRect(left, content.yMin - Margin, right, content.yMax + extra + Margin);
                Place(m_SlotsPaper, paper);
                float edge = m_PadX + m_FrameX;
                var frame = Rect.MinMaxRect(content.xMin - edge, m_Frame0.yMin - FrameDrop, content.xMax + edge, m_Frame0.yMax + extra);
                Place(m_SlotsFrame, frame);

                // The prediction block: as wide as the slots block, with the same edges, the gap between them the one
                // between the slots and the buttons blocks.
                float top = frame.yMax, above = top;
                if (m_PredictionPaper != null)
                {
                    m_PredictionPaper.gameObject.SetActive(tbm);
                    m_PredictionFrame.gameObject.SetActive(tbm);
                }
                if (tbm)
                {
                    float edgeX = paper.xMin - frame.xMin, edgeBottom = paper.yMin - frame.yMin, edgeTop = frame.yMax - paper.yMax;
                    var shown = PredictionBox();
                    var moved = new Vector2(m_Actions != null ? 0f : frame.center.x - shown.center.x, frame.yMax + Gap + edgeBottom + Margin - shown.yMin);
                    if (moved.sqrMagnitude > 0.0001f) m_Prediction.position += m_Root.TransformVector(moved);
                    shown.position += moved;
                    var predictionPaper = m_Actions != null
                        ? Rect.MinMaxRect(paper.xMin, shown.yMin - Margin, paper.xMax, shown.yMax + Margin)
                        : Rect.MinMaxRect(shown.xMin - Margin, shown.yMin - Margin, shown.xMax + Margin, shown.yMax + Margin);
                    if (m_Actions != null) PlaceActions(predictionPaper.xMin + Margin, predictionPaper.xMax - Margin);
                    var predictionFrame = Rect.MinMaxRect(predictionPaper.xMin - edgeX, predictionPaper.yMin - edgeBottom,
                        predictionPaper.xMax + edgeX, predictionPaper.yMax + edgeTop);
                    Place(m_PredictionPaper, predictionPaper);
                    Place(m_PredictionFrame, predictionFrame);
                    top = predictionFrame.yMax;
                    m_HintsLeft = predictionPaper.xMin;
                    m_HintsRight = predictionPaper.xMax;
                    m_HintsBottom = top + Gap;
                    above = Mathf.Max(top, PlaceHints());
                }
                else
                {
                    m_HintsLeft = 1f;
                    m_HintsRight = 0f;
                }

                // The buttons block: the bar's height.
                var buttons = Union(m_Buttons);
                Place(m_ButtonsPaper, Rect.MinMaxRect(buttons.xMin - Margin, content.yMin - Margin, buttons.xMax + Margin, content.yMax + Margin));
                var buttonsFrame = Rect.MinMaxRect(buttons.xMin - edge, m_Frame0.yMin - FrameDrop, buttons.xMax + edge, m_Frame0.yMax);
                Place(m_ButtonsFrame, buttonsFrame);
                foreach (var patch in m_Patches) patch.Layout();
                foreach (var hint in m_RightHints)
                    if (hint.gameObject.activeInHierarchy) MoveX(hint, buttonsFrame.xMax - Visible(hint).xMax);

                // Above the top row stands what stands above the bar alone; in turn-based combat, as far above the
                // prediction block and its hints.
                float over = above - frame.yMax;
                foreach (var item in m_TopItems) MoveY(item.Key, item.Value + extra + over - Box(item.Key).yMin);
                if (tbm && !m_InspectHintSought) FindInspectHint();
                // The names: in turn-based combat in the middle over the slots block, right above their hints.
                foreach (var name in m_Names)
                {
                    if (!tbm)
                    {
                        MoveX(name.Key, name.Value - Box(name.Key).center.x);
                        continue;
                    }
                    var shown = NameBox(name.Key);
                    if (shown == null) continue;
                    MoveX(name.Key, frame.center.x - shown.Value.center.x);
                    MoveY(name.Key, above + Gap - shown.Value.yMin);
                }
                foreach (var item in m_Variants)
                {
                    int row = m_VariantsRow[item.Key];
                    // A window of a row opens above the top row: the rows above that row are lifted over.
                    item.Key.anchoredPosition = item.Value + new Vector2(0f, (m_Count - 1 - row) * Pitch + over);
                }
                // The windows of the buttons sit on the top edge of the top row (in turn-based combat, above the
                // prediction block and its hints).
                float windowsBottom = (tbm ? above : m_Paper0.yMax + extra) + Gap;
                foreach (var window in m_Windows)
                {
                    if (!window.Key.gameObject.activeInHierarchy) continue;
                    MoveY(window.Key, windowsBottom - Box(window.Value).yMin);
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
