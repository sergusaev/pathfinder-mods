using System.Collections.Generic;
using System.Linq;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Facts;
using Kingmaker.Blueprints.Items.Ecnchantments;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.Enums;
using Kingmaker.Items;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Buffs;
using Kingmaker.UnitLogic.Mechanics;

namespace BonusOverlapWarnings
{
    // What the overlaps of an equipped item come to, the worst first.
    internal enum Verdict
    {
        None,
        // Every bonus of the item works, and one of them leaves another source's bonus without use.
        Overrides,
        // Some bonuses of the item work, some do not.
        Reduced,
        // No bonus of the item adds anything: each is lower than or the same as another source's of its type.
        Lost
    }

    internal enum SourceKind
    {
        Unknown,
        Item,
        // A buff: a spell, a potion, an aura; it ends.
        Effect,
        // A lasting fact: a class feature, a feat, an ability.
        Ability
    }

    // One source of bonuses on a character: an item, a buff, a feature. A fact given by an item, even another
    // character's (the master's ring giving the pet a feature), is that item.
    internal sealed class Source
    {
        // The equipped item of the character counted, whose report this is; null for any other source.
        internal ItemEntity Item;
        internal SourceKind Kind;
        internal string Name;
        // Whose it is when not the character's own: who cast the buff, who wears the item, whose feature it is.
        internal string Owner;

        internal string Label
        {
            get
            {
                return Labelled(Strings.Kind(Kind));
            }
        }

        // "By ...": the kind in the instrumental case.
        internal string LabelBy
        {
            get { return Labelled(Strings.KindBy(Kind)); }
        }

        string Labelled(string kind)
        {
            var text = kind + Strings.Quote(Name);
            return string.IsNullOrEmpty(Owner) ? text : text + " (" + Owner + ")";
        }
    }

    // A bonus of another source an item's bonus meets: on one stat, or on the item itself (its enhancement).
    internal struct Rival
    {
        internal Source Source;
        internal int Value;
    }

    internal enum LineKind
    {
        // Lower than a rival's: no effect.
        Lost,
        // The same as a rival's that is kept (a lasting ability, an item of its own properties): adds nothing.
        Tie,
        // The same as another worn item's, and each has properties of its own: the bonus counts once, both are kept.
        Shared,
        // Higher than every rival's: the rivals have no effect.
        Overrides,
        // The same as the rivals', the item is the one kept: they add nothing.
        OverridesTie
    }

    // One finding about an item: its bonus of a type, on the stats listed, against the other sources' of that type.
    internal sealed class Line
    {
        internal LineKind Kind;
        internal ModifierDescriptor Descriptor;
        // The item's enhancement bonus as a weapon or armour, against the enhancement others put on it.
        internal bool OnItem;
        internal int Value;
        internal readonly List<StatType> Stats = new List<StatType>();
        internal readonly List<Rival> Rivals = new List<Rival>();
        internal string Key;

        internal bool Lost
        {
            get { return Kind == LineKind.Lost || Kind == LineKind.Tie; }
        }
    }

    internal sealed class Report
    {
        internal Verdict Verdict;
        // The item has properties no bonus can be compared with (an ability, a resistance, attacks of a weapon) that
        // work whatever its bonuses do, and either its bonuses are lost or another item's equal one is kept as well.
        internal bool Green;
        internal readonly List<Line> Lines = new List<Line>();
        // Its properties of its own, named: they work and are lost when the item is taken off.
        internal readonly List<string> Extras = new List<string>();
        // The item's bonuses counted: (stat, type) pairs plus its enhancement, and how many of them add nothing.
        internal int Bonuses;
        internal int LostBonuses;
    }

    // The overlaps of a character's equipped items, counted from the modifiers on the character's stats as the game
    // applies them (ModifiableValue.ApplyModifiersFiltered): of the bonuses of one type that do not stack on one stat
    // only the highest value counts, once, whoever gives it; equal bonuses do not cancel each other and the order they
    // were put on in changes nothing. Which one of equal items is kept is the mod's choice: the one with properties of
    // its own. Nothing is kept between the counts and nothing of the game's is followed.
    internal static class Overlaps
    {
        static readonly Dictionary<UnitDescriptor, Dictionary<ItemEntity, Report>> s_Units = new Dictionary<UnitDescriptor, Dictionary<ItemEntity, Report>>();
        static int s_Frame = -1;

