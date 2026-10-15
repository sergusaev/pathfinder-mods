using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using Kingmaker.Assets.UI.Utils;
using Kingmaker.UI._ConsoleUI.Overtips;
using UniRx;

namespace KingmakerFixes
{
    // Gamepad interface: each part of a unit's overtip (name, hit points, action...) is switched on and off by the unit's
    // visibility, subscribed to in OvertipViewPartBase.BindWithSettings. The subscription is dropped (Subscribe, then pop)
    // and never disposed. Overtips are pooled, so after an overtip is bound to another unit the old unit still switches
    // the part: when it goes out of sight the part of the new unit is switched off while it is shown, and the hit points
    // of that unit stay hidden. The subscription is kept with the binding now, disposed when the part is bound again, and
    // a change of a unit the part no longer shows is ignored.
    [HarmonyPatch(typeof(OvertipViewPartBase), nameof(OvertipViewPartBase.BindWithSettings))]
    static class OvertipPartVisibility
    {
        static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var list = new List<CodeInstruction>(instructions);
            int found = 0;
            for (int i = 0; i + 1 < list.Count; i++)
            {
                var method = list[i].operand as MethodInfo;
                if (list[i].opcode != OpCodes.Call || method == null || method.Name != "Subscribe" || list[i + 1].opcode != OpCodes.Pop) continue;
                if (method.GetGenericArguments().Length != 1 || method.GetGenericArguments()[0] != typeof(bool)) continue;
                // [observable, action] -> Keep(observable, action, this, viewModel)
                list[i] = new CodeInstruction(OpCodes.Ldarg_0) { labels = list[i].labels };
                list[i + 1] = new CodeInstruction(OpCodes.Ldarg_1);
                list.Insert(i + 2, new CodeInstruction(OpCodes.Call, AccessTools.Method(typeof(OvertipPartVisibility), nameof(Keep))));
                found++;
            }
            if (found != 1) Main.Log.Error("overtip part visibility: " + found + " subscriptions found instead of 1, left as it is");
            return found == 1 ? list : new List<CodeInstruction>(instructions);
        }

        static readonly MethodInfo AddDisposable = AccessTools.Method(typeof(ViewBase<EntityOvertipVM>), "AddDisposable");
        static readonly PropertyInfo ViewModel = AccessTools.Property(typeof(ViewBase<EntityOvertipVM>), "ViewModel");
        static int s_Ignored;

        static void Keep(IObservable<bool> visible, Action<bool> switchPart, OvertipViewPartBase part, EntityOvertipVM viewModel)
        {
            var subscription = visible.Subscribe(v =>
            {
                if (part != null && ReferenceEquals(ViewModel.GetValue(part, null), viewModel))
                {
                    switchPart(v);
                    return;
                }
                // Disposed with the binding, this is not expected; written a few times to show it if it happens.
                if (s_Ignored++ < 5) Main.Log.Log("overtip part: visibility of a unit it no longer shows ignored");
            });
            AddDisposable.Invoke(part, new object[] { subscription });
        }
    }
}
