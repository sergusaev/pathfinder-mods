# Pathfinder mods

[Русская версия](README.ru.md)

Small [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) mods for **Pathfinder: Kingmaker** and **Pathfinder: Wrath of the Righteous**, written and tested on the Steam Deck and Windows. They work with a gamepad as well as with keyboard and mouse and open from the game's own menus.

| Mod | Game | What it does |
|---|---|---|
| [Action Bar Rows](action-bar-rows) | Kingmaker and Wrath of the Righteous | Gamepad: up to three rows of slots on the action bar, the next pages above the bar; L3 switches the rows, the number is kept for each character |
| [Belt Bar](belt-bar) | Kingmaker and Wrath of the Righteous | Gamepad: a belt button on the action bar, right of the weapon set button; it opens a row of the belt slots above the bar, so a potion, a scroll or a wand is used from the bar without the context menu |
| [Belt Consumables Refill](belt-consumables-refill) | Kingmaker and Wrath of the Righteous | A used-up belt consumable is replaced from the party inventory and the belt slot shows how many are left; removes the "0 charges" item abilities left by the refill of Bag of Tricks / ToyBox |
| [Camera Rotation and Compass](kingmaker-gamepad-camera-rotation) | Kingmaker | Gamepad: camera rotation and zoom with the right stick, the WotR compass instead of the hourglass, the WotR console layout for camera mode, turn-based mode and inspect. Keyboard and mouse: rotation with the middle mouse button, the compass in the clock |
| [Kingmaker Cosmetics](kingmaker-cosmetics) | Kingmaker | Character creation: new hair styles for human women (a bob, two with elf ponytails), hair shared by humans and aasimars, a new elf face and hair style made after a portrait, hair colours of all races for every race; detailed eyes of elf women, shorter elf ears. A save with a style of the mod is greyed out as needing a DLC without the mod |
| [Kingmaker Fixes](kingmaker-fixes) | Kingmaker | Fixes of the game's own bugs: no false "immune thanks to the difficulty settings" line in the combat log, no burst of errors on entering the global map, characters do not go silent after an area loads, "reroll if failed" on attacks no longer rerolls hits, hit points no longer go missing over a unit with the gamepad interface, custom portraits from the `Portraits` folder in the gamepad character creation, a touch spell or its potion no longer shows its name twice over the head. Replaces Custom Portraits Gamepad Selection Fix |
| [Level 1 Companions & Free Respec](level1-companions) | Kingmaker and Wrath of the Righteous | Story companions join at level 1 and are leveled entirely by hand; a free respec button back to level 1 keeps the companion's name, voice and looks, and in WotR the respec window can be closed with levels left to spend. In WotR replaces lvl1companions |
| [Scrolls](learn-scrolls) | Kingmaker and Wrath of the Righteous | Teaches the party's wizards, magi and other casters who copy spells every scroll of the inventory at once, by the game's own rules, after the game's confirmation window with the list; the specialization and opposition schools are marked, opposition spells can be left out |
| [Success Chance](success-chance) | Kingmaker and Wrath of the Righteous | In turn-based combat, over the enemy you point at: the chance to hit, to crit, to get through spell resistance and to make the target fail its save, with the damage — calculated by the game's own rules, before you act |
| [Time Control and Deadlines](kingmaker-time-control) | Kingmaker | Fractional multipliers for kingdom durations, travel and camping time; a list of all countdowns, hidden ones included ("N days H hours left until X"); tracking with your own deadline and alerts in the game's look; time skip that refuses to pass a deadline after which something fails |
| [Unity Mod Manager in Game Menus](umm-menu-button) | Kingmaker and Wrath of the Righteous | A "Unity Mod Manager" item in the main menu and the Esc menu, in the game's look: the mod manager opens from the game's menus on any platform |
| [WotR Fixes](wotr-fixes) | Wrath of the Righteous | Fixes of the game's own bugs: a touch spell or its potion no longer shows its name twice over the head, combat text stands over the head instead of to the right of it, the custom portraits list scrolls with the gamepad cursor and the right stick; optional settings for smooth frames on the Steam Deck (frame pacing, incremental garbage collection). Replaces Portrait Scroll Fix (gamepad) |

