using System;
using HarmonyLib;
using Kingmaker.Assets.UI.Utils;
using Kingmaker.UI.SettingsUI;
using Kingmaker.UI.Overtip;
using Kingmaker.UI._ConsoleUI.Overtips;
using TurnBased.Controllers;
using DG.Tweening;

namespace SuccessChance
{
    // The gamepad interface shows the parts of a unit's overtip (name, hit points, action, cooldown) by its own rules:
    // in turn-based combat it ignores the settings and shows hit points and the name of whoever is pointed at or acts.
    // Here its parts follow the rules of the keyboard and mouse interface instead (OvertipController,
    // OvertipComponent.CheckSettings and its overrides), so the overtips look the same in both, as the settings say.
    static class OvertipRules
    {
        static readonly Func<ViewBase<EntityOvertipVM>, EntityOvertipVM> s_ViewModel = (Func<ViewBase<EntityOvertipVM>, EntityOvertipVM>)
            Delegate.CreateDelegate(typeof(Func<ViewBase<EntityOvertipVM>, EntityOvertipVM>), AccessTools.PropertyGetter(typeof(ViewBase<EntityOvertipVM>), "ViewModel"));

        // OvertipComponent.CheckSettings: the party or enemy setting of the part, in real time and in turn-based combat
        // alike.
        static bool Settings(OvertipViewPartBase part, EntityOvertipVM vm)
        {
            var setting = vm.IsEnemy ? part.EnemySetting : part.PartySetting;
            if (setting == null) return true;
            switch (setting.CurrentState)
            {
                case SettingsEntityDropdownState.DropdownState.Never: return false;
                case SettingsEntityDropdownState.DropdownState.Interaction:
                    return vm.ObjectIsHovered.Value || !vm.IsEnemy && vm.IsSingleSelected && !SettingsRoot.Instance.StartGameImmersion.CurrentValue;
                case SettingsEntityDropdownState.DropdownState.ForcedShow: return vm.ForceHotKeyPressed.Value;
                case SettingsEntityDropdownState.DropdownState.OnPause: return vm.IsPause;
                case SettingsEntityDropdownState.DropdownState.Always: return true;
                default: return false;
            }
        }

        // The parts are asked every frame: no allocation here.
        static EntityOvertipVM Model(OvertipViewPartBase part) => s_ViewModel(part);

        // In combat the name follows its setting (OvertipController.NeedName asks m_OvertipName); out of combat the
        // overtip decides by hovering itself (UnitOvertipView.NeedName), as the keyboard interface does.
        [HarmonyPatch(typeof(OvertipViewPartName), nameof(OvertipViewPartName.NeedShow))]
        static class Name
        {
            static bool Prefix(OvertipViewPartName __instance, ref bool __result)
            {
                var vm = Model(__instance);
                if (vm == null) return true;
                __result = Settings(__instance, vm);
                return false;
            }
        }

        // The name part's Show is empty, so its IsShown never changes, and the overtip fades the name in or out
        // (UnitOvertipView.TweenComponentsToPlace) only when some other part appears or disappears. When hit points
        // are always shown, nothing else changes as the cursor leaves a unit, and its name stayed over its head. Here
        // the name's own show and the hovering (NeedName out of combat) lay the overtip out too, once on each change.
        [HarmonyPatch(typeof(OvertipViewPartName), nameof(OvertipViewPartName.Show))]
        static class NameShown
        {
            static readonly System.Collections.Generic.Dictionary<OvertipViewPartName, bool> s_Hovered =
                new System.Collections.Generic.Dictionary<OvertipViewPartName, bool>();

