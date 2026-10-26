using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.EntitySystem.Persistence;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using Kingmaker.PubSubSystem;
using Kingmaker.UI;
using UnityEngine;
using UnityModManagerNet;

namespace ModBar
{
    // An option of Bag of Tricks (Kingmaker) or ToyBox (WotR) that does what a mod of this collection does: a field of
    // the tool's settings, on/off (a bool in ToyBox, a string in Bag of Tricks) or a number with a neutral value.
    internal sealed class ToolOption
    {
        internal readonly string Field;
        // As the tool's window names it.
        internal readonly string Title;
        // A number: the value that leaves the game as it is. NaN for an on/off option.
        internal readonly float Neutral;
        // What else the tool does itself when the option is switched off in its window (a patch made again).
        internal Action Off;
        // A leftover of the tool to hide while the option is off: a child of every component of this type of the tool.
        internal string LeftoverType, LeftoverPath;

        internal ToolOption(string field, string title, float neutral = float.NaN)
        {
            Field = field;
            Title = title;
            Neutral = neutral;
        }
    }

    // While the mod is on, the options of the tool that do the same are off: switched off when the game starts, and
    // when one is switched on again in the tool's window, the game's dialog asks whether to switch it off again. A
    // "Cancel" is remembered in ToolConflicts.txt of the mod's folder, so the next start leaves that option on until it
    // is switched off by hand. The tool is not required: it is found by its id, its settings by name.
    sealed class ToolConflicts : MonoBehaviour
    {
#if KINGMAKER
        const string ToolId = "BagOfTricks", ToolName = "Bag of Tricks", SettingsName = "settings";
        // The settings object is kept by Main and copied into HarmonyPatches.
        static readonly string[] MainTypes = { "BagOfTricks.Main", "BagOfTricks.HarmonyPatches" };
#else
        const string ToolId = "0ToyBox0", ToolName = "ToyBox", SettingsName = "Settings";
        static readonly string[] MainTypes = { "ToyBox.Main" };
#endif
        // One dialog at a time for all the mods: the one asking keeps an object of this name.
        const string DialogMark = "ModBar.ToolConflicts.Dialog";

        UnityModManager.ModEntry m_Mod;
        Func<string> m_Name;
        ToolOption[] m_Options;
        bool[] m_Was;
        HashSet<string> m_Declined;
        UnityModManager.ModEntry m_Tool;
        object m_Settings;
        string m_True, m_False;
        bool m_Started, m_Done;
        float m_Next;
        readonly List<ToolOption> m_Pending = new List<ToolOption>();
        GameObject m_Dialog;

        internal static void Watch(UnityModManager.ModEntry mod, Func<string> name, params ToolOption[] options)
        {
            var go = new GameObject("ModBar.ToolConflicts." + mod.Info.Id);
            DontDestroyOnLoad(go);
            var watch = go.AddComponent<ToolConflicts>();
            watch.m_Mod = mod;
            watch.m_Name = name;
            watch.m_Options = options;
            watch.m_Was = new bool[options.Length];
            watch.m_Declined = watch.ReadDeclined();
        }

        // Once a second: the tool may load after the mod, and its window changes the options at any time.
        void Update()
        {
            if (m_Done || Time.unscaledTime < m_Next) return;
            m_Next = Time.unscaledTime + 1f;
            try
            {
                Check();
            }
            catch (Exception e)
            {
                m_Done = true;
                m_Mod.Logger.Error("Watching " + ToolName + " options: " + e);
            }
        }