        // The bonuses of one type on one stat (or the enhancement of one item), the highest of each source, in the
        // order the game lists them.
        sealed class Group
        {
            internal ModifierDescriptor Descriptor;
            internal StatType Stat;
            internal bool OnItem;
            internal readonly Dictionary<Source, int> Best = new Dictionary<Source, int>();
            internal readonly List<Source> Order = new List<Source>();

            internal void Put(Source source, int value)
            {
                int seen;
                if (!Best.TryGetValue(source, out seen)) { Order.Add(source); Best[source] = value; }
                else if (value > seen) Best[source] = value;
            }
        }

        // The item's report while it is equipped, null for an item no one wears or one without overlaps.
        internal static Report Of(ItemEntity item)
        {
            // Counted once a frame at most: a doll showing its slots asks for each.
            if (UnityEngine.Time.frameCount != s_Frame)
            {
                s_Frame = UnityEngine.Time.frameCount;
                s_Units.Clear();
            }
            var unit = item != null ? item.Wielder : null;
            if (unit == null) return null;
            Dictionary<ItemEntity, Report> reports;
            if (!s_Units.TryGetValue(unit, out reports))
            {
                reports = Count(unit);
                s_Units[unit] = reports;
            }
            Report report;
            return reports.TryGetValue(item, out report) ? report : null;
        }

        internal static void Clear()
        {
            s_Units.Clear();
        }

        static Dictionary<ItemEntity, Report> Count(UnitDescriptor unit)
        {
            var items = new List<ItemEntity>();
            foreach (var slot in unit.Body.CurrentEquipmentSlots)
                if (slot != null && slot.HasItem && slot.Active && !items.Contains(slot.Item)) items.Add(slot.Item);
            var reports = new Dictionary<ItemEntity, Report>();
            if (items.Count == 0) return reports;

            var sources = new Sources(unit, items);
            var extras = new Dictionary<ItemEntity, List<string>>();
            foreach (var item in items) extras[item] = Extras.Of(item);
            var groups = new List<Group>();
            foreach (var stat in unit.Stats.GetList())
                CollectStat(stat, sources, reports, groups);
            foreach (var item in items)
                CollectEnhancement(item, sources, groups);
            foreach (var group in groups)
                foreach (var source in group.Order)
                    if (source.Item != null) Judge(ReportOf(reports, source.Item), source, group, extras);

            foreach (var pair in reports)
            {
                var report = pair.Value;
                if (report.LostBonuses > 0)
                    report.Verdict = report.LostBonuses >= report.Bonuses ? Verdict.Lost : Verdict.Reduced;
                else if (report.Lines.Count > 0)
                    report.Verdict = Verdict.Overrides;
                if (report.Verdict == Verdict.None) continue;
                report.Extras.AddRange(extras[pair.Key]);
                report.Green = report.Extras.Count > 0
                    && (report.Verdict != Verdict.Overrides || report.Lines.Any(l => l.Kind == LineKind.Shared));
                // Lost first, then what the item shares and overrides.
                report.Lines.Sort((a, b) => a.Kind.CompareTo(b.Kind));
            }
            var empty = reports.Where(p => p.Value.Verdict == Verdict.None).Select(p => p.Key).ToList();
            foreach (var item in empty) reports.Remove(item);
            return reports;
        }

        static Report ReportOf(Dictionary<ItemEntity, Report> reports, ItemEntity item)
        {
            Report report;
            if (!reports.TryGetValue(item, out report)) reports[item] = report = new Report();
            return report;
        }

        static void CollectStat(ModifiableValue stat, Sources sources, Dictionary<ItemEntity, Report> reports, List<Group> groups)
        {
            var byType = new Dictionary<ModifierDescriptor, Group>();
            foreach (var mod in stat.Modifiers)
            {
                if (mod == null || mod.ModValue <= 0 || mod.ModDescriptor == ModifierDescriptor.Helpless) continue;
                var source = sources.Of(mod);
                if (mod.Stacks)
                {
                    // Always works: one more bonus of the item that is not lost.
                    if (source.Item != null) ReportOf(reports, source.Item).Bonuses++;
                    continue;
                }
                Group group;
                if (!byType.TryGetValue(mod.ModDescriptor, out group))
                {
                    byType[mod.ModDescriptor] = group = new Group { Descriptor = mod.ModDescriptor, Stat = stat.Type };
                    groups.Add(group);
                }
                group.Put(source, mod.ModValue);
            }
        }

