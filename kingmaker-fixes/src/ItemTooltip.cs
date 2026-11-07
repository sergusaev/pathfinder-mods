using System;
using System.Collections.Generic;
using HarmonyLib;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using Kingmaker.UI.Common;
using Kingmaker.UI.Tooltip;

namespace KingmakerFixes
{
    // Two faults of the item description (the tooltip and the details window):
    //  - the block of an item's bonuses to stats (a breastplate of +2 Wisdom and Charisma) is titled "Qualities", the
    //    title of the qualities block that follows it, so the description shows "Qualities" twice. The first is
    //    titled "Bonuses";
    //  - a block ends with a separator, and the next one may start with its own (the description after the blocks),
    //    so two lines stand one under the other. Of two separators in a row only the later one stays.
    static class ItemTooltip
    {
        static bool s_Failed;

        internal static string Bonuses
        {
            get { return LocalizationManager.CurrentLocale == Locale.ruRU ? "Бонусы" : "Bonuses"; }
        }

        // Plain reflection: the mod's compiler does not handle ref returns of FieldRef.
        static readonly System.Reflection.FieldInfo s_ShowList = AccessTools.Field(typeof(DescriptionBricksBox), "m_ShowList");

        internal static List<DescriptionBrick> ListOf(DescriptionBricksBox box)
        {
            return (List<DescriptionBrick>)s_ShowList.GetValue(box);
        }

        internal static void Fail(string what, Exception e)
        {
            if (!s_Failed) Main.Log.Error("item tooltip, " + what + ": " + e);
            s_Failed = true;
        }
    }

    [HarmonyPatch(typeof(DescriptionTemplatesItem), "ItemStatBonuses")]
    static class ItemStatBonusesTitle
    {

        static void Prefix(DescriptionBricksBox box, out int __state)
        {
            __state = -1;
            try { __state = ItemTooltip.ListOf(box).Count; }
            catch (Exception e) { ItemTooltip.Fail("title", e); }
        }

        // First of the postfixes: the title is the first brick the block added, whatever other mods add after it.
        [HarmonyPriority(Priority.First)]
        static void Postfix(DescriptionBricksBox box, bool __result, int __state)
        {
            if (!__result || __state < 0) return;
            try
            {
                var list = ItemTooltip.ListOf(box);
                if (__state >= list.Count) return;
                var title = list[__state];
                var id = DescriptionTemplatesBase.Bricks.TitleH1.BlockID;
                if (title == null || title.BlockID != id) return;
                title.SetText(UIUtility.GetSaberBookFormat(ItemTooltip.Bonuses));
            }
            catch (Exception e) { ItemTooltip.Fail("title", e); }
        }
    }

    [HarmonyPatch(typeof(DescriptionBricksBox), nameof(DescriptionBricksBox.Add))]
    static class NoSeparatorsInARow
    {

        static bool IsSeparator(DescriptionBrick brick)
        {
            if (brick == null) return false;
            var bricks = DescriptionTemplatesBase.Bricks;
            if (bricks == null) return false;
            var id = brick.BlockID;
            return (bricks.Separator1 != null && id == bricks.Separator1.BlockID)
                || (bricks.Separator2 != null && id == bricks.Separator2.BlockID)
                || (bricks.Separator3 != null && id == bricks.Separator3.BlockID);
        }

        static void Prefix(DescriptionBricksBox __instance, DescriptionBrick block)
        {
            try
            {
                if (!IsSeparator(block)) return;
                var list = ItemTooltip.ListOf(__instance);
                if (list.Count == 0) return;
                var last = list[list.Count - 1];
                if (!IsSeparator(last)) return;
                // Hidden and out of the list: the box takes it again for the next brick of its kind.
                last.gameObject.SetActive(false);
                list.RemoveAt(list.Count - 1);
            }
            catch (Exception e) { ItemTooltip.Fail("separators", e); }
        }
    }
}
