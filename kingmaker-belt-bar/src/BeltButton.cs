using System;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.UI;
using Kingmaker.UI._ConsoleUI.ActionBar;
using Kingmaker.UI._ConsoleUI.Common;
using Kingmaker.UI._ConsoleUI.Models;
using Kingmaker.UI._ConsoleUI.Utils.MultiNavigationTool;
using Rewired;
using UnityEngine;
using UnityEngine.UI;

namespace BeltBar
{
    // The button right of the weapon set button of the gamepad action bar. It is a copy of that button, so it looks
    // the same; the icon is the belt the unit wears, a placeholder when it wears none. The bar is made wider by the
    // button, the slots and the weapon set button stay where they were.
    sealed class BeltButton : MonoBehaviour, IConsoleMultiNavigationEntity
    {
        const string ButtonName = "BeltBarButton";
        // The button is 51 wide, the gap to the weapon set button is the 4 left of the 8 the bar keeps at its edge.
        const float Extension = 55f;
        const string PlaceholderSprite = "EquipIcon_Belt";
        // The whole bar moves left, so the button keeps a gap to the combat log panel at the right edge.
        const float PanelShift = 50f;

        static readonly List<BeltButton> s_Buttons = new List<BeltButton>();
        static Sprite s_Placeholder;

        internal ActionBarVM BarVm;
        internal ConsoleMultiNavigationCollection Nav;
        internal ActionBarView View;

        ConsoleSelectableEntity m_Select;
        Image m_Icon;
        GameObject m_Disable;
        Transform m_Arrow;
        BeltRow m_Row;
        Sprite m_ShownIcon;
        bool m_Pet;
        bool m_HasItems;
        bool m_CloseRequested, m_CloseBar;

        public Func<InputActionEventData, bool> OnLeft { get { return null; } }
        public Func<InputActionEventData, bool> OnRight { get { return null; } }
        public Func<InputActionEventData, bool> OnDown { get { return null; } }

        public Func<InputActionEventData, bool> OnUp
        {
            get
            {
                return delegate
                {
                    Open();
                    return true;
                };
            }
        }

        internal static void Attach(ActionBarView view)
        {
            var actionBar = view.transform.Find("ActionBar") as RectTransform;
            var weapon = actionBar != null ? actionBar.Find("CurrentWeaponSetView") as RectTransform : null;
            if (actionBar == null || weapon == null)
            {
                Main.Log("action bar layout not as expected, no belt button");
                return;
            }
            var existing = actionBar.Find(ButtonName);
            BeltButton button;
            if (existing != null) button = existing.GetComponent<BeltButton>();
            else button = Build(view, actionBar, weapon);
            if (button == null) return;
            button.View = view;
            button.Nav = Traverse.Create(view).Field("m_NavigationCollection").GetValue<ConsoleMultiNavigationCollection>();
            if (button.Nav != null) button.Nav.AddEntityHorizontal(button);
            if (!s_Buttons.Contains(button)) s_Buttons.Add(button);
        }

        static BeltButton Build(ActionBarView view, RectTransform actionBar, RectTransform weapon)
        {
            var copy = (RectTransform)UnityEngine.Object.Instantiate(weapon.gameObject, actionBar, false).transform;
            copy.name = ButtonName;
            // The copy keeps the look and drops the weapon set logic.
            var logic = copy.GetComponent<ActionBarCurrentWeaponSetView>();
            if (logic != null) UnityEngine.Object.DestroyImmediate(logic);
            var sets = copy.Find("WeaponSetsView");
            if (sets != null)
            {
                var setsLogic = sets.GetComponent<ActionBarWeaponSetsView>();
                if (setsLogic != null) UnityEngine.Object.DestroyImmediate(setsLogic);
            }

            // Wider bar: the left edge stays, the weapon set button keeps its place relative to the slots.
            var size = actionBar.sizeDelta;
            actionBar.sizeDelta = new Vector2(size.x + Extension, size.y);
            var position = actionBar.anchoredPosition;
            actionBar.anchoredPosition = new Vector2(position.x + Extension / 2f, position.y);
            var weaponPosition = weapon.anchoredPosition;
            weapon.anchoredPosition = new Vector2(weaponPosition.x - Extension, weaponPosition.y);

            ExtendFrames(view.transform);

            var panel = (RectTransform)view.transform;
            panel.anchoredPosition = new Vector2(panel.anchoredPosition.x - PanelShift, panel.anchoredPosition.y);

            var button = copy.gameObject.AddComponent<BeltButton>();
            button.m_Select = copy.GetComponent<ConsoleSelectableEntity>();
            var face = copy.Find("Button");
            button.m_Icon = face.Find("BackIcon").GetComponent<Image>();
            var disable = face.Find("Disable");
            button.m_Disable = disable != null ? disable.gameObject : null;
            var toggle = face.Find("ToggleAdditionalSpells");
            button.m_Arrow = toggle != null ? toggle.Find("Arrow") : null;
            button.SetArrow(false);
            if (sets != null) sets.gameObject.SetActive(false);
            return button;
        }

