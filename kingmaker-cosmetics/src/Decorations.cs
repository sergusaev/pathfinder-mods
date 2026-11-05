using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.CharGen;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using Kingmaker.ResourceLinks;
using Kingmaker.UI.LevelUp;
using Kingmaker.UI._ConsoleUI.CharGen.Phases.Appearance;
using Kingmaker.UI._ConsoleUI.CharGen.Phases.Common;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Class.LevelUp;
using Kingmaker.Visual.CharacterSystem;
using TMPro;
using UnityEngine;

namespace KingmakerCosmetics
{
    // Face decorations of human women: the rows of horns, empty for humans, offer "none" and the war paint, the row of
    // horn colour a palette of paints of the mod's own. The doll and saves keep them as horns: the game puts the horn
    // and its colour into DollData by itself. "None" is an empty entity left out of DollData. The war paint mesh is cut
    // for the fourth human face: on other heads it is not put on (the console screen only refreshes the rows when race
    // or gender change, so they are not hidden by the head).
    static class Decorations
    {
        // Asset ids: they go into saves and never change.
        internal static readonly string NoneId = Styles.MadeId("EE_Head_Face04_F_HM", "face decoration none");
        internal static readonly string WarpaintId = Styles.MadeId("EE_Head_Face04_F_HM", "face decoration war paint");
        // The same paint mirrored on her left side (the head's triangles of that side cut along the same outline).
        internal static readonly string WarpaintLeftId = Styles.MadeId("EE_Head_Face04_F_HM", "face decoration war paint left");

        // Heads the war paint is cut for: the fourth human face of the game and its copy with detailed eyes.
        internal static readonly HashSet<string> Face04Ids = new HashSet<string>();

        // Paints: the grey shade of the paint texture x / Shade times the colour; the first is the portrait's woad.
        const float Shade = 0.55f;
        static readonly Color32[] s_Paints =
        {
            new Color32(88, 82, 110, 255),   // grey-violet woad
            new Color32(44, 62, 118, 255),   // blue woad
            new Color32(28, 30, 40, 255),    // soot
            new Color32(200, 196, 186, 255), // white clay
            new Color32(138, 40, 30, 255),   // red ochre
            new Color32(168, 116, 44, 255),  // yellow ochre
            new Color32(52, 82, 52, 255),    // green
            new Color32(92, 44, 108, 255),   // violet
        };

        static readonly FieldInfo s_Ramps = AccessTools.Field(typeof(CharacterTextureDescription), "m_Ramps");
        static readonly PropertyInfo s_Horn = AccessTools.Property(typeof(DollState), nameof(DollState.Horn));

        static EquipmentEntityLink[] s_Links;
        static CharacterColorsProfile s_Palette;

        // The war paint entity: a copy of the head keeping only a body part of type Glasses with the paint mesh.
        internal static void Build(string headId, EquipmentEntity head, BodyPart face, Mesh mesh)
        {
            s_Palette = ScriptableObject.CreateInstance<CharacterColorsProfile>();
            s_Palette.name = "KingmakerCosmetics_Paints";
            s_Palette.hideFlags = HideFlags.DontUnloadUnusedAsset;
            s_Palette.PrimaryRamps = s_Paints.Select(Ramp).ToList();

            var paint = SideBraid.ReadTexture("Warpaint.png", null);
            paint.wrapMode = TextureWrapMode.Clamp;
            var all = new Texture2D(4, 4, TextureFormat.RGBA32, false) { name = "KingmakerCosmetics_WholeMask", hideFlags = HideFlags.DontUnloadUnusedAsset };
            all.SetPixels32(Enumerable.Repeat(new Color32(255, 255, 255, 255), 16).ToArray());
            all.Apply(false, true);
            var links = new List<EquipmentEntityLink> { new EquipmentEntityLink { AssetId = NoneId } };
            if (Paint(WarpaintId, "_WarpaintDecoration", headId, head, face, mesh, paint, all))
                links.Add(new EquipmentEntityLink { AssetId = WarpaintId });
            var left = SideBraid.ReadMesh("WarpaintL.mesh", face.SkinnedRenderer.sharedMesh);
            if (left != null && Paint(WarpaintLeftId, "_WarpaintLeftDecoration", headId, head, face, left, paint, all))
                links.Add(new EquipmentEntityLink { AssetId = WarpaintLeftId });

            var none = ScriptableObject.CreateInstance<EquipmentEntity>();
            none.name = "KingmakerCosmetics_NoDecoration";
            none.hideFlags = HideFlags.DontUnloadUnusedAsset;
            Styles.Register(NoneId, none);

            s_Links = links.ToArray();
        }

