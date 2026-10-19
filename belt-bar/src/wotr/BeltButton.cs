using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.UI.MVVM._ConsoleView.ActionBar;
using Kingmaker.UI.MVVM._PCView.ActionBar;
using Kingmaker.UI.MVVM._VM.ActionBar;
using Owlcat.Runtime.UI.ConsoleTools;
using Owlcat.Runtime.UI.ConsoleTools.ClickHandlers;
using Owlcat.Runtime.UI.ConsoleTools.NavigationTool;
using Owlcat.Runtime.UI.Controls.Button;
using UniRx;
using UnityEngine;
using UnityEngine.UI;

namespace BeltBar
{
    // The belt button of the gamepad action bar of Wrath of the Righteous, next to the weapon set button. It is a copy
    // of the weapon set button with its window, so it looks the same; the icon is the belt the unit wears, a
    // placeholder when it wears none. The bar is made wider by the button; the slots and the weapon set button stay
    // where they were.
    sealed class BeltButton : MonoBehaviour, IConsoleNavigationEntity, IConsoleEntity, IConfirmClickHandler, INavigationUpDirectionHandler
    {
        const string CopyName = "BeltBarSetView";
        // The button is 50 wide; the slots of the bar are 11 apart.
        const float Extension = 61f;
        // The whole bar moves left, so the button keeps a gap to the combat log panel at the right edge.
        const float PanelShift = 55f;

        static readonly List<BeltButton> s_Buttons = new List<BeltButton>();
        static Sprite s_Placeholder;
        static bool s_PlaceholderSearched;

        internal ActionBarConsoleView View;
        internal ActionBarVM BarVm;
        internal GridConsoleNavigationBehaviour Nav;
        internal RectTransform SetsContainer;
        internal bool IsFocused;

        OwlcatMultiButton m_Focus;
        Image m_Icon;
        GameObject m_Hand;
        BeltRow m_Row;
        Sprite m_ShownIcon;
        bool m_Pet;
        bool m_HasItems;
        bool m_CloseRequested, m_CloseBar;

        internal static void Attach(ActionBarConsoleView view)
        {
            var existing = view.GetComponentInChildren<BeltButton>(true);
            var button = existing != null ? existing : Build(view);
            if (button == null) return;
            button.View = view;
            button.BarVm = Traverse.Create(view).Property("ViewModel").GetValue<ActionBarVM>();
            button.Nav = Traverse.Create(view).Field("m_NavigationBehaviour").GetValue<GridConsoleNavigationBehaviour>();
            if (button.Nav != null) button.Nav.AddEntityHorizontal(button);
            if (!s_Buttons.Contains(button)) s_Buttons.Add(button);
        }

        static T Field<T>(object owner, string name) where T : class
        {
            var value = Traverse.Create(owner).Field(name).GetValue<T>();
            if (value == null) Main.Log("no field " + name + " in " + owner.GetType().Name);
            return value;
        }

        static BeltButton Build(ActionBarConsoleView view)
        {
            var weapon = Field<ActionBarWeaponBaseView>(view, "m_WeaponView");
            if (weapon == null) return null;
            var home = weapon.transform.parent as RectTransform;
            var main = view.transform.Find("ActionBarMainContainer") as RectTransform;
            if (home == null || main == null) { Main.Log("action bar layout not as expected, no belt button"); return null; }

            var copy = (RectTransform)UnityEngine.Object.Instantiate(home.gameObject, home.parent, false).transform;
            copy.name = CopyName;
            var logic = copy.GetComponentInChildren<ActionBarWeaponConsoleView>(true);
            var focus = Field<OwlcatMultiButton>(logic, "m_FocusMultiButton");
            var sets = Field<RectTransform>(logic, "m_WeaponSetContainer");
            var hand = Field<GameObject>(logic, "m_EmptyIcon");
            var back = Field<Image>(logic, "m_BackIcon");
            var buttonObject = logic.gameObject;
            UnityEngine.Object.DestroyImmediate(logic);
            if (focus == null || sets == null || hand == null) return null;

            // Wider bar: the left edge stays, the weapon set button keeps its place relative to the slots.
            var position = home.anchoredPosition;
            home.anchoredPosition = new Vector2(position.x - Extension, position.y);
            ExtendBar(main);
            main.anchoredPosition = new Vector2(main.anchoredPosition.x - PanelShift, main.anchoredPosition.y);

            var button = buttonObject.AddComponent<BeltButton>();
            button.m_Focus = focus;
            button.SetsContainer = sets;
            button.m_Hand = hand;
            button.m_Icon = hand.GetComponent<Image>();
            if (back != null) back.gameObject.SetActive(false);
            sets.gameObject.SetActive(false);
            return button;
        }