        // A weapon's or armour's own enhancement against the one others put on it (Magic Weapon, Magic Vestment):
        // the game takes the highest of the enchantments, the stacking ones aside.
        static void CollectEnhancement(ItemEntity item, Sources sources, List<Group> groups)
        {
            var target = item;
            var shield = item as ItemEntityShield;
            if (shield != null) target = shield.ArmorComponent;
            if (!(target is ItemEntityWeapon) && !(target is ItemEntityArmor)) return;
            if (!target.IsIdentified) return;
            var group = new Group { Descriptor = ModifierDescriptor.Enhancement, Stat = StatType.Unknown, OnItem = true };
            int own = 0;
            foreach (var enchantment in target.Enchantments)
            {
                int value = 0;
                if (target is ItemEntityWeapon)
                {
                    foreach (var bonus in enchantment.SelectComponents<WeaponEnhancementBonus>())
                        if (!bonus.Stack && bonus.EnhancementBonus > value) value = bonus.EnhancementBonus;
                }
                else
                {
                    var bonus = enchantment.Get<ArmorEnhancementBonus>();
                    if (bonus != null) value = bonus.EnhancementValue;
                }
                if (value <= 0) continue;
                if (enchantment.ParentContext == null) { if (value > own) own = value; }
                else group.Put(sources.OfContext(enchantment.ParentContext, enchantment), value);
            }
            if (own <= 0 || group.Order.Count == 0) return;
            group.Order.Insert(0, sources.OfOwnItem(item));
            group.Best[group.Order[0]] = own;
            groups.Add(group);
        }

        // The item's bonus against the others' of its type in the group.
        //  Higher than all: it overrides them. Lower than one: it has no effect, that one named.
        //  The same as the highest: a lasting ability or another character's item is kept, the item adds nothing;
        //  buffs only: the buff adds nothing, the item's bonus stays when it ends; worn items: the one with properties
        //  of its own is kept, the others add nothing; several with properties of their own: each is kept, the bonus
        //  counts once; none with any: the first the game lists is kept.
        static void Judge(Report report, Source mine, Group group, Dictionary<ItemEntity, List<string>> extras)
        {
            report.Bonuses++;
            int value = group.Best[mine];
            var others = group.Order.Where(s => s != mine).Select(s => new Rival { Source = s, Value = group.Best[s] }).ToList();
            if (others.Count == 0) return;
            int top = others.Max(r => r.Value);
            var highest = others.Where(r => r.Value == top).ToList();
            if (top > value) { Add(report, LineKind.Lost, group, value, highest); return; }
            if (top < value) { Add(report, LineKind.Overrides, group, value, others.OrderByDescending(r => r.Value)); return; }

            if (highest.Any(r => r.Source.Kind != SourceKind.Effect && r.Source.Item == null)) { Add(report, LineKind.Tie, group, value, highest); return; }
            var tiedItems = highest.Where(r => r.Source.Item != null).ToList();
            if (tiedItems.Count == 0) { Add(report, LineKind.OverridesTie, group, value, highest); return; }
            var candidates = new List<Source> { mine };
            candidates.AddRange(tiedItems.Select(r => r.Source));
            var own = candidates.Where(s => extras[s.Item].Count > 0).ToList();
            if (own.Count == 0)
            {
                var first = group.Order.First(s => candidates.Contains(s));
                if (first == mine) Add(report, LineKind.OverridesTie, group, value, highest);
                else Add(report, LineKind.Tie, group, value, highest.Where(r => r.Source == first));
            }
            else if (!own.Contains(mine))
                Add(report, LineKind.Tie, group, value, highest.Where(r => own.Contains(r.Source)));
            else if (own.Count == 1)
                Add(report, LineKind.OverridesTie, group, value, highest);
            else
                Add(report, LineKind.Shared, group, value, highest.Where(r => own.Contains(r.Source)));
        }

        // The same finding on several stats (a cloak of resistance on the three saves) is one line.
        static void Add(Report report, LineKind kind, Group group, int value, IEnumerable<Rival> rivals)
        {
            var list = rivals.ToList();
            if (kind == LineKind.Lost || kind == LineKind.Tie) report.LostBonuses++;
            var key = (int)kind + ":" + (int)group.Descriptor + (group.OnItem ? "I" : "") + value + ":"
                + string.Join(",", list.Select(r => r.Source.GetHashCode() + "=" + r.Value).ToArray());
            var line = report.Lines.FirstOrDefault(l => l.Key == key);
            if (line == null)
            {
                line = new Line { Kind = kind, Descriptor = group.Descriptor, OnItem = group.OnItem, Value = value, Key = key };
                line.Rivals.AddRange(list);
                report.Lines.Add(line);
            }
            if (!group.OnItem && !line.Stats.Contains(group.Stat)) line.Stats.Add(group.Stat);
        }

