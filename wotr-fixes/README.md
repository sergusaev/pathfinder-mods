# WotR Fixes (Pathfinder: Wrath of the Righteous)

[Русская версия](README.ru.md)

A Unity Mod Manager mod that collects small fixes of the game's own bugs. Each fix changes only what is broken and leaves the game's behaviour otherwise as it was. Saves do not depend on the mod. The same kind of mod for Kingmaker: [Kingmaker Fixes](../kingmaker-fixes).

## Fixes

### A touch spell's name twice over the head

**Symptom.** One Cure Light Wounds (or another touch spell), or one potion of such a spell, shows the spell's name over the character's head twice in a row, with the gamepad and with keyboard and mouse alike.

**Cause.** A touch spell is cast twice. First the spell itself is cast: it charges the character's hand with a separate touch ability of the same name (`AbilityEffectStickyTouch`, `UnitPartTouch`). Then that touch is cast. The text over the head is written for each cast (`RuleCastSpell`), by `OvertipsVM` in the gamepad interface and by `CombatTextManager` in the mouse interface, so the same name shows twice. Kingmaker has the same bug.

**Fix.** Prefixes skip the text for the touch that the character holds as the charge of the same spell. The spell's own name shows as before, and the combat log is not changed.

The fix is the same as in Kingmaker Fixes, where it is tested in the game; in Wrath of the Righteous it is checked against the game code only.

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

## Installing

Unpack the release zip into the game's `Mods` folder, so that the mod is in `Mods/WotrFixes`. Nothing to bind and no settings. Setting up Unity Mod Manager: [Steam Deck](../docs/steam-deck.md), [Windows](../docs/windows.md); installing the mod: [by hand](../docs/mods.md).

## Building

See the [repository README](../README.md#building-from-source): `./build.sh`, `./build.sh --install`.
