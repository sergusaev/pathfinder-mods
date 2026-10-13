# Belt Consumables Refill (Pathfinder: Kingmaker and Wrath of the Righteous)

[Русская версия](README.ru.md)

A Unity Mod Manager mod for both Pathfinder games. A belt (quick) slot holds one potion, scroll or flask; when it is used up the slot stays empty. The mod puts the next one of the same kind from the party inventory into that slot and shows on the action bar how many are left. It also cleans up the "0 charges" abilities that the refill of Bag of Tricks (Kingmaker) and ToyBox (WotR) leave behind.

## What it does

- **Refill.** When the last charge of a belt item is spent, the same item from the party inventory goes into the emptied slot. Only free items are taken: never an item from another character's belt. Taking an item off the belt by hand does not refill the slot. Works in real time, turn-based mode and on pause.
- **Stock on the action bar.** A belt slot of a single-charge consumable shows the item plus the free items of the same kind (a slot with alchemist's fire and 9 more in the inventory shows 10). Wands and rods keep showing their charges.
- **Cleanup on load.** On every area load (also after loading a save) the mod removes, from party members:
  - item abilities left without their item: they are listed among the usable items with "Charges: 0" and do nothing;
  - belt slots that still point at an item now held by another character's slot.

  Each removal is written to the UMM log (`Player.log`), and the mod window shows the last result.

Each part can be turned off in the mod's UMM window.

## Why: the refill of Bag of Tricks and ToyBox

Bag of Tricks ("Auto Equip Consumables") and ToyBox ("Refill consumables in belt slots if in inventory") refill a slot with the first item of the same kind in the party inventory. Items on the belts of other characters are in that inventory too, and a one-item stack is not split, so the item itself moves to the new slot: the old owner keeps its ability and a slot pointing at it. When the item is used up the old owner's ability stays with 0 charges. Turn their option off when using this mod; the mod window warns while it is on. Neither mod is required.

## Installing

1. Unity Mod Manager must be installed in the game: [Steam Deck guide](../docs/steam-deck.md), [Windows guide](../docs/windows.md).
2. Download the zip for your game from [Releases](https://github.com/sergusaev/pathfinder-mods/releases): `BeltConsumablesRefill-<version>-kingmaker.zip` or `BeltConsumablesRefill-<version>-wotr.zip`. The two DLLs are not interchangeable.
3. Unpack it into the mods folder, so the result is `Mods/BeltConsumablesRefill/Info.json`.
4. Start the game; the UMM window lists "Belt Consumables Refill" with a green status.

## Building

`./build.sh` builds both games, `./build.sh --game kingmaker --install` builds and installs one; see the [repository README](../README.md#building).