        // Whose a modifier is: the item it comes from by whatever way, or else its fact.
        sealed class Sources
        {
            readonly UnitDescriptor m_Unit;
            readonly HashSet<ItemEntity> m_Items;
            // By character: the contexts of its equipped items' own enchantments and of the facts the items give;
            // the facts made in them (a feature of an enchantment, a buff of an item's ability) are the item's too.
            readonly Dictionary<UnitDescriptor, Dictionary<MechanicsContext, ItemEntity>> m_Contexts = new Dictionary<UnitDescriptor, Dictionary<MechanicsContext, ItemEntity>>();
            readonly Dictionary<object, Source> m_Sources = new Dictionary<object, Source>();
            readonly Source m_Unknown;

            internal Sources(UnitDescriptor unit, List<ItemEntity> items)
            {
                m_Unit = unit;
                m_Items = new HashSet<ItemEntity>(items);
                m_Unknown = new Source { Kind = SourceKind.Unknown, Name = Strings.Unknown };
            }

            static IEnumerable<ItemEntity> Worn(UnitDescriptor unit)
            {
                foreach (var slot in unit.Body.CurrentEquipmentSlots)
                    if (slot != null && slot.HasItem && slot.Active) yield return slot.Item;
            }

            Dictionary<MechanicsContext, ItemEntity> ContextsOf(UnitDescriptor unit)
            {
                Dictionary<MechanicsContext, ItemEntity> map;
                if (m_Contexts.TryGetValue(unit, out map)) return map;
                map = new Dictionary<MechanicsContext, ItemEntity>();
                m_Contexts[unit] = map;
                foreach (var item in Worn(unit))
                {
                    AddEnchantments(map, item, item);
                    var shield = item as ItemEntityShield;
                    if (shield != null)
                    {
                        if (shield.ArmorComponent != null) AddEnchantments(map, shield.ArmorComponent, item);
                        if (shield.WeaponComponent != null) AddEnchantments(map, shield.WeaponComponent, item);
                    }
                    AddContext(map, item.Ability, item);
                    AddContext(map, item.ActivatableAbility, item);
                }
                foreach (var fact in Facts(unit))
                    if (fact.SourceItem != null) AddContext(map, fact, fact.SourceItem);
                return map;
            }

            static void AddEnchantments(Dictionary<MechanicsContext, ItemEntity> map, ItemEntity part, ItemEntity item)
            {
                foreach (var enchantment in part.Enchantments)
                {
                    if (enchantment.ParentContext != null || !enchantment.Active) continue;
                    var context = enchantment.MaybeContext;
                    if (context != null) map[context] = item;
                }
            }

            static void AddContext(Dictionary<MechanicsContext, ItemEntity> map, Fact fact, ItemEntity item)
            {
                if (fact == null || item == null) return;
                var context = fact.MaybeContext;
                if (context != null) map[context] = item;
            }

            static IEnumerable<Fact> Facts(UnitDescriptor unit)
            {
                foreach (var fact in unit.Logic.RawFacts) yield return fact;
                foreach (var fact in unit.Progression.Features.RawFacts) yield return fact;
                foreach (var fact in unit.Buffs.RawFacts) yield return fact;
            }

            // A shield's armour and weapon parts are the shield in the slot.
            static ItemEntity Whole(ItemEntity item, UnitDescriptor unit)
            {
                if (item == null) return null;
                var wearer = item.Wielder ?? unit;
                foreach (var worn in Worn(wearer))
                {
                    if (worn == item) return item;
                    var shield = worn as ItemEntityShield;
                    if (shield != null && (shield.ArmorComponent == item || shield.WeaponComponent == item)) return shield;
                }
                return item;
            }

            internal Source Of(ModifiableValue.Modifier mod)
            {
                if (mod.ItemSource != null) return OfItem(Whole(mod.ItemSource, m_Unit));
                if (mod.Source != null) return OfFact(mod.Source);
                return m_Unknown;
            }

