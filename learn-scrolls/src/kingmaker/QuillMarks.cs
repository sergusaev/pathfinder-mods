using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Items.Components;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.UI.Tooltip;
using Kingmaker.UI._ConsoleUI;
using Kingmaker.UI._ConsoleUI.Inventory;
using Kingmaker.UI._ConsoleUI.Loot;
using Kingmaker.UI._ConsoleUI.Slots;
using Kingmaker.UI._ConsoleUI.Vendor;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using UnityEngine;
using UnityEngine.UI;
using PcItemSlot = Kingmaker.UI.ServiceWindow.ItemSlot;

namespace LearnScrolls
{
    // Who of the main character's people can learn a spell from a scroll, by spell. Filled when a slot shows a
    // scroll and emptied once a second and after every copying: whatever changes what a character can learn (a
    // level, a respec, a new companion, a spell learned) is then seen without following each of those events.
    internal static class Learners
    {
        static readonly Dictionary<BlueprintAbility, List<UnitEntityData>> s_BySpell = new Dictionary<BlueprintAbility, List<UnitEntityData>>();
        static List<UnitEntityData> s_Roster;

        internal static List<UnitEntityData> Of(BlueprintAbility spell)
        {
            List<UnitEntityData> list;
            if (s_BySpell.TryGetValue(spell, out list)) return list;
            if (s_Roster == null) s_Roster = Planner.Roster();
            list = new List<UnitEntityData>();
            foreach (var unit in s_Roster)
                if (Planner.CanLearn(unit, spell)) list.Add(unit);
            s_BySpell[spell] = list;
            return list;
        }

        // The spell of a scroll, null for any other item.
        internal static BlueprintAbility SpellOf(ItemEntity item)
        {
            if (item == null) return null;
            var copy = item.Blueprint.GetComponent<CopyScroll>();
            return copy != null ? Planner.SpellOf(item, copy) : null;
        }

        internal static bool Anyone(ItemEntity item)
        {
            var spell = SpellOf(item);
            return spell != null && Of(spell).Count > 0;
        }

        internal static void Clear()
        {
            s_BySpell.Clear();
            s_Roster = null;
        }
    }

    // The quill in the upper right corner of the icon of a scroll someone of the main character's people can learn:
    // in the inventory, the stash, the loot, the vendor's window; the mouse interface and the gamepad's.
    internal static class QuillMarks
    {
        const string MarkName = "LearnScrolls_Quill";
        static Sprite s_Sprite;
        static bool s_SpriteTried;
        // The marks of the slots showing a scroll now, with their item.
        static readonly Dictionary<Image, ItemEntity> s_Live = new Dictionary<Image, ItemEntity>();
        static float s_Next;

        static Sprite Quill
        {
            get
            {
                if (!s_SpriteTried)
                {
                    s_SpriteTried = true;
                    s_Sprite = Art.Picture("quill_km.png");
                }
                return s_Sprite;
            }
        }

        // The slot's icon shows this item now (null when empty).
        internal static void Show(Image icon, ItemEntity item)
        {
            if (icon == null) return;
            var mark = Find(icon);
            bool scroll = Learners.SpellOf(item) != null;
            if (!scroll)
            {
                if (mark != null)
                {
                    mark.gameObject.SetActive(false);
                    s_Live.Remove(mark);
                }
                return;
            }
            if (mark == null) mark = Create(icon);
            if (mark == null) return;
            s_Live[mark] = item;
            mark.gameObject.SetActive(Learners.Anyone(item));
        }

        static Image Find(Image icon)
        {
            var child = icon.transform.Find(MarkName);
            return child != null ? child.GetComponent<Image>() : null;
        }

        static Image Create(Image icon)
        {
            if (Quill == null) return null;
            var go = new GameObject(MarkName, typeof(RectTransform));
            go.layer = icon.gameObject.layer;
            var rect = (RectTransform)go.transform;
            rect.SetParent(icon.transform, false);
            // The upper left part of the icon (the count of the stack is in the upper right), the quill kept in its
            // proportions inside it.
            rect.anchorMin = new Vector2(-0.02f, 0.56f);
            rect.anchorMax = new Vector2(0.44f, 1.02f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.SetAsLastSibling();
            var image = go.AddComponent<Image>();
            image.sprite = Quill;
            image.preserveAspect = true;
            // The slot's clicks, drags and tooltips stay the slot's.
            image.raycastTarget = false;
            return image;
        }

        // Once a second, and right after a copying, everything is counted anew and the marks shown follow.
        internal static void Tick()
        {
            if (s_Live.Count == 0 || Time.unscaledTime < s_Next) return;
            Refresh();
        }

        internal static void Refresh()
        {
            s_Next = Time.unscaledTime + 1f;
            Learners.Clear();
            if (s_Live.Count == 0) return;
            var gone = new List<Image>();
            foreach (var pair in s_Live)
            {
                // A destroyed window's marks.
                if (pair.Key == null) { gone.Add(pair.Key); continue; }
                bool on = Learners.Anyone(pair.Value);
                if (pair.Key.gameObject.activeSelf != on) pair.Key.gameObject.SetActive(on);
            }
            foreach (var mark in gone) s_Live.Remove(mark);
        }

        internal static void Hide(Image icon)
        {
            Show(icon, null);
        }
    }

