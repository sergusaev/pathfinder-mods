using System;
using System.Collections.Generic;
using System.IO;
using HarmonyLib;
using Kingmaker.PubSubSystem;
using Kingmaker.RuleSystem.Rules;
using UnityEngine;
using UnityModManagerNet;

namespace SuccessChance
{
    public class Settings : UnityModManager.ModSettings
    {
        // Every attack with a weapon: the predicted chance before the roll and the real roll after it, in
        // SuccessChance.log in the mod folder.
        public bool LogAttacks = true;

        // The hit chance of the selected character under the enemy the pointer is on.
        public bool ShowOnHover = true;

        // The mod works in turn-based combat only: in real time there is no time to read a chance.
        internal bool Logging => LogAttacks && TurnBased.Controllers.CombatController.IsInTurnBasedCombat();

        public override void Save(UnityModManager.ModEntry modEntry)
        {
            Save(this, modEntry);
        }
    }

    public static class Main
    {
        internal static UnityModManager.ModEntry.ModLogger Log;
        internal static Settings Settings;
        static readonly HashSet<string> s_Errors = new HashSet<string>();

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Log = modEntry.Logger;
            Settings = UnityModManager.ModSettings.Load<Settings>(modEntry);
            modEntry.OnGUI = OnGUI;
            modEntry.OnSaveGUI = e => Settings.Save(e);
            ChanceLog.Open(Path.Combine(modEntry.Path, "SuccessChance.log"));
            new Harmony(modEntry.Info.Id).PatchAll(typeof(Main).Assembly);
            EventBus.Subscribe(new AttackCheck());
            EventBus.Subscribe(new SpellChecks());
#if WOTR
            EventBus.Subscribe(new EarlyPredictionsHook());
#endif
            HoverChance.Create();
            return true;
        }

        static void OnGUI(UnityModManager.ModEntry modEntry)
        {
            Settings.ShowOnHover = GUILayout.Toggle(Settings.ShowOnHover, " Show the chance of success over the enemy pointed at (turn-based combat)");
            Settings.LogAttacks = GUILayout.Toggle(Settings.LogAttacks, " Log every attack, save and spell resistance check in turn-based combat: predicted chance and the real roll (SuccessChance.log)");
            GUILayout.Label(AttackCheck.Summary());
        }

