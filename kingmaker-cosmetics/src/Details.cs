using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.LevelUp;
using Kingmaker.UI._ConsoleUI.CharGen.Phases.Appearance;
using Kingmaker.UI._ConsoleUI.CharGen.Phases.Common;
using Kingmaker.UI._ConsoleUI.Utils.MultiNavigationTool;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.Visual.CharacterSystem;
using TMPro;
using UnityEngine;

namespace KingmakerCosmetics
{
    // A third colour of the character, "Detail color", after the two of the clothes: the colour of small parts of
    // the mod's styles (the threads in the human braids). Entities with details get these colours as their second
    // colour list; the doll's choice is put on them as their second colour and saved with them
    // (DollData.EntitySecondaryRampIdices), so the game colours them when it loads the character.
    // The row is a copy of the second clothes colour row in both interfaces.
    static class Details
    {
        // Colours at the middle of the shade (the ramp's x / Shade, as the braids were shown on the stand).
        const float Shade = 0.55f;
        static readonly Color32[] s_Colours =
        {
            new Color32(190, 30, 34, 255),   // red
            new Color32(120, 18, 30, 255),   // crimson
            new Color32(200, 90, 24, 255),   // orange
            new Color32(200, 160, 50, 255),  // gold
            new Color32(60, 120, 50, 255),   // green
            new Color32(30, 110, 100, 255),  // teal
            new Color32(40, 70, 160, 255),   // blue
            new Color32(28, 34, 80, 255),    // navy
            new Color32(110, 50, 140, 255),  // violet
            new Color32(190, 70, 120, 255),  // pink
            new Color32(220, 214, 200, 255), // white
            new Color32(130, 132, 140, 255), // silver
            new Color32(110, 70, 40, 255),   // leather brown
            new Color32(24, 24, 28, 255),    // black
        };

        internal static readonly List<Texture2D> Ramps = new List<Texture2D>();
        // Entities coloured by the details.
        internal static readonly HashSet<EquipmentEntity> Entities = new HashSet<EquipmentEntity>();
        internal static readonly HashSet<string> Ids = new HashSet<string>();

        class Choice { internal int Index; }
        static readonly ConditionalWeakTable<DollState, Choice> s_Choices = new ConditionalWeakTable<DollState, Choice>();
        static readonly MethodInfo s_Updated = AccessTools.Method(typeof(DollState), "Updated");

        internal static List<Texture2D> Palette()
        {
            if (Ramps.Count == 0)
                foreach (var colour in s_Colours)
                {
                    var ramp = new Texture2D(256, 1, TextureFormat.RGBA32, false) { name = "KingmakerCosmetics_Detail", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
                    var line = new Color32[256];
                    for (int x = 0; x < 256; x++)
                    {
                        float k = Mathf.Pow(x / 255f, 0.9f) / Shade;
                        line[x] = new Color32((byte)Mathf.Clamp(colour.r * k, 0, 255), (byte)Mathf.Clamp(colour.g * k, 0, 255), (byte)Mathf.Clamp(colour.b * k, 0, 255), 255);
                    }
                    ramp.SetPixels32(line);
                    ramp.Apply(false, false);
                    Ramps.Add(ramp);
                }
            return Ramps;
        }

        internal static void Add(string id, EquipmentEntity ee)
        {
            Palette();
            Ids.Add(id);
            Entities.Add(ee);
        }

        internal static int Index(DollState doll)
        {
            return doll == null ? 0 : s_Choices.GetOrCreateValue(doll).Index;
        }

        internal static void Set(DollState doll, int index)
        {
            if (doll == null || index < 0 || index >= Ramps.Count)
                return;
            s_Choices.GetOrCreateValue(doll).Index = index;
            s_Updated.Invoke(doll, null);
        }

        // en, ru, de, fr, it, es, zh
        static readonly string[] LabelTexts = { "Detail color", "Цвет деталей", "Detailfarbe", "Couleur des détails", "Colore dei dettagli", "Color de los detalles", "细节色" };

        internal static string Label
        {
            get
            {
                switch (LocalizationManager.CurrentLocale)
                {
                    case Locale.ruRU: return LabelTexts[1];
                    case Locale.deDE: return LabelTexts[2];
                    case Locale.frFR: return LabelTexts[3];
                    case Locale.itIT: return LabelTexts[4];
                    case Locale.esES: return LabelTexts[5];
                    case Locale.zhCN: return LabelTexts[6];
                    default: return LabelTexts[0];
                }
            }
        }
    }