        // The frame of the bar and the background of the turn-based panel end at the old right edge. Whatever their
        // anchors are, each is measured in the screen and given the missing width on the right, from the outside in:
        // a parent that grew or moved takes its children along, so each child is put back to its own old left edge
        // and its old right edge plus the extension.
        static void ExtendFrames(Transform bar)
        {
            var chain = new List<RectTransform>();
            foreach (string path in new[] { "Hints", "Background", "Background/Lather", "Background/Lather_TBM", "Background/RaycastBlocker", "Console_PredictionPanel", "Console_PredictionPanel/Bakground" })
            {
                var rect = bar.Find(path) as RectTransform;
                if (rect != null) chain.Add(rect);
                else Main.Log("no " + path + " to widen");
            }
            var left = chain.Select(r => Edge(r, 0)).ToList();
            var right = chain.Select(r => Edge(r, 2)).ToList();
            float wanted = Extension * bar.lossyScale.x;
            for (int i = 0; i < chain.Count; i++)
            {
                var rect = chain[i];
                float scale = rect.lossyScale.x;
                float moveLeft = (left[i] - Edge(rect, 0)) / scale;
                float moveRight = (right[i] + wanted - Edge(rect, 2)) / scale;
                if (Mathf.Abs(moveLeft) < 0.01f && Mathf.Abs(moveRight) < 0.01f) continue;
                if (rect.anchorMin.x != rect.anchorMax.x)
                {
                    rect.offsetMin = new Vector2(rect.offsetMin.x + moveLeft, rect.offsetMin.y);
                    rect.offsetMax = new Vector2(rect.offsetMax.x + moveRight, rect.offsetMax.y);
                }
                else
                {
                    float grow = moveRight - moveLeft;
                    rect.sizeDelta = new Vector2(rect.sizeDelta.x + grow, rect.sizeDelta.y);
                    rect.anchoredPosition = new Vector2(rect.anchoredPosition.x + moveLeft + grow * rect.pivot.x, rect.anchoredPosition.y);
                }
            }

            // The plates centred on the bar (the name of the item, the inspect hint) follow the middle of the wider bar.
            foreach (string path in new[] { "SpellNamePanel", "InspectHint" })
            {
                var plate = bar.Find(path) as RectTransform;
                if (plate != null && plate.anchorMin.x == 0.5f && plate.anchorMax.x == 0.5f)
                    plate.anchoredPosition = new Vector2(plate.anchoredPosition.x + Extension / 2f, plate.anchoredPosition.y);
            }
        }

        // The screen position of the left (corner 0) or the right (corner 2) edge.
        static float Edge(RectTransform rect, int corner)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            return corners[corner].x;
        }

        internal static void Tick()
        {
            s_Buttons.RemoveAll(b => b == null);
            foreach (var button in s_Buttons)
            {
                try { button.Update_(); }
                catch (Exception e) { Main.Error(e, "tick"); }
            }
        }

        void Update_()
        {
            var unit = UnitSelectionManager.Instance != null ? UnitSelectionManager.Instance.CurrentSelectUnit.Value : null;
            Sprite icon = null;
            m_Pet = false;
            m_HasItems = false;
            if (unit != null)
            {
                var body = unit.Body;
                if (body.Belt.HasItem) icon = body.Belt.Item.Icon;
                m_HasItems = body.QuickSlots.Any(q => q.HasItem);
                m_Pet = unit.Descriptor.IsPet;
            }
            bool placeholder = icon == null;
            if (placeholder) icon = Placeholder();
            if (icon != m_ShownIcon)
            {
                m_ShownIcon = icon;
                m_Icon.sprite = icon;
                m_Icon.color = placeholder ? new Color(1f, 1f, 1f, 0.55f) : Color.white;
            }
            if (m_Disable != null) m_Disable.SetActive(m_Pet || !m_HasItems);

            // A pet has no belt: the selection moves off the button, as it does off the weapon set button.
            if (m_Pet && Nav != null && ReferenceEquals(Nav.CurrentEntity, this)) Nav.OnLeft(default(InputActionEventData));

            if (m_Row != null)
            {
                bool barClosed = BarVm == null || !BarVm.Active.Value;
                if (m_CloseRequested) CloseRow(m_CloseBar);
                else if (barClosed || m_Row.Unit != unit) CloseRow(false);
            }
            m_CloseRequested = false;
        }

        static Sprite Placeholder()
        {
            if (s_Placeholder == null)
                s_Placeholder = Resources.FindObjectsOfTypeAll<Sprite>().FirstOrDefault(s => s.name == PlaceholderSprite);
            return s_Placeholder;
        }

        void SetArrow(bool open)
        {
            if (m_Arrow != null) m_Arrow.localEulerAngles = new Vector3(0f, 0f, open ? 0f : 180f);
        }

        internal void Open()
        {
            if (m_Row != null || BarVm == null) return;
            if (m_Pet || !m_HasItems)
            {
                Game.Instance.UI.Common.UISound.Play(UISoundType.ActionBarCanNotSlotClick);
                return;
            }
            try
            {
                m_Row = new BeltRow(this);
                SetArrow(true);
            }
            catch (Exception e)
            {
                Main.Error(e, "open");
                m_Row = null;
            }
        }

        // A click on a slot arrives while the slot is still handling it: the row closes on the next frame.
        internal void RequestClose(bool closeBar)
        {
            m_CloseRequested = true;
            m_CloseBar = closeBar;
        }

        internal void CloseRow(bool closeBar)
        {
            var row = m_Row;
            m_Row = null;
            SetArrow(false);
            if (row != null) row.Dispose();
            if (closeBar && BarVm != null) BarVm.Active.Value = false;
        }

        public void SetSelected(bool value)
        {
            if (m_Select != null) m_Select.SetSelected(value);
            // As the weapon set button does: no slot is selected while the belt button is.
            if (value && BarVm != null) BarVm.CurrentSlot.Value = null;
        }

        public bool IsValid()
        {
            return !m_Pet;
        }

        void OnDestroy()
        {
            if (m_Row != null)
            {
                m_Row.Dispose();
                m_Row = null;
            }
        }
    }
}
