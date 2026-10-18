# How Unity Mod Manager gets into the game

[Русская версия](umm.ru.md)

This page explains how the setup works; the step-by-step commands are in the guides [for the Steam Deck](steam-deck.md) and [for Windows](windows.md). Read it before installing, or when something breaks: almost every breakage we have met is described below.

## What an installation consists of

**Unity Mod Manager (UMM)** is a program that runs inside the game, finds the mod folders and loads them. The game itself knows nothing about mods, so UMM has to be slipped into the game at start. There are two ways to do it: **Doorstop** and **Assembly**. We use Doorstop only.

After the installation the game folder holds:

| File or folder | What it is |
|---|---|
| `winhttp.dll` | the Doorstop loader: the game takes it for a Windows system library and loads it itself |
| `doorstop_config.ini` | the loader settings: whether it is on and which file to start |
| `<Game>_Data/Managed/UnityModManager/` | UMM itself: `UnityModManager.dll`, `0Harmony.dll`, `dnlib.dll` and others |
| `<Game>_Data/Managed/UnityModManager/Config.xml` | the game description for UMM: where the mods folder is and which places of the game to hook into |
| `<Game>_Data/Managed/UnityModManager/Params.xml` | UMM settings: whether to show the window on start, which mods are on |
| `Mods/` | the mods folder, one folder per mod |

`<Game>_Data` is `Kingmaker_Data` for Kingmaker and `Wrath_Data` for Wrath of the Righteous (WotR).

## Doorstop

When a Windows program starts, it loads system libraries. One of them is `winhttp.dll`. Windows looks for it in the program's own folder first and only then in the system folder. Doorstop uses this: its own `winhttp.dll` goes into the game folder. The game loads it instead of the system one; it reads `doorstop_config.ini` and starts the file named in `target_assembly`, that is `UnityModManager.dll`.

```ini
[General]
enabled = true
target_assembly = Kingmaker_Data\Managed\UnityModManager\UnityModManager.dll
```

- `enabled = true` — the loader is on. With `false` the game starts without mods while the files stay in place.
- `target_assembly` — the path to UMM from the game folder. For WotR it says `Wrath_Data` instead of `Kingmaker_Data`. The backslash `\` is there because a Windows program reads the path, on the Steam Deck too.

**Why Doorstop:** no game file is changed, only two new files are added. A game update and Steam's "Verify integrity of game files" leave them alone, so UMM does not disappear.

## Assembly, and why we do not use it

The Assembly method writes a call to UMM right into the game file `<Game>_Data/Managed/UnityEngine.UIModule.dll` (the `EntryPoint` line of `Config.xml`) and keeps the original next to it as `UnityEngine.UIModule.dll.original_`.

The trouble is that this is a changed game file. As soon as Steam updates the game or verifies its files, it silently puts the original `UIModule.dll` back. UMM then simply does not start: no error, no window, the mods sit in their folder and do nothing. This is what happened on the Steam Deck, so all our guides install UMM through Doorstop only.

If UMM was installed with Assembly, the sign is the file `UnityEngine.UIModule.dll.original_` in the `Managed` folder. To move to Doorstop, remove the Assembly installation (the installer's uninstall button, or put `.original_` back) or verify the game files in Steam, then follow the guide.

## Launch options

**Windows needs no launch options**: Windows itself takes `winhttp.dll` from the game folder.

**On the Steam Deck (and any Linux through Proton)** both games run in Proton, which is Wine — a layer that runs Windows programs. Wine has its own built-in copies of the system libraries, and for `winhttp.dll` it uses its own by default and ignores the file in the game folder. So each game gets launch options in Steam:

```
WINEDLLOVERRIDES="winhttp=n,b" %command%
```

Part by part:

| Part | Meaning |
|---|---|
| `%command%` | Steam puts its usual launch command here (Proton plus `Kingmaker.exe` or `Wrath.exe`). Without `%command%` Steam takes the whole line for game arguments, and nothing works |
| whatever is **before** `%command%` | environment variables: settings that the game and Proton get at launch |
| `WINEDLLOVERRIDES=` | a Wine variable: "for these libraries take another copy than usual" |
| `winhttp` | the library name without `.dll` |
| `n,b` | the search order: `n` first (native — the file from the game folder, that is Doorstop), and if there is none, `b` (builtin — Wine's own copy) |
| the quotes `"…"` | keep `winhttp=n,b` in one piece; they must be plain straight quotes `"`, not typographic ones |

