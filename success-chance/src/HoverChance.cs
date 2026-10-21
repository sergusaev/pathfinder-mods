using System;
using System.Collections.Generic;
using System.Linq;
using Kingmaker.RuleSystem;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UnitLogic.Abilities;
using Kingmaker;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
using Kingmaker.Items;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using Kingmaker.PubSubSystem;
using Kingmaker.UI.CombatText;
using Kingmaker.UI.Overtip;
using Kingmaker.UI._ConsoleUI.Overtips;
using Kingmaker.View;
#if WOTR
using Kingmaker.UnitLogic.Commands;
#endif
using DG.Tweening;
using TMPro;
using TurnBased.Controllers;
using UnityEngine;
using UnityEngine.UI;

namespace SuccessChance
{
    // The chance of the selected character's attack on the enemy under the pointer (the mouse or the gamepad
    // cursor), written under the enemy's feet in the game's combat text (the font of "Miss" and the damage numbers),
    // the percentage colored from red through yellow to green.
    class HoverChance : MonoBehaviour, IUnitDirectHoverUIHandler, IUnitBuffHandler, IUnitEquipmentHandler, IUnitActiveEquipmentSetHandler,
        IUnitNewCombatRoundHandler, ITurnBasedModeHandler
    {
        static readonly HashSet<string> s_Errors = new HashSet<string>();

        UnitEntityData m_Hovered;
        UnitEntityData m_Attacker;
        UnitEntityData m_NoPlaceLogged;

        TextMeshProUGUI m_Label;
        RectTransform m_Rect;

        internal static void Create()
        {
            var go = new GameObject("SuccessChanceHover");
            DontDestroyOnLoad(go);
            s_Instance = go.AddComponent<HoverChance>();
            EventBus.Subscribe(s_Instance);
        }

        public void HandleHoverChange(UnitEntityView view, bool isHover)
        {
            if (isHover == (m_Hovered == view.EntityData)) return;
            if (isHover) m_Hovered = view.EntityData;
            else if (m_Hovered == view.EntityData) m_Hovered = null;
            // Pointing at the target again takes the distance and the state anew.
            m_Target = null;
        }

        // The gamepad interface points at units by highlighting them directly (InGameInputLayer), with no hover event:
        // the highlight itself is followed, in both interfaces.
        [HarmonyLib.HarmonyPatch(typeof(UnitEntityView), nameof(UnitEntityView.MouseHighlighted), HarmonyLib.MethodType.Setter)]
        static class Highlighted
        {
            static void Postfix(UnitEntityView __instance)
            {
                if (s_Instance != null && __instance.EntityData != null) s_Instance.HandleHoverChange(__instance, __instance.MouseHighlighted);
            }
        }

        // A game event that may change a chance: any of them makes the cached texts stale, only one about the attacker or
        // the target shown makes the shown text calculated again. Buffs of allies (auras, Bless) count through the
        // buffs they put on the attacker.
        internal static void Changed(UnitEntityData unit)
        {
            s_Cache.Clear();
            var self = s_Instance;
            if (unit == null || self == null || unit == self.m_Attacker || unit == self.m_Target) s_Version++;
        }

        public void HandleBuffDidAdded(Kingmaker.UnitLogic.Buffs.Buff buff) => Changed(buff.Owner?.Unit);
        public void HandleBuffDidRemoved(Kingmaker.UnitLogic.Buffs.Buff buff) => Changed(buff.Owner?.Unit);
        public void HandleEquipmentSlotUpdated(Kingmaker.Items.Slots.ItemSlot slot, ItemEntity previousItem) => Changed(slot.Owner?.Unit);
        public void HandleUnitChangeActiveEquipmentSet(Kingmaker.UnitLogic.UnitDescriptor unit) => Changed(unit?.Unit);

        // The attacker's new round (six seconds in real time): positions, flanking, flat-footed and the distance taken
        // anew once a round. Other units' rounds only make the cache stale.
        public void HandleNewCombatRound(UnitEntityData unit)
        {
            if (unit == m_Attacker) Changed(unit);
        }
        // Turn-based combat: a new round or turn.
        public void HandleSurpriseRoundStarted() => Changed(null);
        public void HandleRoundStarted(int round) => Changed(null);
        public void HandleTurnStarted(UnitEntityData unit) => Changed(null);
        public void HandleUnitControlChanged(UnitEntityData unit) => Changed(unit);
        public void HandleUnitNotSurprised(UnitEntityData unit, RuleSkillCheck perceptionCheck) => Changed(unit);

        [HarmonyLib.HarmonyPatch(typeof(Kingmaker.UnitLogic.ActivatableAbilities.ActivatableAbility), "set_IsOn")]
        static class ActivatableToggled
        {
            static void Postfix(Kingmaker.UnitLogic.ActivatableAbilities.ActivatableAbility __instance) => Changed(__instance.Owner?.Unit);
        }

        // In turn-based combat the character whose turn it is, otherwise the selected one (the first of several).
        static UnitEntityData Attacker()
        {
            if (CombatController.IsInTurnBasedCombat())
            {
#if WOTR
                var unit = CombatController.SelectedUnit;
#else
                var unit = CombatController.CurrentUnit;
#endif
                return unit != null && unit.IsDirectlyControllable ? unit : null;
            }
#if WOTR
            return Game.Instance.SelectionCharacter?.FirstSelectedUnit;
#else
            return Game.Instance.UI.SelectionManager?.FirstSelectUnit;
#endif
        }

        // What is shown now: one entry for an attacker, a target and an ability, its text, where it stands and where on
        // the overtip, calculated together when it is made or marked stale. A frame without a change only compares
        // these fields.
        UnitEntityData m_Target;
        AbilityData m_Ability;
        bool m_Full;
        string m_Text;
        int m_Version = -1;
        Transform m_Place;
        int m_PlaceVersion = -1;

        // Moves on with every game event that may change a chance (Changed).
        static int s_Version;
        // Moves on when an overtip is bound to another unit (overtips are pooled).
        static int s_PlaceVersion;
        // The game's own combat text widgets over the shown target: while one is up the chance hides (one text replaces
        // the other).
        static readonly List<CombatTextController> s_GameTexts = new List<CombatTextController>();
        static HoverChance s_Instance;

        void Update()
        {
            Cost.FrameStart();
            try
            {
                UnitEntityData attacker = null, target = m_Hovered;
                var mode = Game.Instance.CurrentMode;
                // An ability chosen and waiting for its target: its chance; otherwise the attacks with weapons.
#if WOTR
                var ability = Game.Instance.SelectedAbilityHandler?.SelectedAbility;
#else
                var ability = Game.Instance.SelectedAbilityHandler?.Ability;
#endif
                bool full = true;
                bool show = Main.Settings.ShowOnHover && (mode == GameModeType.Default || mode == GameModeType.Pause)
                    && target != null && target.View != null && target.IsPlayersEnemy && target.IsVisibleForPlayer
                    && !target.Descriptor.State.IsDead
                    && (attacker = ability != null ? ability.Caster.Unit : Attacker()) != null && attacker != target
                    && CanActNow(ability, out full);
                if (!show)
                {
                    LogWhyNot(target, ability, mode, attacker);
                    Hide();
                    return;
                }
                m_WhyNot = null;
                if (attacker != m_Attacker || target != m_Target || ability != m_Ability || full != m_Full || s_Version != m_Version)
                    Refresh(attacker, target, ability, full);
                if (m_PlaceVersion != s_PlaceVersion) Locate();
                if (string.IsNullOrEmpty(m_Text) || m_Place == null)
                {
                    Hide();
                    return;
                }
                // In the game's combat text block, the overtip lays it out above the name, hit points and action as
                // it does its own texts; while one of the game's texts is up the chance gives way (one replaces the other).
                bool up = !GameTextUp();
                if (m_Label.gameObject.activeSelf != up) m_Label.gameObject.SetActive(up);
            }
            catch (Exception e)
            {
                // Each error once: the same one would come again with every refresh.
                if (s_Errors.Add(e.GetType().Name + e.Message + e.StackTrace)) Main.Error("hover chance", e);
                m_Text = null;
                Hide();
            }
            finally
            {
                Cost.FrameEnd();
            }
        }

        // The entry made anew: the text from the cache or calculated, the overtip found, the label put there with the
        // text and measured against the overtip at once.
        void Refresh(UnitEntityData attacker, UnitEntityData target, AbilityData ability, bool full)
        {
            bool newTarget = target != m_Target;
            m_Attacker = attacker;
            m_Target = target;
            m_Ability = ability;
            m_Full = full;
            m_Version = s_Version;
            string key = Signature(attacker, target, ability, full);
            if (!s_Cache.TryGetValue(key, out m_Text))
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                m_Text = ability != null ? SpellText(SpellChance.Predict(ability, target)) : AttacksText(attacker, target, full);
                double ms = watch.Elapsed.TotalMilliseconds;
                if (s_Cache.Count > 200) s_Cache.Clear();
                s_Cache[key] = m_Text;
                if (Main.Settings.LogAttacks)
                    ChanceLog.Write(string.Format("hover {0} -> {1}{2}: {3:0.0} ms: {4}", AttackCheck.Name(attacker), AttackCheck.Name(target),
                        ability != null ? " [" + ability.Name + "]" : "", ms, Plain(m_Text)));
            }
            if (newTarget || m_Place == null) Locate();
            else if (m_Label != null && m_Label.text != m_Text) SetText(m_Text);
        }

