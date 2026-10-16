# Belt Bar (Pathfinder: Kingmaker and Wrath of the Righteous)

[Русская версия](README.ru.md)

A Unity Mod Manager mod for the gamepad interface of both games. It adds a belt button to the action bar, right of the weapon set button, so a potion, a scroll or a wand is used straight from the bar and not through the context menu.

## What it does

- **The button.** The last button of the action bar, a copy of the weapon set button in the game's own look. Its icon is the belt the character wears; without a belt it is the belt placeholder of the inventory. The bar and its backdrop, hints and plates are made wider by the button, the slots and the weapon set button stay where they were. The whole bar moves a little to the left so the button does not touch the combat log panel.
- **The row of belt slots.** Press the accept button (or D-pad up) on the belt button: a window like the weapon sets' opens above the bar with one cell per belt slot, with the numbers I, II, III… A cell holds the slot of the bar itself, so everything it does is the game's own: a potion or a scroll is used and the slot is filled again by the next one of the kind (Belt Consumables Refill does that when it is installed), the charges of a wand and the number of items in a stack are shown on the slot, a switchable item shows "on" as an ability does, an unavailable item is dimmed.
- **Controls.** D-pad or the left stick choose a cell, accept uses the item (as from any slot of the bar, the bar closes, an item that needs a target waits for it), decline or D-pad down close the row. The context menu of the bar (assigning abilities, spells and items to its slots) is as it was.
- A character with no items on the belt, and a pet, cannot open the row (in Kingmaker the button is dimmed).

## Installing

1. Unity Mod Manager must be installed in the game: [Steam Deck guide](../docs/steam-deck.md), [Windows guide](../docs/windows.md).
2. Download the zip for your game from [Releases](https://github.com/sergusaev/pathfinder-mods/releases): `BeltBar-<version>-kingmaker.zip` or `BeltBar-<version>-wotr.zip`. The two DLLs are not interchangeable.
3. Unpack it into the mods folder, so the result is `Mods/BeltBar/Info.json` (`mods/BeltBar/Info.json` in Wrath of the Righteous).
4. Start the game; the UMM window lists "Belt Bar" with a green status.

The mod works in the gamepad interface only (the Steam Deck, a gamepad in the console mode of the game); the keyboard-and-mouse interface has no such bar.

## Building

`./build.sh` builds both games, `./build.sh --game kingmaker --install` builds and installs one; see the [repository README](../README.md#building). The code for each game is in `src/kingmaker` and `src/wotr`: the two interfaces differ.
