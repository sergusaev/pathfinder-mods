using Kingmaker.Localization;
using Kingmaker.Localization.Shared;

namespace TimeControl
{
    // In-game texts: Russian for the Russian locale, English for every other one.
    static class S
    {
        static bool Ru
        {
            get { return LocalizationManager.CurrentLocale == Locale.ruRU; }
        }

        static string P(string ru, string en)
        {
            return Ru ? ru : en;
        }

        // Windows
        internal static string WindowTitle { get { return P("Время и сроки", "Time and Deadlines"); } }
        internal static string AlertTitle { get { return P("Напоминание", "Reminder"); } }
        internal static string CheckTitle { get { return P("Проверка сроков", "Deadline Check"); } }
        internal static string SkipTitle { get { return P("Перемотка времени", "Skip Time"); } }
        internal static string TabTimers { get { return P("Сроки", "Deadlines"); } }
        internal static string TabTracked { get { return P("Отслеживаемые", "Tracked"); } }
        internal static string TabTime { get { return P("Время", "Time"); } }
        internal static string TabSetup { get { return P("Настройка отслеживания", "Tracking Setup"); } }
        internal static string TabHelp { get { return P("Справка", "Help"); } }
        internal static string HScroll { get { return P("Прокрутка", "Scroll"); } }
        internal static string Today { get { return P("Сегодня: {0}", "Today: {0}"); } }
        internal static string Empty { get { return P("Сроков нет.", "No deadlines."); } }
        internal static string EmptyTracked { get { return P("Ничего не отслеживается. На вкладке «Сроки» выберите событие и нажмите «Отслеживать».", "Nothing is tracked. Select an event on the Deadlines tab and choose Track."); } }
        internal static string HiddenShown { get { return P("Скрытые таймеры показаны", "Hidden timers shown"); } }
        internal static string HiddenOff { get { return P("Скрытые таймеры не показаны", "Hidden timers not shown"); } }

        // A timer line: "До события «X» осталось 3 дня и 5 часов"
        internal static string Left { get { return P("До события «{0}» осталось {1}", "{1} left until \"{0}\""); } }
        internal static string Due { get { return P("Наступил срок: «{0}»", "Now due: \"{0}\""); } }
        internal static string Hidden { get { return P("скрытый", "hidden"); } }
        internal static string Critical { get { return P("провал", "failure"); } }
        internal static string TrackedMark { get { return P("отслеживается", "tracked"); } }
        internal static string DueDate { get { return P("Срок: {0}", "Due: {0}"); } }
        internal static string Deadline { get { return P("Свой срок: за {0} до события ({1})", "Own deadline: {0} before the event ({1})"); } }
        internal static string DeadlineSame { get { return P("Свой срок: в момент события", "Own deadline: when the event happens"); } }
        internal static string AlertsList { get { return P("Оповещения: {0}", "Alerts: {0}"); } }
        internal static string AlertsNone { get { return P("Оповещений нет", "No alerts"); } }
        internal static string AtMoment { get { return P("в момент срока", "when due"); } }
        internal static string Before { get { return P("за {0}", "{0} before"); } }
        internal static string LeftToDeadline { get { return P("До своего срока «{0}» осталось {1}", "{1} left until your deadline for \"{0}\""); } }
        internal static string DeadlineDue { get { return P("Наступил свой срок: «{0}»", "Your deadline for \"{0}\" is due"); } }
        internal static string Gone { get { return P("Срок «{0}» прошёл: событие наступило.", "\"{0}\" is over: the event has happened."); } }
        internal static string AffectedNote { get { return P("Зависит от коэффициентов королевства.", "Affected by the kingdom coefficients."); } }
        internal static string HiddenNote { get { return P("Скрытый: игра не показывает этот таймер.", "Hidden: the game does not show this timer."); } }
        internal static string CriticalNote { get { return P("По истечении срока что-то будет провалено.", "Something fails when it runs out."); } }

