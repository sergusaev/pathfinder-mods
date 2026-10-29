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

### Hit points missing over a unit with the gamepad interface

**Symptom.** With the gamepad interface a unit's hit points (or another part of its overtip) are sometimes not shown, although the settings show them over every other unit; it stays so for that unit. Seen on one of several zombies in turn-based combat.

**Cause.** Each part of an overtip switches itself on and off by the unit's visibility, subscribed to in `OvertipViewPartBase.BindWithSettings`. The subscription is dropped and never disposed. Overtips are reused for other units, and the unit the overtip showed before still switches the part: when it goes out of sight, the part over the new unit is switched off.

**Fix.** A transpiler keeps the subscription with the binding, so it is disposed when the overtip is bound to another unit; a change of a unit the part no longer shows is ignored.

### A touch spell's name twice over the head

**Symptom.** One Cure Light Wounds (or another touch spell), or one potion of such a spell, shows the spell's name over the character's head twice in a row. It happens with the gamepad and with keyboard and mouse alike.

**Cause.** A touch spell is cast twice. First the spell itself is cast: it charges the character's hand with a separate touch ability of the same name (`AbilityEffectStickyTouch`, `UnitPartTouch`). Then that touch is cast. The text over the head is written for each cast (`RuleCastSpell`), by `OvertipsVM` in the gamepad interface and by `CombatTextManager` in the mouse interface, so the same name shows twice.

**Fix.** Prefixes skip the text for the touch that the character holds as the charge of the same spell. The spell's own name shows as before, and the combat log is not changed.

### Custom portraits in the gamepad character creation

**Symptom.** With the gamepad interface, character creation offers only the built-in portraits: the game does not read the `Portraits` folder there, although the mouse interface does.

**Fix.** A postfix on the console portrait step (`CharGenPortraitPhaseVM`) appends every portrait from the folder after the built-in ones, sorted by folder name. Choosing and saving work as usual: a custom portrait is stored in the character itself, so saves do not depend on the mod. Thumbnails load lazily, so the first opening of the step may pause. The UMM log gets `custom portraits: added N`.

Each portrait is a subfolder of `Portraits` with `Small.png` (185×242), `Medium.png` (330×432) and `Fulllength.png` (692×1024):

- Steam Deck, Windows build under Proton: `~/.local/share/Steam/steamapps/compatdata/640820/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Kingmaker/Portraits/`
- Windows: `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Kingmaker\Portraits\`

If a save uses a portrait whose folder has no pictures, the game puts its default placeholder pictures there by itself. If the main character still has the placeholder after you moved your saves, replace the three PNG files in that portrait's folder with the real ones.

This fix used to be a separate mod, Custom Portraits Gamepad Selection Fix (`Mods/ConsoleCustomPortraits`). Remove that folder: while the old mod is on, Kingmaker Fixes leaves the portraits to it so that they are not listed twice, and says so in the log.

### No zoom for the character doll with the gamepad interface

**Symptom.** In the gamepad interface the right stick turns the character doll in the inventory, but nothing zooms it, and the mouse wheel over the doll does nothing either. The item description (R3) is also remembered between openings, so the inventory may open with it on.

**Cause.** The doll room of the gamepad interface keeps the game's doll camera (`DollCamera`) switched off, so its zoom does nothing and `DollCamera.Current` stays empty. The stick's vertical axis reaches the inventory only while the horizontal one is active too (`InputLayer.AddCursor`), and the inventory uses it to scroll the stash behind the doll page.

**Fix.** The right stick up zooms in and down zooms out, in the inventory on the doll page with the item description off (with it on, the stick scrolls it as before) and on the appearance page of character creation; left and right turn the doll as before, and on the race and class pages the stick keeps scrolling their text. The mouse wheel over the doll zooms in the gamepad interface too; in the keyboard and mouse interface the game's own wheel zoom stays. In the gamepad interface the mod moves the camera itself toward the doll's head, smoothly, and puts it back when the doll is closed or another page of character creation opens. The inventory opens with the item description off, and the hint row shows the right stick with "Zoom". While a window of our mods is open, the doll is left alone.

### Diagnostics: the game started in the keyboard interface after pressing A

**Symptom.** Now and then, with the gamepad connected, the start window "press A or any key" started the game in the keyboard and mouse interface after A was pressed. It came after the game window flickered while going full screen and has not been seen since.

**Suspected cause.** The window picks the keyboard on any keyboard key, checked after the gamepad buttons. While the game window is out of focus, Steam Input may send the gamepad through its desktop layout, where A is Enter.

**What the mod does.** Nothing is changed: when the keyboard interface is chosen, the UMM log gets the keys that chose it, for example `controller mode window: the keyboard interface is chosen by Return`.

## On the Steam Deck

Nothing to bind and no settings. Setting up Unity Mod Manager: [the Steam Deck guide](../docs/steam-deck.md).

Written for Kingmaker 2.1.7b, UMM 0.32.4, Harmony 2.3.6.

## Building

See the [repository README](../README.md#building-from-source): `./build.sh`, `./build.sh --install`.