            // The source of an enchantment put on an item from outside: the buff whose context it came in.
            internal Source OfContext(MechanicsContext context, ItemEnchantment enchantment)
            {
                foreach (var fact in m_Unit.Buffs.RawFacts)
                {
                    var buff = fact as Buff;
                    if (buff != null && buff.Context == context) return OfFact(buff);
                }
                Source source;
                if (m_Sources.TryGetValue(context, out source)) return source;
                var data = context.AssociatedBlueprint as Kingmaker.UI.IUIDataProvider;
                source = new Source
                {
                    Kind = SourceKind.Effect,
                    Name = data != null && !string.IsNullOrEmpty(data.Name) ? data.Name : enchantment.Name,
                    Owner = OtherName(context.MaybeCaster != null ? context.MaybeCaster.Descriptor : null)
                };
                m_Sources[context] = source;
                return source;
            }

            // The item a fact comes from: an enchantment's item, the item that gave the fact, or the item in whose
            // context the fact was made, of the context's own character or of its caster.
            ItemEntity ItemOf(Fact fact, UnitDescriptor unit)
            {
                var enchantment = fact as ItemEnchantment;
                if (enchantment != null) return Whole(enchantment.Owner, unit);
                if (fact.SourceItem != null) return Whole(fact.SourceItem, unit);
                var context = fact.MaybeContext;
                for (int depth = 0; context != null && depth < 16; depth++, context = context.ParentContext)
                {
                    ItemEntity item;
                    var owner = context.MaybeOwner != null ? context.MaybeOwner.Descriptor : unit;
                    if (ContextsOf(owner).TryGetValue(context, out item)) return item;
                    var caster = context.MaybeCaster != null ? context.MaybeCaster.Descriptor : null;
                    if (caster != null && caster != owner && ContextsOf(caster).TryGetValue(context, out item)) return item;
                }
                return null;
            }

            // A pet's feature its master's fact gives it (Add Feature To Companion, Share Features With Companion):
            // the game keeps no link, the master's facts are searched for the one that names it.
            static Fact MastersFact(Fact fact, UnitDescriptor unit, out UnitDescriptor master)
            {
                master = null;
                var masterUnit = unit.Master.Value;
                if (masterUnit == null || fact.Blueprint == null) return null;
                master = masterUnit.Descriptor;
                foreach (var candidate in Facts(master))
                {
                    if (candidate.Blueprint == null) continue;
                    foreach (var add in candidate.Blueprint.GetComponents<AddFeatureToCompanion>())
                        if (add.Feature == fact.Blueprint) return candidate;
                    foreach (var share in candidate.Blueprint.GetComponents<ShareFeaturesWithCompanion>())
                        if (share.Features != null && share.Features.Contains(fact.Blueprint)) return candidate;
                }
                return null;
            }

            Source OfFact(Fact fact)
            {
                Source source;
                if (m_Sources.TryGetValue(fact, out source)) return source;
                var item = ItemOf(fact, m_Unit);
                if (item != null) source = OfItem(item);
                else
                {
                    UnitDescriptor master;
                    var origin = MastersFact(fact, m_Unit, out master);
                    var originItem = origin != null ? ItemOf(origin, master) : null;
                    if (originItem != null) source = OfItem(originItem);
                    else if (origin != null)
                        source = new Source { Kind = SourceKind.Ability, Name = NameOf(origin, NameOf(fact, origin.Blueprint.name)), Owner = OtherName(master) };
                    else
                    {
                        var buff = fact as Buff;
                        source = new Source
                        {
                            Kind = buff != null ? SourceKind.Effect : SourceKind.Ability,
                            Name = NameOf(fact, fact.Blueprint != null ? fact.Blueprint.name : Strings.Unknown),
                            Owner = buff != null && buff.Context != null && buff.Context.MaybeCaster != null ? OtherName(buff.Context.MaybeCaster.Descriptor) : null
                        };
                    }
                }
                m_Sources[fact] = source;
                return source;
            }

            static string NameOf(Fact fact, string fallback)
            {
                var name = fact.Name;
                return string.IsNullOrEmpty(name) ? fallback : name;
            }

            internal Source OfOwnItem(ItemEntity item)
            {
                return OfItem(item);
            }

            Source OfItem(ItemEntity item)
            {
                Source source;
                if (!m_Sources.TryGetValue(item, out source))
                {
                    bool mine = m_Items.Contains(item);
                    m_Sources[item] = source = new Source
                    {
                        Item = mine ? item : null,
                        Kind = SourceKind.Item,
                        Name = item.Name,
                        Owner = mine ? null : OtherName(item.Wielder)
                    };
                }
                return source;
            }

            string OtherName(UnitDescriptor unit)
            {
                if (unit == null || unit == m_Unit) return null;
                return unit.Unit != null ? unit.Unit.CharacterName : unit.CharacterName;
            }
        }
    }
}
