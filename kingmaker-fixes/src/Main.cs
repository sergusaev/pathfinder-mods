using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Root.Strings.GameLog;
using Kingmaker.Globalmap;
using Kingmaker.RuleSystem.Rules;
using Kingmaker.UI._ConsoleUI.CombatLog;
using Kingmaker.UI._ConsoleUI.GlobalMap;
using Kingmaker.UI.Log;
using UnityModManagerNet;

namespace KingmakerFixes
{
    public static class Main
    {
        internal static UnityModManager.ModEntry.ModLogger Log;

        public static bool Load(UnityModManager.ModEntry modEntry)
        {
            Log = modEntry.Logger;
            new Harmony(modEntry.Info.Id).PatchAll(typeof(Main).Assembly);
            return true;
        }
    }

    // Both combat logs (console and PC) write "... is immune to <buff> thanks to the game's difficulty settings" for
    // every visible buff applied through RuleApplyBuff, without looking at RuleApplyBuff.StoryModeImmunity, the flag the
    // rule sets when the difficulty really kept the buff off (the log of RuleDrainEnergy does check its flag). The line
    // is now written only when that flag is set; the rest of the handler is untouched.
    [HarmonyPatch]
    static class StoryModeImmunityLogLine
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(CombatLogVM), "OnEventDidTrigger", new[] { typeof(RuleApplyBuff) });
            yield return AccessTools.Method(typeof(BattleLogManager), "OnEventDidTrigger", new[] { typeof(RuleApplyBuff) });
        }

        // The line is "ldarg.0; ldfld <log>; call get_LogStrings; ldfld StoryModeBuffImmunity; ... callvirt AddLogEntry".
        // Before it: "if (!evt.StoryModeImmunity) goto <after AddLogEntry>".
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator il, MethodBase original)
        {
            var code = new List<CodeInstruction>(instructions);
            var field = AccessTools.Field(typeof(GameLogStrings), "StoryModeBuffImmunity");
            int at = code.FindIndex(c => c.opcode == OpCodes.Ldfld && Equals(c.operand, field));
            int start = at - 3;
            int add = at < 0 ? -1 : code.FindIndex(at, c => (c.opcode == OpCodes.Callvirt || c.opcode == OpCodes.Call)
                && c.operand is MethodInfo && ((MethodInfo)c.operand).Name == "AddLogEntry");
            if (at < 3 || code[start].opcode != OpCodes.Ldarg_0 || add < 0 || add + 1 >= code.Count)
            {
                Main.Log.Error("story mode immunity line: unexpected code in " + original.DeclaringType.Name + ", left as is");
                return code;
            }
            var skip = il.DefineLabel();
            code[add + 1].labels.Add(skip);
            var guard = new List<CodeInstruction>
            {
                new CodeInstruction(OpCodes.Ldarg_1),
                new CodeInstruction(OpCodes.Callvirt, AccessTools.PropertyGetter(typeof(RuleApplyBuff), "StoryModeImmunity")),
                new CodeInstruction(OpCodes.Brfalse, skip)
            };
            // Jumps to the line now land on the check.
            guard[0].labels.AddRange(code[start].labels);
            code[start].labels.Clear();
            code.InsertRange(start, guard);
            Main.Log.Log("story mode immunity line: fixed in " + original.DeclaringType.Name);
            return code;
        }
    }

    // RuleAttackRoll.IsSuccessRoll(d20) adds the rule's own Roll instead of the d20 it is given. The attack roll itself
    // calls it with Roll, so attacks are not affected; but "reroll only if failed" (ModifyD20) asks it about the
    // pre-rolled die before Roll is set, so every die but a natural 20 counted as failed: the reroll was made on hits
    // too, and a single-use reroll was spent on a hit. Wrath of the Righteous uses the d20 here.
    [HarmonyPatch(typeof(RuleAttackRoll), nameof(RuleAttackRoll.IsSuccessRoll))]
    static class AttackSuccessRollUsesTheDie
    {
        static bool Prefix(RuleAttackRoll __instance, int d20, ref bool __result)
        {
            __result = d20 > 1 && (d20 == 20 || d20 + __instance.AttackBonus >= __instance.TargetAC);
            return false;
        }
    }

    // The arrows that point back to the party on the gamepad global map start updating every frame before the global
    // map rules exist, and throw NullReferenceException (a burst of "Error in Observer" in the log on entering the map).
    // Skip the update until the rules, the party pawn and the camera are there; then it runs as before.
    [HarmonyPatch(typeof(GlobalMapCenterArrowsVm), "OnUpdateHandler")]
    static class CenterArrowsBeforeMapLoaded
    {
        static bool Prefix()
        {
            var rules = GlobalMapRules.Instance;
            return rules != null && rules.Pawn != null && Game.Instance != null && Game.Instance.UI != null
                && Game.Instance.UI.GetCameraRig() != null;
        }
    }
}