    // The mouse interface: every slot of an item (inventory, stash, loot, vendor) is an ItemSlot or its descendant.
    [HarmonyPatch(typeof(PcItemSlot), nameof(PcItemSlot.SetItem))]
    static class PcSlotSetItem
    {
        static void Postfix(PcItemSlot __instance)
        {
            try { QuillMarks.Show(__instance.ItemImage, __instance.HasItem ? __instance.Item : null); }
            catch (Exception e) { Main.Error(e, "quill mark (mouse)"); }
        }
    }

    [HarmonyPatch(typeof(PcItemSlot), nameof(PcItemSlot.Clear))]
    static class PcSlotClear
    {
        static void Postfix(PcItemSlot __instance)
        {
            try { QuillMarks.Hide(__instance.ItemImage); }
            catch (Exception e) { Main.Error(e, "quill mark clear (mouse)"); }
        }
    }

    // The gamepad's interface: the slots of the inventory and the stash, the loot, the vendor's exchange and its goods.
    [HarmonyPatch]
    static class ConsoleSlotRefresh
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(InventorySlotView), "RefreshItem");
            yield return AccessTools.Method(typeof(LootSlotView), "RefreshItem");
            yield return AccessTools.Method(typeof(VendorExchangeSlotView), "RefreshItem");
            yield return AccessTools.Method(typeof(InventoryItemSelectorSlotView), "RefreshItem");
        }

        static void Postfix(object __instance)
        {
            try
            {
                var view = __instance as ItemSlotView<ItemSlotVM>;
                if (view == null) return;
                var vm = view.ItemSlotVM;
                QuillMarks.Show(view.Icon, vm != null && vm.Item != null ? vm.Item.Value : null);
            }
            catch (Exception e) { Main.Error(e, "quill mark (gamepad)"); }
        }
    }

    [HarmonyPatch]
    static class ConsoleSlotClear
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(InventorySlotView), "ClearView");
            yield return AccessTools.Method(typeof(LootSlotView), "ClearView");
            yield return AccessTools.Method(typeof(VendorExchangeSlotView), "ClearView");
            yield return AccessTools.Method(typeof(InventoryItemSelectorSlotView), "ClearView");
        }

        static void Postfix(object __instance)
        {
            try
            {
                var view = __instance as ItemSlotView<ItemSlotVM>;
                if (view != null) QuillMarks.Hide(view.Icon);
            }
            catch (Exception e) { Main.Error(e, "quill mark clear (gamepad)"); }
        }
    }

    // The item's description (tooltip and the details window, both interfaces): under the game's "Spell is known by"
    // of a scroll, who of the main character's people can learn it, in the same form.
    [HarmonyPatch(typeof(DescriptionTemplatesItem), "ItemTrainedCompanions")]
    static class DescriptionLearners
    {
        static void Postfix(DescriptionBricksBox box, TooltipData data, bool isTooltip, ref bool __result)
        {
            try
            {
                var spell = Learners.SpellOf(data != null ? data.Item : null);
                if (spell == null) return;
                // Counted anew: the description may come between the refreshes.
                Learners.Clear();
                var units = Learners.Of(spell);
                if (units.Count == 0) return;
                // The game's own block goes before with its separator only after both: one more line under it.
                if (!isTooltip)
                {
                    DescriptionTemplatesBase.AddTitleH2(box, Strings.CanBeLearnedBy);
                    foreach (var unit in units)
                        box.Add(DescriptionTemplatesBase.Bricks.PortraitAndTextSmall)
                            .SetIcon(unit.Descriptor.Portrait.SmallPortrait, AspectRatioFitter.AspectMode.WidthControlsHeight)
                            .SetText(unit.CharacterName);
                }
                else
                {
                    var text = new StringBuilder();
                    text.Append(Strings.CanBeLearnedBy).Append(" <b>");
                    for (int i = 0; i < units.Count; i++)
                    {
                        if (i > 0) text.Append(", ");
                        text.Append(units[i].CharacterName);
                    }
                    text.Append("</b>");
                    box.Add(DescriptionTemplatesBase.Bricks.SimpleText).SetText(text.ToString());
                }
                __result = true;
            }
            catch (Exception e) { Main.Error(e, "learners in the description"); }
        }
    }

    // Any copying, the game's own or the mod's: the scroll may be spent, the spell is known now.
    [HarmonyPatch(typeof(CopyItem), nameof(CopyItem.Copy))]
    static class CopyRefresh
    {
        static void Postfix()
        {
            try { QuillMarks.Refresh(); }
            catch (Exception e) { Main.Error(e, "quill marks after copying"); }
        }
    }
}
