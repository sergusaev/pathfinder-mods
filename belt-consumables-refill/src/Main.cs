using System;
using System.Reflection;
using HarmonyLib;
using Kingmaker.PubSubSystem;
using UnityEngine;
using UnityModManagerNet;

namespace BeltConsumablesRefill
{
    public class Settings : UnityModManager.ModSettings
    {
        // Put the same consumable from the party inventory into a belt slot emptied by using up its item.
        public bool Refill = true;
        // Belt slots of single-charge consumables show the item plus the free stock of the same item.
        public bool ShowStock = true;
        // On every area load remove item abilities and belt slots left behind by refills that took an item from
        // another character's belt (Bag of Tricks / ToyBox).
        public bool Cleanup = true;

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    public static class Main
    {
        internal static UnityModManager.ModEntry.ModLogger Log;
        internal static Settings Settings;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Log = modEntry.Logger;
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = e => Settings.Save(e);
            new Harmony(modEntry.Info.Id).PatchAll(typeof(Main).Assembly);
            EventBus.Subscribe(new Cleanup());
            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.Refill = GUILayout.Toggle(Settings.Refill, " Refill belt slots from the party inventory when a consumable is used up");
            Settings.ShowStock = GUILayout.Toggle(Settings.ShowStock, " Show belt stock: the slot counts the free items of the same kind in the inventory");
            Settings.Cleanup = GUILayout.Toggle(Settings.Cleanup, " On area load remove item abilities left without their item (\"0 charges\")");
            if (Cleanup.LastReport != null)
                GUILayout.Label("Last cleanup: " + Cleanup.LastReport);
            string conflict = OtherRefill.Describe();
            if (conflict != null)
                GUILayout.Label("<color=orange>" + conflict + "</color>");
        }
    }

    // Bag of Tricks (Kingmaker) and ToyBox (WotR) have their own refill, "auto equip consumables": after a belt item
    // is removed they insert the first usable item of the same blueprint from the party inventory, also one that sits
    // on another character's belt. Such an item moves without being unequipped by its first owner, who keeps its
    // ability; when the item is used up the ability stays with 0 charges. Neither mod is required: their settings are
    // looked up by name, and a missing mod means no conflict.
    static class OtherRefill
    {
        internal static string Describe()
        {
            try
            {
                if (BagOfTricksOn())
                    return "Bag of Tricks \"Auto Equip Consumables\" is on: it takes items from other characters' belts. Turn it off, this mod refills instead.";
                if (ToyBoxOn())
                    return "ToyBox \"Refill consumables in belt slots if in inventory\" is on: it takes items from other characters' belts. Turn it off, this mod refills instead.";
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
            }
            return null;
        }

        // BagOfTricks.HarmonyPatches.settings.togglAutoEquipConsumables is a string compared with Storage.isTrueString.
        static bool BagOfTricksOn()
        {
            Type patches = AccessTools.TypeByName("BagOfTricks.HarmonyPatches");
            Type storage = AccessTools.TypeByName("BagOfTricks.Storage");
            object settings = patches == null ? null : AccessTools.Field(patches, "settings")?.GetValue(null);
            if (settings == null || storage == null) return false;
            object value = AccessTools.Field(settings.GetType(), "togglAutoEquipConsumables")?.GetValue(settings);
            object isTrue = AccessTools.Field(storage, "isTrueString")?.GetValue(null);
            return value is string s && isTrue is string t && s == t;
        }

        // ToyBox.Main.Settings.togglAutoEquipConsumables is a bool.
        static bool ToyBoxOn()
        {
            Type main = AccessTools.TypeByName("ToyBox.Main");
            object settings = main == null ? null : AccessTools.Field(main, "Settings")?.GetValue(null);
            if (settings == null) return false;
            return AccessTools.Field(settings.GetType(), "togglAutoEquipConsumables")?.GetValue(settings) is bool b && b;
        }

        static bool s_Warned;

        internal static void WarnOnce()
        {
            if (s_Warned) return;
            string conflict = Describe();
            if (conflict == null) return;
            s_Warned = true;
            Main.Log.Warning(conflict);
        }
    }
}