        // A paint entity: a copy of the head keeping only a body part of type Glasses with the paint mesh, its grey
        // shade repainted by the palette (each its own texture description: the repainted texture is kept in it).
        static bool Paint(string id, string suffix, string headId, EquipmentEntity head, BodyPart face, Mesh mesh, Texture2D paint, Texture2D all)
        {
            try
            {
                var diffuse = new CharacterTextureDescription(CharacterTextureChannel.Diffuse, paint);
                s_Ramps.SetValue(diffuse, new List<RampMaskDescription> { new RampMaskDescription { Mask = all } });
                var copy = Styles.Copy(headId, head, suffix);
                copy.BodyParts = new List<BodyPart>
                {
                    new BodyPart { Type = BodyPartType.Glasses, Material = face.Material, Textures = new List<CharacterTextureDescription> { diffuse } },
                };
                copy.OutfitParts = new List<EquipmentEntity.OutfitPart>();
                copy.SkeletonModifiers = new List<Skeleton.Bone>();
                copy.HideBodyParts = 0;
                copy.ColorsProfile = s_Palette;
                Styles.ReplaceMesh(copy, 0, face, mesh);
                UnderHair.Paints.Add(copy);
                Styles.Register(id, copy);
                Main.Log.Log($"{copy.name}: face decoration, {mesh.vertexCount} vertices, {mesh.triangles.Length / 3} triangles, {s_Paints.Length} paints");
                return true;
            }
            catch (Exception ex)
            {
                Main.Log.Error($"face decoration {suffix} not made: {ex}");
                return false;
            }
        }

        // The decorations go into the horn rows of the options given, if they have no horns of their own; heads with
        // none get an empty list of the mod's (a dash in the row, no colours).
        internal static void Offer(CustomizationOptions options)
        {
            if (s_Links == null)
                return;
            if (options.Horns != null && options.Horns.Length > 0)
            {
                Main.Log.Error($"human women have horns of their own ({options.Horns.Length}), no face decorations");
                return;
            }
            s_Options = options;
            options.Horns = s_Links;
        }

        static CustomizationOptions s_Options;
        static readonly EquipmentEntityLink[] s_None = new EquipmentEntityLink[0];

        static Texture2D Ramp(Color32 paint)
        {
            var ramp = new Texture2D(256, 1, TextureFormat.RGBA32, false) { name = "KingmakerCosmetics_Paint", wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            var line = new Color32[256];
            for (int x = 0; x < 256; x++)
            {
                float k = x / 255f / Shade;
                line[x] = new Color32((byte)Mathf.Clamp(paint.r * k, 0, 255), (byte)Mathf.Clamp(paint.g * k, 0, 255), (byte)Mathf.Clamp(paint.b * k, 0, 255), 255);
            }
            ramp.SetPixels32(line);
            ramp.Apply(false, false);
            return ramp;
        }

        internal static bool Ours(EquipmentEntityLink[] horns)
        {
            return horns != null && s_Links != null && (horns == s_Links || horns == s_None);
        }

        internal static bool Ours(DollState doll)
        {
            if (doll == null || doll.Race == null)
                return false;
            return Ours(doll.Gender == Gender.Male ? doll.Race.MaleOptions.Horns : doll.Race.FemaleOptions.Horns);
        }

        // Whether the decoration of the doll is put on: not "none", and the war paint only on the heads it is cut for.
        internal static bool Worn(string decoration, string head)
        {
            return decoration != NoneId && (decoration != WarpaintId && decoration != WarpaintLeftId || head != null && Face04Ids.Contains(head));
        }

        static string s_Logged;

        // After the game validated the doll: the decorations of its head, one of them on it, a paint for it.
        internal static void Validate(DollState doll)
        {
            if (s_Options == null || doll.Race == null || doll.Gender != Gender.Female || doll.Race.FemaleOptions != s_Options)
                return;
            var offered = doll.Head != null && Face04Ids.Contains(doll.Head.AssetId) ? s_Links : s_None;
            s_Options.Horns = offered;
            if (!offered.Contains(doll.Horn))
                s_Horn.SetValue(doll, offered.FirstOrDefault(), null);
            var ramps = doll.GetHornsRamps();
            if (ramps.Count > 0 && (doll.HornsRampIndex < 0 || doll.HornsRampIndex >= ramps.Count))
                doll.HornsRampIndex = 0;
            string state = $"head {doll.Head?.AssetId}, decorations {offered.Length}, on {doll.Horn?.AssetId ?? "none"}, paints {ramps.Count}, paint {doll.HornsRampIndex}";
            if (state != s_Logged)
            {
                s_Logged = state;
                Main.Log.Log("face decoration: " + state);
            }
        }

        // Shown names: a dash when the head has none, the decorations numbered as the game numbers faces and hair.
        internal static List<string> Names(EquipmentEntityLink[] horns)
        {
            var names = new List<string>();
            int number = 0;
            foreach (var horn in horns)
                names.Add(horn.AssetId == NoneId ? Text(None) : Text(Woad) + " " + ++number);
            return names;
        }

        static string Text(string[] texts)
        {
            switch (LocalizationManager.CurrentLocale)
            {
                case Locale.ruRU: return texts[1];
                case Locale.deDE: return texts[2];
                case Locale.frFR: return texts[3];
                case Locale.itIT: return texts[4];
                case Locale.esES: return texts[5];
                case Locale.zhCN: return texts[6];
                default: return texts[0];
            }
        }

        // en, ru, de, fr, it, es, zh
        static readonly string[] LabelTexts = { "Face decoration", "Украшения лица", "Gesichtsschmuck", "Ornements du visage", "Ornamenti del viso", "Adornos faciales", "面部装饰" };
        static readonly string[] ColorTexts = { "Decoration colour", "Цвет украшения", "Farbe des Schmucks", "Couleur de l'ornement", "Colore dell'ornamento", "Color del adorno", "装饰颜色" };
        static readonly string[] None = { "None", "Нет", "Keiner", "Aucun", "Nessuno", "Ninguno", "无" };
        static readonly string[] Woad = { "Woad", "Вайда", "Waid", "Guède", "Guado", "Glasto", "菘蓝" };
        internal static string Label => Text(LabelTexts);
        internal static string ColorLabel => Text(ColorTexts);
    }