        // Timer names
        internal static string ObjectiveFail { get { return P("Провал цели: {0} ({1})", "Objective fails: {0} ({1})"); } }
        internal static string QuestTrigger { get { return P("Событие задания: {0}", "Quest event: {0}"); } }
        internal static string EventStart { get { return P("Начало события: {0}", "Event starts: {0}"); } }
        internal static string TaskEnd { get { return P("Решение: {0}", "Resolved: {0}"); } }
        internal static string ProjectEnd { get { return P("Завершение проекта: {0}", "Project completes: {0}"); } }
        internal static string AutoResolve { get { return P("Исход события: {0}", "Event resolves: {0}"); } }
        internal static string IgnoredFail { get { return P("Провал без советника: {0}", "Fails without an advisor: {0}"); } }
        internal static string Recurrence { get { return P("Очередное действие: {0}", "Next occurrence: {0}"); } }
        internal static string Timeline { get { return P("Сюжетное событие королевства: {0}", "Kingdom story event: {0}"); } }
        internal static string BuffEnd { get { return P("Конец эффекта: {0}", "Effect ends: {0}"); } }
        internal static string Building { get { return P("Постройка: {0} ({1})", "Building: {0} ({1})"); } }
        internal static string Artisan { get { return P("Изделие мастера: {0}", "Artisan item: {0}"); } }
        internal static string Income { get { return P("Доход казны: +{0} ОС", "Treasury income: +{0} BP"); } }
        internal static string Month { get { return P("Новый месяц королевства", "New kingdom month"); } }
        internal static string IgnoredEvent { get { return P("Провал проигнорированного события: {0}", "Ignored event fails: {0}"); } }
        internal static string Raven { get { return P("Доклад ворона", "Raven's report"); } }

