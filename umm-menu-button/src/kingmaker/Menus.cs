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
                var model = new ContextMenuEntityVM(new ContextMenuCollectionEntity(Main.BookTitle, Main.Open));
                Main.Keep(__instance, model);
                item.Bind(model);
                Main.OneLine(item, options);
                Main.Separate(item);
                // The game's InsertEntityBefore puts the entry one place too high, so the entries are given again.
                var entities = new List<IConsoleNavigationEntity>(navigation.Entities);
                entities.Remove(item);
                int at = entities.IndexOf(exit);
                entities.Insert(at < 0 ? entities.Count : at, item);
                navigation.SetEntities(entities);
                navigation.SelectFirstItem();
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
        static ContextMenuEntityVM s_Model;

        static void Postfix(MainMenuSideBarView __instance, MainMenuSideBarVM viewModel)
        {
            try
            {
                var t = Traverse.Create(__instance);
                var options = t.Field("m_OptionsView").GetValue<ContextMenuEntityView>();
                var exit = t.Field("m_ExitView").GetValue<ContextMenuEntityView>();
                var navigation = t.Field("m_NavigationBehavior").GetValue<ConsoleMultiNavigationCollection>();
                bool made = options.transform.parent.Find(Main.ItemName) == null;
                var item = Main.Item(options, exit);
                var model = new ContextMenuEntityVM(new ContextMenuCollectionEntity(Main.BookTitle, Main.Open));
                Main.Keep(__instance, model);
                item.Bind(model);
                // The menu confirms the entry of its own list that is selected, so the entry is one of them.
                var models = Traverse.Create(viewModel).Field("m_Entities").GetValue<List<ContextMenuEntityVM>>();
                if (models != null)
                {
                    models.Remove(s_Model);
                    models.Add(model);
                }
                s_Model = model;
                Main.OneLine(item, options);
                Main.Separate(item);
                Main.Run(Widen(item, options));
                if (made) Main.Fit(options.transform.parent, item, options, true);
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

        // The frame's picture made longer where it has no ornament: the runs of columns of pixels each like the next
        // one, the plain stretches of its edges, are repeated, the more the longer they are; the ornament stays as it is.
        static Sprite s_Long;

        static Sprite Lengthen(Sprite sprite, int width)
        {
            if (s_Long != null && s_Long.rect.width == width) return s_Long;
            var source = sprite.textureRect;
            int w = Mathf.RoundToInt(source.width), h = Mathf.RoundToInt(source.height);
            var target = new RenderTexture(sprite.texture.width, sprite.texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            var active = RenderTexture.active;
            Graphics.Blit(sprite.texture, target);
            RenderTexture.active = target;
            var read = new Texture2D(w, h, TextureFormat.RGBA32, false, false);
            read.ReadPixels(new Rect(source.x, source.y, w, h), 0, 0, false);
            RenderTexture.active = active;
            target.Release();
            UnityEngine.Object.Destroy(target);
            var pixels = read.GetPixels32();
            UnityEngine.Object.Destroy(read);

            // Plain: a column of pixels hardly different from the next one.
            var plain = new bool[w];
            for (int x = 0; x + 1 < w; x++)
            {
                int most = 0;
                for (int y = 0; y < h; y++)
                {
                    Color32 a = pixels[y * w + x], b = pixels[y * w + x + 1];
                    most = Math.Max(most, Math.Max(Math.Max(Math.Abs(a.r - b.r), Math.Abs(a.g - b.g)), Math.Max(Math.Abs(a.b - b.b), Math.Abs(a.a - b.a))));
                }
                plain[x] = most <= 12;
            }
            // The runs of plain columns long enough to be stretches of an edge, not gaps in the ornament.
            var runs = new List<int[]>();
            int total = 0;
            for (int x = 0; x < w; )
            {
                if (!plain[x]) { x++; continue; }
                int start = x;
                while (x < w && plain[x]) x++;
                if (x - start >= 6)
                {
                    runs.Add(new[] { start, x - start });
                    total += x - start;
                }
            }
            if (total == 0 || width <= w) return null;
            // How many times each column is drawn: the extra width shared among the runs by their length, each run's
            // share spread evenly over its columns.
            var times = new int[w];
            for (int x = 0; x < w; x++) times[x] = 1;
            int extra = width - w, given = 0;
            for (int r = 0; r < runs.Count; r++)
            {
                int share = r + 1 < runs.Count ? extra * runs[r][1] / total : extra - given;
                given += share;
                for (int k = 0; k < share; k++) times[runs[r][0] + k * runs[r][1] / Math.Max(1, share) % runs[r][1]]++;
            }
            var longer = new Color32[width * h];
            int to = 0;
            for (int x = 0; x < w; x++)
            {
                for (int n = 0; n < times[x]; n++, to++)
                    for (int y = 0; y < h; y++)
                        longer[y * width + to] = pixels[y * w + x];
            }
            var texture = new Texture2D(width, h, TextureFormat.RGBA32, false, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.SetPixels32(longer);
            texture.Apply(false, true);
            if (s_Long != null) UnityEngine.Object.Destroy(s_Long.texture);
            s_Long = Sprite.Create(texture, new Rect(0, 0, width, h), new Vector2(0.5f, 0.5f), sprite.pixelsPerUnit, 0, SpriteMeshType.FullRect);
            s_Long.name = sprite.name + "_Long";
            var stretches = new System.Text.StringBuilder();
            foreach (var run in runs) stretches.Append(' ').Append(run[0]).Append('+').Append(run[1]);
            Main.Log.Log("selection frame picture " + w + "x" + h + " made " + width + " long at the plain stretches" + stretches);
            return s_Long;
        }

        // Once the item is laid out: the frame shown on the selected item long enough for the label, with the frame's
        // height on each side for its corners, its picture made longer where it has no ornament. The frame's width and
        // picture are the ones of the neighbour's, so that the item shown again is not made wider again.
        internal static IEnumerator Widen(ContextMenuEntityView item, ContextMenuEntityView neighbour)
        {
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
                if (item == null) yield break;
                var rect = item.transform as RectTransform;
                if (rect != null && rect.rect.width > 1) break;
            }
            if (item == null) yield break;
            try
            {
                var text = item.GetComponentInChildren<TextMeshProUGUI>(true);
                var frames = Traverse.Create(item).Field("m_SelectEntities").GetValue<List<GameObject>>();
                var like = Traverse.Create(neighbour).Field("m_SelectEntities").GetValue<List<GameObject>>();
                if (text == null || frames == null || like == null) yield break;
                float width = text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity).x;
                for (int i = 0; i < frames.Count && i < like.Count; i++)
                {
                    var rect = frames[i] != null ? frames[i].transform as RectTransform : null;
                    var game = like[i] != null ? like[i].transform as RectTransform : null;
                    if (rect == null || game == null) continue;
                    float wide = Mathf.Max(width + 2 * game.rect.height, game.rect.width);
                    if (wide <= game.rect.width) continue;
                    var image = frames[i].GetComponent<Image>();
                    var original = like[i].GetComponent<Image>();
                    if (image == null || original == null || original.sprite == null) continue;
                    // The picture's pixels to the frame's units, as the game draws it.
                    float pixels = original.sprite.rect.width / game.rect.width;
                    var sprite = Lengthen(original.sprite, Mathf.RoundToInt(wide * pixels));
                    if (sprite == null) continue;
                    image.sprite = sprite;
                    image.type = Image.Type.Simple;
                    rect.sizeDelta += new Vector2(wide - rect.rect.width, 0);
                    Main.Log.Log("selection frame " + rect.name + " " + game.rect.size + " made " + wide + " wide, label " + width);
                }
            }
            catch (Exception e)
            {
                Main.Error(e, "Widen");
            }
        }
    }
}