    // The game validates the doll after every change: the decorations follow the head.
    [HarmonyPatch(typeof(DollState), nameof(DollState.Validate))]
    static class DecorationValidate
    {
        static void Postfix(DollState __instance)
        {
            Decorations.Validate(__instance);
        }
    }

    [HarmonyPatch(typeof(DollState), nameof(DollState.CollectEntitiesLinks))]
    static class DecorationOnDoll
    {
        static void Postfix(DollState __instance, List<EquipmentEntityLink> __result)
        {
            var horn = __instance.Horn;
            if (horn != null && Decorations.Ours(__instance) && !Decorations.Worn(horn.AssetId, __instance.Head?.AssetId))
                __result.Remove(horn);
        }
    }

    [HarmonyPatch(typeof(DollState), nameof(DollState.CreateData))]
    static class DecorationInSave
    {
        static void Postfix(DollState __instance, DollData __result)
        {
            var horn = __instance.Horn;
            if (horn == null || !Decorations.Ours(__instance) || Decorations.Worn(horn.AssetId, __instance.Head?.AssetId))
                return;
            __result.EquipmentEntityIds.Remove(horn.AssetId);
            __result.EntityRampIdices.Remove(horn.AssetId);
            __result.EntitySecondaryRampIdices.Remove(horn.AssetId);
        }
    }

    // The PC interface: the horn rows named as decorations, their choices by name, a dash and no colours when the
    // head has none (the block is shown then as the beard row of women is).
    [HarmonyPatch(typeof(CharBDollStyler), "InitSelector")]
    static class DecorationNamesPC
    {
        static void Postfix(CharBSequentialSelector selector, EquipmentEntityLink[] collection)
        {
            if (Decorations.Ours(collection))
                selector.Init(Decorations.Names(collection), Decorations.Label);
        }
    }

    [HarmonyPatch]
    static class DecorationRowsPC
    {
        static readonly FieldInfo s_NewState = AccessTools.Field(typeof(CharBDollStyler), "m_NewState");
        static readonly FieldInfo s_Colour = AccessTools.Field(typeof(CharBDollStyler), "m_HornsColor");
        static readonly FieldInfo s_Label = AccessTools.Field(typeof(CharBColorSelector), "m_Label");

        static DollState Doll => Kingmaker.Game.Instance?.UI?.CharacterBuildController?.LevelUpController?.Doll;

        [HarmonyPatch(typeof(CharBDollStyler), "GetNewState")]
        [HarmonyPostfix]
        static void State(CharBDollStyler __instance, DollState doll)
        {
            if (!Decorations.Ours(doll))
                return;
            // StylerVisibleState is internal: set through reflection and put back.
            object state = s_NewState.GetValue(__instance);
            if (state == null)
                return;
            AccessTools.Field(state.GetType(), "Horns").SetValue(state, true);
            s_NewState.SetValue(__instance, state);
        }