            static void Postfix(OvertipViewPartName __instance, bool state)
            {
                try
                {
                    // A change of IsShown lays the overtip out by itself (UnitOvertipView.BindViewImplementation).
                    __instance.IsShown.Value = state;
                    var vm = Model(__instance);
                    if (vm == null) return;
                    bool hovered = vm.ObjectIsHovered.Value, last;
                    if (s_Hovered.TryGetValue(__instance, out last) && last == hovered) return;
                    if (s_Hovered.Count > 500) s_Hovered.Clear();
                    s_Hovered[__instance] = hovered;
                    __instance.GetComponentInParent<UnitOvertipView>()?.TweenComponentsToPlace();
                }
                catch (Exception e)
                {
                    if (!s_NameFailed) Main.Error("name overtip", e);
                    s_NameFailed = true;
                }
            }
        }

        static bool s_NameFailed;

        // The gamepad settings have checkboxes to always show the hit points of enemies or companions, which the keyboard
        // interface has not: they are kept, over the keyboard rule.
        static bool HitPoints(OvertipViewPartBase part, EntityOvertipVM vm)
        {
            var settings = SettingsRoot.Instance;
            return (vm.IsEnemy ? settings.AlwaysShowEnemyHitPoints.CurrentValue : settings.AlwaysShowCompanioinHitPoints.CurrentValue) || Settings(part, vm);
        }

        static bool IsHPShort(EntityOvertipVM vm)
        {
            return vm.IsEnemy ? SettingsRoot.Instance.EnemiesHPIsShort.CurrentValue : SettingsRoot.Instance.PartyHPIsShort.CurrentValue;
        }

        [HarmonyPatch(typeof(OvertipViewPartStandartHitPoints), nameof(OvertipViewPartStandartHitPoints.NeedShow))]
        static class StandardHitPoints
        {
            static bool Prefix(OvertipViewPartStandartHitPoints __instance, ref bool __result)
            {
                var vm = Model(__instance);
                if (vm == null) return true;
                __result = !IsHPShort(vm) && HitPoints(__instance, vm);
                return false;
            }
        }

        [HarmonyPatch(typeof(OvertipViewPartShortHitPoints), nameof(OvertipViewPartShortHitPoints.NeedShow))]
        static class ShortHitPoints
        {
            static bool Prefix(OvertipViewPartShortHitPoints __instance, ref bool __result)
            {
                var vm = Model(__instance);
                if (vm == null) return true;
                __result = IsHPShort(vm) && HitPoints(__instance, vm);
                return false;
            }
        }

        // OvertipAction.CheckSettings.
        [HarmonyPatch(typeof(OvertipViewPartAction), nameof(OvertipViewPartAction.NeedShow))]
        static class Action
        {
            static bool Prefix(OvertipViewPartAction __instance, ref bool __result)
            {
                var vm = Model(__instance);
                if (vm == null) return true;
                __result = Show(__instance, vm);
                return false;
            }

            static bool Show(OvertipViewPartAction part, EntityOvertipVM vm)
            {
                if (Settings(part, vm)) return true;
                var settings = SettingsRoot.Instance;
                var party = settings.ShowPartyActions.CurrentState;
                bool idle = party == SettingsEntityDropdownState.DropdownState.Idleness && !vm.IsEnemy && part.StandartCommand == null;
                bool never = vm.IsEnemy ? settings.ShowEnemyActions.CurrentState == SettingsEntityDropdownState.DropdownState.Never
                    : party == SettingsEntityDropdownState.DropdownState.Never;
                bool show = (idle || vm.ObjectIsHovered.Value || !settings.StartGameImmersion.CurrentValue && vm.IsSelected) && !never;
                if (show && CombatController.IsInTurnBasedCombat()) show = CombatController.CurrentUnit != null && CombatController.CurrentUnit == vm.Unit;
                return show;
            }
        }

        // OvertipCooldown.CheckSettings: its setting, never in turn-based combat. The cooldown part has no rule of its
        // own, so the base one is taken over for it alone.
        [HarmonyPatch(typeof(OvertipViewPartBase), nameof(OvertipViewPartBase.NeedShow))]
        static class Cooldown
        {
            static bool Prefix(OvertipViewPartBase __instance, ref bool __result)
            {
                if (!(__instance is OvertipViewPartCooldown)) return true;
                var vm = Model(__instance);
                if (vm == null) return true;
                __result = Settings(__instance, vm) && !CombatController.IsInTurnBasedCombat();
                return false;
            }
        }

