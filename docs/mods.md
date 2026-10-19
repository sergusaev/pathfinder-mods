# Installing mods by hand

[Русская версия](mods.ru.md)

How to download a mod from a release, install it, update it and remove it by hand, on the Steam Deck and on Windows. Unity Mod Manager must already be in the game: [Steam Deck](steam-deck.md), [Windows](windows.md).

## Order and independence

The mods are installed **in any order and any combination**; none needs another ([why](umm.md#the-mods-do-not-depend-on-each-other)). The handiest one to start with is **Unity Mod Manager in Game Menus**: it puts the UMM item into the game's menus. The rest are up to you.

## Which file to download

All mods are on the release pages: [pathfinder-mods](https://github.com/sergusaev/pathfinder-mods/releases) and [the buff menu](https://github.com/sergusaev/wrath-epic-buffing/releases). Open the release you need and download the zip from its **Assets** block (not "Source code").

- A mod for one game has one zip: `<Mod>-<version>.zip`.
- A mod for both games has two zips: `<Mod>-<version>-kingmaker.zip` and `<Mod>-<version>-wotr.zip`. They are not interchangeable: each DLL is built for its own game.
- Each zip holds one folder named after the mod, with `Info.json` and the DLL inside (the WotR buff menu also has pictures and resources). This folder goes into the game's `Mods` folder as a whole.

| Mod | Game | File in the release | Folder in `Mods` |
|---|---|---|---|
| [Unity Mod Manager in Game Menus](../umm-menu-button) | both | `UmmMenuButton-…-kingmaker.zip` / `…-wotr.zip` | `UmmMenuButton` |
| [Action Bar Rows](../action-bar-rows) | both | `ActionBarRows-…-kingmaker.zip` / `…-wotr.zip` | `ActionBarRows` |
| [Belt Bar](../belt-bar) | both | `BeltBar-…-kingmaker.zip` / `…-wotr.zip` | `BeltBar` |
| [Belt Consumables Refill](../belt-consumables-refill) | both | `BeltConsumablesRefill-…-kingmaker.zip` / `…-wotr.zip` | `BeltConsumablesRefill` |
| [Level 1 Companions & Free Respec](../level1-companions) | both | `Level1Companions-…-kingmaker.zip` / `…-wotr.zip` | `Level1Companions` |
| [Success Chance](../success-chance) | both | `SuccessChance-…-kingmaker.zip` / `…-wotr.zip` | `SuccessChance` |
| [Kingmaker Fixes](../kingmaker-fixes) | Kingmaker | `KingmakerFixes-….zip` | `KingmakerFixes` |
| [Camera Rotation and Compass](../kingmaker-gamepad-camera-rotation) | Kingmaker | `GamepadCameraRotation-….zip` | `GamepadCameraRotation` |
| [Time Control and Deadlines](../kingmaker-time-control) | Kingmaker | `TimeControl-….zip` | `TimeControl` |
| [Buff It 2 The Limit (Groups) for Kingmaker](https://github.com/sergusaev/wrath-epic-buffing/blob/gamepad/pad-docs/README.md#kingmaker) | Kingmaker | `PadBuffsKingmaker-….zip` (tag `kingmaker-v…`) | `PadBuffsKingmaker` |
| [WotR Fixes](../wotr-fixes) | WotR | `WotrFixes-….zip` | `WotrFixes` |
| [Buff It 2 The Limit (Groups)](https://github.com/sergusaev/wrath-epic-buffing/blob/gamepad/pad-docs/README.md) | WotR | `BuffIt2TheLimit-Pad-….zip` (tag `v…-pad.N`) | `BuffIt2TheLimit` |

## What replaces what

If `Mods` already has a mod from the left column, remove its folder **before** installing the new one (with the game closed), or the same thing is done twice.

| Old mod | Replaced by | What to do |
|---|---|---|
| `ConsoleCustomPortraits` (Custom Portraits Gamepad Selection Fix) | Kingmaker Fixes | delete `Mods/ConsoleCustomPortraits` |
| `PortraitScrollFix` (Portrait Scroll Fix (gamepad), WotR) | WotR Fixes | delete `Mods/PortraitScrollFix` |
| `lvl1companions` (Level 1 Companions by CascadingDragon, WotR) | Level 1 Companions & Free Respec | delete `Mods/lvl1companions` |
| Buff It 2 The Limit (the original), BubbleBuffs (WotR) | Buff It 2 The Limit (Groups) | delete their folders; the settings of the original Buff It 2 The Limit are kept, the BubbleBuffs settings are not read |

Working together with third-party mods:

- **Bag of Tricks** (Kingmaker): with Camera Rotation and Compass turn Bag of Tricks' camera rotation off; with Belt Consumables Refill turn "Auto Equip Consumables" off. Unity Mod Manager in Game Menus takes the place of Bag of Tricks' UMM button in the menu by itself.
- **ToyBox** (WotR): with Belt Consumables Refill turn "Refill consumables in belt slots if in inventory" off.

## Installing on the Steam Deck

1. Switch to Desktop Mode and download the zip with the browser; by default it goes to `~/Downloads`.
2. Open Konsole and paste the commands with your file name. The game folders are as in the [Steam Deck guide](steam-deck.md#2-find-the-game-folders); if the game is on a microSD card, change the first line.

For Kingmaker:

```bash
KM="$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"
cd ~/Downloads
unzip -o BeltBar-1.0.1-kingmaker.zip -d "$KM/Mods/"
ls "$KM/Mods/BeltBar"
```

For WotR:

```bash
WOTR="$HOME/.local/share/Steam/steamapps/common/Pathfinder Second Adventure"
cd ~/Downloads
unzip -o BeltBar-1.0.1-wotr.zip -d "$WOTR/Mods/"
ls "$WOTR/Mods/BeltBar"
```

`ls` must show `Info.json` and the mod's DLL. `-o` lets `unzip` overwrite old files without asking.

**Without Konsole, with the mouse in Dolphin:** right-click the zip → **Extract** → **Extract archive to…** → pick the game's `Mods` folder (the path is in the [table of paths](steam-deck.md#9-where-things-are); hidden folders — Ctrl+H).

## Installing on Windows

**With the mouse:** right-click the zip → **Extract All…** → enter the game's `Mods` folder as the path, e.g. `F:\SteamLibrary\steamapps\common\Pathfinder Kingmaker\Mods` → **Extract**. The result must be `Mods\BeltBar\` with `Info.json` inside, without an extra level like `Mods\BeltBar-1.0.1-kingmaker\BeltBar\`. If there is an extra level, move the inner folder one level up.

**PowerShell** (`$KM` and `$WOTR` are from the [Windows guide](windows.md#game-folders)):

```powershell
cd "$env:USERPROFILE\Downloads"
tar -xf BeltBar-1.0.1-kingmaker.zip -C "$KM\Mods"
tar -xf BeltBar-1.0.1-wotr.zip -C "$WOTR\Mods"
Get-ChildItem "$KM\Mods\BeltBar" -Name
```

## Check

Start the game and open the UMM window (the menu item or the window on start), the **Mods** tab: the mod is listed with a green status. The checkboxes that turn mods on and off work in the game's main menu only.

## Updating

An update goes in the same way, over the old one, with the game closed. Then delete UMM's cache in the mod folder, or the old DLL may load.

Steam Deck:

```bash
KM="$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"
unzip -o ~/Downloads/BeltBar-1.0.1-kingmaker.zip -d "$KM/Mods/"
rm -f "$KM/Mods/BeltBar/"*.cache
```

Windows (PowerShell):

```powershell
tar -xf "$env:USERPROFILE\Downloads\BeltBar-1.0.1-kingmaker.zip" -C "$KM\Mods"
Remove-Item "$KM\Mods\BeltBar\*.cache"
```

The cache files look like `BeltBar.dll.2354.cache`. With the mouse, just delete them in the file manager.

A mod's settings and data in its folder (`Settings.xml`, and for Time Control the `Tracking` folder with the tracked countdowns) survive an update: they are not in the zip. So do not delete the whole mod folder when updating.

## Removing

With the game closed, delete the mod's folder from `Mods`; its settings go with it. Our mods write nothing into the saves, so old saves load normally after removal; what stays (e.g. companions already built by hand with Level 1 Companions) is in the mod's README.

## If a mod does not work

| Symptom | Cause and fix |
|---|---|
| The mod is not in the UMM list | the folder is in the wrong place: it must be `Mods/<Mod>/Info.json`. An extra level (`Mods/<Mod>/<Mod>/Info.json`), or the folder next to `Mods` instead of inside it, and UMM does not see the mod |
| The mod is not listed, on the Steam Deck | the game folder has two mods folders, `Mods` and `mods`, and Proton sees only one. Move everything into `Mods` and delete the other |
| Red status | the zip is not for this game (`-kingmaker` / `-wotr`) or UMM is too old. The cause is in the game log ([Steam Deck paths](steam-deck.md#9-where-things-are), [Windows](windows.md#6-where-things-are)), lines with the mod id |
| WotR: all mods of this collection are red at once, the game log has `Patching exception` and `__args` | older mod versions with the loader from UMM 0.33.2 (Doorstop 4): with it WotR uses its own Harmony 2.0.4, which the older versions cannot work with. Update all the mods to the current releases (Belt Bar 1.0.2, Belt Consumables Refill 1.0.2, Level 1 Companions 1.0.4, Success Chance 1.1.1, UMM in Game Menus 1.0.3, WotR Fixes 1.1.1, buff menu `v1.21.2-pad.9` or later) |
| After an update it behaves like the old version | `*.cache` was not deleted — see "Updating" |
| The mod is listed but does nothing in Kingmaker with the gamepad | UMM's `Config.xml` has `UIStartingPoint` ([Steam Deck](steam-deck.md#4-unity-mod-manager-for-kingmaker), [Windows](windows.md#3-remove-uistartingpoint-for-kingmaker)) |
