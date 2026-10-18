# Time Control and Deadlines (Pathfinder: Kingmaker)

[Русская версия](README.ru.md)

A Unity Mod Manager mod for Kingmaker that controls how game time passes and keeps track of deadlines. Fractional multipliers for kingdom durations, travel and camping time; a list of every countdown in the game, including the ones the game does not show; tracking of chosen deadlines with your own deadline and alerts; a time skip that will not jump over a deadline after which something fails.

## The Time and Deadlines window

Opened from the game's own interface, with any controls: with mouse and keyboard — the hourglass button to the right of the Buff It 2 The Limit bar (without that mod, at the bar's place above the game's menu buttons); with a gamepad — the card in the game's menu, after the game's cards, on the local and the global map. The mod's UMM window has an "Open" button as well. While the window is open the game is paused in real time (not in turn-based combat), travel on the global map stops, and the camera and the game's buttons do nothing behind it.

Tabs switch with LB/RB (keyboard X/C or PageUp/PageDown, mouse: click the tab name).

- **Deadlines** — all countdowns, soonest first: "3 days and 5 hours left until «X»". Marks: *failure* — something fails when it runs out, *hidden* — the game does not show this timer; an eye before the date marks a tracked one. Below the list: the due date and details. A — track / stop tracking, X — tracking setup, Y — show or hide hidden timers.
- **Tracked** — tracked deadlines only. A — setup, Y — stop tracking.
- **Time** — coefficients, time skip and turning the clock back.
- **Help** — how the mod works and its controls.
- **Tracking setup** — your own deadline (how long before the event you need to be done: an hour to two weeks) and alerts counted from it: a month, two weeks, a week, 3 days, a day, 12, 6 and 1 hour before, and when due.

Controls as in the buff menu: D-pad / left stick / W S A D / arrows select and change, A (E, Enter, Space) acts, B (Q, Backspace, right mouse button) goes back and closes. Start or Esc opens the game's menu over the window, as over the game's own windows; the window comes back after it. Mouse: a click selects a row, a second click acts; on rows with a value a click on the left or right half lowers or raises it; the wheel scrolls; a click on a button hint does what the button does.

## Timers

- **Quests:** objectives that fail on a deadline (`AutoFailDays`), hidden objectives included; hidden timed quest events (`TimedObjectiveTrigger`).
- **Kingdom:** resolution of events and projects by advisors; events that resolve themselves; an event nobody took (at the start of a month an event without an advisor that began more than 10 days earlier fails, unless a free advisor is assigned); ignored events; the next tick of recurring events; random events planned for the month (hidden); story events on kingdom days (hidden); end of kingdom effects; buildings under construction; artisans' items; weekly treasury income; start of the month; the raven's report (hidden).

Deadlines after which something fails are tracked as soon as they appear (can be turned off in UMM). Once you stop tracking one, the mod does not add it again.

## Alerts

When a tracked deadline reaches a chosen mark, a "Reminder" window pops up on the game's parchment, with the game's new-entry sound and the game paused (the pause can be turned off in UMM): "1 day and 3 hours left until «X»" or "Now due". A — OK, X — open deadlines, Y — stop tracking. Several alerts at once share one window. Alerts whose time passed before tracking began, or in another branch of saves, are not shown.

Tracking is kept per playthrough (`Player.GameId`) in `Mods/TimeControl/Tracking/<GameId>.json`, shared by all saves of that playthrough.

## Coefficients

Duration multiplier: ×0.5 is twice as fast, ×2 twice as long, from ×0.1 to ×10, any fraction (typed in UMM, in steps of 0.05 / 0.1 / 0.25 in the game window).

| Coefficient | Applies to |
|---|---|
| Kingdom event resolution | resolution time of problems and opportunities, and of events that resolve themselves |
| Kingdom projects | project time |
| Claiming and upgrading regions | claim and upgrade projects |
| Advisor rank-ups | rank-up projects |
| Ruler's time spent on projects | days the ruler spends on a project |
| Building construction | construction time; buildings started after the change only |
| Travel time on the map | game hours spent travelling on the global map |
| Camping time | game time spent camping, hunting and sleeping |

The game recomputes kingdom durations every time, so a new multiplier also moves events and projects already under way. Coefficients take effect on Apply; right after it the Deadline Check window shows which current deadlines moved: "was 5 days, now 2 days and 12 hours", or that a deadline is now already due and happens when the day changes. When travel or camping time changes, it shows the nearest deadlines: the time left is the same, but it will be used up faster or more slowly.

## Time skip

On the Time tab set a span (days and hours) and choose Skip. Skipping works in an area or on the global map, out of combat, dialogue and cutscenes. Time goes day by day, as in play: the kingdom processes every day of its calendar and quest deadlines are checked after each day; at the end the time of day is matched, as the game's own cheat does.

Before skipping the mod checks the deadlines:

- if a deadline after which something fails (a quest objective, an event without an advisor) or your own deadline of a tracked event comes during the span, the skip is refused; the window lists those deadlines and offers to skip the longest safe span (up to an hour before the first one);
- otherwise the confirmation lists everything that comes during the span, with dates.

## Turning the clock back

"Turn the clock back by this span" moves the game clock back (not before the start of the game). Everything tied to the calendar moves away by the same span: quest deadlines, timed quest events, every kingdom task and event; alerts that are ahead again fire once more. Before turning back the window shows how the deadlines change.

What has happened does not come back, by the way the game's code works: finished and failed objectives keep their state, fired timed quest events are marked (`DidTrigger`), the kingdom lives its days only forward (`CurrentDay` never decreases), so resolved events, income and the start of a month are not repeated. The price: after turning back the kingdom waits; until the calendar reaches the day it has already lived nothing happens in it (its date shows the calendar's meanwhile). Turning back does not touch effects on characters, temporary item enchantments, fatigue, rest and random encounters: they stay as they are now, with the same time left. Scripts that check the day of the week or month see the earlier date.

## Installation

1. Unity Mod Manager must be installed in the game: [Steam Deck](../docs/steam-deck.md), [Windows](../docs/windows.md). How to unpack, update and remove a mod, with commands: [installing mods by hand](../docs/mods.md).
2. Unpack the zip from [Releases](https://github.com/sergusaev/pathfinder-mods/releases) into the game's `Mods` folder, so that you get `Mods/TimeControl/Info.json`.
3. Start the game; "Time Control and Deadlines" appears in the UMM window.

## Building

`./build.sh`, `./build.sh --install`; see the [repository README](../README.md). The bar button is drawn in the look of the game's menu buttons the same way the Buff It 2 The Limit bar is; the windows use the game's parchment, font, rule and initials, as the buff menu does.