        // Time page
        internal static string CoefEvents { get { return P("Решение событий королевства", "Kingdom event resolution"); } }
        internal static string CoefProjects { get { return P("Проекты королевства", "Kingdom projects"); } }
        internal static string CoefClaim { get { return P("Присоединение и улучшение регионов", "Claiming and upgrading regions"); } }
        internal static string CoefRankup { get { return P("Повышение рангов советников", "Advisor rank-ups"); } }
        internal static string CoefRuler { get { return P("Время правителя на проекты", "Ruler time on projects"); } }
        internal static string CoefBuilding { get { return P("Строительство зданий", "Building construction"); } }
        internal static string CoefTravel { get { return P("Время в пути по карте", "Travel time on the map"); } }
        internal static string CoefRest { get { return P("Время привала", "Camping time"); } }
        internal static string Apply { get { return P("Применить коэффициенты", "Apply coefficients"); } }
        internal static string Reset { get { return P("Вернуть все к ×1", "Reset all to ×1"); } }
        internal static string Pending { get { return P("{0} (сейчас {1})", "{0} (now {1})"); } }
        internal static string Now { get { return P("сейчас {0}", "now {0}"); } }
        internal static string ButtonApply { get { return P("Применить", "Apply"); } }
        internal static string ButtonReset { get { return P("Сбросить", "Reset"); } }
        internal static string ButtonSkip { get { return P("Перемотать", "Skip"); } }
        internal static string ButtonRewind { get { return P("Отмотать", "Rewind"); } }
        internal static string ButtonStop { get { return P("Снять", "Stop"); } }
        internal static string SwitchOn { get { return P("Вкл", "On"); } }
        internal static string SwitchOff { get { return P("Выкл", "Off"); } }
        internal static string SetupDeadlineRow { get { return P("Свой срок до события", "Own deadline before the event"); } }
        internal static string DeadlineAtOnce { get { return P("в момент", "at the event"); } }
        internal static string SpanDays { get { return P("Срок: дни", "Span: days"); } }
        internal static string SpanHours { get { return P("Срок: часы", "Span: hours"); } }
        internal static string SkipRow { get { return P("Перемотать время на этот срок", "Skip time by this span"); } }
        internal static string RewindRow { get { return P("Отмотать время назад на этот срок", "Turn the clock back by this span"); } }
        internal static string RewindTitle { get { return P("Отмотать время", "Turn the Clock Back"); } }
        internal static string RewindAsk { get { return P("Отмотать время назад на {0}? Будет {1}.", "Turn the clock back by {0}? It will then be {1}."); } }
        internal static string RewindLonger { get { return P("Все сроки станут дальше на {0}:", "Every deadline moves {0} further away:"); } }
        internal static string RewindLine { get { return P("• {0}: было {1}, станет {2}", "• {0}: was {1}, will be {2}"); } }
        internal static string RewindKingdom { get { return P("Королевство уже прожило дни до {0}: пока календарь не дойдёт до этой даты, в королевстве ничего не произойдёт — ни событий, ни дохода, ни нового месяца. Дела королевства продлятся на тот же срок.", "The kingdom has already lived through the days up to {0}: until the calendar gets there again, nothing happens in the kingdom — no events, no income, no new month. Its tasks take that much longer."); } }
        internal static string RewindPast { get { return P("Уже случившееся не вернётся: выполненные и проваленные задания, сработавшие события заданий, решённые события королевства и закончившиеся эффекты останутся как есть.", "What has already happened stays: completed and failed quests, triggered quest events, resolved kingdom events and expired effects remain as they are."); } }
        internal static string RewindUnits { get { return P("Отматывание не затрагивает эффекты на персонажах, временные чары предметов, усталость, отдых и случайные встречи: они останутся в нынешнем состоянии, с тем же оставшимся временем.", "Turning back does not affect effects on characters, temporary item enchantments, fatigue, rest or random encounters: they stay as they are, with the same time remaining."); } }
        internal static string RewindLimit { get { return P("Раньше начала игры отмотать нельзя: не более чем на {0}.", "The clock cannot go back past the start of the game: {0} at most."); } }
        internal static string RewindDone { get { return P("Время отмотано на {0}. Сейчас {1}.", "Clock turned back by {0}. It is now {1}."); } }
        internal static string HRewind { get { return P("Отмотать", "Turn back"); } }
        internal static string Section { get { return P("— {0} —", "— {0} —"); } }
        internal static string SecCoef { get { return P("Коэффициенты длительности", "Duration coefficients"); } }
        internal static string SecSpan { get { return P("Перемотка", "Skipping"); } }
        internal static string CoefNote { get { return P("Множитель длительности: ×0.5 — вдвое быстрее, ×2 — вдвое дольше. Изменение вступает в силу по «Применить».", "Duration multiplier: ×0.5 is twice as fast, ×2 twice as long. Takes effect when applied."); } }
        internal static string BuildingNote { get { return P("Действует на здания, заложенные после изменения.", "Applies to buildings started after the change."); } }
        internal static string TravelNote { get { return P("Сколько игрового времени уходит на путь: ×0.5 — дни идут вдвое медленнее.", "Game time spent travelling: at ×0.5, days pass half as fast."); } }
        internal static string RestNote { get { return P("Сколько игрового времени уходит на привал и сон.", "Game time spent camping and sleeping."); } }

        // Setup page
        internal static string SetupDeadline { get { return P("Свой срок: за {0} до события", "Own deadline: {0} before the event"); } }
        internal static string SetupAlert { get { return P("Оповестить {0}", "Alert {0}"); } }
        internal static string SetupStop { get { return P("Перестать отслеживать", "Stop tracking"); } }

        // Hints
        internal static string HSelect { get { return P("Выбор", "Select"); } }
        internal static string HTrack { get { return P("Отслеживать", "Track"); } }
        internal static string HSetup { get { return P("Настроить", "Set up"); } }
        internal static string HHiddenOn { get { return P("Показать скрытые", "Show hidden timers"); } }
        internal static string HHiddenOff { get { return P("Убрать скрытые", "Hide hidden timers"); } }
        internal static string HTab { get { return P("Вкладка", "Tab"); } }
        internal static string HClose { get { return P("Закрыть", "Close"); } }
        internal static string HBack { get { return P("Назад", "Back"); } }
        internal static string HChange { get { return P("Изменить", "Change"); } }
        internal static string HDo { get { return P("Выполнить", "Confirm"); } }
        internal static string HToggle { get { return P("Вкл/выкл", "On/off"); } }
        internal static string HOk { get { return P("Понятно", "OK"); } }
        internal static string HOpen { get { return P("Открыть сроки", "Open deadlines"); } }
        internal static string HUntrack { get { return P("Не отслеживать", "Stop tracking"); } }
        internal static string HCancel { get { return P("Отмена", "Cancel"); } }
        internal static string HSkip { get { return P("Перемотать", "Skip"); } }
        internal static string HSkipMax { get { return P("Перемотать на {0}", "Skip {0}"); } }

