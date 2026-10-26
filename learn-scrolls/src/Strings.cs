using Kingmaker.Localization;
using Kingmaker.Localization.Shared;

namespace LearnScrolls
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

        internal static string Title => T("Свитки", "Scrolls");

        internal static string LearnTip => T("Изучить всё для всех: кто какие свитки из инвентаря изучит — сначала список, потом подтверждение.",
            "Learn everything for everyone: who learns which scrolls of the inventory — the list first, then a confirmation.");

        internal static string WindowTip => T("Окно мода: персонажи и что каждый может изучить, правила раздачи, справка.",
            "The mod's window: the characters and what each can learn, the rules of the share-out, help.");

        internal static string TabCharacters => T("Персонажи", "Characters");
        internal static string TabSettings => T("Настройки", "Settings");
        internal static string TabHelp => T("Справка", "Help");

        internal static string WholeParty => T("Вся группа", "Whole party");

        internal static string ToLearn(int count)
        {
            if (count == 0) return T("нечего изучать", "nothing to learn");
            return Ru ? count + " " + Plural(count, "свиток", "свитка", "свитков") : count + (count == 1 ? " scroll" : " scrolls");
        }

        internal static string Recipes(int count)
        {
            return Ru ? count + " " + Plural(count, "рецепт", "рецепта", "рецептов") : count + (count == 1 ? " recipe" : " recipes");
        }

        static string Plural(int n, string one, string few, string many)
        {
            int d = n % 10, h = n % 100;
            if (d == 1 && h != 11) return one;
            if (d >= 2 && d <= 4 && (h < 12 || h > 14)) return few;
            return many;
        }

        internal static string NothingToLearn => T("Из свитков в инвентаре изучать нечего.", "There is nothing to learn from the scrolls in the inventory.");

        internal static string Learned(int count)
        {
            return T("Изучено: ", "Learned: ") + count + ".";
        }

        internal static string Question => T("Изучить свитки из инвентаря?", "Learn the scrolls from the inventory?");

        internal static string SpellLevel(int level)
        {
            return Ru ? level + " круг" : "level " + level;
        }

        internal static string RecipesLabel => T("Рецепты", "Recipes");

        internal static string WouldGoTo(string names)
        {
            return T("по общей раздаче достался бы: ", "the party plan gives it to ") + names;
        }

        // Settings page.
        internal static string CriteriaNote => T("Кому достаётся свиток, копий которого меньше, чем желающих. Правила проверяются по порядку, каждое следующее — только среди равных по предыдущим.",
            "Who gets a scroll there are fewer copies of than characters who can learn it. The rules are tried in order, each next one only between the characters the ones before left equal.");

        internal static string[] Criteria => Ru
            ? new[]
            {
                "Может колдовать заклинания этого круга уже сейчас",
                "Заклинание не из противоположной школы",
                "Выше уровень заклинателя",
                "Школа специализации",
                "Фокусировка на школе",
                "Выше колдовская характеристика",
                "Касания и атакующие заклинания — магусу",
                "Меньше известных заклинаний этого круга",
                "Ниже круг заклинания для класса",
                "Сначала главный герой",
            }
            : new[]
            {
                "Can cast spells of its level now",
                "The spell is not of an opposition school",
                "Higher caster level",
                "The school of specialization",
                "Spell Focus in the school",
                "Higher casting ability score",
                "Touch and attack spells to a magus",
                "Fewer known spells of this level",
                "Lower spell level for the class",
                "The main character first",
            };

        internal static string PartyOrder => T("Порядок в группе (всегда)", "The order in the party (always)");

        internal static string ExcludeOpposition => T("Не изучать заклинания противоположных школ", "Do not learn spells of opposition schools");
        internal static string Defaults => T("По умолчанию", "Defaults");

        internal static string On => T("вкл.", "on");
        internal static string Off => T("выкл.", "off");

        // Hints.
        internal static string HintSelect => T("Выбрать", "Select");
        internal static string HintLearn => T("Изучить", "Learn");
        internal static string HintChange => T("Изменить", "Change");
        internal static string HintTab => T("Вкладка", "Tab");
        internal static string HintScroll => T("Прокрутка", "Scroll");
        internal static string HintClose => T("Закрыть", "Close");

        internal static string Help => Ru ? HelpRu : HelpEn;

        // Markup of Help.Build: "# " section, "- " bullet, "{A}| text" button line, "**bold**"; "@pad " and "@kb " lines
        // only with a gamepad or with keyboard and mouse.
        const string HelpRu =
