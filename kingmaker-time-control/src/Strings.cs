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
        internal static string Today { get { return P("Сегодня: {0}", "Today: {0}"); } }
        internal static string Empty { get { return P("Сроков нет.", "No deadlines."); } }
        internal static string EmptyTracked { get { return P("Ничего не отслеживается. На вкладке «Сроки» выберите событие и нажмите «Отслеживать».", "Nothing is tracked. Pick an event on the Deadlines tab and choose Track."); } }
        internal static string HiddenShown { get { return P("Скрытые таймеры показаны", "Hidden timers shown"); } }
        internal static string HiddenOff { get { return P("Скрытые таймеры не показаны", "Hidden timers not shown"); } }

        // A timer line: "До события «X» осталось 3 дня и 5 часов"
        internal static string Left { get { return P("До события «{0}» осталось {1}", "{1} left until «{0}»"); } }
        internal static string Due { get { return P("Наступил срок: «{0}»", "Now due: «{0}»"); } }
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
        internal static string LeftToDeadline { get { return P("До своего срока «{0}» осталось {1}", "{1} left until your deadline for «{0}»"); } }
        internal static string DeadlineDue { get { return P("Наступил свой срок: «{0}»", "Your deadline is due: «{0}»"); } }
        internal static string Gone { get { return P("Срок «{0}» прошёл: событие наступило.", "«{0}» has come: the event happened."); } }
        internal static string AffectedNote { get { return P("Зависит от коэффициентов королевства.", "Depends on the kingdom coefficients."); } }
        internal static string HiddenNote { get { return P("Скрытый: игра не показывает этот таймер.", "Hidden: the game does not show this timer."); } }
        internal static string CriticalNote { get { return P("По истечении срока что-то будет провалено.", "When it runs out something fails."); } }

        // Timer names
        internal static string ObjectiveFail { get { return P("Провал цели: {0} ({1})", "Objective fails: {0} ({1})"); } }
        internal static string QuestTrigger { get { return P("Событие задания: {0}", "Quest event: {0}"); } }
        internal static string EventStart { get { return P("Начало события: {0}", "Event starts: {0}"); } }
        internal static string TaskEnd { get { return P("Решение: {0}", "Resolved: {0}"); } }
        internal static string ProjectEnd { get { return P("Завершение проекта: {0}", "Project completes: {0}"); } }
        internal static string AutoResolve { get { return P("Исход события: {0}", "Event resolves: {0}"); } }
        internal static string IgnoredFail { get { return P("Провал без советника: {0}", "Fails without an advisor: {0}"); } }
        internal static string Recurrence { get { return P("Очередное действие: {0}", "Next tick: {0}"); } }
        internal static string Timeline { get { return P("Сюжетное событие королевства: {0}", "Kingdom story event: {0}"); } }
        internal static string BuffEnd { get { return P("Конец эффекта: {0}", "Effect ends: {0}"); } }
        internal static string Building { get { return P("Постройка: {0} ({1})", "Building: {0} ({1})"); } }
        internal static string Artisan { get { return P("Изделие мастера: {0}", "Artisan's item: {0}"); } }
        internal static string Income { get { return P("Доход казны: +{0} ОС", "Treasury income: +{0} BP"); } }
        internal static string Month { get { return P("Новый месяц королевства", "New kingdom month"); } }
        internal static string IgnoredEvent { get { return P("Провал проигнорированного события: {0}", "Ignored event fails: {0}"); } }
        internal static string Raven { get { return P("Доклад ворона", "Raven's report"); } }
        internal static string ReminderName { get { return P("Напоминание {0}", "Reminder {0}"); } }

        // Time page
        internal static string CoefEvents { get { return P("Решение событий королевства", "Kingdom event resolution"); } }
        internal static string CoefProjects { get { return P("Проекты королевства", "Kingdom projects"); } }
        internal static string CoefClaim { get { return P("Присоединение и улучшение регионов", "Claiming and upgrading regions"); } }
        internal static string CoefRankup { get { return P("Повышение рангов советников", "Advisor rank-ups"); } }
        internal static string CoefRuler { get { return P("Время правителя на проекты", "Ruler's time spent on projects"); } }
        internal static string CoefBuilding { get { return P("Строительство зданий", "Building construction"); } }
        internal static string CoefTravel { get { return P("Время в пути по карте", "Travel time on the map"); } }
        internal static string CoefRest { get { return P("Время привала", "Camping time"); } }
        internal static string Apply { get { return P("Применить коэффициенты", "Apply coefficients"); } }
        internal static string Reset { get { return P("Вернуть все к ×1", "Reset all to ×1"); } }
        internal static string Pending { get { return P("{0} (сейчас {1})", "{0} (now {1})"); } }
        internal static string SpanDays { get { return P("Срок: дни", "Span: days"); } }
        internal static string SpanHours { get { return P("Срок: часы", "Span: hours"); } }
        internal static string SkipRow { get { return P("Перемотать время на этот срок", "Skip time by this span"); } }
        internal static string RewindRow { get { return P("Отмотать время назад на этот срок", "Turn the clock back by this span"); } }
        internal static string RewindTitle { get { return P("Отмотать время", "Turn the Clock Back"); } }
        internal static string RewindAsk { get { return P("Отмотать время назад на {0}? Будет {1}.", "Turn the clock back by {0}? It will be {1}."); } }
        internal static string RewindLonger { get { return P("Все сроки станут дальше на {0}:", "Every deadline moves {0} further away:"); } }
        internal static string RewindLine { get { return P("• {0}: было {1}, станет {2}", "• {0}: was {1}, will be {2}"); } }
        internal static string RewindKingdom { get { return P("Королевство уже прожило дни до {0}: пока календарь не дойдёт до этой даты, в королевстве ничего не произойдёт — ни событий, ни дохода, ни нового месяца. Дела королевства продлятся на тот же срок.", "The kingdom has already lived the days up to {0}: until the calendar is there again nothing happens in the kingdom — no events, no income, no new month. Its tasks last that much longer."); } }
        internal static string RewindPast { get { return P("Прошедшее не вернётся: выполненные и проваленные задания, сработавшие события заданий, решённые события королевства и закончившиеся эффекты остаются как есть.", "What has happened stays: finished and failed quests, fired quest events, resolved kingdom events and expired effects remain as they are."); } }
        internal static string RewindUnits { get { return P("Эффекты на персонажах, усталость, привал и случайные встречи отматываются вместе с часами: их оставшееся время не меняется.", "Effects on characters, fatigue, camping and random encounters go back with the clock: their remaining time stays the same."); } }
        internal static string RewindLimit { get { return P("Раньше начала игры отмотать нельзя: не более чем на {0}.", "The clock cannot go back before the start of the game: at most {0}."); } }
        internal static string RewindDone { get { return P("Время отмотано на {0}. Сейчас {1}.", "Turned back by {0}. It is now {1}."); } }
        internal static string HRewind { get { return P("Отмотать", "Turn back"); } }
        internal static string ReminderRow { get { return P("Напомнить через этот срок", "Remind me after this span"); } }
        internal static string Section { get { return P("— {0} —", "— {0} —"); } }
        internal static string SecCoef { get { return P("Коэффициенты длительности", "Duration coefficients"); } }
        internal static string SecSpan { get { return P("Перемотка и напоминания", "Skipping and reminders"); } }
        internal static string CoefNote { get { return P("Множитель длительности: ×0.5 — вдвое быстрее, ×2 — вдвое дольше. Изменение вступает в силу по «Применить».", "Duration multiplier: ×0.5 is twice as fast, ×2 twice as long. Takes effect on Apply."); } }
        internal static string BuildingNote { get { return P("Действует на здания, заложенные после изменения.", "Applies to buildings started after the change."); } }
        internal static string TravelNote { get { return P("Сколько игрового времени уходит на путь: ×0.5 — дни идут вдвое медленнее.", "Game time spent travelling: ×0.5 makes days pass half as fast."); } }
        internal static string RestNote { get { return P("Сколько игрового времени уходит на привал и сон.", "Game time spent camping and sleeping."); } }
        internal static string MouseNote { get { return P("Мышь: клик по левой/правой половине строки — меньше/больше.", "Mouse: click the left/right half of the row for less/more."); } }
        internal static string ReminderAdded { get { return P("Напоминание «{0}» создано: {1}.", "Reminder «{0}» set: {1}."); } }

        // Setup page
        internal static string SetupDeadline { get { return P("Свой срок: за {0} до события", "Own deadline: {0} before the event"); } }
        internal static string SetupDeadlineZero { get { return P("Свой срок: в момент события", "Own deadline: when the event happens"); } }
        internal static string SetupAlert { get { return P("Оповестить {0}", "Alert {0}"); } }
        internal static string SetupStop { get { return P("Перестать отслеживать", "Stop tracking"); } }

        // Hints
        internal static string HSelect { get { return P("Выбор", "Select"); } }
        internal static string HTrack { get { return P("Отслеживать", "Track"); } }
        internal static string HSetup { get { return P("Настроить", "Set up"); } }
        internal static string HHiddenOn { get { return P("Показать скрытые", "Show hidden"); } }
        internal static string HHiddenOff { get { return P("Убрать скрытые", "Hide hidden"); } }
        internal static string HTab { get { return P("Вкладка", "Tab"); } }
        internal static string HClose { get { return P("Закрыть", "Close"); } }
        internal static string HBack { get { return P("Назад", "Back"); } }
        internal static string HChange { get { return P("Изменить", "Change"); } }
        internal static string HDo { get { return P("Выполнить", "Do"); } }
        internal static string HToggle { get { return P("Вкл/выкл", "On/off"); } }
        internal static string HOk { get { return P("Понятно", "OK"); } }
        internal static string HOpen { get { return P("Открыть сроки", "Open deadlines"); } }
        internal static string HUntrack { get { return P("Не отслеживать", "Stop tracking"); } }
        internal static string HCancel { get { return P("Отмена", "Cancel"); } }
        internal static string HSkip { get { return P("Перемотать", "Skip"); } }
        internal static string HSkipMax { get { return P("Перемотать на {0}", "Skip {0}"); } }

        // Coefficient check
        internal static string CheckChanged { get { return P("«{0}»: было {1}, стало {2}", "«{0}»: was {1}, now {2}"); } }
        internal static string CheckNow { get { return P("«{0}»: было {1}, теперь срок уже наступил — произойдёт при смене дня", "«{0}»: was {1}, now already due — happens when the day changes"); } }
        internal static string CheckNone { get { return P("Текущие сроки королевства не изменились.", "No current kingdom deadline changed."); } }
        internal static string CheckApplied { get { return P("Коэффициенты применены.", "Coefficients applied."); } }
        internal static string CheckTravel { get { return P("Время в пути ×{0}, привал ×{1}: оставшиеся сроки те же, но в пути и на привале они будут расходоваться {2}.", "Travel ×{0}, camping ×{1}: the remaining time is the same, but travelling and camping use it {2}."); } }
        internal static string Slower { get { return P("медленнее", "more slowly"); } }
        internal static string Faster { get { return P("быстрее", "faster"); } }
        internal static string AsBefore { get { return P("как раньше", "as before"); } }
        internal static string Nearest { get { return P("Ближайшие сроки:", "Nearest deadlines:"); } }
        internal static string More { get { return P("… и ещё {0}", "… and {0} more"); } }

        // Skip
        internal static string SkipAsk { get { return P("Перемотать время на {0}? Будет {1}.", "Skip {0}? It will be {1}."); } }
        internal static string SkipWillHappen { get { return P("За это время наступит:", "During this time:"); } }
        internal static string SkipNothing { get { return P("За это время сроков не наступит.", "No deadline comes in this time."); } }
        internal static string SkipBlocked { get { return P("Перемотка на {0} невозможна: она пропустит срок, после которого что-то будет провалено.", "Skipping {0} is not possible: it would pass a deadline after which something fails."); } }
        internal static string SkipMax { get { return P("Без провала можно перемотать не более чем на {0}.", "You can skip at most {0} without a failure."); } }
        internal static string SkipNoMax { get { return P("Срок наступает меньше чем через час — перемотка невозможна.", "A deadline comes in less than an hour — no skip possible."); } }
        internal static string SkipNotNow { get { return P("Сейчас перематывать время нельзя: только на локации или глобальной карте, вне боя, диалога и сценки.", "Time cannot be skipped now: only in an area or on the global map, out of combat, dialogue and cutscenes."); } }
        internal static string SkipDone { get { return P("Время перемотано на {0}. Сейчас {1}.", "Skipped {0}. It is now {1}."); } }
        internal static string SkipZero { get { return P("Задайте срок больше нуля.", "Set a span above zero."); } }
        internal static string TrackedDeadline { get { return P("свой срок", "own deadline"); } }

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
