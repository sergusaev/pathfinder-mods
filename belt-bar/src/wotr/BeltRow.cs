using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.MVVM._ConsoleView.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using Kingmaker.UI.UnitSettings;
using Owlcat.Runtime.UI.ConsoleTools;
using Owlcat.Runtime.UI.ConsoleTools.GamepadInput;
using Owlcat.Runtime.UI.ConsoleTools.NavigationTool;
using Owlcat.Runtime.UI.Controls.Button;
using Rewired;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace BeltBar
{
    // The row of belt slots above the action bar of Wrath of the Righteous: the window of the weapon sets with one
    // slot per cell instead of two, one cell per belt slot. The window, its backdrop and the cells are the weapon
    // window's own, so the backdrop follows the cells; the window hangs by its bottom edge, so it is shorter from the
    // top. In every cell sits a copy of an action bar slot, bound to the game's own slot logic: a belt item is used by
    // the same code as from any slot of the bar (the charges of a wand, the refill of the slot, the mark of an item
    // switched on).
    sealed class BeltRow : IDisposable
    {
        const string CellPrefix = "ActionBarWeaponSetConsoleView";
        // The slot of the bar is 50 wide, the weapon slot of the window 64.
        const float SlotSize = 50f, SlotScale = 1.28f;
        static readonly string[] Roman = { "I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X" };

        internal readonly UnitEntityData Unit;
        readonly BeltButton m_Button;
        readonly RectTransform m_Sets;
        readonly List<ActionBarSlotVM> m_Vms = new List<ActionBarSlotVM>();
        readonly List<GameObject> m_Slots = new List<GameObject>();
        readonly GridConsoleNavigationBehaviour m_Nav;
        readonly InputLayer m_Layer;
        bool m_Disposed;

        internal BeltRow(BeltButton button)
        {
            m_Button = button;
            Unit = button.Unit;
            m_Sets = button.SetsContainer;
            var template = button.View.GetComponentsInChildren<ActionBarBaseSlotConsoleView>(true)
                .FirstOrDefault(s => s.transform.parent != null && s.transform.parent.name == "MainSlots");
            if (template == null) throw new InvalidOperationException("no bar slot to copy");

            var quickSlots = Unit.Body.QuickSlots.ToList();
            var cells = FitCells(quickSlots.Count);

            var entities = new List<IConsoleEntity>();
            for (int i = 0; i < quickSlots.Count; i++)
            {
                var cell = cells[i];
                PrepareCell(cell, i);
                var primary = (RectTransform)cell.Find("Primary");
                var secondary = (RectTransform)cell.Find("Secondary");

                var slotObject = UnityEngine.Object.Instantiate(template.gameObject, cell, false);
                slotObject.name = "BeltSlot";
                slotObject.SetActive(true);
                var rect = (RectTransform)slotObject.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(SlotSize, SlotSize);
                rect.localScale = new Vector3(SlotScale, SlotScale, 1f);
                // Where the first weapon was, moved to the middle of the cell, which lost the second one.
                rect.anchoredPosition = new Vector2(primary.anchoredPosition.x, (primary.anchoredPosition.y + secondary.anchoredPosition.y) / 2f);
                m_Slots.Add(slotObject);

                MechanicActionBarSlot slot;
                if (quickSlots[i].HasItem) slot = new MechanicActionBarSlotItem { Item = quickSlots[i].Item, Unit = Unit };
                else slot = new MechanicActionBarSlotEmpty { Unit = Unit };
                var vm = new ActionBarSlotVM(slot, -1 - i);
                m_Vms.Add(vm);
                vm.OnClickCommand.Subscribe(ignore => m_Button.RequestClose(true));
                vm.UpdateResource();

                var view = slotObject.GetComponent<ActionBarBaseSlotConsoleView>();
                view.Initialize();
                view.Bind(vm);
                // Items offer no cells of a higher level (a potion for another character is not offered here).
                var convert = slotObject.transform.Find("Decoration/ConvertButton");
                if (convert != null) convert.localScale = Vector3.zero;
                BringNumberToFront(cell);
                if (!vm.IsEmpty.Value && view.ConsoleEntityProxy != null) entities.Add(view.ConsoleEntityProxy);
            }
            LayoutRebuilder.ForceRebuildLayoutImmediate(m_Sets);

            var group = m_Sets.GetComponent<CanvasGroup>();
            if (group != null) group.alpha = 1f;
            m_Sets.gameObject.SetActive(true);

            m_Nav = new GridConsoleNavigationBehaviour(null, null, new Vector2Int(1, 0));
            m_Nav.SetEntitiesHorizontal(entities);
            m_Nav.FocusOnFirstValidEntity();

            m_Layer = m_Nav.GetInputLayer(new InputLayer { ContextName = "BeltBarRow" });
            m_Layer.AddButton(delegate { m_Button.CloseRow(false); }, 9, InputActionEventType.ButtonJustReleased);
            m_Layer.AddButton(delegate { m_Button.CloseRow(false); }, 7);
            m_Layer.AddButton(delegate { m_Button.CloseRow(false); }, 16);
            GamePad.Instance.PushLayer(m_Layer);
        }

        // The number of a cell sits over its top edge, in front of the slot and of the backdrop, as in the weapon window.
        static void BringNumberToFront(Transform cell)
        {
            var number = cell.Find("SetIndex");
            if (number == null) return;
            number.SetAsLastSibling();
            var parentCanvas = cell.GetComponentInParent<Canvas>();
            var canvas = number.GetComponent<Canvas>();
            if (canvas == null) canvas = number.gameObject.AddComponent<Canvas>();
            canvas.overrideSorting = true;
            if (parentCanvas != null)
            {
                canvas.sortingLayerID = parentCanvas.sortingLayerID;
                canvas.sortingOrder = parentCanvas.sortingOrder + 1;
            }
        }

        // One weapon set cell per belt slot: surplus ones go, missing ones are copies of the last.
        List<Transform> FitCells(int count)
        {
            var cells = m_Sets.Cast<Transform>().Where(t => t.name.StartsWith(CellPrefix)).ToList();
            if (cells.Count == 0) throw new InvalidOperationException("no weapon set cells to copy");
            while (cells.Count > count)
            {
                UnityEngine.Object.DestroyImmediate(cells[cells.Count - 1].gameObject);
                cells.RemoveAt(cells.Count - 1);
            }
            while (cells.Count < count)
            {
                var copy = UnityEngine.Object.Instantiate(cells[cells.Count - 1].gameObject, m_Sets, false);
                copy.name = CellPrefix + " (" + cells.Count + ")";
                cells.Add(copy.transform);
            }
            return cells;
        }

        // The weapon slots and the weapon set logic of a cell go; the background, the number and the selection mark
        // stay, the cell is as high as one slot needs. Done once for a cell: its weapon slots are off afterwards.
        static void PrepareCell(Transform cell, int index)
        {
            var primary = cell.Find("Primary") as RectTransform;
            var secondary = cell.Find("Secondary") as RectTransform;
            if (primary == null || secondary == null) throw new InvalidOperationException("weapon set cell not as expected");

            var label = cell.Find("SetIndex/IndexLabel");
            var text = label != null ? label.GetComponent("TextMeshProUGUI") : null;
            if (text != null)
            {
                var property = text.GetType().GetProperty("text");
                if (property != null) property.SetValue(text, Roman[Math.Min(index, Roman.Length - 1)], null);
            }
            if (!primary.gameObject.activeSelf) return;

            float step = primary.anchoredPosition.y - secondary.anchoredPosition.y;
            var logic = cell.GetComponent<ActionBarWeaponSetConsoleView>();
            if (logic != null) UnityEngine.Object.DestroyImmediate(logic);
            primary.gameObject.SetActive(false);
            secondary.gameObject.SetActive(false);
            foreach (string name in new[] { "Grip", "SelectMarker" })
            {
                var part = cell.Find(name);
                if (part != null) part.gameObject.SetActive(false);
            }
            foreach (string name in new[] { "BackgroundImage (2)", "BackgroundImage (3)", "ActiveObject" })
            {
                var part = cell.Find(name) as RectTransform;
                if (part != null) part.sizeDelta = new Vector2(part.sizeDelta.x, part.sizeDelta.y - step);
            }
            var element = cell.GetComponent<LayoutElement>();
            var rect = (RectTransform)cell;
            Main.Log("weapon cell: step " + step + ", layout min " + element.minWidth + "x" + element.minHeight + " preferred " +
                element.preferredWidth + "x" + element.preferredHeight + ", size " + rect.sizeDelta);
            if (element != null)
            {
                if (element.minHeight > step) element.minHeight -= step;
                if (element.preferredHeight > step) element.preferredHeight -= step;
            }
            if (rect.sizeDelta.y > step) rect.sizeDelta = new Vector2(rect.sizeDelta.x, rect.sizeDelta.y - step);
        }

        // The slots show their charges and states by what the game computes for them every frame.
        internal void Tick()
        {
            foreach (var vm in m_Vms) vm.UpdateResource();
        }

        public void Dispose()
        {
            if (m_Disposed) return;
            m_Disposed = true;
            if (m_Nav != null) m_Nav.UnFocusCurrentEntity();
            if (m_Layer != null) GamePad.Instance.PopLayer(m_Layer);
            foreach (var vm in m_Vms)
            {
                try { vm.Dispose(); }
                catch (Exception e) { Main.Error(e, "dispose slot"); }
            }
            // The unfocus fade of a slot runs on the global MainThreadDispatcher, not on the slot, and would touch the
            // slot's CanvasGroup after it is gone: hide the slot now, destroy it once the fade is over.
            foreach (var slot in m_Slots)
            {
                slot.SetActive(false);
                UnityEngine.Object.Destroy(slot, 1f);
            }
            if (m_Sets != null) m_Sets.gameObject.SetActive(false);
        }
    }
}