        // Coefficient check
        internal static string CheckChanged { get { return P("«{0}»: было {1}, стало {2}", "\"{0}\": was {1}, now {2}"); } }
        internal static string CheckNow { get { return P("«{0}»: было {1}, теперь срок уже наступил — произойдёт при смене дня", "\"{0}\": was {1}, now already due — happens when the day changes"); } }
        internal static string CheckNone { get { return P("Текущие сроки королевства не изменились.", "No current kingdom deadlines changed."); } }
        internal static string CheckApplied { get { return P("Коэффициенты применены.", "Coefficients applied."); } }
        internal static string CheckTravel { get { return P("Время в пути ×{0}, привал ×{1}: оставшиеся сроки те же, но в пути и на привале они будут расходоваться {2}.", "Travel ×{0}, camping ×{1}: the remaining time is unchanged, but travelling and camping use it up {2}."); } }
        internal static string Slower { get { return P("медленнее", "more slowly"); } }
        internal static string Faster { get { return P("быстрее", "faster"); } }
        internal static string AsBefore { get { return P("как раньше", "as before"); } }
        internal static string Nearest { get { return P("Ближайшие сроки:", "Nearest deadlines:"); } }
        internal static string More { get { return P("… и ещё {0}", "… and {0} more"); } }

        // Skip
        internal static string SkipAsk { get { return P("Перемотать время на {0}? Будет {1}.", "Skip {0}? It will be {1}."); } }
        internal static string SkipWillHappen { get { return P("За это время наступит:", "Coming due during this time:"); } }
        internal static string SkipNothing { get { return P("За это время сроков не наступит.", "No deadlines come due during this time."); } }
        internal static string SkipBlocked { get { return P("Перемотка на {0} невозможна: она пропустит срок, после которого что-то будет провалено.", "Cannot skip {0}: it would pass a deadline after which something fails."); } }
        internal static string SkipMax { get { return P("Без провала можно перемотать не более чем на {0}.", "You can skip at most {0} without a failure."); } }
        internal static string SkipNoMax { get { return P("Срок наступает меньше чем через час — перемотка невозможна.", "A deadline comes due in less than an hour — skipping is not possible."); } }
        internal static string SkipNotNow { get { return P("Сейчас перематывать время нельзя: только на локации или глобальной карте, вне боя, диалога и сценки.", "Time cannot be skipped now: only in an area or on the global map, outside combat, dialogues and cutscenes."); } }
        internal static string SkipDone { get { return P("Время перемотано на {0}. Сейчас {1}.", "Skipped {0}. It is now {1}."); } }
        internal static string SkipZero { get { return P("Задайте срок больше нуля.", "Set a span greater than zero."); } }
        internal static string TrackedDeadline { get { return P("свой срок", "own deadline"); } }

        internal static string HelpBody { get { return Ru ? HelpRu : HelpEn; } }

