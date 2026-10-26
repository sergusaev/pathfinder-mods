using System.Collections.Generic;
using System.Linq;
using Kingmaker;
using Kingmaker.Blueprints;
using Kingmaker.Blueprints.Classes.Selection;
using Kingmaker.Blueprints.Classes.Spells;
using Kingmaker.Blueprints.Items.Components;
using Kingmaker.Blueprints.Items.Equipment;
using Kingmaker.Designers.Mechanics.Facts;
using Kingmaker.EntitySystem.Entities;
using Kingmaker.Items;
using Kingmaker.UnitLogic;
using Kingmaker.UnitLogic.Abilities.Blueprints;
using Kingmaker.UnitLogic.Abilities.Components;

namespace LearnScrolls
{
    // One spell to one character, or a recipe to the party (Unit is null then).
    class Lesson
    {
        internal UnitEntityData Unit;
        // The book the spell goes into that the planner looked at: its specialization and opposition schools.
        internal Spellbook Book;
        internal BlueprintAbility Spell;
        internal int Level;
        internal CopyRecipe Recipe;
        internal string RecipeName;
        internal UnityEngine.Sprite RecipeIcon;
        // For the spellbook button: why the party plan gives this copy to someone else.
        internal string Warning;
    }

    // Who learns what from the scrolls in the inventory. The game copies a scroll for one character at a time and
    // spends it; here every spell gets as many learners as there are copies of it, and when there are too few, the
    // criteria of the settings choose.
    static class Planner
    {
        // A candidate for a spell: the character and its book the criteria look at (the best of the books the spell
        // goes into: the game writes it into all of them).
        class Candidate
        {
            internal UnitEntityData Unit;
            internal Spellbook Book;
            internal int Level;
            internal int Order;
        }

        internal static List<UnitEntityData> Party()
        {
#if KINGMAKER
            return Game.Instance.Player.Party.Where(u => u != null && !u.Descriptor.IsPet).ToList();
#else
            return Game.Instance.Player.Party.Where(u => u != null && !u.IsPet).ToList();
#endif
        }

        // The spell a scroll teaches, as the game finds it (CopyScroll.ExtractSpell is private).
        internal static BlueprintAbility SpellOf(ItemEntity item, CopyScroll copy)
        {
            if (copy.CustomSpell != null) return copy.CustomSpell;
            var usable = item as ItemEntityUsable;
            if (usable == null || usable.Blueprint.Type != UsableItemType.Scroll || usable.Blueprint.Ability == null) return null;
            return usable.Blueprint.Ability.Parent ?? usable.Blueprint.Ability;
        }

        static bool BookTakes(Spellbook book, BlueprintAbility spell)
        {
            return book.Blueprint.CanCopyScrolls && !book.IsKnown(spell) && book.Blueprint.SpellList.Contains(spell);
        }

        // Scrolls of the inventory by spell, with the number of copies.
        static Dictionary<BlueprintAbility, KeyValuePair<CopyScroll, int>> Scrolls(out List<ItemEntity> items)
        {
            var result = new Dictionary<BlueprintAbility, KeyValuePair<CopyScroll, int>>();
            items = new List<ItemEntity>();
            foreach (var item in Game.Instance.Player.Inventory)
            {
                var copy = item.Blueprint.GetComponent<CopyScroll>();
                if (copy == null) continue;
                var spell = SpellOf(item, copy);
                if (spell == null) continue;
                items.Add(item);
                KeyValuePair<CopyScroll, int> had;
                int count = result.TryGetValue(spell, out had) ? had.Value : 0;
                result[spell] = new KeyValuePair<CopyScroll, int>(copy, count + item.Count);
            }
            return result;
        }

        // An item in the inventory that teaches the spell, the same check as the game's button.
        internal static ItemEntity ItemFor(BlueprintAbility spell, UnitEntityData unit)
        {
            foreach (var item in Game.Instance.Player.Inventory)
            {
                var copy = item.Blueprint.GetComponent<CopyScroll>();
                if (copy != null && SpellOf(item, copy) == spell && copy.CanCopy(item, unit)) return item;
            }
            return null;
        }

        static List<Candidate> Candidates(BlueprintAbility spell, ItemEntity item, CopyScroll copy, List<UnitEntityData> party)
        {
            var list = new List<Candidate>();
            for (int i = 0; i < party.Count; i++)
            {
                var unit = party[i];
                if (!copy.CanCopy(item, unit)) continue;
                Candidate best = null;
                foreach (var book in unit.Descriptor.Spellbooks)
                {
                    if (!BookTakes(book, spell)) continue;
                    if (Main.Settings.ExcludeOpposition && Opposition(book, spell)) continue;
                    var c = new Candidate { Unit = unit, Book = book, Level = book.Blueprint.SpellList.GetLevel(spell), Order = i };
                    // The game copies a scroll the character could never cast: a spell needs the casting score of 10 + its level.
                    if (Stat(c) < 10 + c.Level) continue;
                    if (best == null || Compare(spell, c, best) < 0) best = c;
                }
                if (best != null) list.Add(best);
            }
            return list;
        }

