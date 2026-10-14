# Kingmaker Fixes (Pathfinder: Kingmaker)

[Русская версия](README.ru.md)

A Unity Mod Manager mod that collects small fixes of the game's own bugs. Each fix changes only what is broken and leaves the game's behaviour otherwise as it was. Saves do not depend on the mod.

## Fixes

### A false "immune thanks to the difficulty settings" line in the combat log

**Symptom.** After a buff or an effect is applied, the combat log says that the target "is immune to <effect> thanks to the game's difficulty settings", although the effect is on (the Inspect window shows it). Seen with Studied Target on an enemy.

**Cause.** Both combat logs, the console one (`CombatLogVM`) and the PC one (`BattleLogManager`), write this line for every visible buff with a name that goes through `RuleApplyBuff`. They do not look at `RuleApplyBuff.StoryModeImmunity`, the flag the rule sets when the difficulty really kept a negative effect off a member of the party in combat. The log of energy drain does check its flag.

**Fix.** A transpiler puts the check `evt.StoryModeImmunity` before the line in both logs. The line stays where the difficulty did keep the effect off; the other lines of the handler, such as spell immunity, are untouched. The UMM log gets `story mode immunity line: fixed in CombatLogVM` and `... BattleLogManager`.

### "Error in Observer" on entering the global map

**Symptom.** On entering the global map with the gamepad interface, the log gets a burst of the same `Error in Observer: System.NullReferenceException` in `GlobalMapCenterArrowsVm.OnUpdateHandler`. Nothing is visible in the game.

**Cause.** The view model of the arrows that point back to the party starts updating every frame before the global map rules are loaded: `GlobalMapRules.Instance` is still null.

**Fix.** A prefix skips the update until the rules, the party pawn and the camera exist; after that the arrows work as before.

### A character's voiced lines stuck after an area loads

**Symptom.** While an area loads, the log gets `NullReferenceException` in `UnitAsksComponent+Bark.Play`. After that the character may go silent: that line never plays again, and the character's other ordinary lines do not play either, until the area is reloaded.

**Cause.** An animation asks for a voiced line of a character whose view is not on the scene yet. `Bark.Play` first marks the line as playing and the character as busy, then fails on the missing view, so neither mark is ever cleared.

**Fix.** A prefix skips a voiced line while the character has no view, leaving both marks untouched; the line plays the next time it is asked. Lines without sound are not affected.

### "Reroll if failed" on attacks rerolled hits too

**Symptom.** An ability that rerolls an attack roll only when it misses also rerolled hits; a single-use one was spent on an attack that had already hit.

**Cause.** `RuleAttackRoll.IsSuccessRoll(d20)` adds the attack's own roll instead of the die it is given. "Reroll only if failed" (`ModifyD20`) asks it about the first die before the attack's roll is set, so every die but a natural 20 counted as a miss. Wrath of the Righteous tests the die here.

**Fix.** A prefix makes the test use the die it is given. The attack roll itself is not changed: it already asks with its own roll.

### Custom portraits in the gamepad character creation

**Symptom.** With the gamepad interface, character creation offers only the built-in portraits: the game does not read the `Portraits` folder there, although the mouse interface does.

**Fix.** A postfix on the console portrait step (`CharGenPortraitPhaseVM`) appends every portrait from the folder after the built-in ones, sorted by folder name. Choosing and saving work as usual: a custom portrait is stored in the character itself, so saves do not depend on the mod. Thumbnails load lazily, so the first opening of the step may pause. The UMM log gets `custom portraits: added N`.

Each portrait is a subfolder of `Portraits` with `Small.png` (185×242), `Medium.png` (330×432) and `Fulllength.png` (692×1024):

- Linux (native build, Steam Deck): `~/.config/unity3d/Owlcat Games/Pathfinder Kingmaker/Portraits/`
- Windows: `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Kingmaker\Portraits\`

This fix used to be a separate mod, Custom Portraits Gamepad Selection Fix (`Mods/ConsoleCustomPortraits`). Remove that folder: while the old mod is on, Kingmaker Fixes leaves the portraits to it so that they are not listed twice, and says so in the log.

## On the Steam Deck

Nothing to bind and no settings. Setting up Unity Mod Manager: [the Steam Deck guide](../docs/steam-deck.md).

Written for Kingmaker 2.1.7b, UMM 0.32.4, Harmony 2.3.6.

## Building

See the [repository README](../README.md#building): `./build.sh`, `./build.sh --install`.
