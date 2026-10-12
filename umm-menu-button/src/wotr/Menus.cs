using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.UI.MVVM._ConsoleView.ContextMenu;
using Kingmaker.UI.MVVM._ConsoleView.EscMenu;
using Kingmaker.UI.MVVM._ConsoleView.MainMenu;
using Kingmaker.UI.MVVM._PCView.ContextMenu;
using Kingmaker.UI.MVVM._PCView.EscMenu;
using Kingmaker.UI.MVVM._PCView.MainMenu;
using Kingmaker.UI.MVVM._VM.ContextMenu;
using Owlcat.Runtime.UI.ConsoleTools.NavigationTool;
using Owlcat.Runtime.UI.Controls.Button;
using Owlcat.Runtime.UI.Controls.Other;
using TMPro;
using UniRx;

namespace UmmMenuButton
{
    // Esc menu of the mouse interface: a button as Settings, right before Quit.
    [HarmonyPatch(typeof(EscMenuPCView), "BindViewImplementation")]
    static class EscMenuMouse
    {
        static void Postfix(EscMenuPCView __instance)
        {
            try
            {
                var t = Traverse.Create(__instance);
                var options = t.Field("m_OptionsButton").GetValue<OwlcatButton>();
                var quit = t.Field("m_QuitButton").GetValue<OwlcatButton>();
                var item = Main.Item(options, quit);
                item.Interactable = true;
                item.GetComponentInChildren<TextMeshProUGUI>().text = Main.Label;
                Main.OneLine(item, options);
                Main.Keep(__instance, item.OnLeftClickAsObservable().Subscribe(_ => Main.Open()));
            }
            catch (Exception e)
            {
                Main.Error(e, "EscMenuMouse");
            }
        }
    }

    // Main menu of the mouse interface: an entry as Settings, right before Exit.
    [HarmonyPatch(typeof(MainMenuSideBarPCView), "BindViewImplementation")]
    static class MainMenuMouse
    {
        static void Postfix(MainMenuSideBarPCView __instance)
        {
            try
            {
                var t = Traverse.Create(__instance);
                var options = t.Field("m_OptionsView").GetValue<ContextMenuEntityPCView>();
                var exit = t.Field("m_ExitView").GetValue<ContextMenuEntityPCView>();
                Main.DumpPage(options);
                if (options.transform.parent.Find(Main.ItemName) == null) Main.Squeeze(options.transform.parent);
                var item = Main.Item(options, exit);
                var model = new ContextMenuEntityVM(new ContextMenuCollectionEntity(Main.BookTitle, Main.Open));
                Main.Keep(__instance, model);
                item.Bind(model);
                Main.OneLine(item, options);
            }
            catch (Exception e)
            {
                Main.Error(e, "MainMenuMouse");
            }
        }
    }

    // Esc menu of the gamepad interface: an entry as Settings, right before Quit, in the menu's navigation.
    [HarmonyPatch(typeof(EscMenuConsoleView), "BindViewImplementation")]
    static class EscMenuPad
    {
        static void Postfix(EscMenuConsoleView __instance)
        {
            try
            {
                var t = Traverse.Create(__instance);
                var options = t.Field("m_Options").GetValue<ContextMenuEntityConsoleView>();
                var quit = t.Field("m_Quit").GetValue<ContextMenuEntityConsoleView>();
                var entities = t.Field("m_Entities").GetValue<List<ContextMenuEntityConsoleView>>();
                var navigation = t.Field("m_ConsoleNavigation").GetValue<GridConsoleNavigationBehaviour>();
                var item = Main.Item(options, quit);
                var model = new ContextMenuEntityVM(new ContextMenuCollectionEntity(Main.Label, Main.Open));
                Main.Keep(__instance, model);
                item.Bind(model);
                Main.OneLine(item, options);
                entities.Remove(item);
                int at = entities.IndexOf(quit);
                entities.Insert(at < 0 ? entities.Count : at, item);
                navigation.SetEntitiesVertical(entities);
                navigation.FocusOnFirstValidEntity();
            }
            catch (Exception e)
            {
                Main.Error(e, "EscMenuPad");
            }
        }
    }

    // Main menu of the gamepad interface: an entry as Settings, right before Exit, in the menu's navigation.
    [HarmonyPatch(typeof(MainMenuSideBarConsoleView), "BindViewImplementation")]
    static class MainMenuPad
    {
        static void Postfix(MainMenuSideBarConsoleView __instance)
        {
            try
            {
                var t = Traverse.Create(__instance);
                var exit = t.Field("m_ExitView").GetValue<ContextMenuEntityConsoleView>();
                var navigation = t.Field("m_Navigation").GetValue<GridConsoleNavigationBehaviour>();
                var options = t.Field("m_OptionsView").GetValue<ContextMenuEntityConsoleView>();
                Main.DumpPage(options);
                if (options.transform.parent.Find(Main.ItemName) == null) Main.Squeeze(options.transform.parent);
                var item = Main.Item(options, exit);
                var model = new ContextMenuEntityVM(new ContextMenuCollectionEntity(Main.BookTitle, Main.Open));
                Main.Keep(__instance, model);
                item.Bind(model);
                Main.OneLine(item, options);
                // The entries in the order the game gives them, this one before Exit.
                var entities = new List<IConsoleNavigationEntity>();
                foreach (var name in new[] { "m_ContinueView", "m_NewGameView", "m_LoadView", "m_DLCManagerView", "m_OptionsView", "m_CreditView", "m_LicenseView" })
                    entities.Add(t.Field(name).GetValue<ContextMenuEntityConsoleView>());
                entities.Add(item);
                if (exit != null && exit.gameObject.activeSelf)
                    entities.Add(exit);
                navigation.SetEntitiesVertical(entities);
                navigation.FocusOnFirstValidEntity();
            }
            catch (Exception e)
            {
                Main.Error(e, "MainMenuPad");
            }
        }
    }
}
