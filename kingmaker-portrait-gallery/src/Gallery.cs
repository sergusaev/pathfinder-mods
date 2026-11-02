using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using UnityEngine;

namespace PortraitGallery
{
    enum Category { Kingmaker, WotR, WotRCharacters, Custom }

    // One portrait of the list: a built-in blueprint, or a folder of the Portraits folder.
    class Entry
    {
        public Category Category;
        public BlueprintPortrait Blueprint;   // built-in
        public string CustomId;               // folder
        public int Order;
    }

    static class Gallery
    {
        internal const int Count = 4;
        const string Marker = "PortraitGallery.txt";

        static readonly bool[] s_Collapsed = new bool[Count];
        static HashSet<BlueprintPortrait> s_Banned;
        static readonly Dictionary<string, PortraitData> s_Data = new Dictionary<string, PortraitData>();
        static readonly Dictionary<string, KeyValuePair<Category, int>> s_Markers = new Dictionary<string, KeyValuePair<Category, int>>();

        // Folders may have changed (the WotR export): markers are read again.
        internal static void Forget() { s_Markers.Clear(); }

        internal static bool IsCollapsed(Category c) { return s_Collapsed[(int)c]; }
        internal static void Toggle(Category c) { s_Collapsed[(int)c] = !s_Collapsed[(int)c]; }

        internal static string Name(Category c)
        {
            bool ru = LocalizationManager.CurrentLocale == Locale.ruRU;
            switch (c)
            {
                case Category.Kingmaker: return "Kingmaker";
                case Category.WotR: return "Wrath of the Righteous";
                case Category.WotRCharacters: return ru ? "Персонажи WotR" : "WotR characters";
                default: return ru ? "Пользовательские" : "Custom";
            }
        }

        // One PortraitData per folder: its images, once loaded, stay with it.
        internal static PortraitData Data(string id)
        {
            PortraitData d;
            if (!s_Data.TryGetValue(id, out d)) s_Data[id] = d = new PortraitData(id);
            return d;
        }

        // Chargen presets and the templates of created units are not characters: PlayerFighterPregen, PregenPaladinLevel5,
        // LastChaptersPregenClericUnit, PlayerClericStart. Varnhold's pregens are its heroes and are characters.
        static bool IsTemplate(BlueprintUnit unit)
        {
            string n = unit.name ?? "";
            if (n.StartsWith("Varnhold", StringComparison.OrdinalIgnoreCase)) return false;
            return n.IndexOf("Pregen", StringComparison.OrdinalIgnoreCase) >= 0
                || (n.StartsWith("Player", StringComparison.Ordinal) && n.EndsWith("Start", StringComparison.Ordinal));
        }

        // Every portrait worn by a unit of the game, except the chargen presets and the templates of created units.
        static HashSet<BlueprintPortrait> Banned()
        {
            if (s_Banned != null) return s_Banned;
            var root = Game.Instance.BlueprintRoot;
            var exempt = new HashSet<BlueprintUnit>();
            if (root.CharGen.Pregens != null) exempt.UnionWith(root.CharGen.Pregens.Where(u => u != null));
            if (root.DefaultPlayerCharacter) exempt.Add(root.DefaultPlayerCharacter);
            if (root.CustomCompanion) exempt.Add(root.CustomCompanion);
            var field = AccessTools.Field(typeof(BlueprintUnit), "m_Portrait");
            var wearers = new Dictionary<BlueprintPortrait, List<string>>();
            foreach (var bp in ResourcesLibrary.LibraryObject.GetAllBlueprints())
            {
                var unit = bp as BlueprintUnit;
                if (unit == null || exempt.Contains(unit) || IsTemplate(unit)) continue;
                var p = field.GetValue(unit) as BlueprintPortrait;
                if (p == null) continue;
                List<string> names;
                if (!wearers.TryGetValue(p, out names)) wearers[p] = names = new List<string>();
                names.Add(unit.name);
            }
            s_Banned = new HashSet<BlueprintPortrait>(wearers.Keys);
            foreach (var p in root.CharGen.Portraits)
            {
                List<string> names;
                if (wearers.TryGetValue(p, out names))
                    Main.Log.Log("ban: " + p.name + " <- " + names.Count + ": " + string.Join(", ", names.ToArray()));
                else
                    Main.Log.Log("ban: " + p.name + " allowed");
            }
            return s_Banned;
        }