        [HarmonyPatch(typeof(CharBDollStyler), "FillSelectorsData")]
        [HarmonyPostfix]
        static void Filled(CharBDollStyler __instance)
        {
            Refresh(__instance);
        }

        // The game rebuilds the rows only when race or gender change: a new head or decoration rebuilds them here.
        [HarmonyPatch(typeof(CharBDollStyler), nameof(CharBDollStyler.HandleChooseElement))]
        [HarmonyPostfix]
        static void Chosen(CharBDollStyler __instance, CharBSequentialSelector selector)
        {
            if (selector == s_Face.GetValue(__instance) || selector == s_Horns.GetValue(__instance))
                Refresh(__instance);
        }

        static readonly FieldInfo s_Face = AccessTools.Field(typeof(CharBDollStyler), "m_Face");
        static readonly FieldInfo s_Horns = AccessTools.Field(typeof(CharBDollStyler), "m_Horns");
        static readonly PropertyInfo s_Index = AccessTools.Property(typeof(CharBSequentialSelector), nameof(CharBSequentialSelector.CurrentElementIndex));

        static void Refresh(CharBDollStyler styler)
        {
            try
            {
                var colour = (CharBColorSelector)s_Colour.GetValue(styler);
                var horns = (CharBSequentialSelector)s_Horns.GetValue(styler);
                if (colour == null || horns == null)
                    return;
                var doll = Doll;
                if (!Decorations.Ours(doll))
                {
                    // Shown again for horns of other races.
                    colour.gameObject.SetActive(true);
                    return;
                }
                var offered = doll.Race.FemaleOptions.Horns;
                // No decorations: a dash with no arrows and no counter, as the beard row of women.
                horns.Init(Decorations.Names(offered), Decorations.Label);
                if (offered.Length > 0)
                {
                    s_Index.SetValue(horns, Math.Max(0, Array.IndexOf(offered, doll.Horn)), null);
                    horns.FillData();
                }
                var ramps = doll.GetHornsRamps();
                colour.SetData(ramps, Kingmaker.Blueprints.Root.Strings.UIStrings.Instance.CharGen.HornsColor);
                ((TextMeshProUGUI)s_Label.GetValue(colour)).text = Decorations.ColorLabel;
                colour.gameObject.SetActive(ramps.Count > 0);
                if (ramps.Count > 0)
                {
                    colour.CurrentElementIndex = Mathf.Clamp(doll.HornsRampIndex, 0, ramps.Count - 1);
                    colour.TryApplyCurrentElement();
                }
            }
            catch (Exception ex)
            {
                Main.Log.Error("face decoration rows: " + ex);
            }
        }
    }

    // The console interface: the horn block's header and choices, a dash when the head has none. The game refreshes
    // the rows only when race or gender change: the decoration rows are refreshed here when the head or the decoration
    // change too. The horn colour row there marks the hair colour index as the current one; the decoration's is marked.
    [HarmonyPatch(typeof(CharGenAppearancePhaseVM), "GetHornList")]
    static class DecorationNamesConsole
    {
        static void Postfix(LevelUpController levelUpController, List<StringSequentialEntity> __result, ref StringSequentialEntity current)
        {
            var doll = levelUpController?.Doll;
            if (!Decorations.Ours(doll))
                return;
            var names = Decorations.Names(doll.Race.FemaleOptions.Horns);
            for (int i = 0; i < __result.Count && i < names.Count; i++)
                __result[i].Title = names[i];
        }
    }

    [HarmonyPatch(typeof(CharGenAppearancePhaseVM), "GetHornColorList")]
    static class DecorationColourConsole
    {
        static void Postfix(LevelUpController levelUpController, List<TextureSequentialEntity> __result, ref TextureSequentialEntity current)
        {
            var doll = levelUpController?.Doll;
            if (!Decorations.Ours(doll))
                return;
            int i = doll.HornsRampIndex;
            current = i >= 0 && i < __result.Count ? __result[i] : __result.FirstOrDefault();
        }
    }

