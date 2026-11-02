using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.Blueprints;
using Kingmaker.UI._ConsoleUI;
using Kingmaker.UI._ConsoleUI.CharGen.Phases;
using Kingmaker.UI._ConsoleUI.CharGen.Phases.Portrait;
using Kingmaker.UI._ConsoleUI.Utils.MultiNavigationTool;
using Kingmaker.UnitLogic.Class.LevelUp;
using UnityEngine;

namespace PortraitGallery
{
    // Gamepad interface. The portrait step is one virtual list (VirtualListController, 4 cells in a row, one pooled
    // cell prefab). The gallery puts category bars into the same list: a bar is a view model of its own that takes
    // a whole row and cannot be chosen; its cell is a portrait cell turned into a bar (PadCell). A on a bar
    // collapses or opens the category; the portraits of a collapsed category are hidden from the list.

    class HeaderVM : CharGenPortraitSelectorItemVM
    {
        public readonly Category Category;
        public readonly int Count;

        public HeaderVM(Category category, int count) : base(null)
        {
            Category = category;
            Count = count;
            SetForbidenState(true);
        }
    }

    class GalleryItemVM : CharGenPortraitSelectorItemVM
    {
        public readonly Category Category;

        public GalleryItemVM(BlueprintPortrait portrait, Category category) : base(portrait) { Category = category; }

        public override bool IsHidden { get { return Gallery.IsCollapsed(Category); } set { } }
    }

    // The list of the step: built after the game's (and after Kingmaker Fixes, which appends custom portraits).
    [HarmonyPatch(typeof(CharGenPortraitPhaseVM), MethodType.Constructor, new Type[] { typeof(LevelUpController) })]
    [HarmonyPriority(Priority.Last)]
    static class PadList
    {
        static readonly Dictionary<string, BlueprintPortrait> Custom = new Dictionary<string, BlueprintPortrait>();

        // A custom portrait as a blueprint with custom Data: UnitUISettings.SetPortrait keeps it as the unit's custom
        // portrait, saves stay as the game writes them.
        static BlueprintPortrait CustomBlueprint(string id)
        {
            BlueprintPortrait bp;
            if (!Custom.TryGetValue(id, out bp) || bp == null)
            {
                bp = ScriptableObject.CreateInstance<BlueprintPortrait>();
                bp.name = "CustomPortrait_" + id;
                bp.Data = Gallery.Data(id);
                Custom[id] = bp;
            }
            return bp;
        }

        static void Postfix(CharGenPortraitPhaseVM __instance, LevelUpController levelUpController)
        {
            Main.Safe("gamepad list", () =>
            {
                var list = __instance.SelectorGroupVM.VisibleCollection;
                var entries = Gallery.Build(levelUpController);
                list.Clear();
                var counts = new int[Gallery.Count];
                foreach (var e in entries) counts[(int)e.Category]++;
                Category? current = null;
                foreach (var e in entries)
                {
                    if (current != e.Category)
                    {
                        current = e.Category;
                        list.Add(new HeaderVM(e.Category, counts[(int)e.Category]));
                    }
                    list.Add(new GalleryItemVM(e.Blueprint ?? CustomBlueprint(e.CustomId), e.Category));
                }
            });
        }
    }

    // Positions in the list: a bar takes the whole width, every category starts on a new row.
    [HarmonyPatch(typeof(VirtualListController), "UpdateContentSize")]
    static class PadLayout
    {
        const float BarHeight = 44f;
        static readonly System.Reflection.FieldInfo OwnerField = AccessTools.Field(typeof(VirtualListController), "m_ViewListOwner");
        static object Owner(VirtualListController list) { return OwnerField.GetValue(list); }
        static object s_Done;

        static void Prefix(VirtualListController __instance)
        {
            var owner = Owner(__instance) as CharGenPortraitSelectorView;
            if (owner == null) return;
            var data = owner.GetDataList;
            if (data == null || ReferenceEquals(data, s_Done)) return;
            s_Done = data;
            Main.Safe("gamepad layout", () => Layout(data, __instance));
        }