        const string HelpRu =
@"# Вкладки
- **Сроки** — все счётчики игры, ближайшие сверху. Метка «провал» — по истечении срока что-то будет провалено, «скрытый» — игра этот таймер не показывает. Глаз перед датой — срок отслеживается.
- **Отслеживаемые** — только отслеживаемые сроки.
- **Время** — коэффициенты длительности, перемотка и отматывание назад.
- **Справка** — эта страница.
# Сроки и отслеживание
{A}| начать или прекратить отслеживание срока
{X}| настройка: свой срок и оповещения
{Y}| показать или убрать скрытые таймеры
- Сроки, после которых что-то будет провалено, мод начинает отслеживать сам, как только они появляются (отключается в окне UMM). Если снять такой срок с отслеживания, мод больше его не добавит.
- **Свой срок** — за сколько до события вам нужно успеть: от часа до двух недель. Оповещения отсчитываются от него.
- **Оповещение** — окно «Напоминание» на пергаменте со звуком, игра встаёт на паузу. Оповещения, время которых прошло до начала отслеживания, не показываются.
- Отслеживание хранится отдельно для каждого прохождения и общее для всех его сохранений.
# Коэффициенты
- Множитель длительности: ×0.5 — вдвое быстрее, ×2 — вдвое дольше, от ×0.1 до ×10. Новое значение вступает в силу по «Применить коэффициенты».
- **Дела королевства** (события, проекты, регионы, ранги советников, время правителя) пересчитываются сразу, в том числе уже идущие. После применения окно «Проверка сроков» покажет, какие сроки сдвинулись.
- **Строительство** — только для зданий, заложенных после изменения.
- **Время в пути** — сколько игрового времени уходит на путь по глобальной карте: при ×0.5 дни в пути идут вдвое медленнее.
- **Время привала** — сколько игрового времени уходит на привал, охоту и сон.
# Перемотка
- Задайте срок в днях и часах и выберите «Перемотать». Перемотка работает на локации и на глобальной карте, вне боя, диалога и сценки.
- Время идёт по суткам, как в игре: королевство проживает каждый день, сроки заданий проверяются после каждых суток.
- Если за этот срок наступит срок с провалом или ваш свой срок, перемотка не начнётся: мод покажет, что мешает, и предложит перемотать на наибольший безопасный срок — до часа перед первым таким сроком.
# Отматывание назад
- «Отмотать время назад» переводит часы игры назад, но не раньше начала игры. Всё, что привязано к календарю, становится дальше на этот срок: сроки заданий, события заданий и королевства.
- Уже случившееся не вернётся: выполненные и проваленные задания, сработавшие события, решённые события королевства, доход и начало месяца не повторятся.
- Королевство живёт только вперёд: пока календарь снова не дойдёт до уже прожитого дня, в королевстве ничего не происходит.
- **Отматывание не затрагивает** эффекты на персонажах, временные чары предметов, усталость, отдых и случайные встречи: они остаются в нынешнем состоянии, с тем же оставшимся временем.
# Управление
@pad {UP} {DOWN}| выбор строки; в справке — прокрутка
@pad {LEFT} {RIGHT}| изменить значение
{LB} {RB}| соседняя вкладка
{B}| назад, закрыть окно
@pad {START}| меню игры поверх окна мода, как над окнами игры; после него окно возвращается
@kb - [[W]] [[S]] [[A]] [[D]] или стрелки — выбор и изменение; [[E]], [[Enter]] или [[Space]] — действие; [[Q]] или [[Backspace]] — назад; [[Esc]] — меню игры поверх окна мода; [[X]] и [[C]] — вкладки; [[R]] и [[F]] — как кнопки X и Y геймпада.
- Мышь: клик выбирает строку; ползунок тянется за ручку или ставится кликом по дорожке, переключатель — кликом по нужной половине, выбор — стрелками, кнопка — кликом; колесо прокручивает; клик по подсказке внизу делает то же, что её кнопка.";