        // Without a layout in the block (WotR) the box is as wide as the longest line and centred over the head, the
        // lines aligned left in it and standing on its bottom edge, so that more lines go up.
        void SetText(string text)
        {
            m_Label.text = text ?? "";
            if (!m_FitWidth) return;
            float width = string.IsNullOrEmpty(text) ? 0 : m_Label.GetPreferredValues(m_Label.text).x;
            m_Rect.sizeDelta = new Vector2(Mathf.Ceil(width) + 2, m_Rect.sizeDelta.y);
        }

        bool m_FitWidth;

        // The text as shown, without its colors, its lines on one line (for the log).
        static string Plain(string text)
        {
            if (text == null) return "nothing";
            return System.Text.RegularExpressions.Regex.Replace(text, "<[^>]*>", "").Replace("\n", " | ");
        }

        // The overtip of the target, searched once for the entry and again only when overtips are bound anew.
        void Locate()
        {
            m_PlaceVersion = s_PlaceVersion;
            m_Place = CombatTextOf(m_Target);
            s_GameTexts.Clear();
            if (m_Place == null)
            {
                if (m_NoPlaceLogged != m_Target)
                {
                    m_NoPlaceLogged = m_Target;
                    ChanceLog.Write("hover: no combat text over " + m_Target?.CharacterName + " to show the chance in");
                }
                return;
            }
            if (!EnsureLabel(m_Place))
            {
                m_Place = null;
                return;
            }
            if (m_Rect.parent != m_Place) m_Rect.SetParent(m_Place, false);
            foreach (Transform child in m_Place)
            {
                var text = child.GetComponent<CombatTextController>();
                if (text != null && child.gameObject.activeSelf) s_GameTexts.Add(text);
            }
            SetText(m_Text);
            // Placed as the game's own combat text (a miss, damage): in the block's layout, nothing calculated.
            m_Rect.SetAsLastSibling();
        }