@"# Что делает мод
Учит волшебников, магусов и других переписчиков всем свиткам из инвентаря разом, так же, как это делает сама игра: свиток тратится, заклинание попадает в книгу.
# Изучить всё для всех
@kb - Кнопка со свитком над кнопками меню игры.
@pad - Пункт «Вся группа» на вкладке «Персонажи».
- Каждый свиток достаётся тому, кто может его выучить; если копий меньше, чем желающих, решают правила со вкладки «Настройки».
- Сначала окно игры со списком: персонажи, под каждым — его заклинания с кругом и школой. Изучение только после подтверждения. Рецепты из инвентаря изучаются всей группой.
- Школа [+зелёная+] {LIKE} — школа специализации персонажа, [-красная-] {DISLIKE} — его противоположная школа (такое заклинание он колдует хуже), без цвета — обычная.
- Рядом с персонажем — сколько свитков ему достанется по общей раздаче; сумма по персонажам равна числу у «Вся группа».
# Персонажи
- Выберите персонажа — он изучит всё, что может, даже то, что по общей раздаче досталось бы другому; такие заклинания помечены в списке.
# Что не изучается
- Заклинание, которое уже есть в книге; заклинание не из списка класса; заклинание, для которого не хватает колдовской характеристики (нужно 10 + круг).
- Заклинание противоположной школы, если в «Настройках» включено «Не изучать заклинания противоположных школ» (по умолчанию выключено).
# Управление
@pad - Окно открывается карточкой «Свитки» в меню игры.
@kb - Окно открывается второй кнопкой над кнопками меню игры.
{UP} {DOWN}| выбор строки; в справке — прокрутка
{A}| действие
{LB} {RB}| соседняя вкладка
{B}| закрыть окно
@pad {START}| меню игры поверх окна мода; после него окно возвращается
@pad - В окне со списком правый стик прокручивает список.
@kb - Стрелки и Enter делают то же, что W/S и E; Esc или правая кнопка мыши закрывают окно.
@kb - Мышь: щелчок по строке, колесо прокручивает справку и список в окне игры.";

        const string HelpEn =
@"# What the mod does
Teaches wizards, magi and the other copiers every scroll of the inventory at once, the way the game itself does it: the scroll is spent, the spell goes into the book.
# Learn everything for everyone
@kb - The button with a scroll over the game's menu buttons.
@pad - The Whole party item of the Characters tab.
- Every scroll goes to someone who can learn it; when there are fewer copies than characters who can, the rules of the Settings tab decide.
- The game's window with the list comes first: the characters, under each its spells with the level and the school. Nothing is learned before the confirmation. Recipes of the inventory are learned by the whole party.
- The school is [+green+] {LIKE} when it is the character's specialization, [-red-] {DISLIKE} when it is its opposition school (it casts such a spell worse), plain otherwise.
- Next to a character: how many scrolls the party plan gives it; the characters' numbers add up to the Whole party's.
# Characters
- Pick a character: it learns everything it can, also what the party plan would give to someone else; such spells are marked in the list.
# What is not learned
- A spell already in the book; a spell not on the class's list; a spell the casting ability score is too low for (10 + the spell level is needed).
- A spell of an opposition school, when Do not learn spells of opposition schools is on in the Settings (off by default).
# Controls
@pad - The window opens with the Scrolls card of the game's menu.
@kb - The window opens with the second button over the game's menu buttons.
{UP} {DOWN}| select a row; in the help, scroll
{A}| act
{LB} {RB}| the next tab
{B}| close the window
@pad {START}| the game's menu over the mod's window; the window comes back after it
@pad - In the window with the list the right stick scrolls the list.
@kb - The arrows and Enter do what W/S and E do; Esc or the right mouse button close the window.
@kb - Mouse: a click on a row; the wheel scrolls the help and the list in the game's window.";
    }
}
