# Kingmaker Cosmetics (Pathfinder: Kingmaker)

[Русская версия](README.ru.md)

A Unity Mod Manager mod for Pathfinder: Kingmaker. It adds hair styles, a face and hair colours to the character creation and makes the faces of elf women more detailed. Everything is chosen in the game's own appearance page, in the mouse and in the gamepad interface.

## What it does

- **New hair styles for human women:** a bob, shorter at the nape and longer toward the face; the bob with the ponytail of the elf bob; the high ponytail with the lush elf ponytail.
- **Humans and aasimars share hair styles.** Their heads are the same, so each race gets the styles of the other, of the same gender.
- **A new face and hair style for elf women,** made after a portrait: the sixth face, with smaller eyes, eyeliner and a smirk; the elf French braid with a fringe: four thick locks loop out of the middle of the hairline and fall along the face, with short tongues of hair on the forehead under them.
- **The same face for half-elf women:** the elf face as it is, set into the first half-elf head (its skull, ears and neck stay), so half-elf hair styles sit on it as on their own faces.
- **Detailed eyes of elf women.** The five game faces get eye textures four times as large: a white eye white, a round iris with its pupil looking straight ahead, a highlight, a dark upper lid. The flat lash cards that looked jagged up close are taken off the faces. The textures follow the character atlas size of the graphics settings.
- **Shorter elf ears,** halfway between elf and half-elf ears and turned a little away from the head.
- **Hair colours of all races for every race.** In the mouse interface five colours are shown at a time, between arrows like those of the hair style; in the gamepad interface the colour row of the game simply gets longer.

## Saves

The looks are kept in the save by their ids, like the game's own. A save where someone wears a style or the face of the mod is marked as needing a DLC: without the mod the game shows it greyed out with the DLC label and does not load it from the save list, instead of loading a character without hair or head. With the mod back, the save loads with its looks. "Continue", quick load and load after death skip this check: such a load without the mod only loses the looks until the mod is back.

The detailed eyes, the faces without lashes and the shorter ears change the game's own faces and need nothing in the save.

## Installing

1. Unity Mod Manager must be installed in the game: [Steam Deck](../docs/steam-deck.md), [Windows](../docs/windows.md). How to unpack, update and remove a mod, with commands: [installing mods by hand](../docs/mods.md).
2. Download `KingmakerCosmetics-<version>.zip` from [Releases](https://github.com/sergusaev/pathfinder-mods/releases).
3. Unpack it into the mods folder, so the result is `Mods/KingmakerCosmetics/Info.json`.
4. Start the game and open the character creation: the new styles are at the end of the hair and face lists.

## Building

`./build.sh` builds the mod, `./build.sh --install` builds and installs it; see the [repository README](../README.md#building-from-source).