        // Negative when a goes before b.
        static int Compare(BlueprintAbility spell, Candidate a, Candidate b)
        {
            var s = Main.Settings;
            int r;
            if (s.CanCastNow && (r = Better(CanCast(a), CanCast(b))) != 0) return r;
            if (s.NotOpposition && (r = Better(!Opposition(a.Book, spell), !Opposition(b.Book, spell))) != 0) return r;
            if (s.HigherCasterLevel && (r = b.Book.CasterLevel.CompareTo(a.Book.CasterLevel)) != 0) return r;
            if (s.SpecialistSchool && (r = Better(Specialist(a.Book, spell), Specialist(b.Book, spell))) != 0) return r;
            if (s.SchoolFocus && (r = Better(Focus(a.Unit, spell), Focus(b.Unit, spell))) != 0) return r;
            if (s.HigherCastingStat && (r = Stat(b).CompareTo(Stat(a))) != 0) return r;
            if (s.MagusTouch && (r = Better(MagusTouch(a.Book, spell), MagusTouch(b.Book, spell))) != 0) return r;
            if (s.FewerKnown && (r = Known(a).CompareTo(Known(b))) != 0) return r;
            if (s.LowerSpellLevel && (r = a.Level.CompareTo(b.Level)) != 0) return r;
            if (s.MainCharacterFirst && (r = Better(IsMain(a.Unit), IsMain(b.Unit))) != 0) return r;
            return a.Order.CompareTo(b.Order);
        }

        static int Better(bool a, bool b)
        {
            return a == b ? 0 : a ? -1 : 1;
        }

        static bool CanCast(Candidate c)
        {
            return c.Level <= c.Book.MaxSpellLevel;
        }

        internal static bool Opposition(Spellbook book, BlueprintAbility spell)
        {
            return book.OppositionSchools.Contains(spell.School);
        }

        internal static bool Specialist(Spellbook book, BlueprintAbility spell)
        {
            return spell.School != SpellSchool.None && book.IsSpecialistSchool(spell.School);
        }

        static bool Focus(UnitEntityData unit, BlueprintAbility spell)
        {
            if (spell.School == SpellSchool.None) return false;
            FeatureParam school = spell.School;
            foreach (var feature in unit.Descriptor.Progression.Features.Enumerable)
                if (feature.Param == school && feature.Blueprint.GetComponent<SpellFocusParametrized>() != null) return true;
            return false;
        }

        static int Stat(Candidate c)
        {
            var stat = c.Unit.Stats.GetStat(c.Book.Blueprint.CastingAttribute);
            return stat != null ? stat.ModifiedValue : 0;
        }

        static bool MagusTouch(Spellbook book, BlueprintAbility spell)
        {
            if (!book.Blueprint.name.Contains("Magus")) return false;
            if (spell.GetComponent<AbilityEffectStickyTouch>() != null) return true;
            var projectile = spell.GetComponent<AbilityDeliverProjectile>();
            return projectile != null && projectile.NeedAttackRoll;
        }

        static int Known(Candidate c)
        {
            return c.Book.GetKnownSpells(c.Level).Count();
        }

        static bool IsMain(UnitEntityData unit)
        {
            return unit == Game.Instance.Player.MainCharacter.Value;
        }

        // The party plan: every copy is learned by someone, the copies there are too few of go by the criteria.
        internal static List<Lesson> PartyPlan()
        {
            var lessons = new List<Lesson>();
            var party = Party();
            List<ItemEntity> items;
            foreach (var pair in Scrolls(out items))
            {
                var spell = pair.Key;
                var item = items.First(i => SpellOf(i, i.Blueprint.GetComponent<CopyScroll>()) == spell);
                var candidates = Candidates(spell, item, pair.Value.Key, party);
                candidates.Sort((a, b) => Compare(spell, a, b));
                foreach (var c in candidates.Take(pair.Value.Value))
                    lessons.Add(new Lesson { Unit = c.Unit, Book = c.Book, Spell = spell, Level = c.Level });
            }
            // Recipes are known by the whole party at once.
            var seen = new HashSet<object>();
            foreach (var item in Game.Instance.Player.Inventory)
            {
                var recipe = item.Blueprint.GetComponent<CopyRecipe>();
                if (recipe == null || recipe.Recipe == null || !seen.Add(recipe.Recipe) || !recipe.CanCopy(item, null)) continue;
                lessons.Add(new Lesson { Recipe = recipe, RecipeName = item.Name, RecipeIcon = item.Icon });
            }
            return lessons;
        }

        // Everything the character can learn from the inventory, past the party plan; where they differ it is told.
        internal static List<Lesson> UnitPlan(UnitEntityData unit)
        {
            var lessons = new List<Lesson>();
            var party = Party();
            if (!party.Contains(unit)) party.Add(unit);
            List<ItemEntity> items;
            foreach (var pair in Scrolls(out items))
            {
                var spell = pair.Key;
                var item = items.First(i => SpellOf(i, i.Blueprint.GetComponent<CopyScroll>()) == spell);
                var candidates = Candidates(spell, item, pair.Value.Key, party);
                var own = candidates.FirstOrDefault(c => c.Unit == unit);
                if (own == null) continue;
                candidates.Sort((a, b) => Compare(spell, a, b));
                var lesson = new Lesson { Unit = unit, Book = own.Book, Spell = spell, Level = own.Level };
                int place = candidates.IndexOf(own);
                if (place >= pair.Value.Value)
                    lesson.Warning = Strings.WouldGoTo(string.Join(", ", candidates.Take(pair.Value.Value).Select(c => c.Unit.CharacterName).ToArray()));
                lessons.Add(lesson);
            }
            return lessons;
        }
    }
}
