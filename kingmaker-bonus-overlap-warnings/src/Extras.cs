using System.Collections.Generic;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Items.Components;
using Kingmaker.Blueprints.Items.Ecnchantments;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Designers.Mechanics.Buffs;
using Kingmaker.Designers.Mechanics.EquipmentEnchants;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.Items;
using Kingmaker.UnitLogic.FactLogic;

namespace BonusOverlapWarnings
{
    // An item's properties no bonus can be compared with: the ability it lets cast, a resistance, an immunity, attacks
    // of a weapon. They work whatever the item's bonuses do and are lost with the item. Bonuses to stats and the
    // enhancement are counted on their own (Overlaps) and are not among them.
    internal static class Extras
    {
        internal static List<string> Of(ItemEntity item)
        {
            var list = new List<string>();
            if (item == null || !item.IsIdentified) return list;
            if (item is ItemEntityWeapon) list.Add(Strings.WeaponAttacks);

            var equipment = item.Blueprint as BlueprintItemEquipment;
            if (equipment != null)
            {
                if (equipment.Ability != null) Put(list, Strings.Kind(SourceKind.Ability) + Strings.Quote(Named(equipment.Ability.Name, item.Name)));
                if (equipment.ActivatableAbility != null) Put(list, Strings.Kind(SourceKind.Ability) + Strings.Quote(Named(equipment.ActivatableAbility.Name, item.Name)));
            }
            foreach (var component in item.Blueprint.ComponentsArray)
            {
                var add = component as AddFactToEquipmentWielder;
                if (add != null && add.Fact != null && !OnlyBonuses(add.Fact)) Put(list, Quoted(add.Fact.Name));
            }

            Enchantments(item, list);
            var shield = item as ItemEntityShield;
            if (shield != null)
            {
                if (shield.ArmorComponent != null) Enchantments(shield.ArmorComponent, list);
                if (shield.WeaponComponent != null)
                {
                    Put(list, Strings.WeaponAttacks);
                    Enchantments(shield.WeaponComponent, list);
                }
            }
            return list;
        }

        // The item's own enchantments (not the ones buffs put on it): each with something more than bonuses.
        static void Enchantments(ItemEntity item, List<string> list)
        {
            foreach (var enchantment in item.Enchantments)
            {
                if (enchantment.ParentContext != null) continue;
                var blueprint = enchantment.Blueprint;
                if (blueprint == null) continue;
                foreach (var component in blueprint.ComponentsArray)
                {
                    if (component == null || IsBonus(component)) continue;
                    var feature = component as AddUnitFeatureEquipment;
                    if (feature != null)
                    {
                        if (feature.Feature != null && !OnlyBonuses(feature.Feature)) Put(list, Quoted(Named(feature.Feature.Name, enchantment.Name)));
                        continue;
                    }
                    var fact = component as AddUnitFactEquipment;
                    if (fact != null)
                    {
                        if (fact.Blueprint != null && !OnlyBonuses(fact.Blueprint)) Put(list, Quoted(Named(fact.Blueprint.Name, enchantment.Name)));
                        continue;
                    }
                    Put(list, Quoted(enchantment.Name));
                }
            }
        }

        // The components of an enchantment that give bonuses to stats or the enhancement: compared, not extras.
        static bool IsBonus(BlueprintComponent component)
        {
            return component is AddStatBonusEquipment || component is AddStatBonusEquipmentUnlessEnchant || component is IncreaseStatEquipment
                || component is AllSavesBonusEquipment || component is WeaponEnhancementBonus || component is ArmorEnhancementBonus;
        }

        // A fact an item gives that does nothing but bonuses to stats.
        static bool OnlyBonuses(BlueprintFact fact)
        {
            foreach (var component in fact.ComponentsArray)
                if (component != null && !(component is AddStatBonus) && !(component is AddContextStatBonus) && !(component is BuffAllSavesBonus))
                    return false;
            return true;
        }

        // The item's properties its description does not tell, said in words. Bracers of armour: a nameless feature
        // (IncorporealACFeature) adds their bonus to AC against incorporeal touch attacks, where armour does not count
        // (ACBonusAgainstWeaponType, of the type Other, put on only for the attack).
        internal static List<string> Unlisted(ItemEntity item)
        {
            var list = new List<string>();
            if (item == null || !item.IsIdentified) return list;
            foreach (var enchantment in item.Enchantments)
            {
                if (enchantment.ParentContext != null || enchantment.Blueprint == null) continue;
                foreach (var feature in enchantment.Blueprint.GetComponents<AddUnitFeatureEquipment>())
                {
                    if (feature.Feature == null) continue;
                    foreach (var bonus in feature.Feature.GetComponents<ACBonusAgainstWeaponType>())
                    {
                        if (bonus.ArmorClassBonus <= 0) continue;
                        string type = bonus.Type != null ? (string)bonus.Type.TypeName : null;
                        Put(list, Strings.AcAgainst(bonus.ArmorClassBonus, bonus.Type != null && bonus.Type.name == "IncoporealTouchType" ? null : type));
                    }
                }
            }
            return list;
        }

        static string Named(string name, string fallback)
        {
            return string.IsNullOrEmpty(name) ? fallback : name;
        }

        // A nameless property is said as such.
        static string Quoted(string name)
        {
            return string.IsNullOrEmpty(name) ? Strings.SpecialProperty : Strings.Quote(name);
        }

        static void Put(List<string> list, string text)
        {
            if (!list.Contains(text)) list.Add(text);
        }
    }
}