    [HarmonyPatch]
    static class DecorationRowsConsole
    {
        static readonly FieldInfo s_Header = AccessTools.Field(typeof(CharGenAppearancePhaseDetailedView), "m_HornHeader");
        static readonly FieldInfo s_Block = AccessTools.Field(typeof(CharGenAppearancePhaseDetailedView), "m_HornBlock");
        static readonly PropertyInfo s_Controller = AccessTools.Property(typeof(CharGenAppearancePhaseVM), "LevelUpController");
        static readonly FieldInfo s_Row = AccessTools.Field(typeof(CharGenAppearancePhaseDetailedView), "m_HornSelectorView");
        static readonly FieldInfo s_Value = AccessTools.Field(typeof(SequentialSelectorView), "m_CurrentValue");
        static readonly FieldInfo s_Counter = AccessTools.Field(typeof(SequentialSelectorView), "m_Counter");
        static readonly FieldInfo s_Next = AccessTools.Field(typeof(SequentialSelectorView), "m_NextArrow");
        static readonly FieldInfo s_Prev = AccessTools.Field(typeof(SequentialSelectorView), "m_PrevArrow");
        static readonly MethodInfo s_HornList = AccessTools.Method(typeof(CharGenAppearancePhaseVM), "GetHornList");
        static readonly MethodInfo s_ColourList = AccessTools.Method(typeof(CharGenAppearancePhaseVM), "GetHornColorList");
        static CharGenAppearancePhaseDetailedView s_View;
        static CharGenAppearancePhaseVM s_Model;
        static bool s_Refreshing;

        [HarmonyPatch(typeof(CharGenAppearancePhaseDetailedView), nameof(CharGenAppearancePhaseDetailedView.Bind))]
        [HarmonyPostfix]
        static void Bound(CharGenAppearancePhaseDetailedView __instance, CharGenAppearancePhaseVM viewModel)
        {
            s_View = __instance;
            s_Model = viewModel;
            Refresh(false);
        }

        [HarmonyPatch(typeof(CharGenAppearancePhaseVM), "Change")]
        [HarmonyPostfix]
        static void Changed(CharGenAppearancePhaseVM __instance)
        {
            if (__instance == s_Model)
                Refresh(false);
        }

        [HarmonyPatch(typeof(DollState), nameof(DollState.SetHead))]
        [HarmonyPostfix]
        static void HeadSet(DollState __instance)
        {
            if (Decorations.Ours(__instance))
                Refresh(true);
        }

        [HarmonyPatch(typeof(DollState), nameof(DollState.SetHorn))]
        [HarmonyPostfix]
        static void HornSet(DollState __instance)
        {
            if (Decorations.Ours(__instance))
                Refresh(true);
        }

        static void Refresh(bool lists)
        {
            if (s_View == null || s_Model == null || s_Refreshing)
                return;
            s_Refreshing = true;
            try
            {
                var controller = (LevelUpController)s_Controller.GetValue(s_Model, null);
                if (controller?.Doll == null)
                    return;
                // Setting the rows calls the setter of the current entry, that is SetHorn or SetHornsColor again.
                if (lists)
                {
                    var args = new object[] { controller, null };
                    var horns = (List<StringSequentialEntity>)s_HornList.Invoke(s_Model, args);
                    s_Model.HornSelectorVm.SetValues(horns, (StringSequentialEntity)args[1]);
                    args = new object[] { controller, null };
                    var colours = (List<TextureSequentialEntity>)s_ColourList.Invoke(s_Model, args);
                    s_Model.HornColorSelectorVm.SetValues(colours, (TextureSequentialEntity)args[1]);
                }
                // The game shows the block or not only when bound: a race chosen later with horns or decorations has it too.
                bool ours = Decorations.Ours(controller.Doll), any = s_Model.HornSelectorVm.IsValid();
                var block = (GameObject)s_Block.GetValue(s_View);
                if (block != null)
                    block.SetActive(any || ours);
                // No decorations: the row stays with a dash, no counter, no arrows, and is passed over by the cursor
                // (an empty row is not valid for navigation).
                var row = (SequentialSelectorView)s_Row.GetValue(s_View);
                if (row != null && ours && !any)
                {
                    row.gameObject.SetActive(true);
                    ((TextMeshProUGUI)s_Value.GetValue(row)).text = "—";
                    ((TextMeshProUGUI)s_Counter.GetValue(row)).text = "";
                    ((GameObject)s_Next.GetValue(row)).SetActive(false);
                    ((GameObject)s_Prev.GetValue(row)).SetActive(false);
                }
                var header = (TextMeshProUGUI)s_Header.GetValue(s_View);
                if (header != null)
                    header.text = Decorations.Ours(controller.Doll) ? Decorations.Label : (string)Kingmaker.Blueprints.Root.Strings.UIStrings.Instance.CharGen.Horns;
            }
            catch (Exception ex)
            {
                Main.Log.Error("face decoration rows: " + ex);
            }
            finally
            {
                s_Refreshing = false;
            }
        }
    }
}
