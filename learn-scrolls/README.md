# Scrolls (Pathfinder: Kingmaker and Wrath of the Righteous)

[Русская версия](README.ru.md)

A Unity Mod Manager mod for both games and both interfaces (gamepad, keyboard and mouse). It teaches wizards, magi and the other casters who copy spells every scroll of the party's inventory at once, the way the game itself does it: the scroll is spent, the spell goes into the book.

## What it does

- **Whole party.** Every scroll goes to someone who can learn it. When there are fewer copies than characters who can, the rules of the Settings tab decide who gets it. Recipes of the inventory are learned by the whole party.
- **One character.** Pick a character in the Characters tab: it learns everything it can, also what the party plan would give to someone else; such spells are marked in the list.
- **Confirmation first.** The game's own window with the list comes first: the characters, under each its spells with the level and the school. Nothing is learned before you confirm. The school is green with a thumbs up when it is the character's specialization, red with a thumbs down when it is an opposition school.
- **Counts.** Next to each character: how many scrolls the party plan gives it; the characters' numbers add up to the Whole party's.
- **Not learned:** a spell already in the book, a spell not on the class's list, a spell the casting ability score is too low for (10 + the spell level). Optionally (off by default) spells of opposition schools.
- **Opening the window.** With a gamepad, the Scrolls card in the game's menu. With keyboard and mouse, the buttons over the game's menu buttons: a scroll learns everything for the whole party, a scroll with a gear opens the window. The window has a Help tab with all the controls.

The pictures are made once from the scroll of the games' Journal card and shipped in the DLL.

## Installing

1. Unity Mod Manager must be installed in the game: [Steam Deck](../docs/steam-deck.md), [Windows](../docs/windows.md). How to unpack, update and remove a mod, with commands: [installing mods by hand](../docs/mods.md).
2. Download the zip for your game from [Releases](https://github.com/sergusaev/pathfinder-mods/releases): `LearnScrolls-<version>-kingmaker.zip` or `LearnScrolls-<version>-wotr.zip`. The two DLLs are not interchangeable.
3. Unpack it into the mods folder, so the result is `Mods/LearnScrolls/Info.json`.
4. Start the game; the UMM window lists "Scrolls" with a green status.

## Building

`./build.sh` builds both games, `./build.sh --game kingmaker --install` builds and installs one; see the [repository README](../README.md#building-from-source). The code for each game is in `src/kingmaker` and `src/wotr`. The pictures in `art/` are made by `art/make_art.py` (Pillow) from the sources cut out of the games' sprites once.
