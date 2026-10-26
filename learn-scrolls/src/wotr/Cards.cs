using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Kingmaker.UI.MVVM._ConsoleView.Carts;
using Kingmaker.UI.MVVM._ConsoleView.IngameMenu;
using Owlcat.Runtime.UI.ConsoleTools.NavigationTool;
using UnityEngine;
using UnityEngine.UI;

namespace LearnScrolls {

    // The window as a card of the gamepad menu, after Rest and the cards of the other mods. The card is a copy of the
    // game's Rest card with its own picture and title (as the buff menu's card of Buff It 2 The Limit); it takes part in
    // the carousel and in the navigation like the game's cards. The game runs the action of the focused card when the
    // menu closes; the window opens after that.
    static class Cards {
        private const string CardName = "LearnScrolls_MenuCard";
        // A muted green of its own, in the tone of the game's cards (Rest is 9DA476 with its frame 6F764B).
        private static readonly Color CardColor = new Color32(0x8F, 0xB0, 0x7A, 0xFF);
        private static readonly Color FrameColor = new Color32(0x5E, 0x7A, 0x4C, 0xFF);
        private static Sprite big;
        private static Sprite small;
        private static IngameMenuCardsConsoleView pendingMenu;
        private static float pendingAt = -1;

        internal static void Register() { }

        // The menu is bound: add the card at the end and rebuild the carousel and the navigation with it.
        internal static void Bound(IngameMenuCardsConsoleView view) {
            var t = Traverse.Create(view);
            var rest = t.Field("m_RestView").GetValue<InGameMenuCardEntityConsoleView>();
            var collection = t.Field("m_CartCollection").GetValue<CartCollectionManagerView>();
            var navigation = t.Field("m_NavigationBehaviour").GetValue<GridConsoleNavigationBehaviour>();
            if (rest == null || collection == null || navigation == null)
                return;
            var holder = rest.transform.parent.Find(CardName);
            var card = holder != null ? holder.GetComponent<InGameMenuCardEntityConsoleView>() : null;
            if (card == null)
                card = Create(rest);
            var carts = Traverse.Create(collection).Field("m_CartList").GetValue<IList>();
            var list = carts.OfType<InGameMenuCardEntityConsoleView>().Where(c => c != card).ToList();
            card.Initialize(Strings.Title);
            card.Bind(() => Select(view), true);
            // The last card of the fan, laid over or under the card before it as the game's cards lie on each other.
            var previous = list.LastOrDefault();
            list.Add(card);
            if (previous != null && list.Count > 2)
                Stack(card.transform, previous.transform, list[1].transform.GetSiblingIndex() > list[0].transform.GetSiblingIndex());
            collection.Initialize(list);
            navigation.SetEntitiesHorizontal(list);
            collection.FocusOnFirstEntity();
            navigation.FocusOnFirstValidEntity();
        }

        // Cards are drawn in the order of the hierarchy, a later one over an earlier one: right after the card before it
        // when the next card lies over the one before, right before it when under.
        private static void Stack(Transform card, Transform previous, bool overPrevious) {
            card.SetAsLastSibling();
            int index = previous.GetSiblingIndex();
            card.SetSiblingIndex(overPrevious ? index + 1 : index);
        }

        private static InGameMenuCardEntityConsoleView Create(InGameMenuCardEntityConsoleView rest) {
            var go = UnityEngine.Object.Instantiate(rest.gameObject, rest.transform.parent, false);
            go.name = CardName;
            // The card as the game's: Back is its colour, Back/Frame the same colour darker, Back/Symbol_Main the drawing
            // over the whole card, Back/Symbol_Corner the small dark sign in its corner.
            SetColor(go.transform, "Back", CardColor);
            SetColor(go.transform, "Back/Frame", FrameColor);
            Paint(go.transform);
            Main.Log("menu card created");
            return go.GetComponent<InGameMenuCardEntityConsoleView>();
        }

        // The drawing over the card and the sign in its corner: the Journal card's drawing with its background and its
        // sign in the colour of this card's frame, the quill taken away.
        private static void Paint(Transform card) {
            if (big == null)
                big = Art.Picture("wotr_card_big.png");
            if (small == null)
                small = Art.Picture("wotr_card_small.png");
            var mainPart = card.Find("Back/Symbol_Main");
            var main = mainPart != null ? mainPart.GetComponent<Image>() : null;
            if (main != null && big != null) {
                main.sprite = big;
                main.preserveAspect = false;
            }
            var cornerPart = card.Find("Back/Symbol_Corner");
            var corner = cornerPart != null ? cornerPart.GetComponent<Image>() : null;
            if (corner != null && small != null) {
                corner.sprite = small;
                corner.preserveAspect = true;
            }
        }

        private static void SetColor(Transform card, string path, Color color) {
            var part = card.Find(path);
            var image = part != null ? part.GetComponent<Image>() : null;
            if (image != null)
                image.color = color;
        }

        private static void Select(IngameMenuCardsConsoleView view) {
            pendingMenu = view;
            pendingAt = Time.unscaledTime;
        }

        // Called every frame: open the window once the game's menu is gone.
        internal static void Tick() {
            if (pendingAt < 0)
                return;
            bool menuOpen = pendingMenu != null && pendingMenu.gameObject.activeInHierarchy;
            if (menuOpen && Time.unscaledTime - pendingAt < 1.5f)
                return;
            pendingAt = -1;
            pendingMenu = null;
            if (!Window.IsOpen)
                Window.Toggle();
        }
    }
    [HarmonyPriority(Priority.Low)]
    [HarmonyPatch(typeof(IngameMenuCardsConsoleView), "BindEntities")]
    static class CardsBound {
        static void Postfix(IngameMenuCardsConsoleView __instance) {
            try {
                Cards.Bound(__instance);
            } catch (Exception ex) {
                Main.Error(ex, "Cards.Bound");
            }
        }
    }
}
