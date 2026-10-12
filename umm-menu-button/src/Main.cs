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
        internal static string Path;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Log = modEntry.Logger;
            Path = modEntry.Path;
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

        // The list of items made smaller as a whole by the share of the added item, so that it takes the height it took
        // without it; the top left corner stays where it was. Done once, when the item is added.
        internal static void Squeeze(Transform list)
        {
            int shown = 0;
            foreach (Transform child in list)
                if (child.gameObject.activeSelf && child.name != ItemName) shown++;
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

        // Once per menu, before the item is added: the page around the menu's items, for laying the item out.
        static readonly HashSet<string> s_Dumped = new HashSet<string>();

        internal static void DumpPage(Component item)
        {
            var page = item.transform;
            for (int up = 0; up < 3 && page.parent != null; up++) page = page.parent;
            if (!s_Dumped.Add(page.name)) return;
            Log.Log("menu page:\n" + Dump(page, ""));
        }

        // The scene around a menu drawn on a 3D page: every object with a renderer, its material and texture.
        internal static void DumpScene(Component item)
        {
            if (!s_Dumped.Add("scene")) return;
            var s = new System.Text.StringBuilder("menu scene:\n");
            var roots = new List<GameObject>();
            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                var scene = UnityEngine.SceneManagement.SceneManager.GetSceneAt(i);
                if (scene.isLoaded) roots.AddRange(scene.GetRootGameObjects());
            }
            s.Append("item canvas at ").Append(item.transform.position).Append(", scene ").Append(item.gameObject.scene.name).Append('\n');
            foreach (var root in roots)
                foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
                {
                    var t = renderer.transform;
                    string path = t.name;
                    for (var p = t.parent; p != null; p = p.parent) path = p.name + "/" + path;
                    s.Append(path).Append(renderer.gameObject.activeInHierarchy ? "" : " (off)").Append(" ").Append(renderer.GetType().Name)
                        .Append(" pos=").Append(t.position).Append(" scale=").Append(t.lossyScale).Append(" bounds=").Append(renderer.bounds.size);
                    foreach (var m in renderer.sharedMaterials)
                    {
                        if (m == null) continue;
                        s.Append(" [").Append(m.name).Append(" shader=").Append(m.shader != null ? m.shader.name : "-");
                        if (m.mainTexture != null)
                            s.Append(" tex=").Append(m.mainTexture.name).Append(" ").Append(m.mainTexture.width).Append("x").Append(m.mainTexture.height)
                                .Append(" tiling=").Append(m.mainTextureScale).Append(" offset=").Append(m.mainTextureOffset);
                        s.Append("]");
                    }
                    s.Append('\n');
                }
            Log.Log(s.ToString());
        }

        static string Dump(Transform t, string indent)
        {
            var s = new System.Text.StringBuilder();
            var r = t as RectTransform;
            s.Append(indent).Append(t.name).Append(t.gameObject.activeSelf ? "" : " (off)");
            if (r != null)
                s.Append(" pos=").Append(r.anchoredPosition).Append(" size=").Append(r.rect.size).Append(" min=").Append(r.anchorMin)
                    .Append(" max=").Append(r.anchorMax).Append(" pivot=").Append(r.pivot).Append(" scale=").Append(r.localScale).Append(" rot=").Append(r.localEulerAngles);
            var image = t.GetComponent<UnityEngine.UI.Image>();
            if (image != null)
                s.Append(" image=").Append(image.sprite != null ? image.sprite.name : "-").Append(" type=").Append(image.type).Append(" color=").Append(ColorUtility.ToHtmlStringRGBA(image.color));
            foreach (var c in t.GetComponents<Component>())
            {
                var group = c as UnityEngine.UI.HorizontalOrVerticalLayoutGroup;
                if (group != null)
                    s.Append(" ").Append(c.GetType().Name).Append("(spacing ").Append(group.spacing).Append(", pad ").Append(group.padding).Append(", control h ").Append(group.childControlHeight).Append(")");
                else if (c is UnityEngine.UI.LayoutGroup || c is UnityEngine.UI.LayoutElement || c is UnityEngine.UI.ContentSizeFitter || c is UnityEngine.UI.Mask || c is UnityEngine.UI.RectMask2D)
                    s.Append(" ").Append(c.GetType().Name);
            }
            var text = t.GetComponent<TMPro.TextMeshProUGUI>();
            if (text != null)
                s.Append(" text=\"").Append(text.text.Length > 60 ? text.text.Substring(0, 60) : text.text).Append("\" size=").Append(text.fontSize)
                    .Append(" auto=").Append(text.enableAutoSizing).Append(" wrap=").Append(text.enableWordWrapping).Append(" align=").Append(text.alignment);
            s.Append('\n');
            foreach (Transform child in t)
                s.Append(Dump(child, indent + "  "));
            return s.ToString();
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
