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
using Kingmaker.UI.TurnBasedMode;
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
    // next to it. The whole bar moves left by what the gap and the second frame take. In turn-based combat the
    // prediction panel gets a third block of its own, a small gap above the slots, in the middle over them.
    sealed class Rows : MonoBehaviour
    {
        internal const int MaxRows = 3;
        const int PageSize = 12, Pages = 10;
        // Between the two frames.
        const float BlockGap = 4f;

        static readonly FieldInfo NavField = AccessTools.Field(typeof(ActionBarConsoleView), "m_NavigationBehaviour");
        static readonly FieldInfo IsConvertField = AccessTools.Field(typeof(ActionBarConsoleView), "m_IsConvert");
        static readonly FieldInfo StickHintField = AccessTools.Field(typeof(ActionBarConsoleView), "m_RightStickButtonHint");
        static readonly string[] NameHintFields = { "m_ContextMenuHint", "m_InspectHint" };
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

        // The blocks: papers and frames. The slots' paper and frame are the game's own, the rest are copies. The
        // game's turn-based frame (the slots frame grown around the prediction panel) is not shown.
        RectTransform m_SlotsPaper, m_ButtonsPaper, m_SlotsFrame, m_SlotsFrameTbm, m_ButtonsFrame;
        RectTransform m_Prediction, m_PredictionHints, m_PredictionPaper, m_PredictionFrame;
        bool m_PredictionCut;
        float m_FrameWidth0;
        // The panel's actions laid out by the rows, side by side over the whole width of the slots block: five-foot
        // step, move, standard, swift, free; the full round one over move and standard. The move and the standard
        // share what the others leave.
        static readonly string[] ActionFields =
            { "m_FiveFootAction", "m_MoveAction", "m_StandardAction", "m_SwiftAction", "m_FreeAction", "m_FullRoundActionView" };
        RectTransform[] m_Actions;
        float[] m_ActionWidths, m_ActionHeights;
        float m_ActionGap;
        readonly List<RectTransform> m_Buttons = new List<RectTransform>();
        readonly List<Patch> m_Patches = new List<Patch>();
        // The game's layout, measured once: the space between slots (kept around them on every side and between the
        // rows), the frame's edges outside the paper, how much higher the turn-based frame is.
        float m_Margin, m_Pitch, m_EdgeX, m_EdgeBottom, m_EdgeTop, m_TbmShift;

        // Above the rows stands what stands above the bar alone; the variants window of each slot opens above the
        // top row. The game moves them itself (with tweens): the rows lift them from wherever the game put them last.
        sealed class Lifted { internal float Base, Written; }
        readonly Dictionary<RectTransform, Lifted> m_TopItems = new Dictionary<RectTransform, Lifted>();
        // The name of the slot under the cursor with the hints above it, and the windows of the buttons: each a small
        // gap above the frame.
        RectTransform m_SpellPanel, m_SpellName;
        float m_SpellPanelX;
        readonly List<RectTransform> m_Windows = new List<RectTransform>();
        const string ButtonPath = "ActionBarWeaponConsoleView";
        readonly Dictionary<RectTransform, Lifted> m_Variants = new Dictionary<RectTransform, Lifted>();
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
            m_FrameWidth0 = frame0.width;

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
                Patch.Make(m_ButtonsFrame, Sides(buttonsWidth, frame0.width), frameY),
                Patch.Make(m_SlotsPaper, paperX, paperY),
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

            // The prediction panel of turn-based combat: its own paper and frame, drawn right under it. Its frame is
            // cut when the panel first shows, to the panel's width.
            if (m_SlotsFrameTbm != null)
                foreach (var graphic in m_SlotsFrameTbm.GetComponentsInChildren<Graphic>(true)) graphic.enabled = false;
            m_Prediction = m_Main.Find("PredictionPanelConsoleView") as RectTransform;
            if (m_Prediction != null)
            {
                m_PredictionHints = m_Prediction.Find("PredictionHints") as RectTransform;
                Transform under = m_Prediction;
                while (under.parent != m_Main) under = under.parent;
                int at = under.GetSiblingIndex();
                m_PredictionPaper = Copy(paper, m_Main, "ActionBarPredictionPaper", at);
                m_PredictionFrame = Copy(m_SlotsFrame, m_Main, "ActionBarPredictionFrame", at + 1);
                var cut = Patch.Make(m_PredictionPaper, paperX, paperY);
                if (cut != null) m_Patches.Add(cut);
                m_PredictionPaper.gameObject.SetActive(false);
                m_PredictionFrame.gameObject.SetActive(false);

                var view = m_Prediction.GetComponent<PredictionPanelBaseView>();
                var actions = view == null ? null : ActionFields
                    .Select(name => AccessTools.Field(typeof(PredictionPanelBaseView), name).GetValue(view) as Component)
                    .Select(action => action != null ? action.transform as RectTransform : null).ToArray();
                if (actions != null && actions.All(action => action != null))
                {
                    m_Actions = actions;
                    m_ActionWidths = actions.Select(action => Box(action).width).ToArray();
                    m_ActionHeights = actions.Select(action => Box(action).height).ToArray();
                    m_ActionGap = Mathf.Clamp(Box(actions[1]).xMin - Box(actions[0]).xMax, 0f, m_Margin);
                    foreach (var action in actions)
                    {
                        for (var parent = action.parent; parent != null && parent != m_Main; parent = parent.parent)
                        {
                            foreach (var group in parent.GetComponents<LayoutGroup>()) group.enabled = false;
                            foreach (var fitter in parent.GetComponents<ContentSizeFitter>()) fitter.enabled = false;
                        }
                        Stretch(action);
                    }
                }
                else Main.Log("prediction panel actions not as expected, the panel is moved whole");
            }
            else Main.Log("no prediction panel over the bar, it stays where it is");
            var groups = m_Main.Find("AbilitiesGroupsContainer") as RectTransform;
            if (groups == null) Main.Log("no AbilitiesGroupsContainer over the bar, it stays where it is");
            AddTop(groups);
            m_SpellPanel = m_Main.Find("SpellPanelContainer") as RectTransform;
            m_SpellName = m_Main.Find("SpellPanelContainer/SpellNamePanel") as RectTransform;
            if (m_SpellPanel != null) m_SpellPanelX = m_SpellPanel.anchoredPosition.x;
            foreach (string name in NameHintFields)
            {
                var hint = AccessTools.Field(typeof(ActionBarConsoleView), name).GetValue(View) as Component;
                var rect = hint != null ? hint.transform as RectTransform : null;
                if (rect != null) m_NameHints.Add(rect);
                else Main.Log(name + " not found, it stays where it is");
            }
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
            if (rect != null) m_TopItems[rect] = new Lifted { Base = rect.anchoredPosition.y, Written = rect.anchoredPosition.y };
        }

        void AddVariants(ActionBarBaseSlotView slot, int row)
        {
            var variants = slot != null ? slot.transform.Find("SubGroup") as RectTransform : null;
            if (variants == null) return;
            m_Variants[variants] = new Lifted { Base = variants.anchoredPosition.y, Written = variants.anchoredPosition.y };
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

        // What the prediction panel shows, its hints left out: they move with it.
        Rect PredictionBox()
        {
            var boxes = m_Prediction.GetComponentsInChildren<Graphic>()
                .Where(g => g.enabled && g.color.a > 0.01f && !(g is TMP_Text && string.IsNullOrEmpty(((TMP_Text)g).text))
                    && (m_PredictionHints == null || !g.transform.IsChildOf(m_PredictionHints)))
                .Select(g => Box(g.rectTransform)).ToList();
            return boxes.Count == 0 ? Box(m_Prediction) : Union(boxes);
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

        // The prediction panel's hints, as the game makes them: the first at the left edge of the prediction block,
        // the others at its right edge, a small gap above it. Done again after the game's layout, which stacks them
        // in its column whenever they change (m_HintsLeft > m_HintsRight: no block, nothing to do). Returns the top
        // of what shows, or the bottom when nothing does. The hints of the name above (the slot's context menu, the
        // enemy's inspection) join them in the middle, under the name; out of combat they are back where the game
        // put them.
        float m_HintsLeft = 1f, m_HintsRight, m_HintsBottom;
        readonly List<RectTransform> m_NameHints = new List<RectTransform>();
        readonly Dictionary<RectTransform, Vector2> m_NameHintsAt = new Dictionary<RectTransform, Vector2>();

        float PlaceHints()
        {
            float top = m_HintsBottom - BlockGap;
            if (m_HintsLeft > m_HintsRight)
            {
                foreach (var hint in m_NameHintsAt) hint.Key.anchoredPosition = hint.Value;
                m_NameHintsAt.Clear();
                return top;
            }
            if (m_PredictionHints != null && m_PredictionHints.gameObject.activeInHierarchy)
            {
                float right = m_HintsRight;
                for (int i = m_PredictionHints.childCount - 1; i >= 0; i--)
                {
                    var hint = m_PredictionHints.GetChild(i);
                    if (!hint.gameObject.activeInHierarchy) continue;
                    var shown = Visible(hint);
                    float x = i == 0 ? m_HintsLeft : right - shown.width;
                    if (i > 0) right = x - 2f * m_Margin;
                    var moved = new Vector2(x - shown.xMin, m_HintsBottom - shown.yMin);
                    if (moved.sqrMagnitude > 0.0001f) hint.position += m_Main.TransformVector(moved);
                    top = Mathf.Max(top, shown.yMax + moved.y);
                }
            }
            // Side by side, together in the middle.
            var shownHints = m_NameHints.Where(hint => hint.gameObject.activeInHierarchy).ToList();
            var boxes = shownHints.Select(hint => Visible(hint)).ToList();
            float at = (m_HintsLeft + m_HintsRight - boxes.Sum(box => box.width) - 2f * m_Margin * Mathf.Max(0, boxes.Count - 1)) / 2f;
            for (int i = 0; i < shownHints.Count; i++)
            {
                var hint = shownHints[i];
                if (!m_NameHintsAt.ContainsKey(hint)) m_NameHintsAt[hint] = hint.anchoredPosition;
                var moved = new Vector2(at - boxes[i].xMin, m_HintsBottom - boxes[i].yMin);
                if (moved.sqrMagnitude > 0.0001f) hint.position += m_Main.TransformVector(moved);
                top = Mathf.Max(top, boxes[i].yMax + moved.y);
                at += boxes[i].width + 2f * m_Margin;
            }
            return top;
        }

        void AfterLayout()
        {
            try
            {
                if (m_Built && !m_Failed) PlaceHints();
            }
            catch (Exception e)
            {
                Main.Error(e, "prediction hints");
                Canvas.willRenderCanvases -= AfterLayout;
            }
        }

        void OnEnable() { Canvas.willRenderCanvases += AfterLayout; }

        void OnDisable() { Canvas.willRenderCanvases -= AfterLayout; }

        // What a transform shows, its texts by their letters, not their rectangles.
        Rect Shown(Transform root)
        {
            var boxes = new List<Rect>();
            foreach (var graphic in root.GetComponentsInChildren<Graphic>())
            {
                if (!graphic.enabled) continue;
                var text = graphic as TMP_Text;
                if (text == null)
                {
                    boxes.Add(Box(graphic.rectTransform));
                    continue;
                }
                var bounds = text.textBounds;
                if (string.IsNullOrEmpty(text.text) || bounds.size.x <= 0f || bounds.size.x > 10000f) continue;
                Vector2 a = m_Main.InverseTransformPoint(text.transform.TransformPoint(bounds.min));
                Vector2 b = m_Main.InverseTransformPoint(text.transform.TransformPoint(bounds.max));
                boxes.Add(Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.y, b.y), Mathf.Max(a.x, b.x), Mathf.Max(a.y, b.y)));
            }
            return boxes.Count == 0 ? Box((RectTransform)root) : Union(boxes);
        }

        // Moves what the game placed by the rows' lift from where the game put it last.
        static void Lift(RectTransform rect, Lifted lifted, float lift)
        {
            var position = rect.anchoredPosition;
            if (Mathf.Abs(position.y - lifted.Written) > 0.01f) lifted.Base = position.y;
            lifted.Written = lifted.Base + lift;
            rect.anchoredPosition = new Vector2(position.x, lifted.Written);
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
            var scale = rect.localScale;
            // Shrunk to nothing (an animation): no size to give it.
            if (Mathf.Abs(scale.x) < 0.001f || Mathf.Abs(scale.y) < 0.001f) return;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
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
            bool tbm = Vm != null && Vm.PredictionPanelVm.Value != null && m_Prediction != null && m_Prediction.gameObject.activeInHierarchy;
            float extra = (m_Count - 1) * m_Pitch;
            float M = m_Margin;
            var content = SlotsBox();
            var container = Box(m_Slots).center;
            for (int k = 0; k < m_Rows.Count; k++)
            {
                Place(m_Rows[k].Rect, new Rect(content.x, content.y + (k + 1) * m_Pitch, content.width, content.height));
                m_Rows[k].Copy.anchoredPosition = container - content.center;
            }

            // The slots block: one paper under all rows, the frame around it.
            var paper = Rect.MinMaxRect(content.xMin - M, content.yMin - M, content.xMax + M, content.yMax + extra + M);
            Place(m_SlotsPaper, paper);
            var frame = Rect.MinMaxRect(paper.xMin - m_EdgeX, content.yMin - M - m_EdgeBottom, paper.xMax + m_EdgeX, content.yMax + M + m_EdgeTop + extra);
            Place(m_SlotsFrame, frame);
            if (m_SlotsFrameTbm != null) Place(m_SlotsFrameTbm, frame);

            // The prediction block: as wide as the slots block, the gap between them the one between the slots and the
            // buttons blocks.
            float top = frame.yMax, above = top;
            if (m_PredictionPaper != null)
            {
                m_PredictionPaper.gameObject.SetActive(tbm);
                m_PredictionFrame.gameObject.SetActive(tbm);
            }
            if (tbm)
            {
                var shown = PredictionBox();
                var moved = new Vector2(m_Actions != null ? 0f : frame.center.x - shown.center.x, frame.yMax + BlockGap + m_EdgeBottom + M - shown.yMin);
                if (moved.sqrMagnitude > 0.0001f) m_Prediction.position += m_Main.TransformVector(moved);
                shown.position += moved;
                var predictionPaper = m_Actions != null
                    ? Rect.MinMaxRect(paper.xMin, shown.yMin - M, paper.xMax, shown.yMax + M)
                    : Rect.MinMaxRect(shown.xMin - M, shown.yMin - M, shown.xMax + M, shown.yMax + M);
                if (m_Actions != null) PlaceActions(predictionPaper.xMin + M, predictionPaper.xMax - M);
                var predictionFrame = Rect.MinMaxRect(predictionPaper.xMin - m_EdgeX, predictionPaper.yMin - m_EdgeBottom,
                    predictionPaper.xMax + m_EdgeX, predictionPaper.yMax + m_EdgeTop);
                if (!m_PredictionCut)
                {
                    m_PredictionCut = true;
                    var cut = Patch.Make(m_PredictionFrame, Sides(predictionFrame.width, m_FrameWidth0), new[] { 0f, 0.4f, 0.6f, 1f });
                    if (cut != null) m_Patches.Add(cut);
                }
                Place(m_PredictionPaper, predictionPaper);
                Place(m_PredictionFrame, predictionFrame);
                top = predictionFrame.yMax;
                m_HintsLeft = predictionPaper.xMin;
                m_HintsRight = predictionPaper.xMax;
                m_HintsBottom = top + BlockGap;
                above = Mathf.Max(top, PlaceHints());
            }
            else
            {
                m_HintsLeft = 1f;
                m_HintsRight = 0f;
            }

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

            // Above the top row stands what stands above the bar alone; in turn-based combat, above the prediction
            // block, where the game's own frame with the panel ended.
            float over = top - frame.yMax;
            foreach (var item in m_TopItems) Lift(item.Key, item.Value, extra + (tbm ? over - m_TbmShift : 0f));
            foreach (var item in m_Variants)
            {
                int row = m_VariantsRow[item.Key];
                // A window of a row opens above the top row: the rows above that row are lifted over.
                Lift(item.Key, item.Value, (m_Count - 1 - row) * m_Pitch + over);
            }
            // The name of the slot and the windows of the buttons stand a small gap above the slots frame (in
            // turn-based combat, above the prediction block and its hints).
            if (m_SpellPanel != null && m_SpellName != null)
            {
                // In turn-based combat, in the middle over the slots block too.
                if (tbm) MoveX(m_SpellPanel, frame.center.x - Box(m_SpellName).center.x);
                else m_SpellPanel.anchoredPosition = new Vector2(m_SpellPanelX, m_SpellPanel.anchoredPosition.y);
                MoveY(m_SpellPanel, above + BlockGap - Box(m_SpellName).yMin);
            }
            foreach (var window in m_Windows)
                if (window.gameObject.activeInHierarchy) MoveY(window, above + BlockGap - Visible(window).yMin);
        }

        void MoveY(RectTransform rect, float dy)
        {
            if (Mathf.Abs(dy) < 0.01f) return;
            rect.position += m_Main.TransformVector(new Vector3(0f, dy, 0f));
        }

        void OnDestroy()
        {
            // On quitting the game the input layer of the hint may be gone before it.
            try { m_HintBind?.Dispose(); }
            catch (NullReferenceException) { }
            if (Instance == this) Instance = null;
        }
    }
}
