# Success Chance (Pathfinder: Kingmaker and Wrath of the Righteous)

[Русская версия](README.ru.md)

A Unity Mod Manager mod for Kingmaker and Wrath of the Righteous that shows, in turn-based combat, the chance that an attack or a spell succeeds and the damage it does, over the enemy you point at, before you act. It works with a gamepad as well as with keyboard and mouse.

## What it shows

The text stands over the enemy where the game shows its own floating text (a miss, damage), in the same font, its lines aligned left (in Kingmaker as the game's own text); percentages are coloured from red to green, damage is red.

- **Attack:** `95%, 3-10 (crit 5%, 9-30)` — the chance to hit, the damage of a hit, then the chance of a confirmed critical hit and its damage.
- **Full attack:** a line for each attack, as for a single one; the first attack of the series on top.
- **Maneuver on a hit** (a wolf's trip with its bite): after the damage, e.g. `90%, 14-21, trip 72%` — the chance that the attack hits and the maneuver succeeds.
- **Spell:** `Hit 80% Resistance 80% Fails Will 45%, 10-60 / 5-30` — the touch attack, getting through spell resistance, the target failing its save, then the damage when it fails / when it saves. When several projectiles each roll their own attack (Scorching Ray) there is a line for each; when they roll none (Magic Missile) their count stands before the damage: `4x(2-5)`. `Immune` when spell resistance cannot be overcome at all; a part that cannot fail (no resistance, no save) is left out. A spell held as a charge (Inflict Wounds) is counted as its touch, with the save and damage of the spell the touch casts, and spell resistance checked by both.
- **Combat maneuver by an ability** (trip, bull rush, dirty trick): `Trip 65%` — the chance that the combat maneuver bonus with a d20 reaches the target's combat maneuver defence.

The text appears only when the action can be done now: on your character's turn, with an attack still available (a pointer that would only move shows nothing) or with a spell chosen and waiting for its target. While the game's own floating text is over the target, the mod's text gives way to it. The mod does nothing in real time: there is no time to read a chance there.

## How it is calculated

The mod does not repeat the game's formulas: it runs the game's own rules "dry" — the attack bonus, armour class, flat-footedness, concealment and miss chances, critical threat and confirmation, the spell's DC and caster level, spell resistance, saving throws — with every handler that only adds bonuses, and without anything that would roll dice, spend resources or write to the combat log. So everything the game counts is counted: buffs and debuffs, conditions, feats, class features, items, difficulty settings and other mods. Rerolls ("roll twice, take the better", "reroll if failed") are counted exactly; a natural 1 and 20 too.

Results are cached and calculated again only when something that can change them happens to the attacker or the target (a buff, equipment, a new round, a turn-based mode change), not every frame.

In Wrath of the Righteous the same is counted by that game's rules: rerolls of the d20 add up with their sign (a "take the better" and a "take the worse" cancel out), conditions of a reroll test the first die, the attack roll's own bonuses go to the attack bonus, a natural 1 can hit with the feature that allows it, a persistent spell makes the target save twice, and buffs put on right before an attack with a weapon (Power Attack, Deadly Aim) count for the first attack, both their attack bonus and their damage; a maneuver fails on a natural 1 and succeeds on a 20 (Kingmaker has no such rule for maneuvers).

## Overtips (Kingmaker)

With both controls the overtip over a unit (name, hit points, action, cooldown) now follows the rules of the keyboard-and-mouse interface and its settings; the gamepad settings' "always show hit points" checkboxes still apply. With keyboard and mouse the floating texts stand a little higher over the overtip, so they do not stick to its edge.

## Log

`Mods/SuccessChance/SuccessChance.log` (the previous session's is kept as `SuccessChance.prev.log`): every attack, saving throw, spell resistance check and combat maneuver in turn-based combat, with the predicted chance, its breakdown by source and the real roll, so the prediction can be checked against the game. Can be turned off in the mod's UMM window, as can the text over enemies.

## Not counted yet

Skill checks; spells cast through activatable abilities; spells with variants. In Wrath of the Righteous: a skill check that replaces the caster level against spell resistance, and "a 1 counts as the maximum" on the d20.

## Building

`./build.sh [--game kingmaker|wotr] [--install]` builds for both games (or one), see [Building](../README.md#building-from-source).
