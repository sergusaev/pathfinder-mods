# Bonus Overlap Warnings (Pathfinder: Kingmaker)

[Русская версия](README.ru.md)

Bonuses of the same type usually do not stack in Pathfinder: of two deflection bonuses to AC only the higher one works. The mod shows on the character's doll which equipped item gives nothing, or less than it says, because of that.

## Marks on the doll

A diamond with an exclamation mark appears in the upper left corner of an equipped item's icon:

- **red**: none of the item's bonuses has effect. Example: a ring of protection +1 while Shield of Faith +2 is on the character;
- **yellow**: some of the item's bonuses have no effect. Example: a belt of +2 Strength, Dexterity and Constitution while Bull's Strength +4 overrides the Strength;
- **blue**: the item works, but another source's bonus has no effect because of it. Example: a cloak of resistance +2 and the Resistance spell +1. The blue mark can be turned off in the mod's settings.

The marks are shown in the inventory and the vendor's window, with the mouse and the gamepad (there the mark is a little smaller and drawn over the slot's selection frame), for pets too. They are refreshed once a second, so buffs cast or ending with the inventory open show at once.

## The item's tooltip

The tooltip of an equipped item (and its details window) tells under its bonuses what does not stack and with what, for example:

> Deflection +1 (Armor Class): no effect — overridden by effect "Shield of Faith" (Harrim)

The source is named with its kind (item, effect, ability) and whose it is when not the character's own. A bonus given through a chain, such as the master's ring giving the pet an ability, names the item at the chain's start.

Equal bonuses of one type neither cancel each other nor depend on the order the items were put on in: the game takes one number, the highest, once. Which of equal items is the spare one is the mod's choice:

- equal to a lasting ability or another character's item: the item adds nothing (red);
- equal to buffs only: the buff is the spare one, the item's bonus stays when it ends (blue);
- equal to another worn item: the one with properties of its own that cannot be compared (an ability, a resistance, weapon attacks) is kept and blue, the other is red. Example: Rogue's Companion (Trickery +5 and Expeditious Retreat) blue, Trapspringer's Gloves (Trickery +5 only) red;
- both have properties of their own: both are needed, the mark is blue and green, the bonus counts once;
- neither has any: the first one the game lists is kept.

**The green half** of the mark (cut diagonally) means the item has properties of its own that work and are lost when it is taken off. Red and green: the item's bonuses have no effect, but its other properties are a reason to keep it; blue and green: an equal bonus with another item, both have properties of their own. The tooltip then says in a green line that the item's other properties do not overlap and are lost when it is taken off; which they are, its description tells. A property the game gives but the description does not mention is named at the end of that line, for example the bracers of armour: "Not in the description: +1 to AC against touches of incorporeal creatures (armour does not protect against them)".

The enhancement bonus of weapons and armour is checked too: when Magic Weapon or Magic Vestment gives as much as the item itself or more, it shows as well.

## Not covered

Only bonuses to stats (AC, attack, damage, saves, skills, attributes, initiative and the like) and the enhancement of weapons and armour. Energy resistance, damage reduction and immunities work differently in the game; the mod does not compare them.

The mod writes nothing into the save and changes no rules of the game; it can be turned on and off in the main menu.
