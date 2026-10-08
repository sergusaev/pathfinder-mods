using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes;
#if KINGMAKER
using Kingmaker.Controllers.Rest;
#else
using Kingmaker.UI.CharSelect;
#endif
using Kingmaker.Designers.EventConditionActionSystem.Actions;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.GameModes;
using Kingmaker.PubSubSystem;
using Kingmaker.Enums;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Alignments;
using Kingmaker.Visual.Sound;
using UnityEngine;
using UnityModManagerNet;

namespace Level1Companions
{
    // The respec from the dialog (RespecCompanion action: Anoriel in Kingmaker, the respec NPCs in WotR) without
    // the dialog: the same character selector whose confirmation calls Player.RespecCompanion (in WotR through the
    // respec confirmation window, as the action does).
    // Free: no money, RespecsUsed unchanged, and unlike the dialog no day passes.
    static class Respec
    {
        internal static string Status;
        // Set by the button until the respec starts. Player.RespecCompanion then keeps the story companion's identity.
        internal static bool Pending;
        // Player.RespecCompanion is running: it builds the new unit from the blueprint (preset levels included).
        internal static bool Running;

        // A respec builds units of its own: the new unit and, in WotR, the editor's previews while the respec window
        // is open. Their preset levels are not a recruit's and need no experience.
        internal static bool InProgress
        {
            get
            {
#if WOTR
                if (Kingmaker.UI.MVVM._VM.CharGen.RespecWindowVM.Instance != null) return true;
#endif
                return Running;
            }
        }

        internal static void Open()
        {
            Status = null;
            Pending = false;
            Game game = Game.Instance;
            Player player = game?.Player;
            if (player == null || player.MainCharacter.Value == null)
            {
                Status = "Load a game first.";
                return;
            }
            if (player.IsInCombat)
            {
                Status = "Not available in combat.";
                return;
            }
            if (game.CurrentMode != GameModeType.Default && game.CurrentMode != GameModeType.Pause)
            {
                Status = $"Not available now ({game.CurrentMode}); close dialogs and other windows.";
                return;
            }
            var units = new List<UnitEntityData>();
            // Only the active party: companions sent away, or gone from the story but still listed as remote
            // (Camellia in WotR), are not offered.
            IEnumerable<UnitEntityData> candidates = player.PartyCharacters.Select(r => r.Value);
            foreach (UnitEntityData unit in candidates)
            {
                if (unit == null) continue;
                string reason = Check(unit, player);
                Main.Log.Log($"respec: {unit.CharacterName} ({unit.Blueprint.name}, level {unit.Descriptor.Progression.CharacterLevel}): {reason ?? "ok"}");
                if (reason == null) units.Add(unit);
            }
            if (units.Count == 0)
            {
                Status = "Nobody can be respecced: the character must be in the active party and above its preset level.";
                return;
            }
            Pending = true;
            CloseModWindow();
#if KINGMAKER
            EventBus.RaiseEvent(delegate (ICharacterSelectorHandler h) { h.HandleSelectCharacter(units, Finish); });
#else
            var selection = new CharSelectWindowData();
            EventBus.RaiseEvent(delegate (ICharacterSelectorHandler h)
            {
                h.HandleSelectCharacter(units, delegate
                {
                    UnitEntityData chosen = selection.Unit;
                    if (chosen == null) return;
                    EventBus.RaiseEvent(delegate (IRespecInitiateUIHandler r) { r.HandleRespecInitiate(chosen, Finish); });
                }, selection);
            });
#endif
        }

        // RespecCompanion.CanRespec (alive, not a pet, above the preset level limit), limited to the main character,
        // mercenaries and companions: story NPCs that travel with the party (Tartuccio) have no level limit and
        // would be rebuilt from scratch. A story companion goes back to its preset level 1, never below: its class,
        // stats and level 1 abilities are part of the story. The main character and mercenaries have no level limit
        // and are rebuilt from scratch with everything open to change, as by the game's own respec.
        static string Check(UnitEntityData unit, Player player)
        {
            if (unit.Descriptor.State.IsFinallyDead) return "dead";
#if KINGMAKER
            if (unit.Descriptor.IsPet) return "pet";
#else
            if (unit.IsPet) return "pet";
#endif
#if WOTR
            if (unit == player.MainCharacter.Value && player.DisableMainCharacterRespec) return "the story forbids a respec now";
#endif
            ClassLevelLimit limit = RespecBlueprint(unit).GetComponent<ClassLevelLimit>();
            bool own = unit == player.MainCharacter.Value || unit.IsCustomCompanion();
            if (limit == null && !own) return "not a companion";
            int floor = limit == null ? 0 : limit.LevelLimit;
            if (unit.Descriptor.Progression.CharacterLevel <= floor) return $"not above level {floor}";
            return null;
        }