        bool GameTextUp()
        {
            for (int i = s_GameTexts.Count - 1; i >= 0; i--)
                if (s_GameTexts[i] == null || !s_GameTexts[i].gameObject.activeInHierarchy || s_GameTexts[i].transform.parent != m_Place)
                    s_GameTexts.RemoveAt(i);
            return s_GameTexts.Count > 0;
        }

        // The game adds a combat text over a unit (both interfaces): the newest child of the block is the new widget.
        static void GameTextAdded(Transform place)
        {
            var self = s_Instance;
            if (self == null || place != self.m_Place || place.childCount == 0) return;
            var text = place.GetChild(place.childCount - 1).GetComponent<CombatTextController>();
            if (text != null && !s_GameTexts.Contains(text)) s_GameTexts.Add(text);
        }

        [HarmonyLib.HarmonyPatch(typeof(OvertipViewPartCombatText), "AddCombatText")]
        static class ConsoleTextAdded
        {
            static void Postfix(OvertipViewPartCombatText __instance) => GameTextAdded(__instance.transform);
        }

        [HarmonyLib.HarmonyPatch(typeof(CombatTextManager), nameof(CombatTextManager.AddCombatText))]
        static class PcTextAdded
        {
            static void Postfix(CombatTextManager __instance) => GameTextAdded(__instance.transform);
        }