        // The keyboard overtip lifts its combat text (a miss, damage, the chance) right onto the edge of the name or the
        // combat panel (OvertipController.TweenComponentsToPlace): it is lifted a little more, so it does not stick to it.
        const float Gap = 8;

        static readonly System.Reflection.FieldInfo s_PcCooldown = AccessTools.Field(typeof(OvertipController), "m_OvertipCooldown");
        static readonly System.Reflection.FieldInfo s_PcStandardHp = AccessTools.Field(typeof(OvertipController), "m_OvertipStandartHitPoints");
        static readonly System.Reflection.FieldInfo s_PcShortHp = AccessTools.Field(typeof(OvertipController), "m_OvertipShortHitPoints");
        static readonly System.Reflection.FieldInfo s_PcCombatPanel = AccessTools.Field(typeof(OvertipController), "m_CombatPanel");

        [HarmonyPatch(typeof(OvertipController), nameof(OvertipController.TweenComponentsToPlace))]
        static class CombatTextLift
        {
            static void Postfix(OvertipController __instance)
            {
                try
                {
                    if (__instance.CombatText == null || __instance.Unit == null) return;
                    // As the game reckons it: the name +28, the combat panel +38, a bark its height +22.
                    bool name = __instance.NeedName();
                    bool hp = (s_PcStandardHp.GetValue(__instance) as OvertipComponent)?.IsShowed == true
                        || (s_PcShortHp.GetValue(__instance) as OvertipComponent)?.IsShowed == true;
                    bool cooldown = (s_PcCooldown.GetValue(__instance) as OvertipComponent)?.IsShowed == true;
                    bool panel = __instance.IsActionShowed || hp || cooldown;
                    LogLayout(__instance, name, panel);
                    if (!name && !panel) return;
                    float y = (name ? 28 : 0) + (panel ? 38 : 0) + Gap
                        + (__instance.BarkVisible ? __instance.BarkContainer.sizeDelta.y + 22 : 0);
                    var rect = (UnityEngine.RectTransform)__instance.CombatText.transform;
                    rect.DOKill();
                    rect.DOAnchorPosY(y, __instance.TweenDuration).SetUpdate(isIndependentUpdate: true);
                }
                catch (Exception e)
                {
                    if (!s_LiftFailed) Main.Error("combat text lift", e);
                    s_LiftFailed = true;
                }
            }
        }

        static bool s_LiftFailed;
        static readonly System.Collections.Generic.Dictionary<OvertipController, string> s_Layouts = new System.Collections.Generic.Dictionary<OvertipController, string>();

        // Each new layout of an overtip: what it shows and where its parts stand when it is laid out (for the log).
        static void LogLayout(OvertipController overtip, bool name, bool panel)
        {
            if (!Main.Settings.LogAttacks) return;
            var combatPanel = s_PcCombatPanel.GetValue(overtip) as UnityEngine.RectTransform;
            string line = string.Format("NeedName {0} (hovered {1}, hotkey {2}, combat {3}, panel visible {4}), name alpha {5:0.00}; action {6}, combat panel shown {7}, bark {8}; panel at {9:0}, text at {10:0}",
                name, overtip.ObjectIsHovered, overtip.ForceHotKeyPressed, overtip.IsInCombat, overtip.CombatPanelVisible, overtip.NamePanel.alpha,
                overtip.IsActionShowed, panel, overtip.BarkVisible, combatPanel != null ? combatPanel.anchoredPosition.y : float.NaN,
                ((UnityEngine.RectTransform)overtip.CombatText.transform).anchoredPosition.y);
            string last;
            if (s_Layouts.TryGetValue(overtip, out last) && last == line) return;
            s_Layouts[overtip] = line;
            ChanceLog.Write("overtip " + AttackCheck.Name(overtip.Unit) + ": " + line);
        }
    }
}
