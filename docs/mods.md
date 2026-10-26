# Installing mods by hand

[Русская версия](mods.ru.md)

How to download a mod from a release, install it, update it and remove it by hand, on the Steam Deck and on Windows. Unity Mod Manager must already be in the game: [Steam Deck](steam-deck.md), [Windows](windows.md).

## Order and independence

The mods are installed **in any order and any combination**; none needs another ([why](umm.md#the-mods-do-not-depend-on-each-other)). The handiest one to start with is **Unity Mod Manager in Game Menus**: it puts the UMM item into the game's menus. The rest are up to you.

## Which file to download

All mods are on the release pages: [pathfinder-mods](https://github.com/sergusaev/pathfinder-mods/releases) and [the buff menu](https://github.com/sergusaev/wrath-epic-buffing/releases). The newest releases are at the top of the page and each mod has its own: the first release from the top with the mod's name is its latest version. Download the zip from the **Assets** block (not "Source code"). The commands below find and download the latest version by themselves.

- A mod for one game has one zip: `<Name>-<version>.zip`.
- A mod for both games has two zips: `<Name>-<version>-kingmaker.zip` and `<Name>-<version>-wotr.zip`. They are not interchangeable: each DLL is built for its own game.
- Each zip holds one mod folder, with `Info.json` and the DLL inside (the WotR buff menu also has pictures and resources). This folder goes into the game's `Mods` folder as a whole.

| Mod | Game | File name (`MOD`) | Folder in `Mods` |
|---|---|---|---|
| [Unity Mod Manager in Game Menus](../umm-menu-button) | both | `UmmMenuButton` | `UmmMenuButton` |
| [Action Bar Rows](../action-bar-rows) | both | `ActionBarRows` | `ActionBarRows` |
| [Belt Bar](../belt-bar) | both | `BeltBar` | `BeltBar` |
| [Belt Consumables Refill](../belt-consumables-refill) | both | `BeltConsumablesRefill` | `BeltConsumablesRefill` |
| [Level 1 Companions & Free Respec](../level1-companions) | both | `Level1Companions` | `Level1Companions` |
| [Scrolls](../learn-scrolls) | both | `LearnScrolls` | `LearnScrolls` |
| [Success Chance](../success-chance) | both | `SuccessChance` | `SuccessChance` |
| [Kingmaker Fixes](../kingmaker-fixes) | Kingmaker | `KingmakerFixes` | `KingmakerFixes` |
| [Camera Rotation and Compass](../kingmaker-gamepad-camera-rotation) | Kingmaker | `GamepadCameraRotation` | `GamepadCameraRotation` |
| [Time Control and Deadlines](../kingmaker-time-control) | Kingmaker | `TimeControl` | `TimeControl` |
| [Buff It 2 The Limit (Groups) for Kingmaker](https://github.com/sergusaev/wrath-epic-buffing/blob/gamepad/pad-docs/README.md#kingmaker) | Kingmaker | `PadBuffsKingmaker` (buff menu releases) | `PadBuffsKingmaker` |
| [WotR Fixes](../wotr-fixes) | WotR | `WotrFixes` | `WotrFixes` |
| [Buff It 2 The Limit (Groups)](https://github.com/sergusaev/wrath-epic-buffing/blob/gamepad/pad-docs/README.md) | WotR | `BuffIt2TheLimit-Pad` (buff menu releases) | `BuffIt2TheLimit` |

In the commands below three lines pick the mod:

- `MOD` — the file name from the table;
- `GAME` — `-kingmaker` or `-wotr` for a mod for both games; empty for a mod for one game (`GAME=` / `$GAME = ""`);
- `REPO` — `sergusaev/pathfinder-mods`, and `sergusaev/wrath-epic-buffing` for both buff menus.

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

The game is closed. Desktop Mode → Konsole (paste — Ctrl+Shift+V). Paste each block as a whole; change the first lines for your mod.

**1. Download.** With a command — it finds the latest version by itself:

```bash
MOD=BeltBar                       # file name from the table
GAME=-kingmaker                   # -kingmaker / -wotr; empty for a mod for one game: GAME=
REPO=sergusaev/pathfinder-mods    # buff menu: sergusaev/wrath-epic-buffing
cd ~/Downloads
URL=$(curl -s "https://api.github.com/repos/$REPO/releases?per_page=100" | grep -o "https://[^\"]*/$MOD-[0-9][^/\"]*$GAME\.zip" | head -1)
echo "$URL"
curl -L -O "$URL"
F=$(basename "$URL")
```

`echo` must show a link to a zip. An empty line means a typo in `MOD`/`GAME`/`REPO` or no internet.

Or with the browser: the zip goes to `~/Downloads`. Then instead of the block above:

```bash
MOD=BeltBar; GAME=-kingmaker      # as in the block above
F=$(ls -t ~/Downloads/$MOD-*$GAME*.zip | head -1)
echo "$F"
```

**2. Extract into `Mods`.** In the same Konsole, right after step 1. The game folders are as in the [Steam Deck guide](steam-deck.md#2-find-the-game-folders); if the game is on a microSD card, change the first line.

```bash
G="$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"   # WotR: …/common/Pathfinder Second Adventure
D=$(unzip -Z1 "$F" | head -1 | cut -d/ -f1)
unzip -o "$F" -d "$G/Mods/"
rm -f "$G/Mods/$D/"*.cache
ls -l "$G/Mods/$D"
```

`ls` must show `Info.json` and the mod's DLL (the file dates come from the zip, i.e. the release build date, not today). `D` is the mod folder inside the zip, `-o` overwrites old files without asking, `rm` deletes UMM's cache (see "Updating").

**Without Konsole, with the mouse in Dolphin:** right-click the zip → **Extract** → **Extract archive to…** → pick the game's `Mods` folder (the path is in the [table of paths](steam-deck.md#9-where-things-are); hidden folders — Ctrl+H). When updating, then delete the cache (see "Updating").

## Installing on Windows

The game is closed.

**With the mouse:** right-click the zip → **Extract All…** → enter the game's `Mods` folder as the path, e.g. `F:\SteamLibrary\steamapps\common\Pathfinder Kingmaker\Mods` → **Extract**. The result must be `Mods\<Mod>\` with `Info.json` inside, without an extra level like `Mods\<Mod>-<version>-kingmaker\<Mod>\`. If there is an extra level, move the inner folder one level up.

**PowerShell**, the same two steps as on the Deck. `$KM` and `$WOTR` are from the [Windows guide](windows.md#game-folders).

1. Download the latest version:

```powershell
$MOD = "BeltBar"; $GAME = "-kingmaker"; $REPO = "sergusaev/pathfinder-mods"
$A = (Invoke-RestMethod "https://api.github.com/repos/$REPO/releases?per_page=100").assets | Where-Object name -Match "^$MOD-\d.*$GAME\.zip$" | Select-Object -First 1
$A.name
$F = "$env:USERPROFILE\Downloads\$($A.name)"
curl.exe -L -o $F $A.browser_download_url
```

Or, if the zip is already downloaded with the browser: `$MOD = "BeltBar"; $GAME = "-kingmaker"` and then `$F = (Get-ChildItem "$env:USERPROFILE\Downloads\$MOD-*$GAME*.zip" | Sort-Object LastWriteTime | Select-Object -Last 1).FullName`.

2. Extract into `Mods`:

```powershell
$G = $KM     # WotR: $G = $WOTR
$D = (tar -tf $F | Select-Object -First 1).Split('/')[0]
tar -xf $F -C "$G\Mods"
Remove-Item "$G\Mods\$D\*.cache" -ErrorAction SilentlyContinue
Get-ChildItem "$G\Mods\$D"
```

## Check

Start the game and open the UMM window (the menu item or the window on start), the **Mods** tab: the mod is listed with a green status. The checkboxes that turn mods on and off work in the game's main menu only.

## Updating

An update goes in the same way as an install, over the old one, with the game closed: the commands above download the latest version and delete UMM's cache. The cache files look like `BeltBar.dll.2354.cache`: UMM may load them instead of the new DLL. If you installed with the mouse, delete them in the file manager.

A mod's settings and data in its folder (`Settings.xml`, and for Time Control the `Tracking` folder with the tracked countdowns) survive an update: they are not in the zip. So do not delete the whole mod folder when updating.

## Removing

With the game closed, delete the mod's folder from `Mods`; its settings go with it. Our mods write nothing into the saves, so old saves load normally after removal; what stays (e.g. companions already built by hand with Level 1 Companions) is in the mod's README.

## If a mod does not work

| Symptom | Cause and fix |
|---|---|
| The mod is not in the UMM list | the folder is in the wrong place: it must be `Mods/<Mod>/Info.json`. An extra level (`Mods/<Mod>/<Mod>/Info.json`), or the folder next to `Mods` instead of inside it, and UMM does not see the mod |
| The mod is not listed, on the Steam Deck | the game folder has two mods folders, `Mods` and `mods`, and Proton sees only one. Move everything into `Mods` and delete the other |
| Red status | the zip is not for this game (`-kingmaker` / `-wotr`) or UMM is too old. The cause is in the game log ([Steam Deck paths](steam-deck.md#9-where-things-are), [Windows](windows.md#6-where-things-are)), lines with the mod id |
| WotR: all mods of this collection are red at once, the game log has `Patching exception` and `__args` | older mod versions with the loader from UMM 0.33.2 (Doorstop 4): with it WotR uses its own Harmony 2.0.4, which the older versions cannot work with. Update all the mods to the latest releases (at least: Belt Bar 1.0.2, Belt Consumables Refill 1.0.2, Level 1 Companions 1.0.4, Success Chance 1.1.1, UMM in Game Menus 1.0.3, WotR Fixes 1.1.1, buff menu `v1.21.2-pad.9` or later) |
| After an update it behaves like the old version | `*.cache` was not deleted — see "Updating" |
| The mod is listed but does nothing in Kingmaker with the gamepad | UMM's `Config.xml` has `UIStartingPoint` ([Steam Deck](steam-deck.md#4-unity-mod-manager-for-kingmaker), [Windows](windows.md#3-remove-uistartingpoint-for-kingmaker)) |