        // Overtips are pooled and bound to other units.
#if WOTR
        // WotR binds through the view base of every view: the overtip's own part of the binding is patched.
        [HarmonyLib.HarmonyPatch(typeof(UnitOvertipView), "BindViewImplementation")]
        static class ConsoleBound
        {
            static void Postfix(UnitOvertipView __instance)
            {
                var viewModel = s_ViewModel?.GetValue(__instance, null) as EntityOvertipVM;
                if (viewModel?.Unit != null) s_Overtips[viewModel.Unit] = __instance;
                s_PlaceVersion++;
            }
        }
#else
        [HarmonyLib.HarmonyPatch(typeof(UnitOvertipView), nameof(UnitOvertipView.Bind))]
        static class ConsoleBound
        {
            static void Postfix(UnitOvertipView __instance, EntityOvertipVM viewModel)
            {
                if (viewModel?.Unit != null) s_Overtips[viewModel.Unit] = __instance;
                s_PlaceVersion++;
            }
        }
#endif

        [HarmonyLib.HarmonyPatch(typeof(OvertipController), nameof(OvertipController.Initialize), new[] { typeof(UnitEntityData) })]
        static class PcBound
        {
            static void Postfix(OvertipController __instance, UnitEntityData unit)
            {
                if (unit != null) s_Overtips[unit] = __instance;
                s_PlaceVersion++;
            }
        }

        // The overtip each unit was last bound to, as the game binds them: no search of the scene.
        static readonly Dictionary<UnitEntityData, Component> s_Overtips = new Dictionary<UnitEntityData, Component>();

        static readonly Dictionary<string, string> s_Cache = new Dictionary<string, string>();

        // Who, whom, with what and from how far (ranged penalties, concealment by distance); the rest that changes a
        // chance clears the cache (Changed).
        static string Signature(UnitEntityData attacker, UnitEntityData target, AbilityData ability, bool full)
        {
            var body = attacker.Body;
            return string.Join("|", new[]
            {
                attacker.UniqueId, target.UniqueId, Id(ability), full ? "f" : "s",
                Id(body.PrimaryHand.MaybeWeapon), Id(body.SecondaryHand.MaybeWeapon),
                ((int)(attacker.DistanceTo(target) / 1.5f)).ToString(),
            });
        }

        static string Id(object item)
        {
            return item == null ? "-" : System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(item).ToString();
        }

        // In turn-based combat as the game's cursor decides: an attack only when the cursor would attack (not when the
        // character could only walk up, or has already attacked), a full attack only when it is allowed, an ability
        // only in range. Out of turn-based combat the character walks up and attacks, so always.
        static bool CanActNow(AbilityData ability, out bool full)
        {
            full = true;
            if (!CombatController.IsInTurnBasedCombat()) return false;
            var turn = Game.Instance.TurnBasedCombatController.CurrentTurn;
#if WOTR
            var unit = turn?.SelectedUnit;
            var states = unit != null ? turn.GetActionsStates(unit) : null;
#else
            var states = turn?.ActionsStates;
#endif
            if (states == null) return false;
            if (states.IsOutOfRange) return false;
            if (ability != null) return true;
#if WOTR
            full = turn.GetEnabledFullAttack(unit);
#else
            full = turn.EnabledFullAttack;
#endif
            return states.IsAttack();
        }

        // The attacks of a full attack, as UnitAttack.CreateFullAttack lists them: the hands in turn, each with its
        // main attacks and then the ones at -5, -10, then the natural weapons of additional limbs.
        static string AttacksText(UnitEntityData attacker, UnitEntityData target, bool full)
        {
            return SeriesText(Series(attacker, target, full));
        }

