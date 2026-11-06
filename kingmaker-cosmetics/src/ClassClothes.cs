using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Root.Strings;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.LevelUp;
using Kingmaker.UI.ServiceWindow;
using Kingmaker.UI._ConsoleUI.CharGen.Phases.Common;
using Kingmaker.UI._ConsoleUI.Inventory;
using Kingmaker.UI._ConsoleUI.Utils.MultiNavigationTool;
using Kingmaker.View;
using Kingmaker.Visual.CharacterSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace KingmakerCosmetics
{
    // Layers of the class clothes under armour, each shown or hidden on its own, for every member of the party:
    // the shirt and trousers (always there: without them the armour shows bare skin), the skirt (the faulds of the
    // clothes, seen under the armour's own) and the belts and bottles (the class accessories over the armour).
    // Only while armour is worn: without it all the clothes are there. Of the companions with clothes of their own
    // (from their prefab, not from the class) the same layers are taken. Body, head, hair and the items of the
    // slots are never touched.
    //
    // The clothes are not taken off the character: while the game builds its mesh (Character.UpdateCharacter,
    // RebuildOutfit) the hidden parts are lent out of their entities, then given back. The choice is kept in a file
    // of the mod by the game's id and the unit's id, out of the save: without the mod the game shows all of it.
    static class ClassClothes
    {
        internal const int HideSkirt = 1, HideBelts = 2;

        // Layers of the game: class clothes 100, armour 200, class accessories 300.
        const int ArmourLayer = 200;
        const int ClothesLayer = 100;
        const int AccessoriesLayer = 300;

        const BodyPartType HeadParts = BodyPartType.Head | BodyPartType.HeadTop | BodyPartType.Ears | BodyPartType.Hair | BodyPartType.Hair2
            | BodyPartType.Facialhair | BodyPartType.Facialhair2 | BodyPartType.EyeBrows | BodyPartType.HoopHead | BodyPartType.Glasses | BodyPartType.Horns;

        static Dictionary<string, int> s_Flags;
        static string File => Path.Combine(Main.ModPath ?? ".", "ClothesLayers.txt");

        static string Key(UnitEntityData unit)
        {
            return (Game.Instance?.Player?.GameId ?? "") + "\t" + unit.UniqueId;
        }

        static void Load()
        {
            if (s_Flags != null)
                return;
            s_Flags = new Dictionary<string, int>();
            try
            {
                if (!System.IO.File.Exists(File))
                    return;
                foreach (var line in System.IO.File.ReadAllLines(File))
                {
                    var parts = line.Split('\t');
                    int flags;
                    if (parts.Length == 3 && int.TryParse(parts[2], out flags) && flags > 0)
                        s_Flags[parts[0] + "\t" + parts[1]] = flags & (HideSkirt | HideBelts);
                }
            }
            catch (Exception ex)
            {
                Main.Log.Error("clothes layers: file not read: " + ex.Message);
            }
        }

        internal static int Flags(UnitEntityData unit)
        {
            if (unit == null)
                return 0;
            Load();
            int flags;
            return s_Flags.TryGetValue(Key(unit), out flags) ? flags : 0;
        }

        internal static bool Shown(UnitEntityData unit, int flag) => (Flags(unit) & flag) == 0;

        internal static void Show(UnitEntityData unit, int flag, bool shown)
        {
            if (unit == null)
                return;
            int old = Flags(unit);
            int flags = shown ? old & ~flag : old | flag;
            if (flags == old)
                return;
            if (flags == 0)
                s_Flags.Remove(Key(unit));
            else
                s_Flags[Key(unit)] = flags;
            try
            {
                System.IO.File.WriteAllLines(File, s_Flags.Select(kv => kv.Key + "\t" + kv.Value).ToArray());
            }
            catch (Exception ex)
            {
                Main.Log.Error("clothes layers: file not written: " + ex.Message);
            }
            s_Logged.Remove(unit.UniqueId);
            Refresh(unit);
            s_Trace = 8;
            Main.Log.Log($"clothes layers {unit.CharacterName}: hide {old} -> {flags}, depth {s_Depth}; avatars: "
                + string.Join("; ", Avatars(unit).Select(a => $"{a.name} dirty {a.IsDirty} enabled {a.isActiveAndEnabled} baked {a.BakedCharacter != null} unit {UnitOf(a)?.CharacterName ?? "none"}").ToArray()));
        }

        // Rebuilds the unit in the world and its dolls.
        static void Refresh(UnitEntityData unit)
        {
            foreach (var avatar in Avatars(unit))
                avatar.IsDirty = true;
        }

        internal static IEnumerable<Character> Avatars(UnitEntityData unit)
        {
            var avatar = unit.View == null ? null : unit.View.CharacterAvatar;
            if (avatar != null)
                yield return avatar;
            foreach (var room in UnityEngine.Object.FindObjectsOfType<DollRoom>())
                if (room.Unit == unit && room.GetAvatar() != null)
                    yield return room.GetAvatar();
        }

        // The dolls of the inventory, by the units of the avatars they are made from.
        static readonly ConditionalWeakTable<Character, UnitEntityData> s_Dolls = new ConditionalWeakTable<Character, UnitEntityData>();

        internal static void AddDoll(Character doll, Character original, UnitEntityData roomUnit)
        {
            var unit = original == null ? null : UnitOf(original);
            if (unit == null || roomUnit != null && roomUnit.View != null && roomUnit.View.CharacterAvatar == original)
                unit = roomUnit;
            if (doll == null || unit == null)
                return;
            s_Dolls.Remove(doll);
            s_Dolls.Add(doll, unit);
        }

        // The avatar of a story companion is not under the unit's view: it is found by the party's avatars.
        static UnitEntityData UnitOf(Character character)
        {
            UnitEntityData unit;
            if (s_Dolls.TryGetValue(character, out unit))
                return unit;
            var view = character.GetComponentInParent<UnitEntityView>();
            if (view != null && view.EntityData != null)
                return view.EntityData;
            // A doll is built while it is made, before it is known here: the room knows its unit by then.
            if (character.IsInDollRoom)
            {
                var room = Game.Instance?.UI?.Common?.DollRoom;
                if (room != null && room.Unit != null)
                    return room.Unit;
            }
            var all = Game.Instance?.Player?.AllCharacters;
            return all == null ? null : all.FirstOrDefault(u => u != null && u.View != null && u.View.CharacterAvatar == character);
        }

        // Belts and bottles: the accessories over the armour, and the entities of attached things only (pouches).
        static bool IsBelts(EquipmentEntity ee)
        {
            return ee.Layer >= AccessoriesLayer || ee.BodyParts.Count == 0 && ee.OutfitParts.Count > 0;
        }

        // What to hide on this character now: whole entities (belts and bottles) and entities without their skirt.
        static bool Plan(Character character, List<EquipmentEntity> hidden, List<EquipmentEntity> trimmed)
        {
            var unit = UnitOf(character);
            if (unit == null)
                return false;
            int flags = Flags(unit);
            if (flags == 0)
                return false;
            bool armour = unit.Body != null && unit.Body.Armor.HasItem;
            var clothes = ClothesOf(unit, character);
            if (armour)
                foreach (var ee in clothes)
                {
                    if (IsBelts(ee))
                    {
                        if ((flags & HideBelts) != 0)
                            hidden.Add(ee);
                    }
                    else if ((flags & HideSkirt) != 0 && ee.Layer < ArmourLayer && ee.BodyParts.Any(p => p != null && p.Type == BodyPartType.Faulds))
                        trimmed.Add(ee);
                }
            if (!s_Logged.Contains(unit.UniqueId))
            {
                s_Logged.Add(unit.UniqueId);
                var others = character.EquipmentEntities.Where(e => e != null && !clothes.Contains(e));
                Main.Log.Log($"clothes layers {unit.CharacterName}: hide {flags}, armour {armour}, class equipment {unit.Descriptor.UseClassEquipment}; "
                    + $"hidden [{Names(hidden)}], no skirt [{Names(trimmed)}], other clothes [{Names(clothes.Except(hidden).Except(trimmed))}], kept [{Names(others)}]");
            }
            return hidden.Count > 0 || trimmed.Count > 0;
        }

        static readonly HashSet<string> s_Logged = new HashSet<string>();

        static string Names(IEnumerable<EquipmentEntity> ees)
        {
            return string.Join(", ", ees.Select(e => e.name + " " + e.Layer + " {" + string.Join(" ", e.BodyParts.Where(p => p != null).Select(p => p.Type.ToString()).ToArray())
                + (e.OutfitParts.Count > 0 ? " +" + e.OutfitParts.Count + " outfit" : "") + "}").ToArray());
        }

        // The class clothes of the main character and the custom companions; the companions' own clothes otherwise:
        // entities on the avatar not from a slot, without head and hair parts, at the layer of the clothes or above,
        // or with attached things only.
        static HashSet<EquipmentEntity> ClothesOf(UnitEntityData unit, Character character)
        {
            var result = new HashSet<EquipmentEntity>();
            if (unit.Descriptor.UseClassEquipment)
            {
                var cls = unit.Descriptor.Progression.GetEquipmentClass();
                var race = unit.Descriptor.Progression.Race;
                if (cls != null && race != null)
                    foreach (var ee in cls.LoadClothes(unit.Gender, race))
                        if (ee != null)
                            result.Add(ee);
                return result;
            }
            var slots = new HashSet<EquipmentEntity>();
            if (unit.View != null && unit.Body != null)
                foreach (var slot in unit.Body.AllSlots)
                    foreach (var ee in unit.View.ExtractEquipmentEntities(slot))
                        if (ee != null)
                            slots.Add(ee);
            foreach (var ee in character.EquipmentEntities)
                if (ee != null && !slots.Contains(ee) && (ee.Layer >= ClothesLayer && ee.Layer != ArmourLayer || ee.BodyParts.Count == 0 && ee.OutfitParts.Count > 0)
                    && !ee.BodyParts.Any(p => p != null && (p.Type & HeadParts) != 0))
                    result.Add(ee);
            return result;
        }

        // While the character is built: the lent parts, given back afterwards.
        class Lent
        {
            internal EquipmentEntity Entity;
            internal List<BodyPart> Parts;
            internal List<EquipmentEntity.OutfitPart> Outfit;
            internal BodyPartType Hide;
        }

        static readonly List<Lent> s_Lent = new List<Lent>();
        static int s_Depth;

        static int s_Trace;

        internal static void Begin(Character character)
        {
            if (s_Trace > 0 && character != null)
            {
                s_Trace--;
                var traced = UnitOf(character);
                Main.Log.Log($"clothes layers build {character.name}: depth {s_Depth}, unit {traced?.CharacterName ?? "none"}, hide {Flags(traced)}");
            }
            if (s_Depth++ > 0 || character == null || character.BakedCharacter)
                return;
            try
            {
                var hidden = new List<EquipmentEntity>();
                var trimmed = new List<EquipmentEntity>();
                if (!Plan(character, hidden, trimmed))
                    return;
                foreach (var ee in hidden)
                {
                    s_Lent.Add(new Lent { Entity = ee, Parts = ee.BodyParts, Outfit = ee.OutfitParts, Hide = ee.HideBodyParts });
                    ee.BodyParts = new List<BodyPart>();
                    ee.OutfitParts = new List<EquipmentEntity.OutfitPart>();
                    ee.HideBodyParts = 0;
                }
                foreach (var ee in trimmed)
                {
                    s_Lent.Add(new Lent { Entity = ee, Parts = ee.BodyParts, Outfit = ee.OutfitParts, Hide = ee.HideBodyParts });
                    ee.BodyParts = ee.BodyParts.Where(p => p == null || p.Type != BodyPartType.Faulds).ToList();
                    ee.HideBodyParts &= ~BodyPartType.Faulds;
                }
            }
            catch (Exception ex)
            {
                End();
                s_Depth++;
                Main.Log.Error("clothes layers: " + ex);
            }
        }

        internal static void End()
        {
            if (--s_Depth > 0)
                return;
            s_Depth = 0;
            foreach (var lent in s_Lent)
            {
                lent.Entity.BodyParts = lent.Parts;
                lent.Entity.OutfitParts = lent.Outfit;
                lent.Entity.HideBodyParts = lent.Hide;
            }
            s_Lent.Clear();
        }

        // The detail colour of a unit in the game (the threads of the braids): the second colour of its entities
        // with details, kept in its doll data, so the game puts it on when it loads the unit. Null without them.
        internal static TextureSequentialSelectorVm DetailsModel(UnitEntityData unit)
        {
            var doll = unit == null ? null : unit.Descriptor.Doll;
            var ids = doll == null ? null : doll.EquipmentEntityIds.Where(Details.Ids.Contains).ToList();
            if (ids == null || ids.Count == 0)
                return null;
            var ramps = Details.Palette();
            int current;
            if (!doll.EntitySecondaryRampIdices.TryGetValue(ids[0], out current) || current < 0 || current >= ramps.Count)
                current = 0;
            var entries = new List<TextureSequentialEntity>();
            for (int i = 0; i < ramps.Count; i++)
            {
                int index = i;
                entries.Add(new TextureSequentialEntity
                {
                    Texture = ramps[i],
                    Setter = () =>
                    {
                        foreach (var id in ids)
                            doll.EntitySecondaryRampIdices[id] = index;
                        foreach (var avatar in Avatars(unit))
                            foreach (var ee in avatar.EquipmentEntities.Where(e => e != null && Details.Entities.Contains(e)).ToList())
                                avatar.SetSecondaryRampIndex(ee, index);
                    }
                });
            }
            return new TextureSequentialSelectorVm(entries, entries[current]);
        }

        // en, ru, de, fr, it, es, zh
        static readonly string[][] Texts =
        {
            new[] { "Clothing", "Colors", "Show skirt", "Show belts and bottles", "Primary", "Secondary", "Details" },
            new[] { "Одежда", "Цвета", "Показывать подол", "Показывать ремни и бутылки", "Основной", "Дополнительный", "Детали" },
            new[] { "Kleidung", "Farben", "Rockschoß zeigen", "Gürtel und Flaschen zeigen", "Primär", "Sekundär", "Details" },
            new[] { "Vêtements", "Couleurs", "Afficher la jupe", "Afficher ceintures et fioles", "Principale", "Secondaire", "Détails" },
            new[] { "Abiti", "Colori", "Mostra la gonna", "Mostra cinture e fiale", "Primario", "Secondario", "Dettagli" },
            new[] { "Ropa", "Colores", "Mostrar faldón", "Mostrar cinturones y frascos", "Principal", "Secundario", "Detalles" },
            new[] { "服装", "颜色", "显示裙摆", "显示腰带和瓶子", "主色", "副色", "细节" },
        };

        internal const int TextClothing = 0, TextColours = 1, TextSkirt = 2, TextBelts = 3, TextPrimary = 4, TextSecondary = 5, TextDetails = 6;

        internal static string Text(int text)
        {
            int language;
            switch (LocalizationManager.CurrentLocale)
            {
                case Locale.ruRU: language = 1; break;
                case Locale.deDE: language = 2; break;
                case Locale.frFR: language = 3; break;
                case Locale.itIT: language = 4; break;
                case Locale.esES: language = 5; break;
                case Locale.zhCN: language = 6; break;
                default: language = 0; break;
            }
            return Texts[language][text];
        }

        static readonly PropertyInfo s_ViewModel = AccessTools.Property(typeof(Kingmaker.Assets.UI.Utils.ViewBase<InventoryAppearanceVM>), "ViewModel");

        internal static InventoryAppearanceVM ModelOf(InventoryAppearanceView view)
        {
            return (InventoryAppearanceVM)s_ViewModel.GetValue(view, null);
        }

        // Characters whose look is built of entities (not a baked mesh).
        internal static bool Buildable(UnitEntityData unit)
        {
            var avatar = unit == null || unit.View == null ? null : unit.View.CharacterAvatar;
            return avatar != null && avatar.BakedCharacter == null;
        }

        internal static void NoLocalizer(GameObject copy)
        {
            // The game's localizer would put the original's text back on its first Awake.
            foreach (var localizer in copy.GetComponentsInChildren<LocalizedUIText>(true))
                UnityEngine.Object.DestroyImmediate(localizer);
        }

        internal static string PathOf(Transform t)
        {
            var names = new List<string>();
            for (; t != null; t = t.parent)
                names.Add(t.name + (t.GetComponent<LayoutGroup>() != null ? "[layout]" : "") + (t.GetComponent<ContentSizeFitter>() != null ? "[fitter]" : ""));
            return string.Join(" < ", names.ToArray());
        }

        internal static string Tree(Transform t, string indent)
        {
            var rect = t as RectTransform;
            var text = t.GetComponent<TextMeshProUGUI>();
            var parts = new List<string> { indent + t.name + (t.gameObject.activeSelf ? "" : " (off)") + " [" + string.Join(",", t.GetComponents<Component>().Select(c => c.GetType().Name).ToArray()) + "]"
                + (rect == null ? "" : " " + rect.anchoredPosition + " " + rect.rect.size) + (text == null ? "" : " \"" + text.text + "\"") };
            for (int i = 0; i < t.childCount; i++)
                parts.Add(Tree(t.GetChild(i), indent + "  "));
            return string.Join("\n", parts.ToArray());
        }
    }

    [HarmonyPatch(typeof(Character), "UpdateCharacter")]
    static class ClassClothesBuild
    {
        static void Prefix(Character __instance) => ClassClothes.Begin(__instance);
        static void Finalizer() => ClassClothes.End();
    }

    [HarmonyPatch(typeof(Character), nameof(Character.RebuildOutfit))]
    static class ClassClothesOutfit
    {
        static void Prefix(Character __instance) => ClassClothes.Begin(__instance);
        static void Finalizer() => ClassClothes.End();
    }

    [HarmonyPatch(typeof(DollRoom), nameof(DollRoom.CreateAvatar))]
    static class ClassClothesDoll
    {
        static void Postfix(DollRoom __instance, Character originalAvatar, Character __result) => ClassClothes.AddDoll(__result, originalAvatar, __instance.Unit);
    }

    // The gamepad: the "Appearance" window on Y for every character built of entities, not only the main character
    // and the custom companions (the game's own rows of helmet and backpack work for all of them).
    [HarmonyPatch(typeof(InventoryVM), "OnChangedUnit")]
    static class ClassClothesAppearanceForAll
    {
        static readonly FieldInfo s_Unit = AccessTools.Field(typeof(InventoryVM), "UnitEntity");

        static void Postfix(InventoryVM __instance)
        {
            var unit = ((UniRx.IReactiveProperty<UnitEntityData>)s_Unit.GetValue(__instance)).Value;
            if (ClassClothes.Buildable(unit))
                __instance.IsMainCharacterOrCustom.Value = true;
        }
    }

    // The gamepad window: rows of the skirt and of the belts and bottles (copies of the helmet row) under the
    // helmet, a row of the detail colour (a copy of the second colour's group) under the colours.
    [HarmonyPatch(typeof(InventoryAppearanceView), "InternalBind")]
    static class ClassClothesRowsConsole
    {
        static readonly FieldInfo s_Helm = AccessTools.Field(typeof(InventoryAppearanceView), "m_HelmSelectorView");
        static readonly FieldInfo s_Primary = AccessTools.Field(typeof(InventoryAppearanceView), "m_PrimaryColorSelectorView");
        static readonly FieldInfo s_Secondary = AccessTools.Field(typeof(InventoryAppearanceView), "m_SecondaryColorSelectorView");
        static readonly FieldInfo s_Counter = AccessTools.Field(typeof(TextureSequentialSelectorView), "m_Counter");
        static readonly FieldInfo s_Unit = AccessTools.Field(typeof(InventoryAppearanceVM), "m_Unit");

        internal class Rows
        {
            internal SequentialSelectorView Skirt, Belts;
            internal TextureSequentialSelectorView Details;
            internal readonly List<IDisposable> Models = new List<IDisposable>();
        }

        internal static readonly Dictionary<InventoryAppearanceView, Rows> All = new Dictionary<InventoryAppearanceView, Rows>();

        // Before the game's InternalBind sets the cursor's order (UpdateNavigation, patched below).
        static void Prefix(InventoryAppearanceView __instance)
        {
            try
            {
                foreach (var gone in All.Keys.Where(k => k == null).ToList())
                    All.Remove(gone);
                var helm = (SequentialSelectorView)s_Helm.GetValue(__instance);
                var primary = (TextureSequentialSelectorView)s_Primary.GetValue(__instance);
                var secondary = (TextureSequentialSelectorView)s_Secondary.GetValue(__instance);
                var unit = (UnitEntityData)s_Unit.GetValue(ClassClothes.ModelOf(__instance));
                if (helm == null || secondary == null || unit == null)
                    return;
                Rows rows;
                if (!All.TryGetValue(__instance, out rows) || rows.Skirt == null || rows.Details == null)
                {
                    Main.Log.Log("clothes layers window (gamepad):\n" + ClassClothes.Tree(__instance.transform, "  "));
                    rows = new Rows();
                    rows.Belts = CopyRow(helm, "KingmakerCosmetics_Belts");
                    rows.Skirt = CopyRow(helm, "KingmakerCosmetics_Skirt");
                    rows.Details = CopyColour(secondary);
                    All[__instance] = rows;
                }
                foreach (var model in rows.Models)
                    model?.Dispose();
                rows.Models.Clear();
                bool buildable = ClassClothes.Buildable(unit);
                Bind(rows, rows.Skirt, buildable ? unit : null, ClassClothes.HideSkirt, ClassClothes.TextSkirt);
                Bind(rows, rows.Belts, buildable ? unit : null, ClassClothes.HideBelts, ClassClothes.TextBelts);
                var details = ClassClothes.DetailsModel(unit);
                rows.Models.Add(details);
                rows.Details.Bind(details);
                Group(rows.Details).gameObject.SetActive(details != null);
                Label(primary, ClassClothes.TextPrimary);
                Label(secondary, ClassClothes.TextSecondary);
                Label(rows.Details, ClassClothes.TextDetails);
            }
            catch (Exception ex)
            {
                Main.Log.Error("clothes layers rows (gamepad): " + ex);
            }
        }

        static SequentialSelectorView CopyRow(SequentialSelectorView helm, string name)
        {
            var copy = UnityEngine.Object.Instantiate(helm.gameObject, helm.transform.parent, false);
            copy.name = name;
            copy.transform.SetSiblingIndex(helm.transform.GetSiblingIndex() + 1);
            ClassClothes.NoLocalizer(copy);
            return copy.GetComponent<SequentialSelectorView>();
        }

        static void Bind(Rows rows, SequentialSelectorView row, UnitEntityData unit, int flag, int text)
        {
            if (unit == null)
            {
                row.gameObject.SetActive(false);
                return;
            }
            var on = new StringSequentialEntity { Title = UIStrings.Instance.SettingsUI.BoolOn, Setter = () => ClassClothes.Show(unit, flag, true) };
            var off = new StringSequentialEntity { Title = UIStrings.Instance.SettingsUI.BoolOff, Setter = () => ClassClothes.Show(unit, flag, false) };
            var model = new StringSequentialSelectorVm(new List<StringSequentialEntity> { on, off }, ClassClothes.Shown(unit, flag) ? on : off);
            rows.Models.Add(model);
            row.Bind(model);
            row.SetTitleText(ClassClothes.Text(text));
        }

        // The group of a colour row: its highest ancestor that holds no other colour row (with the row's label).
        static Transform Group(TextureSequentialSelectorView row)
        {
            var group = row.transform;
            while (group.parent != null && group.parent.GetComponentsInChildren<TextureSequentialSelectorView>(true).Length == 1
                && group.parent.GetComponent<InventoryAppearanceView>() == null)
                group = group.parent;
            return group;
        }

        static TextureSequentialSelectorView CopyColour(TextureSequentialSelectorView secondary)
        {
            var group = Group(secondary);
            var copy = UnityEngine.Object.Instantiate(group.gameObject, group.parent, false);
            copy.name = "KingmakerCosmetics_Details";
            copy.transform.SetSiblingIndex(group.GetSiblingIndex() + 1);
            ClassClothes.NoLocalizer(copy);
            return copy.GetComponentInChildren<TextureSequentialSelectorView>(true);
        }

        // The names of the colours: the texts of the row's group but its counter.
        static void Label(TextureSequentialSelectorView row, int text)
        {
            if (row == null)
                return;
            var counter = (TextMeshProUGUI)s_Counter.GetValue(row);
            foreach (var label in Group(row).GetComponentsInChildren<TextMeshProUGUI>(true))
                if (label != counter)
                {
                    ClassClothes.NoLocalizer(label.gameObject);
                    label.text = ClassClothes.Text(text);
                }
        }
    }

    // The cursor's order: backpack, helmet, skirt, belts and bottles, then the colours where there are any. (The game
    // puts the colour rows in always and once more when they exist; on characters without them they have no model.)
    [HarmonyPatch(typeof(InventoryAppearanceView), "UpdateNavigation")]
    static class ClassClothesNavigation
    {
        static readonly FieldInfo s_Navigation = AccessTools.Field(typeof(InventoryAppearanceView), "m_ConsoleNavigation");
        static readonly FieldInfo s_Backpack = AccessTools.Field(typeof(InventoryAppearanceView), "m_BackpackSelectorView");
        static readonly FieldInfo s_Helm = AccessTools.Field(typeof(InventoryAppearanceView), "m_HelmSelectorView");
        static readonly FieldInfo s_Primary = AccessTools.Field(typeof(InventoryAppearanceView), "m_PrimaryColorSelectorView");
        static readonly FieldInfo s_Secondary = AccessTools.Field(typeof(InventoryAppearanceView), "m_SecondaryColorSelectorView");

        static bool Prefix(InventoryAppearanceView __instance)
        {
            try
            {
                var navigation = (ConsoleMultiNavigationCollection)s_Navigation.GetValue(__instance);
                var vm = ClassClothes.ModelOf(__instance);
                ClassClothesRowsConsole.Rows rows;
                ClassClothesRowsConsole.All.TryGetValue(__instance, out rows);
                var list = new List<IConsoleMultiNavigationEntity>
                {
                    (IConsoleMultiNavigationEntity)s_Backpack.GetValue(__instance),
                    (IConsoleMultiNavigationEntity)s_Helm.GetValue(__instance),
                };
                if (rows != null && rows.Skirt != null && rows.Skirt.gameObject.activeSelf)
                    list.Add(rows.Skirt);
                if (rows != null && rows.Belts != null && rows.Belts.gameObject.activeSelf)
                    list.Add(rows.Belts);
                if (vm.OutfitPrimarySelectorVm != null)
                    list.Add((IConsoleMultiNavigationEntity)s_Primary.GetValue(__instance));
                if (vm.OutfitSecondarySelectorVm != null)
                    list.Add((IConsoleMultiNavigationEntity)s_Secondary.GetValue(__instance));
                if (rows != null && rows.Details != null && rows.Details.gameObject.activeInHierarchy)
                    list.Add(rows.Details);
                navigation.SetEntitiesVertical(list);
                navigation.SelectCurrentEntity();
                return false;
            }
            catch (Exception ex)
            {
                Main.Log.Error("clothes layers navigation (gamepad): " + ex);
                return true;
            }
        }
    }

    [HarmonyPatch(typeof(InventoryAppearanceView), "DestroyViewImplementation")]
    static class ClassClothesRowsConsoleClose
    {
        static void Postfix(InventoryAppearanceView __instance)
        {
            ClassClothesRowsConsole.Rows rows;
            if (!ClassClothesRowsConsole.All.TryGetValue(__instance, out rows))
                return;
            foreach (var model in rows.Models)
                model?.Dispose();
            rows.Models.Clear();
        }
    }

    // The mouse: the window of the gear by the doll, in two groups. "Clothing": the game's helmet and backpack checks,
    // then the skirt and the belts and bottles (only on characters built of entities). "Colors": the primary and
    // secondary clothes colours (where the gamepad's "Appearance" window has them) and the detail colour (where the
    // character has details), each as in the character creation: a name and the circle of the colour, which opens the
    // window of the colours. A click out of the window closes it.
    [HarmonyPatch(typeof(CharacterVisualSettingsView), nameof(CharacterVisualSettingsView.Initialize))]
    static class ClassClothesWindowPC
    {
        static readonly FieldInfo s_Helm = AccessTools.Field(typeof(CharacterVisualSettingsView), "m_HelmEntityView");
        static readonly FieldInfo s_Backpack = AccessTools.Field(typeof(CharacterVisualSettingsView), "m_BackpackEntityView");
        static readonly FieldInfo s_Window = AccessTools.Field(typeof(CharacterVisualSettingsView), "m_Window");
        static readonly FieldInfo s_SettingsBtn = AccessTools.Field(typeof(CharacterVisualSettingsView), "m_SettingsBtn");
        static readonly FieldInfo s_Show = AccessTools.Field(typeof(CharacterVisualSettingsView), "m_WindowShow");
        static readonly MethodInfo s_Toggle = AccessTools.Method(typeof(CharacterVisualSettingsView), "OnSettingsBtnClick");
        static readonly FieldInfo s_Check = AccessTools.Field(typeof(CharacterVisualSettingsEntityView), "m_Check");
        static readonly FieldInfo s_Button = AccessTools.Field(typeof(CharacterVisualSettingsEntityView), "m_Button");

        class Rows
        {
            internal TextMeshProUGUI ClothingHeader, ColoursHeader;
            internal CharacterVisualSettingsEntityView Skirt, Belts;
            internal ColourRow Primary, Secondary, Details;
            internal InventoryAppearanceVM Model;
            internal TextureSequentialSelectorVm DetailsModel;

            internal bool Alive => ClothingHeader != null && ColoursHeader != null && Skirt != null && Belts != null
                && (Primary == null || Primary.Row != null && Secondary.Row != null && Details.Row != null);

            internal void HidePopups()
            {
                Primary?.Hide();
                Secondary?.Hide();
                Details?.Hide();
            }
        }

        // The gear button opens and closes the window (a click out of it too): the windows of the colours close with it.
        [HarmonyPatch(typeof(CharacterVisualSettingsView), "OnSettingsBtnClick")]
        static class CloseColours
        {
            static void Prefix(CharacterVisualSettingsView __instance)
            {
                Rows rows;
                if (s_Rows.TryGetValue(__instance, out rows))
                    rows.HidePopups();
            }
        }

        static readonly Dictionary<CharacterVisualSettingsView, Rows> s_Rows = new Dictionary<CharacterVisualSettingsView, Rows>();

        static void Postfix(CharacterVisualSettingsView __instance, UnitEntityData unit)
        {
            try
            {
                foreach (var gone in s_Rows.Keys.Where(k => k == null).ToList())
                    s_Rows.Remove(gone);
                Rows rows;
                if (!s_Rows.TryGetValue(__instance, out rows) || !rows.Alive)
                {
                    rows = Make(__instance);
                    if (rows == null)
                        return;
                    s_Rows[__instance] = rows;
                }
                rows.ClothingHeader.text = ClassClothes.Text(ClassClothes.TextClothing);
                rows.ColoursHeader.text = ClassClothes.Text(ClassClothes.TextColours);
                bool buildable = ClassClothes.Buildable(unit);
                var avatar = unit == null || unit.View == null ? null : unit.View.CharacterAvatar;
                Main.Log.Log($"clothes layers window (PC): {unit?.CharacterName ?? "nobody"}, avatar {(avatar == null ? "none" : avatar.name)}, baked {(avatar != null && avatar.BakedCharacter != null)}, "
                    + $"class equipment {unit?.Descriptor.UseClassEquipment}, pet {unit?.Descriptor.IsPet}, hide {ClassClothes.Flags(unit)}");
                BindCheck(rows.Skirt, buildable ? unit : null, ClassClothes.HideSkirt, ClassClothes.TextSkirt);
                BindCheck(rows.Belts, buildable ? unit : null, ClassClothes.HideBelts, ClassClothes.TextBelts);
                rows.Model?.Dispose();
                rows.DetailsModel?.Dispose();
                rows.Model = unit != null && unit.Descriptor.Doll != null && unit.Descriptor.Progression.GetEquipmentClass() != null ? new InventoryAppearanceVM(unit, null) : null;
                rows.DetailsModel = ClassClothes.DetailsModel(unit);
                var cls = rows.Model == null ? null : unit.Descriptor.Progression.GetEquipmentClass();
                rows.Primary?.Bind(rows.Model?.OutfitPrimarySelectorVm, ClassClothes.Text(ClassClothes.TextPrimary), cls?.PrimaryColor ?? 0);
                rows.Secondary?.Bind(rows.Model?.OutfitSecondarySelectorVm, ClassClothes.Text(ClassClothes.TextSecondary), cls?.SecondaryColor ?? 0);
                rows.Details?.Bind(rows.DetailsModel, ClassClothes.Text(ClassClothes.TextDetails), 0);
                rows.ColoursHeader.gameObject.SetActive(rows.Primary != null && (rows.Model != null || rows.DetailsModel != null));
            }
            catch (Exception ex)
            {
                Main.Log.Error("clothes layers window (PC): " + ex);
            }
        }

        static void BindCheck(CharacterVisualSettingsEntityView row, UnitEntityData unit, int flag, int text)
        {
            row.gameObject.SetActive(unit != null);
            if (unit == null)
                row.Initialize(null, null);
            else
                row.Initialize(() => ClassClothes.Show(unit, flag, !ClassClothes.Shown(unit, flag)), () => ClassClothes.Shown(unit, flag));
            var check = (GameObject)s_Check.GetValue(row);
            foreach (var label in row.GetComponentsInChildren<TextMeshProUGUI>(true))
                if (check == null || !label.transform.IsChildOf(check.transform))
                    label.text = ClassClothes.Text(text);
        }

        static GameObject Keep(List<GameObject> made, GameObject row)
        {
            made.Add(row);
            return row;
        }

        static GameObject Copy(CharacterVisualSettingsEntityView helm, string name, ref int sibling)
        {
            var copy = UnityEngine.Object.Instantiate(helm.gameObject, helm.transform.parent, false);
            copy.name = name;
            copy.transform.SetSiblingIndex(sibling++);
            ClassClothes.NoLocalizer(copy);
            return copy;
        }

        // A header of a group: a copy of a check's label, alone in a row of its own, in the middle.
        static TextMeshProUGUI Header(CharacterVisualSettingsEntityView helm, string name, int sibling)
        {
            var label = helm.GetComponentsInChildren<TextMeshProUGUI>(true).FirstOrDefault();
            var row = new GameObject(name, typeof(RectTransform));
            row.transform.SetParent(helm.transform.parent, false);
            row.transform.SetSiblingIndex(sibling);
            var helmElement = helm.GetComponent<LayoutElement>();
            var element = row.AddComponent<LayoutElement>();
            element.preferredHeight = element.minHeight = helmElement != null && helmElement.preferredHeight > 0 ? helmElement.preferredHeight : 34f;
            var text = UnityEngine.Object.Instantiate(label.gameObject, row.transform, false).GetComponent<TextMeshProUGUI>();
            text.name = "Text";
            ClassClothes.NoLocalizer(text.gameObject);
            foreach (var fitter in text.GetComponents<MonoBehaviour>().Where(c => c.GetType().Name.StartsWith("ContentSizeFitter")))
                fitter.enabled = false;
            var rect = (RectTransform)text.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = rect.offsetMax = Vector2.zero;
            text.alignment = TextAlignmentOptions.Center;
            text.fontStyle |= FontStyles.Bold;
            return text;
        }

        static Rows Make(CharacterVisualSettingsView view)
        {
            var helm = (CharacterVisualSettingsEntityView)s_Helm.GetValue(view);
            var backpack = (CharacterVisualSettingsEntityView)s_Backpack.GetValue(view);
            if (helm == null || backpack == null || helm.transform.parent != backpack.transform.parent)
                return null;
            var rows = new Rows();
            int first = Math.Min(helm.transform.GetSiblingIndex(), backpack.transform.GetSiblingIndex());
            rows.ClothingHeader = Header(helm, "KingmakerCosmetics_ClothingHeader", first);
            int next = Math.Max(helm.transform.GetSiblingIndex(), backpack.transform.GetSiblingIndex()) + 1;
            rows.Skirt = Copy(helm, "KingmakerCosmetics_Skirt", ref next).GetComponent<CharacterVisualSettingsEntityView>();
            rows.Skirt.PreInit();
            rows.Belts = Copy(helm, "KingmakerCosmetics_Belts", ref next).GetComponent<CharacterVisualSettingsEntityView>();
            rows.Belts.PreInit();
            rows.ColoursHeader = Header(helm, "KingmakerCosmetics_ColoursHeader", next++);
            var template = Template();
            var window = (s_Window.GetValue(view) as Component)?.transform;
            if (template != null && window != null)
            {
                var made = new List<GameObject>();
                try
                {
                    rows.Primary = new ColourRow(Keep(made, Copy(helm, "KingmakerCosmetics_PrimaryColour", ref next)), template, window);
                    rows.Secondary = new ColourRow(Keep(made, Copy(helm, "KingmakerCosmetics_SecondaryColour", ref next)), template, window);
                    rows.Details = new ColourRow(Keep(made, Copy(helm, "KingmakerCosmetics_DetailColour", ref next)), template, window);
                }
                catch (Exception ex)
                {
                    // Without the colour rows the window keeps its checks.
                    Main.Log.Error("clothes layers window (PC): colour rows: " + ex);
                    foreach (var go in made)
                        if (go != null)
                            UnityEngine.Object.Destroy(go);
                    foreach (Transform child in window)
                        if (child.name == "KingmakerCosmetics_ColourWindow")
                            UnityEngine.Object.Destroy(child.gameObject);
                    rows.Primary = rows.Secondary = rows.Details = null;
                }
            }
            else
                Main.Log.Log("clothes layers window (PC): no colour selector of the character creation, no colour rows");
            view.gameObject.AddComponent<OutsideClickCloser>().View = view;
            Main.Log.Log("clothes layers window (PC): rows under " + ClassClothes.PathOf(helm.transform)
                + (template == null || rows.Primary == null ? "" : "; template " + ClassClothes.PathOf(template.transform) + ":\n" + ClassClothes.Tree(template.transform, "  ")
                    + "\ncolour row:\n" + ClassClothes.Tree(rows.Primary.Row.transform, "  ")));
            return rows;
        }

        // The game's own colour selector of the character creation (CharBColorSelectorHidden), cloned into the window:
        // the name, then the circle with the current colour in a golden frame; a click opens the game's window of
        // the colours with the name as its title and the "Reset" button. Reset puts the class colour back (the
        // first colour for the details).
        static CharBColorSelectorHidden s_Template;

        static CharBColorSelectorHidden Template()
        {
            if (s_Template != null)
                return s_Template;
            var all = Resources.FindObjectsOfTypeAll<CharBColorSelectorHidden>().Where(s => s != null && !s.name.StartsWith("KingmakerCosmetics")).ToList();
            Main.Log.Log("clothes layers window (PC): colour selectors found: " + string.Join(", ", all.Select(s => s.name + (s.gameObject.scene.IsValid() ? "" : " (asset)")).ToArray()));
            s_Template = all.FirstOrDefault(s => s.name.IndexOf("Second", StringComparison.OrdinalIgnoreCase) >= 0) ?? all.FirstOrDefault();
            return s_Template;
        }

        class ColourRow : IColorSelectorChanged, IClothColorReset
        {
            static readonly FieldInfo s_SelectorLabel = AccessTools.Field(typeof(CharBColorSelector), "m_Label");
            static readonly FieldInfo s_Ignore = AccessTools.Field(typeof(CharBColorSelector), "m_IgnoreAction");
            static readonly FieldInfo s_Animator = AccessTools.Field(typeof(CharBColorSelectorHidden), "m_ColorPickerAnimator");
            static readonly FieldInfo s_CloseButton = AccessTools.Field(typeof(CharBColorSelectorHidden), "m_CloseButton");
            static readonly FieldInfo s_ToggleGroup = AccessTools.Field(typeof(CharBColorSelector), "m_ColorToggleGroup");
            static readonly FieldInfo s_ToggleItem = AccessTools.Field(typeof(CharBColorSelector), "m_ColorToggleItem");
            static readonly FieldInfo s_Values = AccessTools.Field(typeof(SequentialSelectorVm<TextureSequentialEntity>), "ValueList");

            internal readonly GameObject Row;
            readonly TextMeshProUGUI m_Label;
            readonly CharBColorSelectorHidden m_Selector;
            readonly GameObject m_Popup;
            readonly Canvas m_PopupCanvas;
            readonly List<TextMeshProUGUI> m_Titles;
            TextureSequentialSelectorVm m_Model;
            int m_Reset;

            internal ColourRow(GameObject row, CharBColorSelectorHidden template, Transform window)
            {
                Row = row;
                var entity = row.GetComponent<CharacterVisualSettingsEntityView>();
                var check = (GameObject)s_Check.GetValue(entity);
                var back = (RectTransform)((Button)s_Button.GetValue(entity)).transform;
                UnityEngine.Object.DestroyImmediate(entity);
                // The check's name lies in its button: it is taken out to the row, where it stands.
                m_Label = row.GetComponentsInChildren<TextMeshProUGUI>(true).First(t => check == null || !t.transform.IsChildOf(check.transform));
                m_Label.transform.SetParent(row.transform, true);

                // The circle stands where the check's circle stood, the name where its name stood.
                var copy = UnityEngine.Object.Instantiate(template.gameObject, row.transform, false);
                copy.name = "KingmakerCosmetics_ColourSelector";
                copy.SetActive(true);
                ClassClothes.NoLocalizer(copy);
                var rect = (RectTransform)copy.transform;
                rect.anchorMin = back.anchorMin;
                rect.anchorMax = back.anchorMax;
                rect.pivot = back.pivot;
                rect.sizeDelta = back.sizeDelta;
                rect.anchoredPosition = back.anchoredPosition;
                rect.SetSiblingIndex(back.GetSiblingIndex());
                var backElement = back.GetComponent<LayoutElement>();
                if (backElement != null)
                {
                    var element = copy.GetComponent<LayoutElement>() ?? copy.AddComponent<LayoutElement>();
                    element.ignoreLayout = backElement.ignoreLayout;
                    element.minWidth = backElement.minWidth;
                    element.minHeight = backElement.minHeight;
                    element.preferredWidth = backElement.preferredWidth;
                    element.preferredHeight = backElement.preferredHeight;
                }
                UnityEngine.Object.DestroyImmediate(back.gameObject);
                if (check != null)
                    UnityEngine.Object.DestroyImmediate(check);
                m_Selector = copy.GetComponent<CharBColorSelectorHidden>();
                s_SelectorLabel.SetValue(m_Selector, m_Label);

                // The window of the colours is not inside the selector: each row gets a copy of its own, over the
                // lower right corner of the "Appearance" window, with the selector's links moved onto the copy.
                var source = ((Component)s_Animator.GetValue(template)).transform;
                m_Popup = UnityEngine.Object.Instantiate(source.gameObject, window, false);
                m_Popup.name = "KingmakerCosmetics_ColourWindow";
                foreach (var field in new[] { s_Animator, s_ToggleGroup, s_ToggleItem, s_CloseButton })
                {
                    var original = field.GetValue(template) as Component;
                    if (original == null || !original.transform.IsChildOf(source))
                        continue;
                    var path = Relative(original.transform, source);
                    var found = path.Length == 0 ? m_Popup.transform : m_Popup.transform.Find(path);
                    field.SetValue(m_Selector, found == null ? null : found.GetComponent(original.GetType()));
                }
                var close = s_CloseButton.GetValue(m_Selector) as Button;
                var report = new List<string>();
                foreach (var button in m_Popup.GetComponentsInChildren<Button>(true))
                {
                    var targets = new List<string>();
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                    {
                        var target = button.onClick.GetPersistentTarget(i);
                        targets.Add((target == null ? "null" : target.GetType().Name) + "." + button.onClick.GetPersistentMethodName(i));
                    }
                    report.Add(Relative(button.transform, m_Popup.transform) + " [" + string.Join(", ", targets.ToArray()) + "]");
                    if (button == close)
                        continue;
                    // The reset button: its listener in the scene calls the original selector.
                    for (int i = 0; i < button.onClick.GetPersistentEventCount(); i++)
                        button.onClick.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);
                    var selector = m_Selector;
                    button.onClick.AddListener(() => selector.OnResetAction());
                }
                var popup = (RectTransform)m_Popup.transform;
                popup.anchorMin = popup.anchorMax = new Vector2(1f, 0f);
                // Over the right edge, its lower edge a little above the window's, clear of the portraits below.
                popup.pivot = new Vector2(0.5f, 0f);
                popup.anchoredPosition = new Vector2(0f, 24f);
                popup.localRotation = Quaternion.identity;
                var free = m_Popup.GetComponent<LayoutElement>() ?? m_Popup.AddComponent<LayoutElement>();
                free.ignoreLayout = true;
                m_Popup.AddComponent<ClickSink>();
                m_PopupCanvas = m_Popup.GetComponent<Canvas>() ?? m_Popup.AddComponent<Canvas>();
                if (m_Popup.GetComponent<GraphicRaycaster>() == null)
                    m_Popup.AddComponent<GraphicRaycaster>();
                // The title only: the reset button keeps its own text and the game's localizer of it.
                m_Titles = m_Popup.GetComponentsInChildren<TextMeshProUGUI>(true)
                    .Where(t => t.GetComponentsInParent<Component>(true).All(c => !(c is Selectable) && !c.GetType().Name.Contains("Button"))).ToList();
                foreach (var title in m_Titles)
                    foreach (var localizer in title.GetComponents<LocalizedUIText>())
                        UnityEngine.Object.DestroyImmediate(localizer);
                m_Popup.SetActive(false);
                if (!s_Reported)
                {
                    s_Reported = true;
                    Main.Log.Log("clothes layers window (PC): colour window " + ClassClothes.PathOf(source) + ", close " + (close == null ? "none" : Relative(close.transform, m_Popup.transform))
                        + ", buttons: " + string.Join("; ", report.ToArray()) + ", titles " + m_Titles.Count + "\n" + ClassClothes.Tree(m_Popup.transform, "  "));
                }

                EventBus.Subscribe(this);
                row.AddComponent<Unsubscriber>().Subscriber = this;
            }

            static bool s_Reported;

            static string Relative(Transform t, Transform root)
            {
                var names = new List<string>();
                for (; t != null && t != root; t = t.parent)
                    names.Insert(0, t.name);
                return string.Join("/", names.ToArray());
            }

            internal void Hide()
            {
                if (m_Popup != null)
                    m_Popup.SetActive(false);
            }

            internal void Bind(TextureSequentialSelectorVm model, string label, int reset)
            {
                Hide();
                m_Model = model;
                Row.SetActive(model != null);
                if (model == null)
                    return;
                m_Reset = Mathf.Clamp(reset, 0, Math.Max(0, model.TotalCount - 1));
                m_Popup.SetActive(false);
                var root = Row.GetComponentInParent<Canvas>();
                m_PopupCanvas.overrideSorting = true;
                m_PopupCanvas.sortingOrder = (root != null ? root.rootCanvas.sortingOrder : 0) + 50;
                var textures = ((List<TextureSequentialEntity>)s_Values.GetValue(model)).Select(e => e.Texture).ToList();
                m_Selector.gameObject.SetActive(true);
                s_Ignore.SetValue(m_Selector, true);
                try
                {
                    m_Selector.SetData(textures, UIStrings.Instance.CharGen.SecondaryClothColor);
                    m_Selector.CurrentElementIndex = model.CurrentIndex.Value;
                    m_Selector.TryApplyCurrentElement();
                }
                finally
                {
                    s_Ignore.SetValue(m_Selector, false);
                }
                m_Label.text = label;
                foreach (var title in m_Titles)
                    title.text = label;
            }

            public void HandleChooseColorElement(CharBColorSelector selector, int index)
            {
                if (m_Selector != null && selector == m_Selector && m_Model != null)
                    m_Model.SetCurrentIndex(index);
            }

            public void HandleClothColorReset(CharBColorSelector selector)
            {
                if (m_Selector == null || selector != m_Selector || m_Model == null)
                    return;
                m_Selector.CurrentElementIndex = m_Reset;
                m_Model.SetCurrentIndex(m_Reset);
            }
        }

        class Unsubscriber : MonoBehaviour
        {
            internal object Subscriber;

            void OnDestroy()
            {
                if (Subscriber != null)
                    EventBus.Unsubscribe(Subscriber);
            }
        }

        // Keeps the clicks on the window of the colours from reaching the circle under it.
        class ClickSink : MonoBehaviour, UnityEngine.EventSystems.IPointerClickHandler
        {
            public void OnPointerClick(UnityEngine.EventSystems.PointerEventData data)
            {
            }
        }

        // Closes the open window on a click anywhere out of it and out of its gear button.
        class OutsideClickCloser : MonoBehaviour
        {
            internal CharacterVisualSettingsView View;
            static readonly List<UnityEngine.EventSystems.RaycastResult> s_Hits = new List<UnityEngine.EventSystems.RaycastResult>();

            void Update()
            {
                if (View == null || !(bool)s_Show.GetValue(View) || !(Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1)))
                    return;
                var events = UnityEngine.EventSystems.EventSystem.current;
                var window = s_Window.GetValue(View) as Component;
                var gear = s_SettingsBtn.GetValue(View) as Component;
                if (events != null)
                {
                    s_Hits.Clear();
                    events.RaycastAll(new UnityEngine.EventSystems.PointerEventData(events) { position = Input.mousePosition }, s_Hits);
                    var hit = s_Hits.Count == 0 ? null : s_Hits[0].gameObject;
                    if (hit != null && (window != null && hit.transform.IsChildOf(window.transform) || gear != null && hit.transform.IsChildOf(gear.transform)))
                        return;
                }
                s_Toggle.Invoke(View, null);
            }
        }
    }

    // The keyboard and mouse inventory covers the doll of a pet with a picture, though the doll room builds the pet
    // (a copy of its view, as for any unit without a character avatar). The picture stays for a polymorphed character.
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.SetCharacter))]
    static class PetDoll
    {
        static readonly FieldInfo s_Simple = AccessTools.Field(typeof(DollRoom), "m_SimpleAvatar");
        static readonly FieldInfo s_Camera = AccessTools.Field(typeof(DollRoom), "m_Camera");

        static void Postfix(Inventory __instance)
        {
            try
            {
                var unit = Kingmaker.UI.Group.GroupController.Instance?.GetCurrentCharacter();
                if (unit == null || !unit.Descriptor.IsPet || unit.Body.IsPolymorphed || __instance.Placeholder == null)
                    return;
                __instance.Placeholder.gameObject.SetActive(false);
                var room = Game.Instance?.UI?.Common?.DollRoom;
                var simple = room == null ? null : s_Simple.GetValue(room) as Component;
                Main.Log.Log("pet doll: " + unit.CharacterName + ", picture off, " + Report(room));
                if (simple != null)
                {
                    // The copy is made of the pet's view as it is in the world: switched off while the pet is not
                    // on the map (on the global map, in the capital). It is switched on and animated as the game
                    // does with a copy that comes out on.
                    if (!simple.gameObject.activeSelf)
                    {
                        simple.gameObject.SetActive(true);
                        var animation = simple.GetComponentInChildren<Kingmaker.Visual.Animation.Kingmaker.UnitAnimationManager>();
                        if (animation != null)
                        {
                            if (animation.PlayableGraph.IsValid())
                                animation.PlayableGraph.SetTimeUpdateMode(UnityEngine.Playables.DirectorUpdateMode.UnscaledGameTime);
                            animation.IsInCombat = true;
                            animation.Tick();
                        }
                        Main.Log.Log("pet doll: the copy switched on, animation " + (animation == null ? "none" : animation.PlayableGraph.IsValid().ToString()));
                    }
                    simple.gameObject.AddComponent<LateReport>().Room = room;
                }
            }
            catch (Exception ex)
            {
                Main.Log.Error("pet doll: " + ex);
            }
        }

        internal static string Report(DollRoom room)
        {
            var simple = room == null ? null : s_Simple.GetValue(room) as Component;
            if (simple == null)
                return "no copy of the view";
            var owner = s_Camera.GetValue(room) as Component;
            var camera = owner == null ? null : owner.GetComponent<Camera>() ?? owner.GetComponentInChildren<Camera>(true);
            var parts = new List<string>
            {
                $"copy {simple.name} active {simple.gameObject.activeInHierarchy} layer {simple.gameObject.layer} at {simple.transform.position} scale {simple.transform.lossyScale}",
                camera == null ? "no camera" : $"camera {camera.name} enabled {camera.isActiveAndEnabled} mask {camera.cullingMask:X} at {camera.transform.position} looks {camera.transform.forward} near {camera.nearClipPlane} far {camera.farClipPlane}",
            };
            foreach (var r in simple.GetComponentsInChildren<Renderer>(true))
            {
                var view = camera == null ? Vector3.zero : camera.WorldToViewportPoint(r.bounds.center);
                parts.Add($"{r.GetType().Name} {r.name} enabled {r.enabled} active {r.gameObject.activeInHierarchy} layer {r.gameObject.layer} bounds {r.bounds.center} {r.bounds.size} view {view} "
                    + $"materials {string.Join(",", r.sharedMaterials.Select(m => m == null ? "null" : m.name + "/" + (m.shader == null ? "null" : m.shader.name)).ToArray())}"
                    + (r is SkinnedMeshRenderer ? " mesh " + (((SkinnedMeshRenderer)r).sharedMesh == null ? "null" : ((SkinnedMeshRenderer)r).sharedMesh.name) + " offscreen " + ((SkinnedMeshRenderer)r).updateWhenOffscreen : ""));
            }
            return string.Join("\n  ", parts.ToArray());
        }

        class LateReport : MonoBehaviour
        {
            internal DollRoom Room;
            float m_Time;

            void Update()
            {
                m_Time += Time.unscaledDeltaTime;
                if (m_Time < 1f)
                    return;
                Main.Log.Log("pet doll a second later: " + Report(Room));
                Destroy(this);
            }
        }
    }
}
