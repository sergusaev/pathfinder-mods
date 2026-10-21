# WotR Fixes (Pathfinder: Wrath of the Righteous)

[Русская версия](README.ru.md)

A Unity Mod Manager mod that collects small fixes of the game's own bugs. Each fix changes only what is broken and leaves the game's behaviour otherwise as it was. Saves do not depend on the mod. The same kind of mod for Kingmaker: [Kingmaker Fixes](../kingmaker-fixes).

## Fixes

### A touch spell's name twice over the head

**Symptom.** One Cure Light Wounds (or another touch spell), or one potion of such a spell, shows the spell's name over the character's head twice in a row, with the gamepad and with keyboard and mouse alike.

**Cause.** A touch spell is cast twice. First the spell itself is cast: it charges the character's hand with a separate touch ability of the same name (`AbilityEffectStickyTouch`, `UnitPartTouch`). Then that touch is cast. The text over the head is written for each cast (`RuleCastSpell`), by `OvertipsVM` in the gamepad interface and by `CombatTextManager` in the mouse interface, so the same name shows twice. Kingmaker has the same bug.

**Fix.** Prefixes skip the text for the touch that the character holds as the charge of the same spell. The spell's own name shows as before, and the combat log is not changed.

The fix is the same as in Kingmaker Fixes, where it is tested in the game; in Wrath of the Righteous it is checked against the game code only.

### Combat text to the right of the head instead of over it

**Symptom.** Damage, misses, saving throws and spell names pop up to the right of the unit's head instead of over its middle. In Kingmaker the same text stands right over the head. With the gamepad and with keyboard and mouse alike.

**Cause.** In Kingmaker the text lives inside the unit's overtip. Wrath of the Righteous moves it to a shared container (`OvertipsView.CombatTextContainer`) and every frame puts the text's pivot on the overtip part (`OvertipViewPartCombatText.GetCombatTextPosition`). An absent spell icon is hidden by alpha only, so its place stays. The text thus starts at the head and runs to the right. Stacked texts are 25 screen pixels apart rather than 25 interface units, so at an interface scale other than 1:1 they close up or drift apart.

**Fix.** The visible text with its icon is centred over the overtip's anchor, the point over the head. The height above the name, the HP bar and a bark stays the game's own (`UnitOvertipView.TweenComponentsToPlace`), as in Kingmaker. Stacked texts are 25 interface units apart. Once per session the mod writes the layout it works with to the UMM log (`combat text layout`). On an error the fix switches itself off and logs the error.

### Hints around the compass lie on its ring and on each other

**How it shows.** In the gamepad interface the hints around the compass in the lower left corner cover it: the Cursor and Highlight icons lie on the ring, the camera mode hint covers the tip of the arrow, and Skip Time sits on the Highlight hint.

**Cause.** The game places the hints at fixed points that do not allow for the arrow, which turns with the camera and reaches past the ring.

**Fix.** The hints on the right stand on one arc around the compass, as in Kingmaker with [Camera Rotation and Compass](../kingmaker-gamepad-camera-rotation). The icons of Pause, Cursor, Menu and Highlight keep their heights and stand at one distance from the compass centre, 4 interface units clear of the arrow's reach. The camera mode hint stands above the compass, clear of the hints next to it. Skip Time, which the game puts below the compass almost at the screen's edge, moves right above the camera mode hint, icon over icon. The places are measured on screen every frame, after the game's layout. The layout is written to the UMM log 2, 10 and 30 seconds after the hints appear (`compass hints after`). On an error the fix turns itself off and writes it to the log.

### Custom portraits do not scroll with the gamepad

**Symptom.** In the gamepad interface the "Custom" tab of the character creation portraits does not follow the cursor: the cursor leaves the screen, and the right stick does not move the list.

**Cause.**

- Custom portraits in the console interface (`CharGenCustomPortraitGroupConsoleView`) live in a virtual list `VirtualListGridVertical`. The focused object is a `VirtualListElement`, a plain object rather than a `MonoBehaviour`.
- `CharGenPortraitPhaseDetailedConsoleView.SetSelected` scrolls only the outer `ScrollRect` and only to a `MonoBehaviour`, so for custom portraits it does nothing.
- The standard `ScrollController.ForceScrollToElement` only works with rows the list has already laid out: the visible ones plus one spare.
- The right stick (`CharGenPortraitPhaseDetailedConsoleView.Scroll`) scrolls the outer `ScrollRect`, which does not contain the virtual list.

