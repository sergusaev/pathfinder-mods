# Pathfinder mods

[Русская версия](README.ru.md)

Small [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) mods that make **Pathfinder: Kingmaker** and **Pathfinder: Wrath of the Righteous** more comfortable with a gamepad (console interface) and, where noted, with keyboard and mouse; written and tested on the Steam Deck and Windows.

| Mod | Game | What it does |
|---|---|---|
| [Camera Rotation and Compass](kingmaker-gamepad-camera-rotation) | Kingmaker | Gamepad: camera rotation and zoom with the right stick, the WotR compass instead of the hourglass, the WotR console layout for camera mode, turn-based mode and inspect. Keyboard and mouse: rotation with the middle mouse button and Alt+A / Alt+D, the compass in the clock |
| [Custom Portraits Gamepad Selection Fix](kingmaker-custom-portraits-gamepad) | Kingmaker | Custom portraits from the `Portraits` folder appear in the gamepad character creation |
| [Level 1 Companions & Free Respec](level1-companions) | Kingmaker and Wrath of the Righteous | Story companions join at level 1 and are leveled entirely by hand; a free respec button back to level 1 keeps the companion's name, voice and looks, and in WotR the respec window can be closed with levels left to spend. In WotR replaces lvl1companions |
| [Portrait Scroll Fix (gamepad)](wotr-portrait-scroll-fix) | Wrath of the Righteous | The custom portraits list scrolls with the gamepad cursor and the right stick |