        // Each kind of error once to the UMM log, every time to SuccessChance.log.
        internal static void Error(string what, Exception e)
        {
            ChanceLog.Write("ERROR " + what + ": " + e);
            if (s_Errors.Add(what + e.GetType().Name)) Log.Error(what + ": " + e);
        }
    }

    static class ChanceLog
    {
        static StreamWriter s_Writer;

        internal static void Open(string path)
        {
            try
            {
                // The log of the session before is kept beside it (the game is often restarted to switch the interface).
                var previous = Path.ChangeExtension(path, ".prev.log");
                if (File.Exists(path))
                {
                    if (File.Exists(previous)) File.Delete(previous);
                    File.Move(path, previous);
                }
                s_Writer = new StreamWriter(path, false) { AutoFlush = true };
                s_Writer.WriteLine("Success Chance log, " + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            }
            catch (Exception e)
            {
                Main.Log.Error("log file: " + e.Message);
            }
        }

        internal static void Write(string line)
        {
            try { s_Writer?.WriteLine(DateTime.Now.ToString("HH:mm:ss ") + line); }
            catch { }
        }
    }

    // Checks the prediction against the game: before every attack with a weapon the chance is calculated, after its
    // roll the real attack bonus, armor class and result are written next to it.
    class AttackCheck : IGlobalRulebookHandler<RuleAttackWithWeapon>, IGlobalRulebookHandler<RuleAttackRoll>
    {
        readonly Dictionary<RuleAttackWithWeapon, AttackPrediction> m_Pending = new Dictionary<RuleAttackWithWeapon, AttackPrediction>();
        static int s_Attacks, s_Hits, s_Mismatches;
        static double s_Expected;

        internal static string Summary()
        {
            return s_Attacks == 0 ? "No attacks checked yet."
                : string.Format("Attacks checked: {0}, hits {1}, expected {2:0.0}; AB/AC mismatches: {3}", s_Attacks, s_Hits, s_Expected, s_Mismatches);
        }

        public void OnEventAboutToTrigger(RuleAttackWithWeapon evt)
        {
            if (!Main.Settings.Logging || evt.Weapon == null || evt.Target == null) return;
            try
            {
                m_Pending[evt] = AttackChance.Predict(evt.Initiator, evt.Target, evt.Weapon, evt.AttackBonusPenalty, evt);
            }
            catch (Exception e)
            {
                Main.Error("attack prediction", e);
            }
        }

        internal static string ReasonName(Kingmaker.RuleSystem.RulebookEvent evt)
        {
            var reason = evt.Reason;
            return reason?.Ability?.Name ?? reason?.Fact?.Name ?? reason?.Context?.AssociatedBlueprint?.name ?? "?";
        }

        internal static string Name(Kingmaker.EntitySystem.Entities.UnitEntityData unit)
        {
            string name = unit.CharacterName;
            return string.IsNullOrEmpty(name) ? unit.Blueprint.name : name;
        }

        public void OnEventDidTrigger(RuleAttackWithWeapon evt)
        {
            m_Pending.Remove(evt);
        }

        public void OnEventAboutToTrigger(RuleAttackRoll evt)
        {
        }

        public void OnEventDidTrigger(RuleAttackRoll evt)
        {
            var attack = evt.RuleAttackWithWeapon;
            AttackPrediction p = null;
            if (attack == null) p = EarlyPredictions.Take(evt) as AttackPrediction;
            if (p == null && (attack == null || !m_Pending.TryGetValue(attack, out p)))
            {
                // An attack roll the prediction did not see (no attack with a weapon around it): written down so
                // that what is not covered yet shows.
                if (Main.Settings.Logging)
                    ChanceLog.Write(string.Format("{0} -> {1} [{2}]: not predicted ({3}), real: {4}, d20 {5} + {6} vs AC {7}",
                        Name(evt.Initiator), Name(evt.Target), evt.Weapon?.Blueprint.Name,
                        attack == null ? "no attack with a weapon, reason " + (evt.Reason?.Ability?.Name ?? evt.Reason?.Fact?.Name ?? "?") : "no prediction",
                        evt.Result, GameRules.Die(evt), evt.AttackBonus, evt.TargetAC));
                return;
            }
            if (attack != null) m_Pending.Remove(attack);
            try
            {
                s_Attacks++;
                s_Expected += p.Hit;
                if (evt.IsHit) s_Hits++;
                // AB and AC are only rolled against when the attack got past concealment and was not automatic.
                bool rolled = GameRules.Die(evt) > 0;
                bool mismatch = rolled && (evt.AttackBonus != p.AttackBonus || evt.TargetAC != p.TargetAC);
                if (mismatch) s_Mismatches++;
                var sb = new System.Text.StringBuilder();
                sb.AppendFormat("{0} -> {1} [{2}{3}]{4}: {5}", Name(evt.Initiator), Name(evt.Target), p.Weapon,
                    p.Penalty != 0 ? " -" + p.Penalty : "", attack == null ? " " + ReasonName(evt) : "", p);
                if (p.AttackBonusParts != null)
                    sb.AppendFormat("\n    AB {0} = {1}\n    AC {2} = {3}", p.AttackBonus, Breakdown.Format(p.AttackBonusParts),
                        p.TargetAC, Breakdown.Format(p.ArmorClassParts));
                sb.AppendFormat("\n    real: {0}{1}", evt.Result,
                    rolled ? string.Format(", d20 {0} + {1} vs AC {2}", GameRules.Die(evt), evt.AttackBonus, evt.TargetAC) : "");
                if (mismatch)
                {
                    // The real terms, taken while the roll's own bonuses are still on, and what differs by name.
                    var realAB = evt.AttackBonusRule != null ? Breakdown.AttackBonus(evt.AttackBonusRule, evt.Initiator) : null;
                    var realAC = evt.ACRule != null ? Breakdown.ArmorClass(evt.ACRule, evt.Target) : null;
                    sb.Append("\n    <<< MISMATCH");
                    if (evt.AttackBonus != p.AttackBonus)
                        sb.AppendFormat("\n    real AB {0} = {1}\n    AB differs: {2}", evt.AttackBonus, Breakdown.Format(realAB),
                            Breakdown.Diff(p.AttackBonusParts, realAB));
                    if (evt.TargetAC != p.TargetAC)
                        sb.AppendFormat("\n    real AC {0} = {1}\n    AC differs: {2}", evt.TargetAC, Breakdown.Format(realAC),
                            Breakdown.Diff(p.ArmorClassParts, realAC));
                }
                ChanceLog.Write(sb.ToString());
            }
            catch (Exception e)
            {
                Main.Error("attack check", e);
            }
        }
    }
}
