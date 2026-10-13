# Unity Mod Manager in Game Menus (Pathfinder: Kingmaker and Wrath of the Righteous)

[Русская версия](README.ru.md)

A Unity Mod Manager mod for both Pathfinder games. It adds a "Unity Mod Manager" item to the game's main menu and Esc menu, so the mod manager opens from the menus with whatever you play with: keyboard and mouse, an Xbox or PlayStation gamepad, the Steam Deck.

## What it does

- **The item in four menus of each game:** the main menu and the Esc menu, in the mouse interface and in the gamepad (console) interface. It stands right before "Exit" and opens the Unity Mod Manager window.
- **The game's look.** The item is a copy of a neighbouring one: the same font, colors and frames. In the main menus and in the gamepad Esc menus it has the large first letter of the game's items, and in Kingmaker's gamepad menus the separator line too. The gamepad stick and buttons move through it in order, as through the game's items.
- **The menus keep their size.** The lists of the main menus are made smaller by the share of the added item, so they take the room they took before. In Kingmaker's mouse main menu the frames around the first letters are painted into the table picture, and the mod paints an eighth frame there for the new item. In Kingmaker's gamepad main menu the selection frame is made longer at its plain stretches, so it holds the longer label.
- **One item, whoever else adds one.** Bag of Tricks adds its own Unity Mod Manager button to Kingmaker's mouse Esc menu; this mod takes its place, so there is one item in every menu.
- **No window at start.** When the mod loads, it turns off the "show the window on start" option of Unity Mod Manager, since the manager opens from the menus now.
- **Always on.** The mod is the way into the mod manager, so it switches itself back on if it is unchecked in the mod list.

## Installing

1. Unity Mod Manager must be installed in the game: [Steam Deck guide](../docs/steam-deck.md), [Windows guide](../docs/windows.md).
2. Download the zip for your game from [Releases](https://github.com/sergusaev/pathfinder-mods/releases): `UmmMenuButton-<version>-kingmaker.zip` or `UmmMenuButton-<version>-wotr.zip`. The two DLLs are not interchangeable.
3. Unpack it into the mods folder, so the result is `Mods/UmmMenuButton/Info.json`.
4. Start the game; the main menu has the "Unity Mod Manager" item.

## Building

`./build.sh` builds both games, `./build.sh --game kingmaker --install` builds and installs one; see the [repository README](../README.md#building).