    // The character screen's doll: entities with details take the doll's detail colour.
    [HarmonyPatch(typeof(DollState), nameof(DollState.ApplyRamps))]
    static class DetailsOnDoll
    {
        static void Postfix(DollState __instance, Character character)
        {
            if (character == null || Details.Entities.Count == 0)
                return;
            int index = Details.Index(__instance);
            foreach (var ee in character.EquipmentEntities.Where(e => e != null && Details.Entities.Contains(e)).ToList())
                character.SetSecondaryRampIndex(ee, index);
        }
    }

    // Saved with the character: the game puts it on when it loads her.
    [HarmonyPatch(typeof(DollState), nameof(DollState.CreateData))]
    static class DetailsInSave
    {
        static void Postfix(DollState __instance, DollData __result)
        {
            int index = Details.Index(__instance);
            foreach (var id in __result.EquipmentEntityIds.Where(Details.Ids.Contains))
                __result.EntitySecondaryRampIdices[id] = index;
        }
    }

    // The PC interface: a copy of the second clothes colour row under it.
    [HarmonyPatch(typeof(CharBClothColorSelector), nameof(CharBClothColorSelector.FillData))]
    static class DetailsRowPC
    {
        static readonly FieldInfo s_Secondary = AccessTools.Field(typeof(CharBClothColorSelector), "m_ClothSecondaryColor");
        static readonly FieldInfo s_Label = AccessTools.Field(typeof(CharBColorSelector), "m_Label");
        static readonly Dictionary<CharBClothColorSelector, CharBColorSelector> s_Rows = new Dictionary<CharBClothColorSelector, CharBColorSelector>();
        static Listener s_Listener;

        static DollState Doll => Kingmaker.Game.Instance?.UI?.CharacterBuildController?.LevelUpController?.Doll;

        class Listener : IColorSelectorChanged, IClothColorReset
        {
            public void HandleChooseColorElement(CharBColorSelector selector, int index)
            {
                if (s_Rows.ContainsValue(selector))
                    Details.Set(Doll, index);
            }

            public void HandleClothColorReset(CharBColorSelector selector)
            {
                if (!s_Rows.ContainsValue(selector))
                    return;
                selector.CurrentElementIndex = 0;
                Details.Set(Doll, 0);
            }
        }

        static void Postfix(CharBClothColorSelector __instance)
        {
            try
            {
                if (s_Listener == null)
                {
                    s_Listener = new Listener();
                    EventBus.Subscribe(s_Listener);
                }
                foreach (var gone in s_Rows.Keys.Where(k => k == null).ToList())
                    s_Rows.Remove(gone);
                CharBColorSelector row;
                if (!s_Rows.TryGetValue(__instance, out row) || row == null)
                {
                    var secondary = (CharBColorSelector)s_Secondary.GetValue(__instance);
                    if (secondary == null)
                        return;
                    var copy = UnityEngine.Object.Instantiate(secondary.gameObject, secondary.transform.parent, false);
                    copy.name = "KingmakerCosmetics_DetailColor";
                    copy.transform.SetSiblingIndex(secondary.transform.GetSiblingIndex() + 1);
                    row = copy.GetComponent<CharBColorSelector>();
                    s_Rows[__instance] = row;
                    Main.Log.Log($"detail colours row (PC) under {secondary.name} in {secondary.transform.parent?.name}");
                }
                row.SetData(Details.Palette(), Kingmaker.Blueprints.Root.Strings.UIStrings.Instance.CharGen.SecondaryClothColor);
                ((TextMeshProUGUI)s_Label.GetValue(row)).text = Details.Label;
                row.CurrentElementIndex = Details.Index(Doll);
                row.TryApplyCurrentElement();
            }
            catch (Exception ex)
            {
                Main.Log.Error("detail colours row (PC): " + ex);
            }
        }
    }

    // The console interface: a copy of the second clothes colour row and its header, last in the cursor's order.
    [HarmonyPatch(typeof(CharGenAppearancePhaseDetailedView), nameof(CharGenAppearancePhaseDetailedView.Bind))]
    static class DetailsRowConsole
    {
        static readonly FieldInfo s_Row = AccessTools.Field(typeof(CharGenAppearancePhaseDetailedView), "m_SecondaryOutfitColorSelectorView");
        static readonly FieldInfo s_Header = AccessTools.Field(typeof(CharGenAppearancePhaseDetailedView), "m_SecondaryOutfitHeader");
        static readonly FieldInfo s_Navigation = AccessTools.Field(typeof(CharGenAppearancePhaseDetailedView), "m_NavigationCollection");
        static readonly PropertyInfo s_Controller = AccessTools.Property(typeof(CharGenAppearancePhaseVM), "LevelUpController");
        static readonly Dictionary<CharGenAppearancePhaseDetailedView, TextureSequentialSelectorView> s_Rows = new Dictionary<CharGenAppearancePhaseDetailedView, TextureSequentialSelectorView>();