        const string HelpEn =
@"# Tabs
- **Deadlines** — every countdown in the game, soonest first. ""failure"" means something fails when it runs out, ""hidden"" means the game does not show this timer. An eye before the date means the deadline is tracked.
- **Tracked** — tracked deadlines only.
- **Time** — duration coefficients, time skip and turning the clock back.
- **Help** — this page.
# Deadlines and tracking
{A}| start or stop tracking a deadline
{X}| set up your own deadline and alerts
{Y}| show or hide hidden timers
- Deadlines after which something fails are tracked as soon as they appear (this can be turned off in UMM). Once you stop tracking one, the mod will not add it again.
- **Own deadline** — how long before the event you want to be done: from an hour to two weeks. Alerts are counted from it.
- **Alert** — a ""Reminder"" window on parchment with a sound; the game pauses. Alerts whose time had passed before tracking began are not shown.
- Tracking is stored per playthrough and shared by all of its saves.
# Coefficients
- Duration multiplier: ×0.5 is twice as fast, ×2 twice as long, from ×0.1 to ×10. A new value takes effect when you choose Apply.
- **Kingdom tasks** (events, projects, regions, advisor ranks, ruler time) are recalculated at once, including those in progress. After applying, the Deadline Check window shows which deadlines moved.
- **Building construction** — only buildings started after the change.
- **Travel time** — game time spent travelling on the global map: at ×0.5, days on the road pass half as fast.
- **Camping time** — game time spent camping, hunting and sleeping.
# Time skip
- Set a span in days and hours and choose Skip. Works in an area or on the global map, outside combat, dialogues and cutscenes.
- Time passes day by day, as in normal play: the kingdom lives through every day, and quest deadlines are checked after each one.
- If a deadline after which something fails, or your own deadline, comes due within the span, the skip does not start: the mod shows what is in the way and offers the longest safe span, up to an hour before the first such deadline.
# Turning the clock back
- Moves the game clock back, but not past the start of the game. Everything tied to the calendar moves that much further away: quest deadlines, quest events and kingdom events.
- What has already happened does not come back: completed and failed quests, triggered events, resolved kingdom events, income and month starts are not repeated.
- The kingdom only lives forward: nothing happens in it until the calendar reaches the days it has already lived through.
- **Turning back does not affect** effects on characters, temporary item enchantments, fatigue, rest or random encounters: they stay as they are, with the same time remaining.
# Controls
@pad {UP} {DOWN}| select a row; scroll the help
@pad {LEFT} {RIGHT}| change a value
{LB} {RB}| switch tab
{B}| back; close the window
@pad {START}| open the game's menu over the mod window, as over the game's own windows; the window returns afterwards
@kb - [[W]] [[S]] [[A]] [[D]] or the arrow keys select and change; [[E]], [[Enter]] or [[Space]] confirm; [[Q]] or [[Backspace]] go back; [[Esc]] opens the game's menu over the mod window; [[X]] and [[C]] switch tabs; [[R]] and [[F]] act as the gamepad's X and Y.
- Mouse: click a row to select it; drag a slider's handle or click its track; click the side of a switch you want; use a choice's arrows; click a button; the wheel scrolls; clicking a hint at the bottom does what its button does.";

        internal static string Days(int n)
        {
            if (!Ru) return n == 1 ? "1 day" : n + " days";
            int m10 = n % 10, m100 = n % 100;
            if (m10 == 1 && m100 != 11) return n + " день";
            if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) return n + " дня";
            return n + " дней";
        }

        internal static string Hours(int n)
        {
            if (!Ru) return n == 1 ? "1 hour" : n + " hours";
            int m10 = n % 10, m100 = n % 100;
            if (m10 == 1 && m100 != 11) return n + " час";
            if (m10 >= 2 && m10 <= 4 && (m100 < 12 || m100 > 14)) return n + " часа";
            return n + " часов";
        }

        // "за неделю", "за 3 дня", "за 12 часов": an alert offset in hours.
        internal static string Offset(int hours)
        {
            if (hours == 0) return AtMoment;
            if (hours == 168) return string.Format(Before, P("неделю", "a week"));
            if (hours == 336) return string.Format(Before, P("две недели", "two weeks"));
            if (hours == 720) return string.Format(Before, P("месяц", "a month"));
            return string.Format(Before, Span(hours));
        }

        internal static string Span(int hours)
        {
            int d = hours / 24, h = hours % 24;
            if (d > 0 && h > 0) return Days(d) + " " + Hours(h);
            if (d > 0) return Days(d);
            return Hours(h);
        }
    }
}