        static void Layout(IList<IVirtualItemData> data, VirtualListController list)
        {
            Vector2 cell = new Vector2(120, 160);
            foreach (var d in data)
                if (!(d is HeaderVM) && d.VirtualSize.x > 0) { cell = d.VirtualSize; break; }
            int perRow = Math.Max(1, list.ElementsInRow);
            Vector2 gap = list.Spacing;
            var bar = new Vector2(perRow * cell.x + (perRow - 1) * gap.x, BarHeight);
            float y = 0;
            int column = 0;
            foreach (var d in data)
            {
                if (d is HeaderVM)
                {
                    if (column > 0) { y += cell.y + gap.y; column = 0; }
                    d.SetVirtualView(new Vector2(0, y), bar);
                    y += BarHeight + gap.y;   // the same gap as between portrait rows: their frames stick out of the cells
                    continue;
                }
                d.SetVirtualView(new Vector2(column * (cell.x + gap.x), y), cell);
                if (++column == perRow) { column = 0; y += cell.y + gap.y; }
            }
        }
    }

    // A cell shows a bar for a bar's view model and a portrait otherwise.
    [HarmonyPatch(typeof(CharGenPortraitSelectorItemView), nameof(CharGenPortraitSelectorItemView.RefreshView))]
    static class PadCellView
    {
        static bool Prefix(CharGenPortraitSelectorItemView __instance)
        {
            var vm = __instance.GetViewModelClass();
            var header = vm as HeaderVM;
            try
            {
                var cell = PadCell.Get(__instance);
                if (header == null) { cell.ShowPortrait(); return true; }
                cell.ShowHeader(header.Category, header.VirtualSize);
            }
            catch (Exception e) { Main.Log.Error("gamepad cell: " + e); }
            return header == null;
        }
    }

    // Stick and d-pad move by rows as the list shows them: a bar is a row of one, a category's last row may be short.
    [HarmonyPatch(typeof(ConsoleMultiNavigationCollection), nameof(ConsoleMultiNavigationCollection.SetEntitiesGrid),
        new Type[] { typeof(List<IConsoleMultiNavigationEntity>), typeof(int) })]
    static class PadRows
    {
        static bool Prefix(ConsoleMultiNavigationCollection __instance, List<IConsoleMultiNavigationEntity> entities)
        {
            if (entities == null || entities.Count == 0 || !(entities[0] is CharGenPortraitSelectorItemView)) return true;
            var rows = new List<List<IConsoleMultiNavigationEntity>>();
            float rowY = float.NaN;
            foreach (var e in entities)
            {
                var view = e as CharGenPortraitSelectorItemView;
                float y = view ? Mathf.Round(((RectTransform)view.transform).anchoredPosition.y) : rowY;
                if (rows.Count == 0 || y != rowY) { rows.Add(new List<IConsoleMultiNavigationEntity>()); rowY = y; }
                rows[rows.Count - 1].Add(e);
            }
            __instance.SetEntities(rows);
            return false;
        }
    }

    // A (the step's confirm) on a bar collapses or opens its category instead of going on to the next step.
    [HarmonyPatch(typeof(CharGenPortraitPhaseDetailedView), nameof(CharGenPortraitPhaseDetailedView.PressConfirmOnPhase))]
    static class PadToggle
    {
        static readonly System.Reflection.FieldInfo NavigationField = AccessTools.Field(typeof(CharGenPortraitPhaseDetailedView), "m_ConsoleNavigationBehavior");
        static readonly System.Reflection.FieldInfo SelectorField = AccessTools.Field(typeof(CharGenPortraitPhaseDetailedView), "m_PortraitSelector");
        static ConsoleMultiNavigationCollection Navigation(CharGenPortraitPhaseDetailedView v) { return (ConsoleMultiNavigationCollection)NavigationField.GetValue(v); }
        static CharGenPortraitSelectorView Selector(CharGenPortraitPhaseDetailedView v) { return (CharGenPortraitSelectorView)SelectorField.GetValue(v); }

        static bool Prefix(CharGenPortraitPhaseDetailedView __instance, ref bool __result)
        {
            var view = Navigation(__instance)?.CurrentEntity as CharGenPortraitSelectorItemView;
            var header = view ? view.GetViewModelClass() as HeaderVM : null;
            if (header == null) return true;
            Gallery.Toggle(header.Category);
            Main.Safe("gamepad toggle", () => Selector(__instance).UpdateViews());
            __result = false;
            return false;
        }
    }
}
