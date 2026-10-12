using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityModManagerNet;

namespace UmmMenuButton
{
    // A "Unity Mod Manager" item in the main menu and in the Esc menu of the game, in both interfaces (mouse and
    // gamepad): the mod manager opens from the menus, without a hotkey. The item is a copy of a neighbouring one, so it
    // looks as the game's items do. The mod cannot be switched off: it is the way into the mod manager.
    public static class Main
    {
        internal const string Label = "Unity Mod Manager";
        // The name of the item's object; Bag of Tricks names its own Esc menu button UnityModManager_Button.
        internal const string ItemName = "UmmMenuButton_Item";

        internal static UnityModManager.ModEntry.ModLogger Log;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Log = modEntry.Logger;
            // Unchecked in the mod list, the mod checks itself again.
            modEntry.OnUpdate = (entry, delta) => { if (!entry.Enabled) entry.Enabled = true; };
            NoWindowOnStart();
            new Harmony(modEntry.Info.Id).PatchAll(typeof(Main).Assembly);
            return true;
        }

        // The mod manager opens from the menus, so its window does not open by itself when the game starts.
        static void NoWindowOnStart()
        {
            try
            {
                var settings = AccessTools.Property(typeof(UnityModManager), "Params").GetValue(null, null) as UnityModManager.Param;
                if (settings == null || settings.ShowOnStart == 0) return;
                settings.ShowOnStart = 0;
                UnityModManager.SaveSettingsAndParams();
                Log.Log("the mod manager window no longer opens at start");
            }
            catch (Exception e)
            {
                Log.LogException(e);
            }
        }

        // The label on one line, as the game's are: a line too long for the item is made smaller, down to three quarters,
        // and beyond that runs past the item's edge instead of breaking into two lines.
        // The size is the one of the neighbouring item, so that the item opened again is not made smaller again.
        internal static void OneLine(Component item, Component neighbour)
        {
            var text = item.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            var like = neighbour.GetComponentInChildren<TMPro.TextMeshProUGUI>();
            if (text == null || like == null) return;
            text.enableAutoSizing = false;
            text.fontSize = like.enableAutoSizing ? like.fontSizeMax : like.fontSize;
            text.enableWordWrapping = false;
            text.overflowMode = TMPro.TextOverflowModes.Overflow;
            float room = text.rectTransform.rect.width;
            float width = text.GetPreferredValues(text.text, float.PositiveInfinity, float.PositiveInfinity).x;
            if (room > 0 && width > room)
                text.fontSize *= Mathf.Max(0.75f, room / width);
        }

        // The items of a list: its shown children that are items as the given one is, not the separators between them.
        static List<RectTransform> Items(Transform list, Component like)
        {
            var items = new List<RectTransform>();
            foreach (Transform child in list)
            {
                var rect = child as RectTransform;
                if (rect != null && child.gameObject.activeSelf && child.GetComponent(like.GetType()) != null) items.Add(rect);
            }
            return items;
        }

        // The list of items made smaller as a whole by the share of the added item, so that it takes the height it took
        // without it; the top left corner stays where it was. Done once, when the item is added.
        internal static void Squeeze(Transform list, Component like)
        {
            int shown = 0;
            foreach (var item in Items(list, like))
                if (item.name != ItemName) shown++;
            if (shown == 0) return;
            var rect = list as RectTransform;
            var before = new Vector3[4];
            var after = new Vector3[4];
            if (rect != null) rect.GetWorldCorners(before);
            list.localScale *= (float)shown / (shown + 1);
            if (rect == null) return;
            rect.GetWorldCorners(after);
            // Corner 1 is the top left one.
            list.position += before[1] - after[1];
            Log.Log("list " + list.name + " squeezed to " + shown + "/" + (shown + 1));
        }

        // The list made smaller as Squeeze makes it, the middle of its items kept where it is, or put where the middle
        // of the game's items was. The list is to be laid out.
        internal static void SqueezeToMiddle(Transform list, Component like, bool toGame)
        {
            var items = Items(list, like);
            RectTransform lastGame = null;
            int shown = 0;
            foreach (var item in items)
            {
                if (item.name == ItemName) continue;
                lastGame = item;
                shown++;
            }
            if (lastGame == null || items.Count < 2) return;
            var first = items[0];
            var last = items[items.Count - 1];
            var corners = new Vector3[4];
            first.GetWorldCorners(corners);
            var top = corners[1];
            var down = (Middle(last) - Middle(first)).normalized;
            // The items stand one row apart; the game's ones took a row less.
            var row = (Middle(last) - Middle(first)) / (items.Count - 1);
            var all = Middle(first) + row * (items.Count - 1) / 2;
            // The page of Kingmaker's menu has more room above the game's items than below them: the list goes higher
            // than their middle by a little more than half a row.
            var target = toGame ? Middle(first) + row * ((shown - 1) / 2f - 0.6f) : all;
            float scale = (float)shown / (shown + 1);
            Free(list);
            list.localScale *= scale;
            list.position = top + (list.position - top) * scale;
            list.position += down * Vector3.Dot(target - (top + (all - top) * scale), down);
            Log.Log("list " + list.name + " squeezed to " + shown + "/" + (shown + 1) + " around " + (toGame ? "the game's middle" : "its middle"));
        }

