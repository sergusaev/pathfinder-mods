# Pathfinder gamepad mods

[Русская версия](README.ru.md)

Small [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) mods that make **Pathfinder: Kingmaker** and **Pathfinder: Wrath of the Righteous** more comfortable with a gamepad (console interface), written and tested on the Steam Deck.

| Mod | Game | What it does |
|---|---|---|
| [Gamepad Camera Rotation](kingmaker-gamepad-camera-rotation) | Kingmaker | Camera rotation and zoom with the right stick, the WotR compass instead of the hourglass, and the WotR console layout for camera mode, turn-based mode and inspect |
| [Custom Portraits Gamepad Selection Fix](kingmaker-custom-portraits-gamepad) | Kingmaker | Custom portraits from the `Portraits` folder appear in the gamepad character creation |
| [Portrait Scroll Fix (gamepad)](wotr-portrait-scroll-fix) | Wrath of the Righteous | The custom portraits list scrolls with the gamepad cursor and the right stick |

A gamepad menu for the buff automation mod Buff It 2 The Limit lives in a separate fork: [sergusaev/wrath-epic-buffing](https://github.com/sergusaev/wrath-epic-buffing).

## Installing

**Steam Deck:** follow [docs/steam-deck.md](docs/steam-deck.md) — Unity Mod Manager for both games, the Steam Input layout (UMM window on R4, buff menu on L5, mouse on the right trackpad) and the mods, step by step.

Elsewhere:

1. Install [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) for the game.
2. Download the mod's zip from [Releases](https://github.com/sergusaev/pathfinder-mods/releases).
3. Unpack it into `<game>/Mods/` for Kingmaker or `<game>/mods/` for WotR, so the result is `Mods/<ModId>/Info.json`. On Linux the folder and file names are case-sensitive.
4. Start the game; the mod is listed in the UMM window with a green status.

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