Buff It 2 The Limit (Groups), a menu of buff groups for the buff automation mod Buff It 2 The Limit, for WotR and Kingmaker, lives in a separate fork: [sergusaev/wrath-epic-buffing](https://github.com/sergusaev/wrath-epic-buffing).

## Installing

Nothing has to be built to install: the ready mods are on the [Releases](https://github.com/sergusaev/pathfinder-mods/releases) page. The order:

1. Install Unity Mod Manager into the game with the guide for your platform:
   - [Steam Deck](docs/steam-deck.md) — both games under Proton;
   - [Windows](docs/windows.md).
2. Install the mods: [installing mods by hand](docs/mods.md) — which zip to download, where to unpack it, how to update and remove it, what replaces what.

The mods go in in any order and any combination; none needs another. How UMM gets into the game, what the launch options mean and what broke UMM on the Steam Deck is in [a separate page](docs/umm.md).

## Advanced

What follows is building from source and installing the mods with scripts. A normal installation does not need it.

### Building from source

#### 1. Tools

| Tool | What for |
|---|---|
| Mono with `mcs` (C# compiler, 6.12) | compiling |
| Bash | the `build.sh` script |
| Git | getting the repository |
| `ssh` | copying the game's files from another machine and installing onto it (the Steam Deck) |

Installing them in one go:

**Windows** — in PowerShell:

```powershell
winget install -e --id Git.Git
winget install -e --id Mono.Mono
```

On Windows all further commands run in **Git Bash** (Start → Git Bash), not in PowerShell. Mono does not add itself to `PATH` — `build-mod.sh` finds it in `C:\Program Files\Mono\bin` by itself; `ssh` comes with Git for Windows.

**Linux (Debian, Ubuntu)**:

```bash
sudo apt install mono-devel git openssh-client
```

Other distributions call the Mono package `mono-complete` or `mono`.

**macOS** (with [Homebrew](https://brew.sh)):

```bash
brew install mono
```

**The Steam Deck** does not do for building: the SteamOS system partition is read-only, Mono cannot be installed onto it. Build on another computer and install onto the deck over SSH ([below](#automatic-installation)).

#### 2. The repository

```bash
git clone https://github.com/sergusaev/pathfinder-mods.git
cd pathfinder-mods
git config core.filemode false
```

`core.filemode false` is needed on Windows: it has no executable bit, and without this setting git shows every `.sh` file as modified. On Linux and macOS it does no harm.

#### 3. Where the build gets the game's files

The mods compile against the game's own assemblies. On a mod's first build `build.sh` copies `<Game>_Data/Managed/*.dll` from the game, and `UnityModManager.dll` and `0Harmony.dll` from UMM, into the mod's `refs/` folder. Hence two conditions:

- the game with UMM installed must be on this machine or on a machine reachable over SSH;
- a mod for both games needs both games (or the `--game` option, see below).

The script finds the game through these variables:

| Variable | Meaning | Default |
|---|---|---|
| `DECK` | SSH address of the game machine, e.g. `deck@steamdeck.local` | empty: the game is on this machine |
| `KINGMAKER_DIR` | Kingmaker folder on the game machine | `~/.local/share/Steam/steamapps/common/Pathfinder Kingmaker` |
| `WOTR_DIR` | WotR folder on the game machine | `~/.local/share/Steam/steamapps/common/Pathfinder Second Adventure` |

The defaults are the Steam layout of Linux and the Steam Deck. On Windows put your paths into `local.env` next to `build-mod.sh` (git does not track it). An example for games in `F:\SteamLibrary`, in Git Bash:

```bash
cat > local.env <<'EOF'
KINGMAKER_DIR="/f/SteamLibrary/steamapps/common/Pathfinder Kingmaker"
WOTR_DIR="/f/SteamLibrary/steamapps/common/Pathfinder Second Adventure"
EOF
```

In Git Bash the `F:` drive is written `/f`, and the backslash becomes a forward slash. The quotes are required: the paths have spaces.

If the game's files in `refs/` are outdated (the game was updated), copy them again with `--fetch`. `refs/` holds the game's files, so it is excluded from git and must not be published.

#### 4. Commands

```bash
./kingmaker-fixes/build.sh             # build into kingmaker-fixes/build/
./kingmaker-fixes/build.sh --fetch     # copy the game's files again first, then build
```

A mod for both games (its `build.sh` sets `GAMES="kingmaker wotr"`) is built from one set of sources for each game:

```bash
./belt-bar/build.sh                    # both games in turn: build/kingmaker/, build/wotr/
./belt-bar/build.sh --game wotr        # one game only
```

Without `--game`, `--fetch` and `--install` apply to every game of the mod. The compiler gets `-define:KINGMAKER` or `-define:WOTR`; `src/*.cs` are shared, `src/kingmaker/` and `src/wotr/` are compiled for that game only, and `shared/*.cs` go into every mod. Builds are byte-identical on macOS and Windows.

### Automatic installation

`--install` builds the mod and puts `Info.json` and the DLL straight into the game's `Mods` folder, deleting UMM's cache (`*.cache`) on the way. The game must be closed: if it runs, the script refuses to install. UMM must already be in the game ([Steam Deck](docs/steam-deck.md), [Windows](docs/windows.md)).

**Onto this computer** (paths from `local.env` or the defaults):

```bash
./belt-bar/build.sh --install
./kingmaker-fixes/build.sh --install
```

**Onto the Steam Deck over SSH.** Turn SSH on on the deck once. In Desktop Mode, in Konsole:

```bash
passwd                              # set a password for the deck user (if there is none yet)
sudo systemctl enable --now sshd    # turn the SSH server on, also after reboots
```

Then, once on the build machine (on Windows in Git Bash), create a key and give it to the deck, so the password is not asked every time:

```bash
[ -f ~/.ssh/id_ed25519 ] || ssh-keygen -t ed25519 -N "" -f ~/.ssh/id_ed25519
ssh-copy-id deck@steamdeck.local     # asks for the deck's password once
ssh deck@steamdeck.local true && echo OK
```

If the name `steamdeck.local` is not found, use the deck's IP address (Settings → Internet → your network), e.g. `deck@192.168.1.72`.

After that, installing onto the deck:

```bash
DECK=deck@steamdeck.local ./belt-bar/build.sh --install
```

`DECK=` at the start of the command applies to this command only and wins over `local.env`: the game paths from `local.env` (they are for this computer) are not used, the deck's default folders are. If the games are on a microSD card of the deck, add their paths to the same command: `DECK=deck@steamdeck.local KINGMAKER_DIR="/run/media/deck/<card>/steamapps/common/Pathfinder Kingmaker" ./kingmaker-fixes/build.sh --install`. If you install onto the deck only, you can put `DECK=deck@steamdeck.local` into `local.env` and stop repeating it.

### Pitfalls

| Symptom | Cause and fix |
|---|---|
| `$'\r': command not found` when running a `.sh` | the script has Windows line endings (CRLF). `.gitattributes` keeps `*.sh` in LF; a clone made before it appeared needs one refresh of the scripts in Git Bash: `git ls-files -z '*.sh' \| xargs -0 rm && git checkout -- '*.sh'` |
| git shows every `.sh` as modified | `git config core.filemode false` was not run (section 2). The setting lives in `.git/config`: copying `.git` from another machine brings back the old value |
| `mcs: command not found` | Mono is not installed, or not into `C:\Program Files\Mono` |
| the build looks for the game in `~/.local/share/Steam/…` on Windows | no `local.env` with the paths (section 3); the defaults do not fit Windows |
| `Kingmaker.exe is running, close the game before installing` | the game is running — close it and repeat |
| `local.env` copied from another machine | it holds that machine's `DECK` and paths — check it |
| the mod is installed, but the game runs the old version | installed without the script: delete `*.cache` ([details](docs/mods.md#updating)) |


## License

[MIT](LICENSE). Pathfinder: Kingmaker and Pathfinder: Wrath of the Righteous are games by Owlcat Games; no game files are included in this repository.