        bool Find()
        {
            if (m_Settings != null) return true;
            m_Tool = UnityModManager.FindMod(ToolId);
            if (m_Tool == null)
            {
                m_Done = true;
                return false;
            }
            if (!m_Tool.Active || m_Tool.Assembly == null) return false;
            bool known = false;
            foreach (var name in MainTypes)
            {
                var main = m_Tool.Assembly.GetType(name);
                var field = main == null ? null : AccessTools.Field(main, SettingsName);
                known |= field != null;
                m_Settings = field == null ? null : field.GetValue(null);
                if (m_Settings != null) break;
            }
#if KINGMAKER
            var storage = m_Tool.Assembly.GetType("BagOfTricks.Storage");
            m_True = storage == null ? null : AccessTools.Field(storage, "isTrueString")?.GetValue(null) as string;
            m_False = storage == null ? null : AccessTools.Field(storage, "isFalseString")?.GetValue(null) as string;
            if (m_True == null || m_False == null) m_Settings = null;
#endif
            if (!known)
            {
                m_Done = true;
                m_Mod.Logger.Warning(ToolName + " is installed, but its settings are not where they were: its options are not watched.");
            }
            return m_Settings != null;
        }

        void Check()
        {
            if (!Find()) return;
            if (!m_Started)
            {
                m_Started = true;
                var turned = m_Options.Where(o => On(o) && !m_Declined.Contains(o.Field)).ToList();
                foreach (var option in turned) TurnOff(option);
                if (turned.Count > 0)
                {
                    Save();
                    m_Mod.Logger.Log(ToolName + " options switched off, they do what this mod does: " + string.Join(", ", turned.Select(o => o.Title).ToArray()));
                }
                for (int i = 0; i < m_Options.Length; i++) m_Was[i] = On(m_Options[i]);
            }
            else
            {
                for (int i = 0; i < m_Options.Length; i++)
                {
                    var option = m_Options[i];
                    bool on = On(option);
                    if (on && !m_Was[i] && !m_Declined.Contains(option.Field) && !m_Pending.Contains(option))
                        m_Pending.Add(option);
                    if (!on)
                    {
                        m_Pending.Remove(option);
                        if (m_Declined.Remove(option.Field)) WriteDeclined();
                    }
                    m_Was[i] = on;
                }
            }
            foreach (var option in m_Options)
                if (option.LeftoverType != null && !On(option)) HideLeftover(option);
            if (m_Pending.Count > 0 && m_Dialog == null && CanAsk()) Ask();
        }

        FieldInfo Field(ToolOption option)
        {
            return AccessTools.Field(m_Settings.GetType(), option.Field);
        }

        bool On(ToolOption option)
        {
            var field = Field(option);
            if (field == null) return false;
            object value = field.GetValue(m_Settings);
            if (value is bool) return (bool)value;
            if (value is string) return (string)value == m_True;
            if (value is float) return !float.IsNaN(option.Neutral) && !Mathf.Approximately((float)value, option.Neutral);
            return false;
        }

        void TurnOff(ToolOption option)
        {
            var field = Field(option);
            if (field == null) return;
            if (field.FieldType == typeof(bool)) field.SetValue(m_Settings, false);
            else if (field.FieldType == typeof(string)) field.SetValue(m_Settings, m_False);
            else if (field.FieldType == typeof(float))
            {
                field.SetValue(m_Settings, option.Neutral);
                // Bag of Tricks keeps the number typed in its window next to it.
                var text = AccessTools.Field(m_Settings.GetType(), option.Field + "String");
                if (text != null && text.FieldType == typeof(string)) text.SetValue(m_Settings, option.Neutral.ToString());
            }
            if (option.Off != null) option.Off();
        }

        void Save()
        {
            var settings = m_Settings as UnityModManager.ModSettings;
            if (settings != null) settings.Save(m_Tool);
        }

        void HideLeftover(ToolOption option)
        {
            var type = m_Tool.Assembly.GetType(option.LeftoverType);
            if (type == null) return;
            foreach (var found in Resources.FindObjectsOfTypeAll(type))
            {
                var component = found as Component;
                var leftover = component == null ? null : component.transform.Find(option.LeftoverPath);
                if (leftover != null && leftover.gameObject.activeSelf) leftover.gameObject.SetActive(false);
            }
        }

        // In a game, not while loading, with the mod manager's window closed (the dialog would open under it), and with
        // no other mod's dialog open.
        bool CanAsk()
        {
            if (Game.Instance == null || Game.Instance.CurrentlyLoadedArea == null) return false;
            if (LoadingProcess.Instance != null && LoadingProcess.Instance.IsLoadingInProcess) return false;
            if (UnityModManager.UI.Instance != null && UnityModManager.UI.Instance.Opened) return false;
            return GameObject.Find(DialogMark) == null;
        }

