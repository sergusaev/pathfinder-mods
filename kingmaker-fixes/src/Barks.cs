using HarmonyLib;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Visual.Sound;

namespace KingmakerFixes
{
    // An animation can ask for a voiced line of a unit whose view is not on the scene yet (seen while an area loads).
    // Bark.Play marks the line as playing and the unit as busy, then throws NullReferenceException on the missing view,
    // so neither mark is ever cleared: that line never plays again and the unit's other ordinary lines are blocked as
    // "another one is playing". Without the view a voiced line is skipped untouched, and plays the next time it is asked;
    // a line without sound needs no view and plays as before.
    [HarmonyPatch(typeof(UnitAsksComponent.Bark), "Play")]
    static class BarkWithoutView
    {
        static readonly AccessTools.FieldRef<UnitAsksComponent, UnitEntityData> Unit =
            AccessTools.FieldRefAccess<UnitAsksComponent, UnitEntityData>("m_Unit");

        static bool Prefix(UnitAsksComponent.Bark __instance, UnitAsksComponent.BarkEntry entry)
        {
            if (entry == null || string.IsNullOrEmpty(entry.AkEvent)) return true;
            var owner = __instance.Owner;
            var unit = owner != null ? Unit(owner) : null;
            return unit != null && unit.View != null;
        }
    }
}