        // The backdrop of the bar and what is centred on it end at the old right edge. Whatever their anchors are, each
        // is measured in the screen and put to its old left edge and its old right edge plus the extension, from the
        // outside in: a parent that grew or moved takes its children along.
        static void ExtendBar(RectTransform main)
        {
            var chain = new List<RectTransform> { main };
            foreach (string path in new[] { "Background/ActionBack", "Background/TBMBack", "AbilitiesGroupsContainer", "SpellPanelContainer/CenterPlace" })
            {
                var rect = main.Find(path) as RectTransform;
                if (rect != null) chain.Add(rect);
                else Main.Log("no " + path + " to widen");
            }
            var left = chain.Select(r => Edge(r, 0)).ToList();
            var right = chain.Select(r => Edge(r, 2)).ToList();
            float wanted = Extension * main.lossyScale.x;
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

        internal UnitEntityData Unit
        {
            get { return BarVm != null ? BarVm.SelectedUnit.Value : null; }
        }

        void Update_()
        {
            var unit = Unit;
            Sprite icon = null;
            m_Pet = false;
            m_HasItems = false;
            if (unit != null)
            {
                var body = unit.Body;
                if (body.Belt.HasItem) icon = body.Belt.Item.Icon;
                m_HasItems = body.QuickSlots.Any(q => q.HasItem);
                m_Pet = unit.IsPet;
            }
            bool placeholder = icon == null;
            if (placeholder) icon = Placeholder();
            if (icon != null && icon != m_ShownIcon)
            {
                m_ShownIcon = icon;
                m_Icon.sprite = icon;
                m_Icon.color = placeholder ? new Color(1f, 1f, 1f, 0.67f) : Color.white;
            }
            m_Hand.SetActive(true);

            // A pet has no belt: the focus moves off the button, as it does off the weapon set button.
            if (m_Pet && IsFocused && Nav != null) Nav.FocusOnFirstValidEntity();

            if (m_Row != null)
            {
                bool barClosed = BarVm == null || !BarVm.IsActive.Value;
                if (m_CloseRequested) CloseRow(m_CloseBar);
                else if (barClosed || m_Row.Unit != unit) CloseRow(false);
                else m_Row.Tick();
            }
            m_CloseRequested = false;
        }

        // The slot of the belt in the inventory shows a belt sign; Wrath names it differently from Kingmaker.
        static Sprite Placeholder()
        {
            if (s_PlaceholderSearched) return s_Placeholder;
            s_PlaceholderSearched = true;
            var all = Resources.FindObjectsOfTypeAll<Sprite>();
            s_Placeholder = all.FirstOrDefault(s => s.name == "UI_SlotsIcon_Belt")
                ?? all.FirstOrDefault(s => Regex.IsMatch(s.name, "slot.*belt|belt.*slot", RegexOptions.IgnoreCase));
            Main.Log("belt placeholder: " + (s_Placeholder != null ? s_Placeholder.name : "none found"));
            return s_Placeholder;
        }

        public void SetFocus(bool value)
        {
            IsFocused = value;
            m_Focus.SetFocus(value);
            m_Focus.SetActiveLayer(value ? "Selected" : "Normal");
        }

        public bool IsValid()
        {
            return !m_Pet;
        }

        public bool CanConfirmClick()
        {
            return true;
        }

        public string GetConfirmClickHint()
        {
            return string.Empty;
        }

        public void OnConfirmClick()
        {
            Open();
        }

        // Up from the button, with the stick as with the D-pad, opens the row, as the weapon set button opens its
        // window. Without items there is nothing to open: up goes on as usual.
        public bool HandleUp()
        {
            if (m_Pet || !m_HasItems) return false;
            Open();
            return true;
        }

        internal void Open()
        {
            if (m_Row != null || BarVm == null) return;
            if (m_Pet || !m_HasItems) return;
            try
            {
                m_Row = new BeltRow(this);
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
            if (row != null) row.Dispose();
            if (closeBar && BarVm != null) BarVm.IsActive.Value = false;
            else if (BarVm != null && BarVm.IsActive.Value && Nav != null) Nav.FocusOnCurrentEntity();
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