        // The predictions of the attacks of a full attack (only the first one when it is not full).
        internal static List<AttackPrediction> Series(UnitEntityData attacker, UnitEntityData target, bool full)
        {
            var attacks = new List<KeyValuePair<ItemEntityWeapon, int>>();
#if WOTR
            // As the game lists a full attack (UnitAttack.EnumerateFullAttack): the hands in turn, then the limbs.
            foreach (var a in UnitAttack.EnumerateFullAttack(attacker))
                if (a.Hand?.MaybeWeapon != null) attacks.Add(new KeyValuePair<ItemEntityWeapon, int>(a.Hand.MaybeWeapon, a.AttackBonusPenalty));
#else
            var body = attacker.Body;
            if (body.HandsAreEnabled)
            {
                var count = Rulebook.Trigger(new RuleCalculateAttacksCount(attacker));
                var primary = Penalties(count.PrimaryHand);
                var secondary = Penalties(count.SecondaryHand);
                for (int i = 0; i < Math.Max(primary.Count, secondary.Count); i++)
                {
                    if (i < primary.Count && body.PrimaryHand.MaybeWeapon != null)
                        attacks.Add(new KeyValuePair<ItemEntityWeapon, int>(body.PrimaryHand.MaybeWeapon, primary[i]));
                    if (i < secondary.Count && body.SecondaryHand.MaybeWeapon != null)
                        attacks.Add(new KeyValuePair<ItemEntityWeapon, int>(body.SecondaryHand.MaybeWeapon, secondary[i]));
                }
            }
            foreach (var limb in body.AdditionalLimbs)
                if (limb.HasWeapon && limb.HasItem) attacks.Add(new KeyValuePair<ItemEntityWeapon, int>(limb.MaybeWeapon, 0));
#endif
            if (attacks.Count == 0)
            {
                var weapon = attacker.GetFirstWeapon();
                if (weapon == null) return new List<AttackPrediction>();
                attacks.Add(new KeyValuePair<ItemEntityWeapon, int>(weapon, 0));
            }
            if (!full) attacks.RemoveRange(1, attacks.Count - 1);
            var predictions = new List<AttackPrediction>();
            foreach (var a in attacks) predictions.Add(AttackChance.Predict(attacker, target, a.Key, a.Value));
            return predictions;
        }

        // One line an attack, in the order of the series (the first one, with the best bonus, on top): its chance, its
        // damage, the critical hit and a maneuver the hit makes.
        internal static string SeriesText(List<AttackPrediction> predictions)
        {
            if (predictions == null || predictions.Count == 0) return null;
            var lines = new List<string>();
            foreach (var p in predictions) lines.Add(AttackText(p));
            return string.Join("\n", lines.ToArray());
        }

        static string AttackText(AttackPrediction p)
        {
            return Colored(p.Hit, p.Hit) + ", " + Damage(p.DamageMin, p.DamageMax) + CritText(p) + OnHitText(p);
        }

        // The maneuver of a hit, its chance counted with the hit's.
        static string OnHitText(AttackPrediction p)
        {
            var m = p.Maneuver;
            if (m == null) return "";
            bool ru = LocalizationManager.CurrentLocale == Locale.ruRU;
            string name = ManeuverChance.Name(m.Type, ru).ToLowerInvariant();
            if (m.Immune) return ", " + name + " " + (ru ? "невозможно" : "impossible");
            double chance = p.Hit * m.Success;
            return ", " + name + " " + Colored(chance, chance);
        }

#if !WOTR
        static List<int> Penalties(RuleCalculateAttacksCount.AttacksCount count)
        {
            var list = new List<int>();
            for (int i = 0; i < count.MainAttacks; i++) list.Add(0);
            for (int j = 1; j <= count.PenalizedAttacks; j++) list.Add(5 * j);
            return list;
        }
#endif

        internal static string CritText(AttackPrediction p)
        {
            if (p == null || p.Crit < 0.005) return "";
            bool ru = LocalizationManager.CurrentLocale == Locale.ruRU;
            // The crit is colored by how much of the weapon's threat range it gets (the confirmation and the rest).
            double threat = p.CanCrit ? (21 - Math.Max(2, p.CriticalEdge)) / 20.0 : 0;
            return string.Format(" ({0} {1}, {2})", ru ? "крит" : "crit", Colored(p.Crit, threat > 0 ? p.Crit / threat : 0),
                Damage(p.CritDamageMin, p.CritDamageMax));
        }

        internal static string Ordinal(int n, bool ru)
        {
            return ru ? n + "й" : "#" + n;
        }

        // A damage range in red.
        internal static string Damage(int min, int max)
        {
            return "<color=#" + ColorUtility.ToHtmlStringRGB(Red) + ">" + min + "-" + max + "</color>";
        }