        static KeyValuePair<Category, int> ReadMarker(string root, string id)
        {
            KeyValuePair<Category, int> m;
            if (s_Markers.TryGetValue(id, out m)) return m;
            m = new KeyValuePair<Category, int>(Category.Custom, 0);
            try
            {
                string file = Path.Combine(Path.Combine(root, id), Marker);
                if (File.Exists(file))
                {
                    Category c = Category.Custom;
                    int order = 0;
                    foreach (string line in File.ReadAllLines(file))
                    {
                        int eq = line.IndexOf('=');
                        if (eq < 0) continue;
                        string key = line.Substring(0, eq).Trim(), value = line.Substring(eq + 1).Trim();
                        if (key == "category" && value == "WotR") c = Category.WotR;
                        else if (key == "category" && value == "WotRCharacters") c = Category.WotRCharacters;
                        else if (key == "order") int.TryParse(value, out order);
                    }
                    m = new KeyValuePair<Category, int>(c, order);
                }
            }
            catch (Exception e) { Main.Log.Error("gallery: marker of " + id + ": " + e.Message); }
            s_Markers[id] = m;
            return m;
        }

        // The list for the unit being built, in category order. Portraits of the other characters of the party are
        // left out too: no two characters share a face.
        internal static List<Entry> Build(LevelUpController controller)
        {
            var banned = Banned();
            var usedBlueprints = new HashSet<BlueprintPortrait>();
            var usedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var player = Game.Instance.Player;
            UnitDescriptor self = controller != null ? controller.Unit : null;
            if (player != null && player.AllCharacters != null)
                foreach (UnitEntityData u in player.AllCharacters)
                {
                    if (u == null || u.Descriptor == self) continue;
                    var s = u.UISettings;
                    if (s == null) continue;
                    if (s.CustomPortraitRaw != null && s.CustomPortraitRaw.IsCustom) usedIds.Add(s.CustomPortraitRaw.CustomId);
                    else if (s.PortraitBlueprintRaw) usedBlueprints.Add(s.PortraitBlueprintRaw);
                }

            var list = new List<Entry>();
            int order = 0;
            foreach (var p in Game.Instance.BlueprintRoot.CharGen.Portraits)
                if (p != null && p.PlayerHasAccess && !banned.Contains(p) && !usedBlueprints.Contains(p))
                    list.Add(new Entry { Category = Category.Kingmaker, Blueprint = p, Order = order++ });

            string folder = CustomPortraitsManager.PortraitsRootFolderPath;
            string[] ids = CustomPortraitsManager.Instance.GetExistingCustomPortraitIds();
            Array.Sort(ids, StringComparer.OrdinalIgnoreCase);
            int custom = 0;
            foreach (string id in ids)
            {
                if (usedIds.Contains(id)) continue;
                var m = ReadMarker(folder, id);
                list.Add(new Entry { Category = m.Key, CustomId = id, Order = m.Key == Category.Custom ? custom++ : m.Value });
            }
            list = list.OrderBy(e => (int)e.Category).ThenBy(e => e.Order).ToList();
            Main.Log.Log("gallery: " + string.Join(", ", Enumerable.Range(0, Count).Select(i => Name((Category)i) + " "
                + list.Count(e => (int)e.Category == i)).ToArray()) + "; left out as taken in the party: "
                + (usedBlueprints.Count + usedIds.Count));
            return list;
        }
    }
}