        void Ask()
        {
            var asked = m_Pending.ToList();
            m_Pending.Clear();
            m_Dialog = new GameObject(DialogMark);
            DontDestroyOnLoad(m_Dialog);
            bool ru = LocalizationManager.CurrentLocale == Locale.ruRU;
            string titles = string.Join(", ", asked.Select(o => (ru ? "«" : "\"") + o.Title + (ru ? "»" : "\"")).ToArray());
            string name = ru ? "«" + m_Name() + "»" : "\"" + m_Name() + "\"";
            string text = asked.Count == 1
                ? (ru
                    ? "Включённая опция " + ToolName + " " + titles + " конфликтует с установленным модом " + name + ", возможно непредсказуемое поведение. Выключить опцию " + ToolName + " обратно?"
                    : "The " + ToolName + " option " + titles + " you switched on conflicts with the installed mod " + name + ": the game may behave unpredictably. Switch the " + ToolName + " option off again?")
                : (ru
                    ? "Включённые опции " + ToolName + " " + titles + " конфликтуют с установленным модом " + name + ", возможно непредсказуемое поведение. Выключить эти опции " + ToolName + " обратно?"
                    : "The " + ToolName + " options " + titles + " you switched on conflict with the installed mod " + name + ": the game may behave unpredictably. Switch these " + ToolName + " options off again?");
            string yes = ru ? "Принять" : "Accept";
            string no = ru ? "Отменить" : "Cancel";
#if KINGMAKER
            EventBus.RaiseEvent<IDialogMessageBoxUIHandler>(w => w.HandleOpen(text, DialogMessageBoxBase.BoxType.Dialog,
                button => Answer(asked, button == DialogMessageBoxBase.BoxButton.Yes), yes, no));
#else
            EventBus.RaiseEvent<IMessageModalUIHandler>(w => w.HandleOpen(text, MessageModalBase.ModalType.Dialog,
                button => Answer(asked, button == MessageModalBase.ButtonType.Yes), null, yes, no));
#endif
        }

        void Answer(List<ToolOption> asked, bool accept)
        {
            try
            {
                if (m_Dialog != null) Destroy(m_Dialog);
                m_Dialog = null;
                var still = asked.Where(On).ToList();
                if (accept)
                {
                    foreach (var option in still) TurnOff(option);
                    if (still.Count > 0) Save();
                    for (int i = 0; i < m_Options.Length; i++) m_Was[i] = On(m_Options[i]);
                }
                else
                {
                    foreach (var option in still) m_Declined.Add(option.Field);
                    WriteDeclined();
                }
                m_Mod.Logger.Log(ToolName + " options " + (accept ? "switched off again: " : "left on: ") + string.Join(", ", still.Select(o => o.Title).ToArray()));
            }
            catch (Exception e)
            {
                m_Done = true;
                m_Mod.Logger.Error("Watching " + ToolName + " options: " + e);
            }
        }

        string DeclinedPath
        {
            get { return Path.Combine(m_Mod.Path, "ToolConflicts.txt"); }
        }

        HashSet<string> ReadDeclined()
        {
            try
            {
                if (File.Exists(DeclinedPath))
                    return new HashSet<string>(File.ReadAllLines(DeclinedPath).Select(l => l.Trim()).Where(l => l.Length > 0));
            }
            catch (Exception e)
            {
                m_Mod.Logger.Error("Reading " + DeclinedPath + ": " + e.Message);
            }
            return new HashSet<string>();
        }

        void WriteDeclined()
        {
            try
            {
                if (m_Declined.Count == 0)
                {
                    if (File.Exists(DeclinedPath)) File.Delete(DeclinedPath);
                }
                else File.WriteAllLines(DeclinedPath, m_Declined.ToArray());
            }
            catch (Exception e)
            {
                m_Mod.Logger.Error("Writing " + DeclinedPath + ": " + e.Message);
            }
        }
    }
}
