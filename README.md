# Pathfinder gamepad mods

[Русская версия](README.ru.md)

Small [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) mods that make **Pathfinder: Kingmaker** and **Pathfinder: Wrath of the Righteous** more comfortable with a gamepad (console interface), written and tested on the Steam Deck.

| Mod | Game | What it does |
|---|---|---|
| [Gamepad Camera Rotation](kingmaker-gamepad-camera-rotation) | Kingmaker | Camera rotation and zoom with the right stick, the WotR compass instead of the hourglass, and the WotR console layout for camera mode, turn-based mode and inspect |
| [Custom Portraits Gamepad Selection Fix](kingmaker-custom-portraits-gamepad) | Kingmaker | Custom portraits from the `Portraits` folder appear in the gamepad character creation |
| [Portrait Scroll Fix (gamepad)](wotr-portrait-scroll-fix) | Wrath of the Righteous | The custom portraits list scrolls with the gamepad cursor and the right stick |

A gamepad menu for the buff automation mod Buff It 2 The Limit lives in a separate fork: [sergusaev/wrath-epic-buffing](https://github.com/sergusaev/wrath-epic-buffing).

## Installing a mod

1. Install Unity Mod Manager for the game. On the Steam Deck:
   - Kingmaker runs natively on Linux; use the DoorstopProxy method and the launch options `./run.sh %command%`.
   - Wrath of the Righteous runs through Proton; put `winhttp.dll` and `doorstop_config.ini` into the game folder and use the launch options `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
2. Build the mod (below) or take `Info.json` and the DLL from a release.
3. Copy both files into `<game>/Mods/<ModId>/` for Kingmaker or `<game>/mods/<ModId>/` for WotR. On Linux the folder and `Info.json` names are case-sensitive.
4. Start the game; the mod is listed in the UMM window (Ctrl+F10 by default) with a green status.

## Building

Requirements:

- Mono with the `mcs` compiler (`brew install mono` on macOS, `mono-devel` on Linux, Mono for Windows with Git Bash).
- The game itself: the mods compile against its assemblies. `build.sh` copies them into `<mod>/refs` on the first run. These are the game's files, so `refs/` is excluded from git and must not be published.

```bash
./kingmaker-gamepad-camera-rotation/build.sh            # copy refs if missing, then build into build/
./kingmaker-gamepad-camera-rotation/build.sh --fetch    # re-copy the game's assemblies (after a game update)
./kingmaker-gamepad-camera-rotation/build.sh --install  # build and copy into the game's mods folder; the game must be closed
```

All mods share [`build-mod.sh`](build-mod.sh). It finds the game through these variables:

| Variable | Meaning | Default |
|---|---|---|
| `DECK` | SSH target when the game runs on another machine, e.g. `deck@steamdeck.local` | empty: the game is on this machine |
| `KINGMAKER_DIR` | Kingmaker folder on the game machine | `~/.local/share/Steam/steamapps/common/Pathfinder Kingmaker` |
| `WOTR_DIR` | Wrath of the Righteous folder on the game machine | `~/.local/share/Steam/steamapps/common/Pathfinder Second Adventure` |

Put personal values into `local.env` next to `build-mod.sh` (not tracked by git), for example:

```bash
DECK=deck@steamdeck.local
```

On Windows the game is usually under `C:/Program Files (x86)/Steam/steamapps/common/`, so set `KINGMAKER_DIR` / `WOTR_DIR` and run the scripts from Git Bash.

## License

[MIT](LICENSE). Pathfinder: Kingmaker and Pathfinder: Wrath of the Righteous are games by Owlcat Games; no game files are included in this repository.