        // The list out of the layout of the object it lies in, staying where it is, so that it can be moved.
        static void Free(Transform list)
        {
            if (list.parent == null || list.parent.GetComponent<UnityEngine.UI.LayoutGroup>() == null) return;
            var element = list.GetComponent<UnityEngine.UI.LayoutElement>() ?? list.gameObject.AddComponent<UnityEngine.UI.LayoutElement>();
            element.ignoreLayout = true;
        }

        static Vector3 Middle(RectTransform rect)
        {
            return rect.TransformPoint(rect.rect.center);
        }

        // Once the game has laid the list out: the label made to fit its item, and the list made smaller, keeping its top
        // (middle null), its middle (false) or putting it at the middle of the game's items (true). Items are not laid
        // out yet when the gamepad menus are bound.
        internal static void Fit(Transform list, Component item, Component neighbour, bool? middle)
        {
            Run(Fitting(list, item, neighbour, middle));
        }

        static System.Collections.IEnumerator Fitting(Transform list, Component item, Component neighbour, bool? middle)
        {
            for (int frame = 0; frame < 120; frame++)
            {
                yield return null;
                if (item == null) yield break;
                var rect = item.transform as RectTransform;
                if (rect != null && rect.rect.width > 1 && rect.rect.height > 1) break;
            }
            try
            {
                OneLine(item, neighbour);
                if (middle == null) Squeeze(list, neighbour);
                else SqueezeToMiddle(list, neighbour, middle.Value);
            }
            catch (Exception e)
            {
                Error(e, "Fit");
            }
        }

        // The item between the game's items, the separator before it repeated after it when the game's items have
        // separators between them.
        internal static void Separate(Component item)
        {
            var t = item.transform;
            var parent = t.parent;
            var separator = parent.Find(ItemName + "_Separator");
            if (separator != null) separator.SetAsLastSibling();
            int at = t.GetSiblingIndex();
            if (at == 0 || at + 1 >= parent.childCount) return;
            var before = parent.GetChild(at - 1);
            if (before.GetComponent(item.GetType()) != null) return;
            if (separator == null)
            {
                separator = UnityEngine.Object.Instantiate(before.gameObject, parent, false).transform;
                separator.name = ItemName + "_Separator";
            }
            separator.gameObject.SetActive(before.gameObject.activeSelf);
            separator.SetSiblingIndex(t.GetSiblingIndex() + 1);
        }

        // Coroutines of the mod run on an object of its own, not on the menus, which may be inactive or go away.
        class Runner : MonoBehaviour { }

        static Runner s_Runner;

        internal static void Run(System.Collections.IEnumerator routine)
        {
            if (s_Runner == null)
            {
                var host = new GameObject("UmmMenuButton");
                UnityEngine.Object.DontDestroyOnLoad(host);
                s_Runner = host.AddComponent<Runner>();
            }
            s_Runner.StartCoroutine(routine);
        }

        // The title of the main menus, with the large first letter the game gives its own.
        internal static string BookTitle
        {
            get
            {
#if KINGMAKER
                return Kingmaker.UI.Common.UIUtility.GetSaberBookFormat(Label, default(Color), 140, null);
#else
                return Kingmaker.UI.Common.UIUtility.GetSaberBookFormat(Label, default(Color), 140, null, 0f);
#endif
            }
        }

        internal static void Open()
        {
            try
            {
                UnityModManager.UI.Instance.ToggleWindow(true);
            }
            catch (Exception e)
            {
                Log.LogException(e);
            }
        }

        // The item next to the template, made once per menu and found again on the next opening, laid right before
        // the given item (the menu's last one, Exit), or last when there is none.
        internal static T Item<T>(T template, Component before) where T : Component
        {
            var parent = template.transform.parent;
            var found = parent.Find(ItemName);
            var item = found != null ? found.gameObject : UnityEngine.Object.Instantiate(template.gameObject, parent, false);
            item.name = ItemName;
            item.SetActive(true);
            item.transform.SetAsLastSibling();
            if (before != null && before.transform.parent == parent && before.gameObject.activeSelf)
                item.transform.SetSiblingIndex(before.transform.GetSiblingIndex());
            if (found == null) Log.Log("item added to " + parent.name);
            return item.GetComponent<T>();
        }

        // The model of the item: one per menu, the one of the previous opening let go.
        static readonly Dictionary<int, IDisposable> s_Models = new Dictionary<int, IDisposable>();

        internal static void Keep(Component menu, IDisposable model)
        {
            IDisposable old;
            if (s_Models.TryGetValue(menu.GetInstanceID(), out old) && old != null && !ReferenceEquals(old, model))
                old.Dispose();
            s_Models[menu.GetInstanceID()] = model;
        }

        internal static void Error(Exception e, string where)
        {
            Log.Error(where + ": " + e);
        }
    }
}
