# Action Bar Rows (Pathfinder: Kingmaker and Wrath of the Righteous)

[Русская версия](README.ru.md)

A Unity Mod Manager mod for the gamepad interface of both games. The action bar shows up to three rows of slots instead of one, so more abilities, spells and items are at hand without turning pages.

## What it does

- **Rows.** The rows above the bar are the next pages of the same ten: with the bar on page 1 they show pages 2 and 3. LB and RB turn all rows together. The slots of the rows are the bar's own: they are assigned through the context menu, used, moved and cleared as on the bar.
- **How many rows.** One, two or three, for each character apart. The number is kept in the game's own setting of the extra rows of the mouse interface's bar, so it is saved with the game.
- **Controls in the open bar.**
  - L3 switches the rows: one, two, three, one again. Its hint "Rows" is under the bar.
  - The stick and the D-pad go between the slots and the rows.
  - D-pad up on a slot with variants opens the variants, as on the single bar; elsewhere it goes to the row above. D-pad down goes to the row below.
  - The page counter under the bar shows the page of the row with the cursor.
- **The look.** The bar becomes two blocks, each in its own frame: the slots with the rows, and right of them the weapon set button with the belt button of [Belt Bar](../belt-bar). The frame of the slots grows with the rows; its corners and ornaments keep their size. The bar moves a little to the left to make room for the second frame. The name of the slot under the cursor, its hints and the windows of the weapon sets and the belt open above the top row.
- **Weapon sets and the belt.** Up on the weapon set button or the belt button opens its window with the stick as with the D-pad; down closes it, with the stick too.

In Kingmaker L3 also switches the highlighting of objects. [Camera Rotation and Compass](../kingmaker-gamepad-camera-rotation) 1.2.5 or later moves the highlighting to holding L3, so a press switches the rows only.

## Installing

1. Unity Mod Manager must be installed in the game: [Steam Deck](../docs/steam-deck.md), [Windows](../docs/windows.md). How to unpack, update and remove a mod, with commands: [installing mods by hand](../docs/mods.md).
2. Download the zip for your game from [Releases](https://github.com/sergusaev/pathfinder-mods/releases): `ActionBarRows-<version>-kingmaker.zip` or `ActionBarRows-<version>-wotr.zip`. The two DLLs are not interchangeable.
3. Unpack it into the mods folder, so the result is `Mods/ActionBarRows/Info.json`.
4. Start the game; the UMM window lists "Action Bar Rows" with a green status.

The mod works in the gamepad interface only (the Steam Deck, a gamepad in the console mode of the game); the keyboard-and-mouse interface has rows of its own.

## Building

`./build.sh` builds both games, `./build.sh --game kingmaker --install` builds and installs one; see the [repository README](../README.md#building-from-source). The code for each game is in `src/kingmaker` and `src/wotr`: the two interfaces differ. `src/Patch.cs`, shared by both, draws a frame or a paper of the bar in pieces, so that it changes size without stretching its corners.
