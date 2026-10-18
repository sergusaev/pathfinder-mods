# Steam Deck: Unity Mod Manager and the mods from scratch

[Русская версия](steam-deck.ru.md)

This guide installs Unity Mod Manager (UMM) into **Pathfinder: Kingmaker** and **Pathfinder: Wrath of the Righteous** (WotR) on the Steam Deck. Both games run as Windows builds under Proton, and UMM loads through Doorstop. Checked on SteamOS: Kingmaker 2.1.7b under Proton Experimental, WotR 2.7 under GE-Proton 10, UMM 0.32.4 and 0.33.2.

What Doorstop, Proton and the launch options are, and why everything is done this way, is in [a separate page](umm.md). Read it at least up to the part about `UIStartingPoint`.

Go through the sections in order. Every command block can be copied and pasted into Konsole as a whole.

## 1. Before you start

1. **Kingmaker: the Windows build only.** By default Steam installs the Linux build of Kingmaker on the deck, and it will not do ([why](umm.md#why-not-the-native-linux-build-of-kingmaker)). In Gaming Mode: Kingmaker → ⚙ → **Properties** → **Compatibility** → turn on "Force the use of a specific Steam Play compatibility tool" → pick **Proton Experimental**. Steam downloads the game again (several GB). WotR exists for Windows only, Proton is on for it by itself; nothing to change.
2. **Start each game once** up to the main menu and quit. This makes Steam create the folder with the game's data (the Proton prefix), where the logs and saves will be.
3. **Switch to Desktop Mode:** the Steam button → **Power** → **Switch to Desktop**.
4. **Open Konsole** (the terminal): the application menu in the bottom left corner → System → Konsole. To paste into Konsole press **Ctrl+Shift+V** or right-click → Paste. Without a keyboard, Steam + X opens the on-screen one.
5. While you change the game files, the game must be closed.

## 2. Find the game folders

The commands below assume the games are in the internal storage. Check where they really are:

```bash
find ~/.local/share/Steam/steamapps/common /run/media -maxdepth 5 -type d \( -name "Pathfinder Kingmaker" -o -name "Pathfinder Second Adventure" \) 2>/dev/null
```

- If the output starts with `/home/deck/.local/share/Steam/`, everything is as in this guide; change nothing.
- If it starts with `/run/media/…`, the game is on a microSD card. Then in every block below replace the first line with `KM=` or `WOTR=` by the path from the output, for example `KM="/run/media/deck/SD1TB/steamapps/common/Pathfinder Kingmaker"`.

The WotR folder is called **`Pathfinder Second Adventure`** — the game's internal name; there is no other folder.

Check that Kingmaker is the Windows build:

```bash
KM="$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"
ls "$KM"
```

The list must have `Kingmaker.exe` and `UnityPlayer.dll`. If you see `UnityPlayer.so`, Proton is not on for Kingmaker — go back to item 1 of section 1. If you see `run.sh` or `libdoorstop.so`, these are leftovers of an old installation on the Linux build; delete them (Dolphin → Move to Trash).

## 3. Download UMM

The author distributes UMM on [Nexus Mods](https://www.nexusmods.com/site/mods/21) as a Windows installer. The deck does not need it: the same files are in the update package that UMM downloads by itself (its link is in [Repository_beta_13.json](https://github.com/newman55/unity-mod-manager/blob/master/Repository_beta_13.json) of the UMM repository).

```bash
mkdir -p ~/umm && cd ~/umm
curl -L -o umm_update.zip "https://www.dropbox.com/s/wt18wcq5six02ku/umm_update.zip?dl=1"
unzip -o umm_update.zip -d package
ls package
```

The output must have `UnityModManager.dll`, `0Harmony.dll`, `dnlib.dll`, `winhttp_x64.dll` and the `Harmony` folder.

## 4. Unity Mod Manager for Kingmaker

```bash
KM="$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"
UMM="$KM/Kingmaker_Data/Managed/UnityModManager"
mkdir -p "$UMM" "$KM/Mods"

# UMM itself and Harmony (older mods need Harmony 1.2, so it goes in too).
cp ~/umm/package/UnityModManager.dll ~/umm/package/UnityModManager.xml ~/umm/package/0Harmony.dll ~/umm/package/dnlib.dll ~/umm/package/Harmony/1.2/*.dll "$UMM/"

# Doorstop: the loader and its settings.
cp ~/umm/package/winhttp_x64.dll "$KM/winhttp.dll"
printf '[General]\nenabled = true\ntarget_assembly = Kingmaker_Data\\Managed\\UnityModManager\\UnityModManager.dll\n' > "$KM/doorstop_config.ini"

# The game description for UMM. There is NO UIStartingPoint line here, on purpose.
cat > "$UMM/Config.xml" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<Config xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" Name="Pathfinder: Kingmaker">
  <Folder>Pathfinder Kingmaker</Folder>
  <ModsDirectory>Mods</ModsDirectory>
  <ModInfo>Info.json</ModInfo>
  <GameExe>Kingmaker.exe</GameExe>
  <EntryPoint>[UnityEngine.UIModule.dll]UnityEngine.Canvas.cctor:Before</EntryPoint>
  <StartingPoint>[Assembly-CSharp.dll]Kingmaker.GameStarter.Awake:Before</StartingPoint>
  <OldPatchTarget>[Assembly-CSharp.dll]Kingmaker.GameStarter.Awake:Before</OldPatchTarget>
  <GameVersionPoint>[Assembly-CSharp.dll]Kingmaker.GameVersion.GetVersion</GameVersionPoint>
  <MinimalManagerVersion>0.12.0</MinimalManagerVersion>
</Config>
EOF

# UMM settings: the window on start. An existing file is left alone: it holds which mods are on.
[ -f "$UMM/Params.xml" ] || cat > "$UMM/Params.xml" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<Param xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <CheckUpdates>0</CheckUpdates>
  <ShowOnStart>1</ShowOnStart>
</Param>
EOF

ls "$KM/winhttp.dll" "$KM/doorstop_config.ini" "$UMM/UnityModManager.dll"
grep -c UIStartingPoint "$UMM/Config.xml"
```

The last two commands check the result: the three files must be found, and `grep` must print `0`.

**Important.** Without the `UIStartingPoint` line the UMM window works in gamepad mode. If you ever reinstall UMM another way (with the installer, from another guide), it brings the line back, and gamepad mode breaks again: the UMM item in the menu opens nothing, the mods are visible but do nothing. Details are [here](umm.md#configxml-and-the-uistartingpoint-line). To remove the line from an existing file:

```bash
KM="$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"
sed -i '/UIStartingPoint/d' "$KM/Kingmaker_Data/Managed/UnityModManager/Config.xml"
```

## 5. Unity Mod Manager for Wrath of the Righteous

```bash
WOTR="$HOME/.local/share/Steam/steamapps/common/Pathfinder Second Adventure"
UMM="$WOTR/Wrath_Data/Managed/UnityModManager"
mkdir -p "$UMM" "$WOTR/Mods"

cp ~/umm/package/UnityModManager.dll ~/umm/package/UnityModManager.xml ~/umm/package/0Harmony.dll ~/umm/package/dnlib.dll "$UMM/"

cp ~/umm/package/winhttp_x64.dll "$WOTR/winhttp.dll"
printf '[General]\nenabled = true\ntarget_assembly = Wrath_Data\\Managed\\UnityModManager\\UnityModManager.dll\n' > "$WOTR/doorstop_config.ini"

# WotR keeps the UIStartingPoint line: it has one main menu for both modes.
cat > "$UMM/Config.xml" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<Config xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" Name="Pathfinder: Wrath of the Righteous">
  <Folder>Pathfinder Second Adventure</Folder>
  <ModsDirectory>Mods</ModsDirectory>
  <ModInfo>Info.json</ModInfo>
  <GameExe>Wrath.exe</GameExe>
  <EntryPoint>[UnityEngine.UIModule.dll]UnityEngine.Canvas.cctor:Before</EntryPoint>
  <StartingPoint>[Assembly-CSharp.dll]Kingmaker.GameStarter.Awake:Before</StartingPoint>
  <UIStartingPoint>[Assembly-CSharp.dll]Kingmaker.MainMenu.Start:After</UIStartingPoint>
  <GameVersionPoint>[Assembly-CSharp.dll]Kingmaker.GameVersion.GetVersion</GameVersionPoint>
  <MinimalManagerVersion>0.22.15</MinimalManagerVersion>
</Config>
EOF

[ -f "$UMM/Params.xml" ] || cat > "$UMM/Params.xml" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<Param xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <CheckUpdates>0</CheckUpdates>
  <ShowOnStart>1</ShowOnStart>
</Param>
EOF

# Keep WotR in gamepad mode (see below). The old file is kept as startup.json.bak.
[ -f "$WOTR/startup.json" ] && cp "$WOTR/startup.json" "$WOTR/startup.json.bak"
echo '{"ForceControllerMode":"gamepad"}' > "$WOTR/startup.json"

ls "$WOTR/winhttp.dll" "$WOTR/doorstop_config.ini" "$UMM/UnityModManager.dll" "$WOTR/startup.json"
```

**Why `startup.json`.** Without it WotR asks "Switch to keyboard and mouse?" on any key press, the Steam on-screen keyboard included. With it the game always stays in gamepad mode, and the mouse and keyboard mode is unavailable. If you want the mouse mode, delete `startup.json`. Steam's file verification may restore the original file; then write it again with the last lines of the block.

## 6. Launch options

**Both** games get the same launch options in Steam:

```
WINEDLLOVERRIDES="winhttp=n,b" %command%
```

What each part means is [here](umm.md#launch-options). In short: without them Proton ignores `winhttp.dll` in the game folder, and UMM does not start.

It is easiest to enter them in Desktop Mode by pasting, so the quotes come out right:

1. Open Steam on the desktop (the Steam shortcut, or the application menu → Games → Steam).
2. Library → right-click the game → **Properties** → **General** → the **Launch Options** field.
3. Copy the line above and paste it (Ctrl+V). Close the window — it saves by itself.
4. Repeat for the other game.

In Gaming Mode it is the same field: the game → ⚙ → **Properties** → **General** → **Launch Options**. The quotes must be plain `"`.

## 7. Check

1. Go back to Gaming Mode (the "Return to Gaming Mode" shortcut on the desktop) and start the game.
2. The UMM window appears at start; its **Mods** tab is empty for now. The window is used with the mouse: on the deck that is the **right trackpad**, and pressing the trackpad clicks.
3. If there is no window, check whether UMM started at all (in Konsole, after quitting the game):

```bash
grep -m3 "\[Manager\]" "$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker/Kingmaker_Data/Managed/UnityModManager/Log.txt"
```

For WotR, the same path with `Pathfinder Second Adventure/Wrath_Data`. Lines like `[Manager] Version: …` mean UMM works; look for the cause under [troubleshooting](#10-troubleshooting). No file, or only old lines, means UMM did not start: check the launch options (section 6) and the files of sections 4–5.

## 8. Installing the mods

How to download, install, update and remove the mods is in the [mod installation guide](mods.md). The first one to install is [Unity Mod Manager in Game Menus](../umm-menu-button): with it the UMM window opens from the game's menus (the main one and the Esc one), and it turns the window on start off.

## 9. Where things are

Each game has two folders, and they are easy to confuse:

| What | Where | Example |
|---|---|---|
| **The game folder**: game files, UMM, mods, `winhttp.dll` | `~/.local/share/Steam/steamapps/common/<game folder>/` | `…/common/Pathfinder Kingmaker/Mods/` |
| **The Proton prefix**: everything the game writes as "a Windows user": logs, saves, portraits, settings | `~/.local/share/Steam/steamapps/compatdata/<AppID>/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/<game>/` | see the table below |

The prefix is a fake `C:` drive, a separate one for each game. The folder number in `compatdata` is the game's Steam number (AppID): **640820** for Kingmaker, **1184370** for WotR. UMM and the mods are **not** in the prefix; look for them in the game folder only. If a game is on a microSD card, both folders are on the card: `/run/media/…/steamapps/common/…` and `/run/media/…/steamapps/compatdata/…`.

Paths in the logs like `Z:\home\deck\…` or `S:\steamapps\…` are the same Linux folders as Wine sees them: its drive letters point at folders of the deck.

| | Kingmaker | WotR |
|---|---|---|
| Game folder | `~/.local/share/Steam/steamapps/common/Pathfinder Kingmaker` | `~/.local/share/Steam/steamapps/common/Pathfinder Second Adventure` |
| UMM | `…/Kingmaker_Data/Managed/UnityModManager/` | `…/Wrath_Data/Managed/UnityModManager/` |
| Mods | `…/Pathfinder Kingmaker/Mods/` | `…/Pathfinder Second Adventure/Mods/` |
| UMM log | `…/UnityModManager/Log.txt` | `…/UnityModManager/Log.txt` |
| Game data (prefix) | `~/.local/share/Steam/steamapps/compatdata/640820/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Kingmaker/` | `~/.local/share/Steam/steamapps/compatdata/1184370/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Wrath Of The Righteous/` |
| Game and mod log | `output_log.txt` in the game data | `Player.log` and `GameLogFull.txt` in the game data |
| Saves | `Saved Games/` in the game data | `Saved Games/` in the game data |
| Custom portraits | `Portraits/` in the game data | `Portraits/` in the game data |

Mod lines in the game log start with the mod id in brackets, e.g. `[BeltBar]`; the buff menu writes `[PAD]`.

**In Dolphin** (the file manager), folders whose names start with a dot (`.local`) are hidden — **Ctrl+H** shows them. A path can be pasted into the address bar (Ctrl+L). The game folder is easiest to open from Steam: the game → ⚙ → **Manage** → **Browse local files**.

## 10. Troubleshooting

| Symptom | Cause and fix |
|---|---|
| No UMM window, no fresh `[Manager]` lines in `Log.txt` | UMM does not start. Check the launch options (section 6: straight quotes, `%command%` present) and that `winhttp.dll` and `doorstop_config.ini` are in the game folder |
| Kingmaker does not start at all | the launch options still say `./run.sh %command%` from the Linux build — replace them (section 6) |
| Kingmaker, gamepad mode: the UMM menu item opens nothing, the mods are visible but do nothing; with the mouse everything works | `Config.xml` has `UIStartingPoint` — delete it (end of section 4) |
| UMM worked and vanished after a game update | UMM was installed with Assembly — reinstall with sections 4–5 ([why](umm.md#assembly-and-why-we-do-not-use-it)) |
| `Managed` has `UnityEngine.UIModule.dll.original_` | traces of the Assembly method. In Steam: the game → ⚙ → Properties → Installed Files → **Verify integrity of game files**, then sections 4–6 |
| WotR asks "Switch to keyboard and mouse?" | no `startup.json`, or file verification restored the original — end of section 5 |
| The mod is not in the UMM list | the mod folder is in the wrong place: it must be `Mods/<mod folder>/Info.json`, without an extra nested folder. Another cause: the game folder has two mods folders, `Mods` and `mods`, and Proton sees only one. Move everything into `Mods` and delete the other |
| The mod is listed with a red status | the zip is not for this game (mods for both games have `-kingmaker` and `-wotr` zips) or UMM is too old. The cause is in the game log (section 9) |
| WotR: all mods of this collection are red at once, the game log has `Patching exception` and `__args` | older mod versions with the loader from UMM 0.33.2 (Doorstop 4): with it WotR uses its own Harmony 2.0.4, which the older versions cannot work with. Update all the mods to the current releases (Belt Bar 1.0.2, Belt Consumables Refill 1.0.2, Level 1 Companions 1.0.4, Success Chance 1.1.1, UMM in Game Menus 1.0.3, WotR Fixes 1.1.1, buff menu `v1.21.2-pad.9` or later) |
| After an update the old version works | delete `*.cache` in the mod folder ([details](mods.md#updating)) |
| No "Unity Mod Manager" item in the game's menu | [Unity Mod Manager in Game Menus](../umm-menu-button) is not installed, or UMM did not start (first row) |

## 11. Uninstalling

- **To turn UMM off for a while:** clear the game's launch options. The files stay, the game starts without mods.
- **To remove everything:** delete `winhttp.dll`, `doorstop_config.ini`, the `<Game>_Data/Managed/UnityModManager` and `Mods` folders from the game folder; for WotR also `startup.json` (put back `startup.json.bak` if there was one). Clear the launch options. The saves in the prefix are not touched.

## 12. Advanced: installing from another computer

The mods can be built from source and installed on the deck over SSH with one command — see the [repository README](../README.md#automatic-installation). It also tells how to turn SSH on on the deck.