        // The attack roll, the spell resistance, the chance the target fails its save and a maneuver's chance; nothing
        // when the spell rolls none of them. Projectiles with an attack roll each (Scorching Ray) are a line each, as
        // many as the spell sends; those without one (Magic Missile) put their count before the damage.
        internal static string SpellText(SpellPrediction p)
        {
            if (p == null || p.Empty) return null;
            bool ru = LocalizationManager.CurrentLocale == Locale.ruRU;
            var parts = new List<string>();
            if (p.Attack != null) parts.Add((ru ? "Попадание " : "Hit ") + Colored(p.Attack.Hit, p.Attack.Hit));
            var sr = p.SpellResistance;
            if (sr != null && sr.Immune && !sr.Ignored) parts.Add(ru ? "Невосприимчив" : "Immune");
            else if (sr != null && sr.Pass < 1) parts.Add((ru ? "Сопротивление " : "Resistance ") + Colored(sr.Pass, sr.Pass));
            if (p.Save != null)
            {
                double fail = 1 - p.Save.Pass;
                parts.Add(SaveName(p.Save.Type, ru) + " " + Colored(fail, fail));
            }
            if (p.Maneuver != null)
            {
                string name = ManeuverChance.Name(p.Maneuver.Type, ru);
                parts.Add(p.Maneuver.Immune ? name + " " + (ru ? "невозможно" : "impossible")
                    : name + " " + Colored(p.Maneuver.Success, p.Maneuver.Success));
            }
            bool lines = p.Attack != null && p.Projectiles > 1;
            int times = lines ? 1 : p.Projectiles;
            // The damage after the last part (after the save: on a failed save / on a passed one), or alone.
            if (p.DamageMax >= 0)
            {
                string damage = Damage(p.DamageMin, p.DamageMax, times);
                if (p.Save != null && p.SavedMax >= 0) damage += " / " + Damage(p.SavedMin, p.SavedMax, times);
                if (parts.Count > 0) parts[parts.Count - 1] += ", " + damage;
                else parts.Add((ru ? "Урон " : "Damage ") + damage);
            }
            string line = string.Join(" ", parts.ToArray());
            if (!lines) return line;
            var all = new string[p.Projectiles];
            for (int i = 0; i < all.Length; i++) all[i] = line;
            return string.Join("\n", all);
        }

        // The damage of several projectiles: their count before the damage of one, "4x(2-5)" (a plain x: the game's
        // fonts may have no multiplication sign).
        static string Damage(int min, int max, int times)
        {
            if (times <= 1) return Damage(min, max);
            return "<color=#" + ColorUtility.ToHtmlStringRGB(Red) + ">" + times + "x(" + min + "-" + max + ")</color>";
        }

        internal static string SaveName(SavingThrowType type, bool ru)
        {
            switch (type)
            {
                case SavingThrowType.Fortitude: return ru ? "Провал Стойкости" : "Fails Fortitude";
                case SavingThrowType.Reflex: return ru ? "Провал Реакции" : "Fails Reflex";
                case SavingThrowType.Will: return ru ? "Провал Воли" : "Fails Will";
                default: return ru ? "Провал спасброска" : "Fails save";
            }
        }

        void Hide()
        {
            if (m_Label != null && m_Label.gameObject.activeSelf) m_Label.gameObject.SetActive(false);
        }

        string m_WhyNot;

        // Why the chance is not shown over a unit pointed at, when the reason changes (for the log).
        void LogWhyNot(UnitEntityData target, AbilityData ability, GameModeType mode, UnitEntityData attacker)
        {
            string why;
            bool full;
            if (target == null) why = null;
            else if (!Main.Settings.ShowOnHover) why = "off";
            else if (mode != GameModeType.Default && mode != GameModeType.Pause) why = "mode " + mode;
            else if (!target.IsPlayersEnemy) why = "not an enemy";
            else if (!target.IsVisibleForPlayer) why = "not visible";
            else if (target.Descriptor.State.IsDead) why = "dead";
            else if ((ability != null ? ability.Caster.Unit : Attacker()) == null) why = "no attacker";
            else if (!CombatController.IsInTurnBasedCombat()) why = "real time";
            else if (!CanActNow(ability, out full)) why = "cannot act now";
            else why = "other";
            if (why == m_WhyNot) return;
            m_WhyNot = why;
            if (why != null && Main.Settings.LogAttacks)
                ChanceLog.Write(string.Format("hover {0}: not shown, {1} ({2})", AttackCheck.Name(target), why,
                    Game.Instance.IsControllerGamepad ? "gamepad" : "mouse"));
        }
        internal static string Percent(double x)
        {
            return Math.Round(x * 100).ToString("0") + "%";
        }

        static readonly Color Red = new Color(0.92f, 0.25f, 0.2f);

