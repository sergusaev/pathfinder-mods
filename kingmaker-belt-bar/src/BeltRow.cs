using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Assets.Console.GamepadInput;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI;
using Kingmaker.UI._ConsoleUI.ActionBar;
using Kingmaker.UI._ConsoleUI.Common;
using Kingmaker.UI._ConsoleUI.Models;
using Kingmaker.UI._ConsoleUI.Utils.MultiNavigationTool;
using Kingmaker.UI.UnitSettings;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace BeltBar
{
    // The row of belt slots above the action bar: the window of the weapon sets with one weapon slot per cell instead
    // of two, one cell per belt slot. The cells, their frames, numbers and the spacing are the weapon window's own;
    // the window sits on the same line above the bar, so it is shorter from the top. In every cell sits a copy of an
    // action bar slot, bound to the game's own slot logic: a belt item is used by the same code as from any slot of
    // the bar (the charges of a wand, the refill of the slot, the mark of an item switched on).
    sealed class BeltRow : IDisposable
    {
        // Positions in the window of the weapon sets: the first cell, the step between cells, the margin right of the last.
        const float FirstCellX = 48f, CellPitch = 90f, RightMargin = 58f;
        // The icon of a weapon is 54 wide, the copy of a bar slot (48 for the icon) is scaled to match it.
        const float SlotScale = 1.12f;
        const float ShowY = 37f, ShowYTurnBased = 100f;
        static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };
        const string TopSuffix = "Top", BottomSuffix = "Bottom";

        // What the weapon cell looks like, measured once from the game's own cell (see Measure).
        static bool s_Measured;
        static float s_Step;        // distance between the two weapon slots of a cell: the band that is cut out
        static float s_CutFromTop;  // where the cut starts, from the top of the cell
        static float s_CellHeight;  // height of the weapon cell
        static float s_RootHeight;  // height of the weapon window
        static float s_FrameScaleX = 1f, s_FrameScaleY = 1f, s_FrameMarginX, s_FrameMarginY;
        static Vector2 s_DecorInset;

        internal readonly UnitEntityData Unit;
        readonly BeltButton m_Button;
        readonly List<ActionBarSlotVM> m_Vms = new List<ActionBarSlotVM>();
        readonly List<ConsoleSelectableEntity> m_Entities = new List<ConsoleSelectableEntity>();
        readonly List<Transform> m_SelectBottoms = new List<Transform>();
        readonly List<GameObject> m_Cells = new List<GameObject>();
        readonly ConsoleMultiNavigationCollection m_Nav = new ConsoleMultiNavigationCollection();
        readonly Transform m_Root;
        InputLayer m_Layer;
        bool m_Disposed;

        internal BeltRow(BeltButton button)
        {
            m_Button = button;
            Unit = UnitSelectionManager.Instance.CurrentSelectUnit.Value;
            m_Root = button.transform.Find("WeaponSetsView");
            var template = button.View.transform.Find("ActionBar/Slots/Console_ActionBarSlot_01");
            if (m_Root == null || template == null) throw new InvalidOperationException("action bar layout not as expected");

            var rootRect = (RectTransform)m_Root;
            var group = m_Root.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
                group.blocksRaycasts = false;
            }
            m_Root.gameObject.SetActive(true);

            var quickSlots = Unit.Body.QuickSlots.ToList();
            int count = quickSlots.Count;
            var sets = FitSets(rootRect, count);

            ActionBarSlotView first = null;
            var views = new List<ActionBarSlotView>();
            for (int i = 0; i < count; i++)
            {
                var entity = sets[i].Find("Console_WeaponSetEntityView");
                PrepareCell(entity, i);
                m_Entities.Add(entity.GetComponent<ConsoleSelectableEntity>());
                m_SelectBottoms.Add(entity.Find("Select" + BottomSuffix));

                var frame = entity.Find("Primary_Back") as RectTransform;
                var cell = UnityEngine.Object.Instantiate(template.gameObject, entity, false);
                cell.name = "BeltSlot_" + i;
                cell.SetActive(true);
                var rect = (RectTransform)cell.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.localScale = new Vector3(SlotScale, SlotScale, 1f);
                // The middle of the frame of the first weapon slot: the frame hangs by its top edge.
                rect.anchoredPosition = new Vector2(frame.anchoredPosition.x,
                    frame.anchoredPosition.y - frame.rect.height * frame.localScale.y / 2f);
                m_Cells.Add(cell);

                MechanicActionBarSlot slot;
                if (quickSlots[i].HasItem) slot = new MechanicActionBarSlotItem { Item = quickSlots[i].Item, Unit = Unit };
                else slot = new MechanicActionBarSlotEmpty { Unit = Unit };

                var vm = new ActionBarSlotVM(OnSlotSelected, button.BarVm.Active);
                m_Vms.Add(vm);
                vm.OnClickCommand.Subscribe(ignore => m_Button.RequestClose(true));
                vm.SetMechanicSlot(new IndexedMechanicSlot { Index = -1 - i, Slot = slot });

                var view = cell.GetComponent<ActionBarSlotView>();
                view.Initialize();
                view.Bind(vm);
                // The frame of the cell is the weapon window's own; items offer no cells of a higher level
                // (a potion for another character is not offered here).
                HideChild(cell.transform, "BackgroundIcon/Frame");
                HideChild(cell.transform, "BackgroundIcon/ToggleAdditionalSpells");
                if (!vm.IsEmpty)
                {
                    views.Add(view);
                    if (first == null) first = view;
                }
            }

            rootRect.localPosition = new Vector3(rootRect.localPosition.x, button.BarVm.TBMModeIsActive.Value ? ShowYTurnBased : ShowY, rootRect.localPosition.z);

            m_Nav.SetEntitiesHorizontal(views.Cast<IConsoleMultiNavigationEntity>().ToList());
            m_Nav.SelectCurrentEntity(first);

            m_Layer = m_Nav.GetInputLayer(new InputLayer { ContextName = "BeltBarRow" });
            m_Layer.AddButton(delegate { Confirm(); }, 8);
            m_Layer.AddButton(delegate { m_Button.CloseRow(false); }, 9);
            m_Layer.AddButton(delegate { m_Button.CloseRow(false); }, 7);
            GamePad.Instance.PushLayer(m_Layer);
            Game.Instance.UI.Common.UISound.Play(UISoundType.ActionBarFocusOn);
        }

        static void HideChild(Transform parent, string path)
        {
            var child = parent.Find(path);
            if (child != null) child.gameObject.SetActive(false);
        }

        // The weapon cell holds two weapon slots one under the other; the belt cell holds one. The distance between the
        // slots and the place of the cut (the middle of the gap between them) are taken from the cell itself.
        static void Measure(RectTransform root, List<Transform> sets)
        {
            if (s_Measured) return;
            var entity = sets[0].Find("Console_WeaponSetEntityView");
            var first = (RectTransform)entity.Find("Primary_Back");
            var second = (RectTransform)entity.Find("Secondary_Back");
            var cell = (RectTransform)entity;
            s_CellHeight = cell.rect.height;
            s_Step = first.anchoredPosition.y - second.anchoredPosition.y;
            float frameHeight = first.rect.height * first.localScale.y;
            float gap = s_Step - frameHeight;
            // From the middle of the cell: the bottom of the first frame and half the gap below it.
            float cutFromMiddle = first.anchoredPosition.y - frameHeight - gap / 2f;
            s_CutFromTop = s_CellHeight / 2f - cutFromMiddle;
            s_RootHeight = root.sizeDelta.y;

            // The backdrop (frame) is drawn smaller than its size says, by the scale it is given: its margins around the
            // cells are measured as they are seen, then kept around any number of cells.
            var frame = root.Find("Frame") as RectTransform;
            if (frame != null)
            {
                s_FrameScaleX = frame.lossyScale.x / root.lossyScale.x;
                s_FrameScaleY = frame.lossyScale.y / root.lossyScale.y;
                s_FrameMarginX = (frame.sizeDelta.x * s_FrameScaleX - sets.Count * CellPitch) / 2f;
                s_FrameMarginY = frame.sizeDelta.y * s_FrameScaleY - s_CellHeight;
                var decor = frame.Find("Decor") as RectTransform;
                s_DecorInset = decor != null ? frame.sizeDelta - decor.sizeDelta : Vector2.zero;
            }
            s_Measured = true;
            Main.Log("weapon cell: height " + s_CellHeight + ", slot step " + s_Step + ", frame " + frameHeight + ", cut " + s_CutFromTop +
                " from the top; backdrop scale " + s_FrameScaleX + ", margins " + s_FrameMarginX + " x " + s_FrameMarginY);
        }

        // One weapon set cell per belt slot: surplus ones go, missing ones are copies of the last. The cells are as high
        // as one weapon slot needs; the window is as high and as wide as its cells, with the backdrop (frame and
        // decoration) around them at the margins the weapon window has. The bottom of the window stays on the line of
        // the weapon window's bottom (the window hangs by its bottom edge), so it is shorter from the top.
        List<Transform> FitSets(RectTransform rootRect, int count)
        {
            var sets = m_Root.Cast<Transform>().Where(t => t.name.StartsWith("WeaponSet_")).OrderBy(t => t.name).ToList();
            if (sets.Count == 0) throw new InvalidOperationException("no weapon set cells to copy");
            Measure(rootRect, sets);
            while (sets.Count > count)
            {
                UnityEngine.Object.DestroyImmediate(sets[sets.Count - 1].gameObject);
                sets.RemoveAt(sets.Count - 1);
            }
            while (sets.Count < count)
            {
                var copy = UnityEngine.Object.Instantiate(sets[sets.Count - 1].gameObject, m_Root, false);
                copy.name = "WeaponSet_" + (sets.Count + 1).ToString("00");
                sets.Add(copy.transform);
            }
            float cellHeight = s_CellHeight - s_Step;
            for (int i = 0; i < sets.Count; i++)
            {
                var wrapper = (RectTransform)sets[i];
                wrapper.anchoredPosition = new Vector2(FirstCellX + CellPitch * i, wrapper.anchoredPosition.y);
                wrapper.sizeDelta = new Vector2(wrapper.sizeDelta.x, cellHeight);
                var entity = (RectTransform)sets[i].Find("Console_WeaponSetEntityView");
                entity.sizeDelta = new Vector2(entity.sizeDelta.x, cellHeight);
            }

            rootRect.sizeDelta = new Vector2(FirstCellX + CellPitch * count + RightMargin, s_RootHeight - s_Step);
            var frame = m_Root.Find("Frame") as RectTransform;
            if (frame != null)
            {
                float width = count * CellPitch + 2f * s_FrameMarginX;
                float height = cellHeight + s_FrameMarginY;
                frame.sizeDelta = new Vector2(width / s_FrameScaleX, height / s_FrameScaleY);
                var decor = frame.Find("Decor") as RectTransform;
                if (decor != null) decor.sizeDelta = frame.sizeDelta - s_DecorInset;
            }
            return sets;
        }

        // The weapons and the weapon set logic of a copied cell go; the background (cut to one slot), the selection and
        // the number stay.
        static void PrepareCell(Transform entityTransform, int index)
        {
            var entity = (RectTransform)entityTransform;
            var logic = entity.GetComponent<ActionBarWeaponSetEntityView>();
            if (logic != null) UnityEngine.Object.DestroyImmediate(logic);

            var label = entity.Find("IndexLabel");
            var text = label != null ? label.GetComponent("TextMeshProUGUI") : null;
            if (text != null)
            {
                var property = text.GetType().GetProperty("text");
                if (property != null) property.SetValue(text, Roman[Math.Min(index, Roman.Length - 1)], null);
            }

            if (entity.Find("Normal" + TopSuffix) != null) return;
            // The second weapon slot goes; the first moves down with the middle of the cell, which moved up.
            HideChild(entity, "Secondary_Back");
            HideChild(entity, "Secondary");
            HideChild(entity, "Primary");
            var frame = entity.Find("Primary_Back") as RectTransform;
            if (frame != null) frame.anchoredPosition = new Vector2(frame.anchoredPosition.x, frame.anchoredPosition.y - s_Step / 2f);

            // The background is one picture of the whole cell: cut out the band of the second slot by showing its
            // top and its bottom edge only, one above the other.
            float bottom = s_CellHeight - s_CutFromTop - s_Step;
            int sibling = 0;
            foreach (string name in new[] { "Normal", "Select" })
            {
                var picture = entity.Find(name) as RectTransform;
                if (picture == null) continue;
                var top = NewMask(entity, name + TopSuffix, true, s_CutFromTop);
                top.SetSiblingIndex(sibling++);
                var bottomMask = NewMask(entity, name + BottomSuffix, false, bottom);
                bottomMask.SetSiblingIndex(sibling++);
                var lower = (RectTransform)UnityEngine.Object.Instantiate(picture.gameObject, bottomMask, false).transform;
                picture.SetParent(top, false);
                Anchor(picture, true, s_CellHeight);
                Anchor(lower, false, s_CellHeight);
                lower.name = name + "Edge";
                bottomMask.name = name + BottomSuffix;
                // The bottom copy of a selection mark is shown with the cell's own mark.
                if (name == "Select") lower.gameObject.SetActive(true);
                bottomMask.gameObject.SetActive(name != "Select");
            }
        }

        static RectTransform NewMask(RectTransform parent, string name, bool atTop, float height)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(RectMask2D));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = new Vector2(0f, atTop ? 1f : 0f);
            rect.anchorMax = new Vector2(1f, atTop ? 1f : 0f);
            rect.pivot = new Vector2(0.5f, atTop ? 1f : 0f);
            rect.sizeDelta = new Vector2(0f, height);
            rect.anchoredPosition = Vector2.zero;
            return rect;
        }

        // The whole picture of the cell, hung by its top (or bottom) edge in a window showing a part of it.
        static void Anchor(RectTransform picture, bool atTop, float height)
        {
            picture.anchorMin = new Vector2(0f, atTop ? 1f : 0f);
            picture.anchorMax = new Vector2(1f, atTop ? 1f : 0f);
            picture.pivot = new Vector2(0.5f, atTop ? 1f : 0f);
            picture.sizeDelta = new Vector2(0f, height);
            picture.anchoredPosition = Vector2.zero;
        }

        // The slot views tell which of them is selected; the game's own bar does the same for its slots.
        void OnSlotSelected(ActionBarSlotVM selected)
        {
            for (int i = 0; i < m_Vms.Count; i++)
            {
                bool on = m_Vms[i] == selected;
                m_Vms[i].SetSelect(on);
                if (m_Entities[i] != null) m_Entities[i].SetSelected(on);
                if (m_SelectBottoms[i] != null) m_SelectBottoms[i].gameObject.SetActive(on);
            }
        }

        void Confirm()
        {
            var view = m_Nav.CurrentEntity as ActionBarSlotView;
            if (view != null) view.OnClick();
        }

        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;
            if (m_Layer != null) GamePad.Instance.PopLayer(m_Layer);
            foreach (var vm in m_Vms)
            {
                try
                {
                    vm.SetSelect(false);
                    vm.Dispose();
                }
                catch (Exception e) { Main.Error(e, "dispose slot"); }
            }
            foreach (var cell in m_Cells) UnityEngine.Object.Destroy(cell);
            foreach (var entity in m_Entities) if (entity != null) entity.SetSelected(false);
            foreach (var bottom in m_SelectBottoms) if (bottom != null) bottom.gameObject.SetActive(false);
            if (m_Root != null) m_Root.gameObject.SetActive(false);
        }
    }
}