Buff It 2 The Limit (Groups), a menu of buff groups for the buff automation mod Buff It 2 The Limit, works with a gamepad, keyboard and mouse in WotR and Kingmaker; it lives in a separate fork: [sergusaev/wrath-epic-buffing](https://github.com/sergusaev/wrath-epic-buffing).

## Installing

**Steam Deck:** follow [docs/steam-deck.md](docs/steam-deck.md) — Unity Mod Manager for both games, the Steam Input layout (UMM window on R4, buff menu on L5, mouse on the right trackpad) and the mods, step by step.

**Windows without the UMM installer** (e.g. over SSH): [docs/windows.md](docs/windows.md) — UMM for both games with PowerShell commands, and the mods.

Elsewhere:

1. Install [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) for the game.
2. Download the mod's zip from [Releases](https://github.com/sergusaev/pathfinder-mods/releases).
3. Unpack it into `<game>/Mods/`, so the result is `Mods/<ModId>/Info.json`. `Mods` is the folder UMM's `Config.xml` names for both games. On Linux the names are case-sensitive for the native Kingmaker; WotR runs through Proton, which ignores case, so the `mods` folder of the Steam Deck guide works too.
4. Start the game; the mod is listed in the UMM window with a green status.

## Building

### Third-party tools

| Tool | What for | macOS | Linux | Windows |
|---|---|---|---|---|
| Mono with `mcs` (C# compiler, 6.12) | compiling | `brew install mono` | `sudo apt install mono-devel` (Debian/Ubuntu; `mono-complete` on other distros) | `winget install Mono.Mono` |
| Bash | `build.sh`, `release.sh` | built in | built in | Git for Windows: `winget install Git.Git`, run the scripts from **Git Bash** |
| `ssh` | copying the game's assemblies from / installing to another machine (`DECK`) | built in | `openssh-client` | comes with Git for Windows |
| `zip` or bsdtar | packing release zips | built in (`tar`) | `sudo apt install zip` | `C:\Windows\System32\tar.exe`, used automatically |
| GitHub CLI `gh` | `release.sh --publish` only | `brew install gh` | [cli.github.com](https://cli.github.com/) | `winget install GitHub.cli` |

After installing `gh`, sign in once with `gh auth login`. Mono for Windows does not add itself to `PATH`; `build-mod.sh` finds it in `C:\Program Files\Mono\bin` and converts paths for `mcs.exe` itself. Builds are byte-identical on macOS and Windows.

**The game.** The mods compile against the game's own assemblies: `build.sh` copies `<Game>_Data/Managed/*.dll` and UMM's `UnityModManager.dll` and `0Harmony.dll` into `<mod>/refs` on the first run (or with `--fetch`), from this machine or over SSH. Unity Mod Manager must already be installed in that game. `refs/` holds the game's files, so it is excluded from git and must not be published.

**A Steam Deck as the game machine.** Enable SSH once in the desktop mode (Konsole): `passwd` to set a password for `deck`, then `sudo systemctl enable --now sshd`; from the build machine `ssh-copy-id deck@steamdeck.local`, then `DECK=deck@steamdeck.local` in `local.env`.

### Commands

```bash
./kingmaker-gamepad-camera-rotation/build.sh            # copy refs if missing, then build into build/
./kingmaker-gamepad-camera-rotation/build.sh --fetch    # re-copy the game's assemblies (after a game update)
./kingmaker-gamepad-camera-rotation/build.sh --install  # build and copy into the game's mods folder; the game must be closed
```

**Mods for both games.** A mod whose `build.sh` sets `GAMES="kingmaker wotr"` instead of `GAME=` ([Level 1 Companions & Free Respec](level1-companions)) is built from one set of sources for each game:

```bash
./level1-companions/build.sh                              # every game in turn: build/kingmaker/, build/wotr/
./level1-companions/build.sh --game wotr                  # one game only
./level1-companions/build.sh --game kingmaker --install   # build and install into one game
```

The compiler gets `-define:KINGMAKER` or `-define:WOTR`; `src/*.cs` are shared, `src/kingmaker/` and `src/wotr/` are compiled for that game only. The game's assemblies go to `refs/<game>/`, so both games must be installed with UMM on the game machine for the full build. Without `--game`, `--fetch` and `--install` apply to every game.

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

On Windows the game is usually under `C:/Program Files (x86)/Steam/steamapps/common/`, so set `KINGMAKER_DIR` / `WOTR_DIR` and run the scripts from Git Bash. Two things to do once after cloning on Windows, or after copying a working copy over from macOS or Linux:

- `git config core.filemode false` in each clone. Windows has no executable bit, so otherwise git shows every `.sh` file as modified. The setting lives in `.git/config`: copying `.git` from another machine brings back the old value, so set it again.
- `local.env` copied from another machine keeps that machine's `DECK` and game paths; check it.
- Line endings. Git for Windows checks text files out with CRLF (`core.autocrlf=true` by default), and Bash fails on a script with CRLF (`$'\r': command not found`). `.gitattributes` keeps `*.sh` in LF on every machine; a clone made before it appeared needs one refresh: `git ls-files -z '*.sh' | xargs -0 rm && git checkout -- '*.sh'` in Git Bash. `release.sh` strips `\r` when it reads `Info.json` and `build.sh`, so CRLF there does no harm.

## Releasing

```bash
./release.sh kingmaker-gamepad-camera-rotation              # build and pack into dist/GamepadCameraRotation-<version>.zip
./release.sh kingmaker-gamepad-camera-rotation --publish    # the same, then tag and publish a GitHub release
./release.sh wotr-portrait-scroll-fix --publish --notes notes.md
```

A mod for both games is packed into one zip per game, `dist/<ModId>-<version>-kingmaker.zip` and `dist/<ModId>-<version>-wotr.zip`, attached to the same release (tag `level1-companions-v1.0.0`).

The version comes from the mod's `Info.json`; bump it before publishing, the script refuses to publish a version that is already released, uncommitted changes or an unpushed `main`. The tag is the folder name without the game prefix, e.g. `gamepad-camera-rotation-v1.1.0`. Publishing needs the [GitHub CLI](https://cli.github.com/) (`gh auth login`). `dist/` is not tracked by git.

## License

[MIT](LICENSE). Pathfinder: Kingmaker and Pathfinder: Wrath of the Righteous are games by Owlcat Games; no game files are included in this repository.