        // Red at 0%, yellow at 50%, green at 100%.
        internal static string Colored(double chance, double shade)
        {
            var red = Red;
            var yellow = new Color(0.98f, 0.85f, 0.25f);
            var green = new Color(0.35f, 0.88f, 0.3f);
            float t = Mathf.Clamp01((float)shade);
            var c = t < 0.5f ? Color.Lerp(red, yellow, t * 2) : Color.Lerp(yellow, green, (t - 0.5f) * 2);
            return "<color=#" + ColorUtility.ToHtmlStringRGB(c) + ">" + Percent(chance) + "</color>";
        }

        // The label is a copy of the game's combat text widget, put where the game puts its own combat text over the
        // enemy, so it has its size and place and moves with the overtip. Made again when the interface is rebuilt.
        bool EnsureLabel(Transform place)
        {
            if (m_Label != null) return true;
            var template = place.GetComponent<OvertipViewPartCombatText>()?.CombatTextPrefab ?? place.GetComponent<CombatTextManager>()?.CombatTextPrefab;
            if (template == null) return false;
            var go = Instantiate(template.gameObject, place, false);
            go.name = "SuccessChanceLabel";
            DestroyImmediate(go.GetComponent<CombatTextController>());
            foreach (var image in go.GetComponentsInChildren<Image>(true)) image.gameObject.SetActive(false);
            var group = go.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1;
                group.blocksRaycasts = false;
                group.interactable = false;
            }
            m_Label = go.GetComponent<TextMeshProUGUI>();
            if (m_Label == null)
            {
                Destroy(go);
                return false;
            }
            // The widget shrinks a text that does not fit its box; this line is longer than "Miss", so the size is
            // fixed at the largest the widget uses and the box made wide.
            float fontSize = m_Label.fontSize;
            if (m_Label.enableAutoSizing) m_Label.fontSize = m_Label.fontSizeMax;
            m_Label.enableAutoSizing = false;
            m_Label.raycastTarget = false;
            m_Label.enableWordWrapping = false;
            m_Label.color = Color.white;
            m_Rect = (RectTransform)go.transform;
            m_Rect.sizeDelta = new Vector2(Mathf.Max(m_Rect.sizeDelta.x, 1000), m_Rect.sizeDelta.y);
#if WOTR
            // WotR has no layout in the block: the widget stands by its pivot, which is on its left edge, so the text
            // would start over the head and run to the right: the box is centred over the head and fitted to the text
            // (SetText).
            if (place.GetComponent<LayoutGroup>() == null)
            {
                m_Rect.anchorMin = m_Rect.anchorMax = m_Rect.pivot = new Vector2(0.5f, 0.5f);
                m_Rect.anchoredPosition = Vector2.zero;
                m_Label.alignment = TextAlignmentOptions.BottomLeft;
                m_FitWidth = true;
            }
#endif
            ChanceLog.Write(string.Format("hover label made in {0} ({1}), font {2} (template {3}, auto {4}..{5}), layout {6}", place.name,
                place.root.name, m_Label.fontSize, fontSize, m_Label.fontSizeMin, m_Label.fontSizeMax,
                place.GetComponent<LayoutGroup>()?.GetType().Name ?? "none"));
            return true;
        }

        static readonly System.Reflection.PropertyInfo s_ViewModel = HarmonyLib.AccessTools.Property(typeof(UnitOvertipView), "ViewModel");

        // The unit whose overtip holds this combat text.
        static UnitEntityData OvertipUnit(Transform place)
        {
            var console = place.GetComponentInParent<UnitOvertipView>();
            if (console != null) return (s_ViewModel?.GetValue(console, null) as EntityOvertipVM)?.Unit;
            return place.GetComponentInParent<OvertipController>()?.Unit;
        }

        // Where the game shows combat text over the unit: its overtip from the registry, still bound to it and up.
        static Transform CombatTextOf(UnitEntityData unit)
        {
            Component overtip;
            if (unit == null || !s_Overtips.TryGetValue(unit, out overtip)) return null;
            if (overtip == null || !overtip.gameObject.activeInHierarchy)
            {
                if (overtip == null) s_Overtips.Remove(unit);
                return null;
            }
            var console = overtip as UnitOvertipView;
            if (console != null)
                return (s_ViewModel?.GetValue(console, null) as EntityOvertipVM)?.Unit == unit && console.CombatText != null ? console.CombatText.transform : null;
            var pc = overtip as OvertipController;
            return pc != null && pc.Unit == unit && pc.CombatText != null ? pc.CombatText.transform : null;
        }
    }
}
