# Portrait Gallery (Pathfinder: Kingmaker)

[Русская версия](README.ru.md)

A Unity Mod Manager mod for character creation. The portrait step shows one scrolling list split into categories that fold and unfold, the same with the gamepad and with keyboard and mouse:

1. **Kingmaker** — the game's own portraits for created characters;
2. **Wrath of the Righteous** — the generic portraits of WotR's character creation, in that game's order;
3. **WotR characters** — WotR's companions, NPCs and mythic forms, and the NPCs WotR offers in its own creation (Horgus, Anevia, Irabeth, Staunton, Nurah, Trever);
4. **Custom** — your portrait folders.

Saves do not depend on the mod: a built-in portrait is stored by the game as before, any other one as a custom portrait, by its folder name.

## Categories

A category bar has the portrait's frame, the game's gold chevron and the category's name. It lights up the same way as the current portrait: under the mouse, or under the gamepad cursor. A click or A on a bar folds or unfolds its category. All categories start unfolded; what you fold stays folded until you quit the game.

With the gamepad, the stick and the d-pad move by the rows of the list: a bar is a row of its own, the last row of a category may be short. The scrollbar is always shown, in both interfaces.

A category with no portraits is not shown.

## Portraits that are never offered

- A portrait worn by any character of Kingmaker: companions, NPCs, the heroes of Varnhold, its mercenaries and the named adventurers. The game's own list has some of them; the mod leaves them out for every created character, the main one and the mercenaries alike.
- A portrait of another character of your party: no two characters share a face.

Custom portraits are not checked against the characters of Kingmaker.

## The WotR portraits

The mod ships no pictures of Wrath of the Righteous. If WotR is installed on the same computer or Steam Deck, the mod writes its portraits into Kingmaker's `Portraits` folder by itself, one folder per portrait, as custom portraits are made: `Small.png` (185×242), `Medium.png` (330×432), `Fulllength.png` (692×1024) and `PortraitGallery.txt` (the category and the place in it).

- **When.** At the start of the game and when the mod is switched on in Unity Mod Manager, in the background: the menu does not wait. The first time it takes about 15 seconds; then the mod only sees that nothing has changed.
- **What is checked.** `Portraits/PortraitGallery-WotR.txt` records which files of WotR the portraits were made from and which folders came out. While those files are the same and every folder is whole, nothing is read. A deleted folder or picture is written again; pictures already there are not touched.
- **Where WotR is looked for.** In the Steam library of Kingmaker, then in every library Steam knows of (on the Steam Deck the microSD card too). Without WotR the two WotR categories are simply not shown.
- **Folder names never change**, so saves keep their portraits: the creation portraits of WotR are `!WotR_01_<name>` … `!WotR_31_<name>` by their place in WotR's list, the others `!WotR_<name>`.
- The UMM log gets `wotr export: 82 portraits (WotR 25, WotRCharacters 57), 82 written now, ...`, or `up to date`, or `Wrath of the Righteous is not found`.

A portrait folder without `PortraitGallery.txt` is a custom portrait and goes to **Custom**.

## Custom portraits

Each portrait is a subfolder of `Portraits` with `Small.png`, `Medium.png` and `Fulllength.png`:

- Steam Deck, Windows build under Proton: `~/.local/share/Steam/steamapps/compatdata/640820/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Kingmaker/Portraits/`
- Windows: `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Kingmaker\Portraits\`

**Custom** lists the folders sorted by name. With keyboard and mouse only the small pictures of the portraits in sight are loaded, so a folder of a thousand portraits opens without a pause. The game's button for making a portrait from your pictures is not offered (with a big `Portraits` folder it hung the game); the button that opens the folder stays.

## With other mods

- **Kingmaker Fixes** adds custom portraits to the gamepad character creation by itself; with Portrait Gallery on, it leaves the list to Portrait Gallery and says so in the log (`custom portraits: left to PortraitGallery`).
- Remove Custom Portraits Gamepad Selection Fix (`Mods/ConsoleCustomPortraits`) if you still have it.

Written for Kingmaker 2.1.7b, UMM 0.32.4, Harmony 2.3.6.

## Building

See the [repository README](../README.md#building-from-source): `./build.sh`, `./build.sh --install`.
