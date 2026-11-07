using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kingmaker.UI._ConsoleUI;
using Kingmaker.UI._ConsoleUI.Inventory;
using Kingmaker.UI._ConsoleUI.Slots;
using UnityEngine;
using UnityEngine.UI;

namespace KingmakerFixes
{
    // In the gamepad's interface the circle of a magic item (the slot's magic layer) is drawn over the item's picture;
    // in the mouse interface it is under it. The layer is put under the picture in every item slot (inventory, doll,
    // loot, vendor).
    [HarmonyPatch]
    static class ConsoleMagicLayer
    {
        static bool s_Failed;

        // The views' code is shared by every reference type argument; both closed types are named for safety.
        static IEnumerable<MethodBase> TargetMethods()
        {
            var seen = new HashSet<MethodBase>();
            foreach (var type in new Type[] { typeof(ItemSlotView<ItemSlotVM>), typeof(ItemSlotView<EquipSlotVM>) })
            {
                var method = AccessTools.Method(type, "SetupMagicLayer");
                if (method != null && seen.Add(method)) yield return method;
            }
        }

        static void Postfix(object __instance)
        {
            try
            {
                var type = __instance.GetType();
                var layer = AccessTools.Field(type, "m_MagicLayer")?.GetValue(__instance) as Transform;
                var icon = AccessTools.Field(type, "Icon")?.GetValue(__instance) as Image;
                if (layer == null || icon == null || layer.parent != icon.transform.parent) return;
                int at = icon.transform.GetSiblingIndex();
                if (layer.GetSiblingIndex() > at) layer.SetSiblingIndex(at);
            }
            catch (Exception e)
            {
                if (!s_Failed) Main.Log.Error("magic layer under the item: " + e);
                s_Failed = true;
            }
        }
    }
}