        static void Postfix(CharGenAppearancePhaseDetailedView __instance, CharGenAppearancePhaseVM viewModel)
        {
            try
            {
                var secondary = (TextureSequentialSelectorView)s_Row.GetValue(__instance);
                var header = (TextMeshProUGUI)s_Header.GetValue(__instance);
                var controller = (LevelUpController)s_Controller.GetValue(viewModel, null);
                var doll = controller?.Doll;
                if (secondary == null || doll == null)
                    return;
                foreach (var gone in s_Rows.Keys.Where(k => k == null).ToList())
                    s_Rows.Remove(gone);
                TextureSequentialSelectorView row;
                if (!s_Rows.TryGetValue(__instance, out row) || row == null)
                {
                    // The header (its golden bar) and the row lie in a group of their own: copy that group, the
                    // lowest common ancestor of the two, if it holds no other selector; else the row alone.
                    Transform group = null;
                    if (header != null)
                    {
                        var ancestors = new HashSet<Transform>();
                        for (var t = secondary.transform.parent; t != null; t = t.parent)
                            ancestors.Add(t);
                        for (var t = header.transform.parent; t != null && group == null; t = t.parent)
                            if (ancestors.Contains(t))
                                group = t;
                        if (group != null && (group.parent == null || group.GetComponentsInChildren<TextureSequentialSelectorView>(true).Length != 1))
                            group = null;
                    }
                    if (group != null)
                    {
                        var clone = UnityEngine.Object.Instantiate(group.gameObject, group.parent, false);
                        clone.name = "KingmakerCosmetics_DetailColorGroup";
                        clone.transform.SetSiblingIndex(group.GetSiblingIndex() + 1);
                        row = clone.GetComponentInChildren<TextureSequentialSelectorView>(true);
                        var texts = clone.GetComponentsInChildren<TextMeshProUGUI>(true);
                        var text = texts.FirstOrDefault(t => t.name == header.name && t.text == header.text) ?? texts.FirstOrDefault(t => t.name == header.name);
                        if (text != null)
                            text.text = Details.Label;
                        Main.Log.Log($"detail colours row (console): copy of group {group.name} in {group.parent.name}, header text {(text == null ? "not found" : "set")}");
                    }
                    else
                    {
                        var copy = UnityEngine.Object.Instantiate(secondary.gameObject, secondary.transform.parent, false);
                        copy.name = "KingmakerCosmetics_DetailColor";
                        copy.transform.SetSiblingIndex(secondary.transform.GetSiblingIndex() + 1);
                        row = copy.GetComponent<TextureSequentialSelectorView>();
                        var path = new List<string>();
                        for (var t = header == null ? null : header.transform; t != null; t = t.parent)
                            path.Add(t.name);
                        Main.Log.Log($"detail colours row (console): row alone under {secondary.name}, no group; header path {string.Join(" < ", path.ToArray())}");
                    }
                    s_Rows[__instance] = row;
                }
                var ramps = Details.Palette();
                var entries = new List<TextureSequentialEntity>();
                for (int i = 0; i < ramps.Count; i++)
                {
                    int index = i;
                    entries.Add(new TextureSequentialEntity { Texture = ramps[i], Setter = () => Details.Set(doll, index) });
                }
                int current = Mathf.Clamp(Details.Index(doll), 0, ramps.Count - 1);
                row.Bind(new TextureSequentialSelectorVm(entries, entries[current]));
                // Last in the cursor's order.
                var navigation = (ConsoleMultiNavigationCollection)s_Navigation.GetValue(__instance);
                var entities = navigation == null ? null : navigation.EntitiesList;
                if (entities != null && !entities.Contains(row))
                {
                    entities.Add(row);
                    navigation.SetEntitiesVertical(entities);
                    navigation.SelectCurrentEntity();
                }
            }
            catch (Exception ex)
            {
                Main.Log.Error("detail colours row (console): " + ex);
            }
        }
    }
}