        // The blueprint the respec rebuilds the unit from: in WotR a companion may name another one for the respec.
        internal static BlueprintUnit RespecBlueprint(UnitEntityData unit)
        {
#if WOTR
            BlueprintUnit replacement = unit.Blueprint.GetComponent<ReplaceUnitBlueprintForRespec>()?.Blueprint;
            if (replacement != null) return replacement;
#endif
            return unit.Blueprint;
        }

        // A story companion: not the main character, its clone or a mercenary (LevelUpState.IsLoreCompanion).
        internal static bool IsStoryCompanion(UnitDescriptor unit)
        {
#if KINGMAKER
            return !unit.IsCustomCompanion() && !unit.IsMainCharacter && !unit.IsCloneOfMainCharacter;
#else
            return !unit.IsCustomCompanion() && !unit.IsMainCharacter && !unit.Unit.IsCloneOfMainCharacter;
#endif
        }

        // RespecCompanion.FinishRespecialization with ForFree, minus the day.
        static void Finish()
        {
#if KINGMAKER
            foreach (UnitEntityData unit in Game.Instance.Player.ControllableCharacters)
            {
                if (!unit.Descriptor.State.IsFinallyDead)
                    RestController.ApplyRest(unit.Descriptor);
            }
#else
            foreach (UnitEntityData unit in Game.Instance.Player.PartyAndPets)
            {
                if (!unit.Descriptor.State.IsFinallyDead)
                    UnitHelper.Replenish(unit.Descriptor);
            }
#endif
        }

        static void CloseModWindow()
        {
            try
            {
                Type ui = typeof(UnityModManager).GetNestedType("UI", BindingFlags.Public | BindingFlags.NonPublic);
                object instance = ui?.GetProperty("Instance", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null, null);
                MethodInfo toggle = ui?.GetMethod("ToggleWindow", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance, null, new[] { typeof(bool) }, null);
                if (instance != null && toggle != null)
                    toggle.Invoke(instance, new object[] { false });
                else
                    Status = "Close this window to see the companion selection.";
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
                Status = "Close this window to see the companion selection.";
            }
        }
    }

    // Player.RespecCompanion recreates the unit from its blueprint with preset levels up to ClassLevelLimit.
    // On success the old unit is populated from the new one except inventory, body, UI settings, buffs and non class
    // features, so a story companion would also get the blueprint's name, voice and alignment, and the chargen's
    // empty doll and birthday (LevelUpController.SetupNewCharacher in Respec mode), which leaves it without a model.
    // The respec from the mod buttons changes only class and abilities: the rest goes onto the new unit before the
    // copy (UnitEntityData.PrepareRespec), so the view is built from the companion's own model.
    [HarmonyPatch(typeof(Player), nameof(Player.RespecCompanion))]
    static class RespecCompanionPatch
    {
        static void Prefix(UnitEntityData unit, ref Action successCallback)
        {
            Respec.Running = true;
            Identity.Pending = null;
            if (!Respec.Pending || unit == null) return;
            Respec.Pending = false;
            if (!Respec.IsStoryCompanion(unit.Descriptor)) return;
            Identity snapshot = new Identity(unit.Descriptor);
            Identity.Pending = snapshot;
            successCallback = (Action)delegate { snapshot.Check(unit); } + successCallback;
        }

        static Exception Finalizer(Exception __exception)
        {
            Respec.Running = false;
            return __exception;
        }
    }

    // Called on the new unit right before it is serialized onto the old one.
    [HarmonyPatch(typeof(UnitEntityData), nameof(UnitEntityData.PrepareRespec))]
    static class PrepareRespecPatch
    {
        static void Prefix(UnitEntityData __instance)
        {
            Identity snapshot = Identity.Pending;
            Identity.Pending = null;
            if (snapshot == null) return;
            try
            {
                snapshot.Apply(__instance.Descriptor);
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
            }
        }
    }