**Fix.**

- The list follows the cursor for the D-pad, the left stick, single presses and auto-repeat, all the way to both ends. Every frame the focused element is pulled into view with `ScrollController.ScrollTowards`, which also handles far rows not laid out yet.
- The right stick on the custom portraits tab moves the cursor by rows: threshold 0.5, the first step at once, auto-repeat after 0.35 s every 0.09 s. On other tabs the stick works as before.
- On an error the focus follower switches itself off and the error goes to the UMM log.

Custom portraits are subfolders of `Portraits` with `Small.png` (185×242), `Medium.png` (330×432) and `Fulllength.png` (692×1024); nested subfolders are not seen by the game:

- Steam Deck / Proton: `~/.local/share/Steam/steamapps/compatdata/1184370/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Wrath Of The Righteous/Portraits/`
- Windows: `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Wrath Of The Righteous\Portraits\`

This used to be the separate mod Portrait Scroll Fix (gamepad) (`Mods/PortraitScrollFix`). Delete its folder: while the old mod is on, WotR Fixes leaves the scrolling to it, so the list is not scrolled twice, and says so in the log. Tested in the game on the Steam Deck as the old mod.

### The game keeps running after quitting on the Steam Deck

**Symptom.** After quitting to the desktop the game is gone from the screen, but Steam shows it still running and does not start it again; the game ends only when Steam is told to stop it.

**Cause.** The game quits to the end: `Player.log` gets its last lines. But the process stays: its main thread is over while the threads of the engine (DXVK, sound, input) wait for it forever. In the Steam log every session on the deck ended only when the game was started next time.

**Fix.** When the game has done its own work at quitting (`Game.HandleQuit`: settings, saves, temporary area files, logs), the mod starts a system timer. If the process is still running 3 seconds later, the timer ends it. Saves are not affected: they are written before that. If the game never gets to that work, a second timer ends the process 15 seconds after quitting starts. A game that ends by itself is gone before the timers fire. The UMM log gets the line `quit: the process is ended in 3 s if it is still running`. Tested in the game on the Steam Deck.

## Settings: smooth frames on the Steam Deck

Both are off by default and live in the mod's settings in the Unity Mod Manager window. Measured on a Steam Deck OLED (90 Hz) with the graphics lowered: SSR and depth of field off, SSAO and shadows Medium, FXAA.

**Frame pacing.** With vsync the game shows a frame every refresh of the screen or none. When it cannot draw a frame every refresh, frames stay on the screen for different times: on the Deck at about 35 fps they took 22, 33 and 44 ms by turns, and the picture judders. The frame limit of the Deck's quick access menu does not help: it limits only the frames shown, while the game goes on drawing faster (about 57 frames a second with 30 shown). The setting "a frame every N refreshes" (2nd, 3rd or 4th) makes the engine itself wait for every N-th refresh (`QualitySettings.vSyncCount`): on the 90 Hz Deck "every 3rd" gives 30 fps, every frame 33 ms. The setting is kept when the game applies its graphics settings. On the Deck turn the quick access menu frame limit off or set it to the same rate.

**Incremental garbage collection.** The engine stops the game for the whole collection of unused memory: on the Deck about 0.3 s every two to three minutes of play. With incremental collection the engine does it in slices of 3 ms between frames. The engine reads it only from `Wrath_Data/boot.config` at start (`gc-max-time-slice=3`), so the setting writes that line and takes effect at the next start of the game. A Steam update or file check puts the game's own `boot.config` back; the mod writes the line again at the next start, so the collection is incremental again from the start after that. Turned off in the settings, the line is taken out. The settings show whether incremental collection is on now.

## Installing

Unpack the release zip into the game's `Mods` folder, so that the mod is in `Mods/WotrFixes`. Nothing to bind; the settings are optional (above). Setting up Unity Mod Manager: [Steam Deck](../docs/steam-deck.md), [Windows](../docs/windows.md); installing the mod: [by hand](../docs/mods.md).

## Building

See the [repository README](../README.md#building-from-source): `./build.sh`, `./build.sh --install`.