Two useful consequences:

- **To turn the mods off for a while**, just clear the launch options: Wine takes its own `winhttp.dll`, Doorstop does not load, and the files stay.
- **Without the options** UMM does not start on the deck, even with every file in place. Check this first.

The native Linux build of Kingmaker was started differently, with `./run.sh %command%` (more [below](#why-not-the-native-linux-build-of-kingmaker)). These options do not work with the Windows build: Steam cannot find `run.sh`, and the game does not start.

## Config.xml and the UIStartingPoint line

`Config.xml` tells UMM which places of the game to hook into:

| Line | Meaning |
|---|---|
| `StartingPoint` | when to load the mods: at game start (`Kingmaker.GameStarter.Awake`) |
| `UIStartingPoint` | when to create the UMM window: after the main menu starts (`Kingmaker.MainMenu.Start`). Without this line the window is created right at `StartingPoint` |
| `EntryPoint` | where the Assembly method hooks in; Doorstop does not use it, but it does no harm |
| `ModsDirectory` | the name of the mods folder, `Mods` here |

**Kingmaker: delete the `UIStartingPoint` line.** This line is what broke UMM on the Steam Deck. Kingmaker has two separate interfaces: one for mouse and keyboard and a console one for the gamepad, with its own main menu. `Kingmaker.MainMenu.Start` is the main menu of the mouse interface. In gamepad mode it never runs, and UMM never creates its window. What it looks like:

- the "Unity Mod Manager" item in the game's menu is there but opens nothing (a `NullReferenceException` in the log);
- the mods are loaded and their buttons and cards are visible, but many do nothing: UMM calls their per-frame handler (`OnUpdate`) from its window, and there is no window;
- with mouse and keyboard everything works, so the breakage is easy to mistake for a control problem.

The UMM installer and the UMM package write `Config.xml` with the `UIStartingPoint` line for both games. So after **every** installation or reinstallation of UMM in Kingmaker, check for the line and delete it; the commands are in the guides for the [Steam Deck](steam-deck.md#4-unity-mod-manager-for-kingmaker) and [Windows](windows.md#3-remove-uistartingpoint-for-kingmaker).

**WotR: keep the line.** WotR has one main menu for both modes, and the UMM window works with it both with the gamepad and with the mouse (checked with the gamepad on the Steam Deck and with the mouse on Windows).

## Why not the native Linux build of Kingmaker

Kingmaker has a Linux build in Steam, and on the Steam Deck Steam installs it by default. We dropped it:

- **A game bug with no fix:** in gamepad mode the kingdom management window opens but cannot be closed — the game is stuck. Neither mods nor settings fix it.
- It needs a separate Linux loader (`libdoorstop.so` plus the `run.sh` script with the launch options `./run.sh %command%`), edited by hand for the game.
- Names are case-sensitive: `Mods` and `mods` are different folders, `Info.json` and `info.json` are different files.
- Logs and saves are elsewhere (`~/.config/unity3d/Owlcat Games/Pathfinder Kingmaker/`), not in the Proton prefix as for WotR.
- The saves of the native build and of the Windows build are in different folders and have to be moved by hand when switching.

The Windows build under Proton works the same way as WotR and as on a Windows PC: the same Doorstop, the same launch options, logs and saves in the Proton prefix. How to turn Proton on for Kingmaker is in the [Steam Deck guide](steam-deck.md#1-before-you-start).

## The mods do not depend on each other

- The mods of this repository can be installed **in any order and any combination**. None needs another: `Requirements` is empty in every `Info.json`, and the shared code (mod windows, the checkbox lock) is built into each mod. If some mod is missing, the others work as usual.
- The handiest one to start with is [Unity Mod Manager in Game Menus](../umm-menu-button): it adds the "Unity Mod Manager" item to the game's menus, so the UMM window opens without a keyboard.
- The checkboxes that turn mods on and off in the UMM window work in the game's main menu only. In a loaded game they are locked, so that no mod is switched off in the middle of a playthrough.
- Some of our mods **replace** other or older mods; then the old folder has to go, or the same thing is done twice. The list is in the [mod installation guide](mods.md#what-replaces-what).

## UMM cache

When loading, UMM puts a copy with the `.cache` extension next to the mod's DLL and may load that copy next time. After updating a mod by hand, delete `*.cache` in its folder, or the old version may load. The commands are in the [mod installation guide](mods.md#updating).