    // What a story companion keeps through the respec besides what Player.RespecCompanion keeps itself.
    // WotR keeps the alignment itself (UnitAlignment.CopyFrom) and keeps the doll in a unit part, not in the
    // descriptor, so there the snapshot has neither.
    // OverrideAsks is not kept: it is the voice of a polymorph buff (ReplaceAsksList), set while the buff is on and
    // cleared with it, and the respec takes the buffs' effects off.
    class Identity
    {
        internal static Identity Pending;

#if KINGMAKER
        static readonly MethodInfo SetVector = AccessTools.PropertySetter(typeof(UnitAlignment), nameof(UnitAlignment.Vector));
        static readonly FieldInfo History = AccessTools.Field(typeof(UnitAlignment), "m_History");
#endif

        readonly string m_Name;
        readonly Gender? m_Gender;
        readonly BlueprintUnitAsksList m_Asks;
        readonly bool? m_LeftHanded;
        readonly string m_Prefab;
        readonly int m_BirthDay;
        readonly int m_BirthMonth;
#if KINGMAKER
        readonly DollData m_Doll;
        readonly Vector2 m_Alignment;
        readonly List<AlignmentHistoryRecord> m_History;
#endif

        internal Identity(UnitDescriptor d)
        {
            m_Name = d.CustomName;
            m_Gender = d.CustomGender;
            m_Asks = d.CustomAsks;
            m_LeftHanded = d.LeftHandedOverride;
            m_Prefab = d.CustomPrefabGuid;
            m_BirthDay = d.BirthDay;
            m_BirthMonth = d.BirthMonth;
#if KINGMAKER
            m_Doll = d.Doll;
            m_Alignment = d.Alignment.Vector;
            m_History = new List<AlignmentHistoryRecord>(d.Alignment.History);
#endif
        }

        internal void Apply(UnitDescriptor d)
        {
            d.CustomName = m_Name;
            d.CustomGender = m_Gender;
            d.CustomAsks = m_Asks;
            d.LeftHandedOverride = m_LeftHanded;
            d.CustomPrefabGuid = m_Prefab;
            d.BirthDay = m_BirthDay;
            d.BirthMonth = m_BirthMonth;
#if KINGMAKER
            d.Doll = m_Doll;
            SetVector.Invoke(d.Alignment, new object[] { m_Alignment });
            History.SetValue(d.Alignment, new List<AlignmentHistoryRecord>(m_History));
            d.Alignment.UpdateValue();
#endif
        }

        // After the copy: everything should already match; log what does not, and fix the descriptor.
        internal void Check(UnitEntityData unit)
        {
            try
            {
                UnitDescriptor d = unit.Descriptor;
                var diff = new List<string>();
                Compare(diff, "name", d.CustomName, m_Name);
                Compare(diff, "gender", d.CustomGender, m_Gender);
                Compare(diff, "voice", d.CustomAsks, m_Asks);
                Compare(diff, "left-handed", d.LeftHandedOverride, m_LeftHanded);
                Compare(diff, "model", d.CustomPrefabGuid, m_Prefab);
                Compare(diff, "birthday", d.BirthDay, m_BirthDay);
                Compare(diff, "birth month", d.BirthMonth, m_BirthMonth);
#if KINGMAKER
                Compare(diff, "doll", d.Doll, m_Doll);
                Compare(diff, "alignment", d.Alignment.Vector, m_Alignment);
                string alignment = d.Alignment.Value.ToString();
#else
                string alignment = d.Alignment.ValueRaw.ToString();
#endif
                if (diff.Count > 0) Apply(d);
                Main.Log.Log($"respec: {unit.CharacterName} kept name, voice, looks, birthday and alignment {alignment}" +
                    (diff.Count == 0 ? "" : $" (fixed after the copy: {string.Join("; ", diff.ToArray())}; the model may need an area reload)"));
            }
            catch (Exception e)
            {
                Main.Log.Error(e.ToString());
            }
        }

        static void Compare(List<string> diff, string field, object actual, object kept)
        {
            if (!Equals(actual, kept))
                diff.Add($"{field} {Show(actual)} -> {Show(kept)}");
        }

        static string Show(object value)
        {
            if (value == null) return "none";
            var blueprint = value as BlueprintScriptableObject;
            return blueprint != null ? blueprint.name : value.ToString();
        }
    }

    // A cancelled selection must not leave the identity snapshot pending for the next respec from a dialog.
    [HarmonyPatch(typeof(RespecCompanion), nameof(RespecCompanion.RunAction))]
    static class RespecActionPatch
    {
        static void Prefix()
        {
            Respec.Pending = false;
        }
    }
}
