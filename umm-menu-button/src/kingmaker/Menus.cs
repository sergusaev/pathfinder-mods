using System;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.Assets.UI._ConsoleUI.EscMenu;
using Kingmaker.UI.Common;
using Kingmaker.UI.MainMenuUI;
using Kingmaker.UI.ServiceWindow;
using Kingmaker.UI._ConsoleUI.ContextMenu;
using Kingmaker.UI._ConsoleUI.MainMenuSideBar;
using Kingmaker.UI._ConsoleUI.Utils.MultiNavigationTool;
using Kingmaker.UI._ConsoleUI.Utils.NavigationTool;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UmmMenuButton
{
    // Esc menu of the mouse interface: a button as Save, right before Quit. Bag of Tricks may add its own Unity Mod
    // Manager button here; this one takes its place.
    [HarmonyPatch(typeof(EscMenuWindow), "OnShow")]
    static class EscMenuMouse
    {
        static void Postfix(EscMenuWindow __instance)
        {
            try
            {
                var save = __instance.ButtonSave;
                var parent = save.transform.parent;
                var other = parent.Find("UnityModManager_Button");
                if (other != null) UnityEngine.Object.Destroy(other.gameObject);
                Button quit = null;
                foreach (Transform child in parent)
                {
                    var button = child.GetComponent<Button>();
                    if (button != null && child.gameObject.activeSelf && child != other && child.name != Main.ItemName) quit = button;
                }
                bool made = parent.Find(Main.ItemName) == null;
                var item = Main.Item(save, quit);
                if (!made) return;
                item.onClick = new Button.ButtonClickedEvent();
                item.onClick.AddListener(Main.Open);
                item.interactable = true;
                item.GetComponentInChildren<TextMeshProUGUI>().text = Main.Label;
                Main.OneLine(item, save);
            }
            catch (Exception e)
            {
                Main.Error(e, "EscMenuMouse");
            }
        }
    }

    // Main menu of the mouse interface: a button as Settings, right before Exit, with the large first letter of the
    // game's buttons.
    [HarmonyPatch(typeof(MainMenuButtons), "SetupButtons")]
    static class MainMenuMouse
    {
        static void Postfix(MainMenuButtons __instance)
        {
            try
            {
                __instance.StartCoroutine(Add(__instance));
            }
            catch (Exception e)
            {
                Main.Error(e, "MainMenuMouse");
            }
        }

        // The game lays its buttons out in the frames after this: the item is added once they stand in their rows.
        static IEnumerator Add(MainMenuButtons menu)
        {
            var t = Traverse.Create(menu);
            var options = t.Field("m_ButtonOptions").GetValue<Button>();
            var exit = t.Field("m_ButtonExit").GetValue<Button>();
            MainMenuPage page = null;
            for (int frame = 0; frame < 120 && page == null; frame++)
            {
                yield return null;
                if (options == null || options.transform.parent.Find(Main.ItemName) != null) yield break;
                page = MainMenuPage.Measure(options, exit);
            }
            if (page == null) Main.Log.Log("main menu: the buttons were not laid out, the item goes without the frame");
            try
            {
                var buttons = t.Field("m_Buttons").GetValue<List<Button>>();
                var item = Main.Item(options, exit);
                item.onClick = new Button.ButtonClickedEvent();
                item.onClick.AddListener(Main.Open);
                item.GetComponentInChildren<TextMeshProUGUI>().text = Main.BookTitle;
                Main.OneLine(item, options);
                if (buttons != null && !buttons.Contains(item))
                {
                    int at = buttons.IndexOf(exit);
                    buttons.Insert(at < 0 ? buttons.Count : at, item);
                }
                if (page != null) page.Lay();
            }
            catch (Exception e)
            {
                Main.Error(e, "MainMenuMouse.Add");
            }
        }
    }

    // Esc menu of the gamepad interface: an entry as Settings, right before Exit, in the menu's navigation.
    [HarmonyPatch(typeof(EscMenuView), "Bind")]
    static class EscMenuPad
    {
        static void Postfix(EscMenuView __instance)
        {
            try
            {
                var t = Traverse.Create(__instance);
                var options = t.Field("m_BtnOptionsView").GetValue<ContextMenuEntityView>();
                var exit = t.Field("m_BtnExitView").GetValue<ContextMenuEntityView>();
                var navigation = t.Field("m_NavigationBehavior").GetValue<ConsoleNavigationBehavior>();
                var item = Main.Item(options, exit);
                var model = new ContextMenuEntityVM(new ContextMenuCollectionEntity(Main.Label, Main.Open));
                Main.Keep(__instance, model);
                item.Bind(model);
                Main.OneLine(item, options);
                if (exit != null && exit.gameObject.activeSelf && navigation.Entities.Contains(exit))
                    navigation.InsertEntityBefore(exit, item);
                else
                    navigation.AddEntity(item);
            }
            catch (Exception e)
            {
                Main.Error(e, "EscMenuPad");
            }
        }
    }

    // Main menu of the gamepad interface: an entry as Settings, right before Exit, in the menu's navigation.
    [HarmonyPatch(typeof(MainMenuSideBarView), "Bind")]
    static class MainMenuPad
    {
        static void Postfix(MainMenuSideBarView __instance)
        {
            try
            {
                var t = Traverse.Create(__instance);
                var options = t.Field("m_OptionsView").GetValue<ContextMenuEntityView>();
                var exit = t.Field("m_ExitView").GetValue<ContextMenuEntityView>();
                var navigation = t.Field("m_NavigationBehavior").GetValue<ConsoleMultiNavigationCollection>();
                var item = Main.Item(options, exit);
                var model = new ContextMenuEntityVM(new ContextMenuCollectionEntity(Main.BookTitle, Main.Open));
                Main.Keep(__instance, model);
                item.Bind(model);
                Main.OneLine(item, options);
                var entities = new List<IConsoleMultiNavigationEntity>(navigation.EntitiesList);
                entities.Remove(item);
                int at = entities.IndexOf(exit);
                entities.Insert(at < 0 ? entities.Count : at, item);
                navigation.SetEntitiesVertical(entities);
                navigation.SelectCurrentEntity(null);
            }
            catch (Exception e)
            {
                Main.Error(e, "MainMenuPad");
            }
        }
    }
}
