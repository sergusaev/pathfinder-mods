# WotR Fixes (Pathfinder: Wrath of the Righteous)

[Русская версия](README.ru.md)

A Unity Mod Manager mod that collects small fixes of the game's own bugs. Each fix changes only what is broken and leaves the game's behaviour otherwise as it was. Saves do not depend on the mod. The same kind of mod for Kingmaker: [Kingmaker Fixes](../kingmaker-fixes).

## Fixes

### A touch spell's name twice over the head

**Symptom.** One Cure Light Wounds (or another touch spell), or one potion of such a spell, shows the spell's name over the character's head twice in a row, with the gamepad and with keyboard and mouse alike.

**Cause.** A touch spell is cast twice. First the spell itself is cast: it charges the character's hand with a separate touch ability of the same name (`AbilityEffectStickyTouch`, `UnitPartTouch`). Then that touch is cast. The text over the head is written for each cast (`RuleCastSpell`), by `OvertipsVM` in the gamepad interface and by `CombatTextManager` in the mouse interface, so the same name shows twice. Kingmaker has the same bug.

**Fix.** Prefixes skip the text for the touch that the character holds as the charge of the same spell. The spell's own name shows as before, and the combat log is not changed.

## Installing

Unpack the release zip into the game's `Mods` folder, so that the mod is in `Mods/WotrFixes`. Nothing to bind and no settings. Setting up Unity Mod Manager: [the Steam Deck guide](../docs/steam-deck.md), [Windows](../docs/windows.md).

The fix is the same as in Kingmaker Fixes, where it is tested in the game; in Wrath of the Righteous it is checked against the game code only.

## Building

See the [repository README](../README.md#building): `./build.sh`, `./build.sh --install`.
