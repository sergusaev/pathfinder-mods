using Kingmaker.Localization;
using Kingmaker.Localization.Shared;

namespace BonusOverlapWarnings
{
    static class Strings
    {
        static bool Ru
        {
            get { return LocalizationManager.CurrentLocale == Locale.ruRU; }
        }

        static string T(string ru, string en)
        {
            return Ru ? ru : en;
        }

        internal static string Quote(string name)
        {
            return Ru ? "«" + name + "»" : "\"" + name + "\"";
        }

        // Before the quoted name of a source.
        internal static string Kind(SourceKind kind)
        {
            switch (kind)
            {
                case SourceKind.Item: return T("предмет ", "item ");
                case SourceKind.Effect: return T("эффект ", "effect ");
                case SourceKind.Ability: return T("способность ", "ability ");
                default: return "";
            }
        }

        // Before the quoted name of a source, "by ...": the instrumental case in Russian.
        internal static string KindBy(SourceKind kind)
        {
            switch (kind)
            {
                case SourceKind.Item: return T("предметом ", "item ");
                case SourceKind.Effect: return T("эффектом ", "effect ");
                case SourceKind.Ability: return T("способностью ", "ability ");
                default: return "";
            }
        }

        internal static string WeaponAttacks => T("атаки этим оружием", "attacks with this weapon");
        internal static string SpecialProperty => T("особое свойство", "a special property");
        internal static string ExtrasLine => T("<b>Остальные свойства</b> предмета не конфликтуют и пропадут при снятии", "<b>The other properties</b> of the item do not overlap and are lost when it is taken off");
        // Of the item's properties its description does not tell, after the green line.
        internal static string Unlisted(string list) => T(". Не указано в описании: " + list, ". Not told in its description: " + list);
        // typeName null: incorporeal touch attacks.
        internal static string AcAgainst(int value, string typeName) => string.IsNullOrEmpty(typeName)
            ? T("+" + value + " к защите от касаний бестелесных существ (доспехи от них не защищают)", "+" + value + " AC against incorporeal touch attacks (armour does not count against them)")
            : T("+" + value + " к защите от атак «" + typeName + "»", "+" + value + " AC against \"" + typeName + "\" attacks");
        internal static string TitleLostOwn => T("Бонусы предмета не действуют, но у него есть свои свойства", "The item's bonuses have no effect, but it has properties of its own");
        internal static string TitleShared => T("Бонус совпадает с другим предметом, у обоих свои свойства", "The bonus is the same as another item's, both have properties of their own");
        internal static string Shared(string bonus, string rivals) => T(bonus + ": такой же бонус даёт " + rivals + " — учитывается один раз, нужны оба предмета", bonus + ": " + rivals + " gives the same bonus — it counts once, both items are needed");

        internal static string Unknown => T("неизвестный источник", "unknown source");

        internal static string TitleLost => T("Бонусы предмета не действуют", "The item's bonuses have no effect");
        internal static string TitleReduced => T("Часть бонусов предмета не действует", "Some of the item's bonuses have no effect");
        internal static string TitleOverrides => T("Бонус предмета перекрывает чужой", "The item's bonus overrides another one");

        // The item's own enhancement as a weapon or armour, in place of the list of stats.
        internal static string ItemItself => T("сам предмет", "the item itself");

        internal static string AndMore(int count) => T(" и ещё " + count, " and " + count + " more");

        // rivalsBy: the rivals in the instrumental case.
        internal static string Lost(string bonus, string rivalsBy) => T(bonus + ": не действует — перекрывается " + rivalsBy, bonus + ": no effect — overridden by " + rivalsBy);
        internal static string Tie(string bonus, string rivals) => T(bonus + ": не действует — такой же бонус уже даёт " + rivals, bonus + ": no effect — " + rivals + " already gives the same bonus");
        internal static string OverridesTie(string bonus, string rivals) => T(bonus + ": такой же бонус даёт " + rivals + " — тот ничего не добавляет", bonus + ": " + rivals + " gives the same bonus and adds nothing");
        internal static string Overrides(string bonus, string rivals) => T(bonus + ": перекрывает " + rivals + ", тот не действует", bonus + ": overrides " + rivals + ", which has no effect");

        internal static string SettingOverrides => T("Синий значок на предмете, который перекрывает чужой бонус (сам работает)",
            "Blue mark on an item that overrides another source's bonus (the item itself works)");

        internal static string SettingsNote => T("Красный значок — бонусы предмета не действуют, жёлтый — часть не действует. Подробности — в подсказке предмета на кукле.",
            "Red mark: the item's bonuses have no effect; yellow: some of them have none. The item's tooltip on the doll tells with what.");
    }
}
