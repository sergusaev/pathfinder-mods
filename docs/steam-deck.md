# Steam Deck: from a clean install to working mods

[Русская версия](steam-deck.ru.md)

This guide sets up Unity Mod Manager (UMM) for **Pathfinder: Kingmaker** and **Pathfinder: Wrath of the Righteous** on the Steam Deck and installs the mods from this repository and the [Buff It 2 The Limit (Groups)](https://github.com/sergusaev/wrath-epic-buffing) fork. Every step was done and checked on a Steam Deck with SteamOS, Kingmaker 2.1.7b and UMM 0.32.4.

The two games are set up differently:

| | Kingmaker | Wrath of the Righteous |
|---|---|---|
| How it runs | native Linux build | Windows build through Proton |
| How UMM is loaded | Doorstop for Linux: `libdoorstop.so` + `run.sh` | Doorstop for Windows: `winhttp.dll` |
| Mods folder | `Mods` (case-sensitive) | `mods` |
| Launch options | `./run.sh %command%` | `WINEDLLOVERRIDES="winhttp=n,b" %command%` |

## 0. Before you start

- Install the games and start each one once, so Steam creates its folders and the Proton prefix of WotR.
- Switch to the desktop mode (Steam → Power → Switch to Desktop) and open **Konsole**. All commands below are typed there; each block can be pasted as a whole (Ctrl+Shift+V).
- The commands assume the games are in the internal Steam library. If a game is on the microSD card, change the path in the first line of each block, e.g. `/run/media/deck/<card name>/steamapps/common/...`.
- Close the games while you change their files.

## 1. Unity Mod Manager

### 1.1 Download UMM

UMM is distributed by its author on [Nexus Mods](https://www.nexusmods.com/site/mods/21) as a Windows installer, which does not work for the native Kingmaker. The same files are available as the update package that UMM itself downloads (the link is in [Repository_beta_13.json](https://github.com/newman55/unity-mod-manager/blob/master/Repository_beta_13.json) of the [UMM repository](https://github.com/newman55/unity-mod-manager)):

```bash
mkdir -p ~/umm && cd ~/umm
curl -L -o umm_update.zip "https://www.dropbox.com/s/wt18wcq5six02ku/umm_update.zip?dl=1"
python3 -m zipfile -e umm_update.zip package
curl -L -o libdoorstop.so https://raw.githubusercontent.com/newman55/unity-mod-manager/master/lib/libdoorstop_x64.so
curl -L -o run.sh https://raw.githubusercontent.com/newman55/unity-mod-manager/master/lib/run.sh
ls package
```

`package` must contain `UnityModManager.dll`, `0Harmony.dll`, `dnlib.dll` and `winhttp_x64.dll`.

### 1.2 Kingmaker

```bash
KM="$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"
UMM="$KM/Kingmaker_Data/Managed/UnityModManager"
mkdir -p "$UMM" "$KM/Mods"
cp ~/umm/package/{UnityModManager.dll,UnityModManager.xml,0Harmony.dll,dnlib.dll} ~/umm/package/Harmony/1.2/*.dll "$UMM/"

# Doorstop for Linux: run.sh starts the game with libdoorstop.so, which loads UMM.
cp ~/umm/libdoorstop.so "$KM/"
sed -e 's|^executable_name=""|executable_name="Kingmaker.exe"|' \
    -e 's|^target_assembly="Doorstop.dll"|target_assembly="Kingmaker_Data/Managed/UnityModManager/UnityModManager.dll"|' \
    ~/umm/run.sh > "$KM/run.sh"
chmod +x "$KM/run.sh"

# The game description for UMM. UIStartingPoint is left out on purpose: with the gamepad
# interface Kingmaker never calls MainMenu.Start, and the UMM window would never appear.
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

# The UMM window shows on start.
cat > "$UMM/Params.xml" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<Param xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <CheckUpdates>0</CheckUpdates>
  <ShowOnStart>1</ShowOnStart>
</Param>
EOF
```

In Steam: Kingmaker → ⚙ → Properties → Launch options: `./run.sh %command%`.

### 1.3 Wrath of the Righteous

```bash
WOTR="$HOME/.local/share/Steam/steamapps/common/Pathfinder Second Adventure"
UMM="$WOTR/Wrath_Data/Managed/UnityModManager"
mkdir -p "$UMM" "$WOTR/mods"
cp ~/umm/package/{UnityModManager.dll,UnityModManager.xml,0Harmony.dll,dnlib.dll} "$UMM/"

# Doorstop for Windows: Proton loads winhttp.dll from the game folder, which loads UMM.
cp ~/umm/package/winhttp_x64.dll "$WOTR/winhttp.dll"
printf '[General]\nenabled = true\ntarget_assembly = Wrath_Data\\Managed\\UnityModManager\\UnityModManager.dll\n' > "$WOTR/doorstop_config.ini"

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
cp "$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker/Kingmaker_Data/Managed/UnityModManager/Params.xml" "$UMM/" 2>/dev/null \
  || printf '<?xml version="1.0" encoding="utf-8"?>\n<Param>\n  <CheckUpdates>0</CheckUpdates>\n  <ShowOnStart>1</ShowOnStart>\n</Param>\n' > "$UMM/Params.xml"

# Keep WotR in gamepad mode: otherwise any keyboard key (the Steam on-screen keyboard too) pops up
# "Switch to keyboard and mouse?". The mouse and keyboard interface of WotR is unavailable while this is set.
[ -f "$WOTR/startup.json" ] && cp "$WOTR/startup.json" "$WOTR/startup.json.bak"
echo '{"ForceControllerMode":"gamepad"}' > "$WOTR/startup.json"
```

In Steam: Wrath of the Righteous → ⚙ → Properties → Launch options: `WINEDLLOVERRIDES="winhttp=n,b" %command%`.

"Verify integrity of game files" in Steam may restore the original `startup.json`; write it again afterwards.

### 1.4 Check

Start the game in the game mode. The UMM window shows up on start (`ShowOnStart`), with the Mods tab listing nothing yet. If the window does not appear, see [Troubleshooting](#4-troubleshooting).

## 2. Installing the mods

Download the zip files from the release pages: [pathfinder-mods releases](https://github.com/sergusaev/pathfinder-mods/releases) and [Buff It 2 The Limit (Groups) releases](https://github.com/sergusaev/wrath-epic-buffing/releases). Every zip holds one folder with `Info.json` and the DLL; unpack it into the mods folder of the game, so it becomes `Mods/<ModId>/Info.json`:

```bash
KM="$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"
WOTR="$HOME/.local/share/Steam/steamapps/common/Pathfinder Second Adventure"
cd ~/Downloads
python3 -m zipfile -e GamepadCameraRotation-1.1.0.zip "$KM/Mods/"            # example for Kingmaker
python3 -m zipfile -e PortraitScrollFix-1.1.0.zip "$WOTR/mods/"              # example for WotR
```

When a mod is updated, delete `*.cache` files in its folder before starting the game.

| Mod | Game | Folder | Notes |
|---|---|---|---|
| [Unity Mod Manager in Game Menus](../umm-menu-button) | Kingmaker, WotR | `Mods/UmmMenuButton`, `mods/UmmMenuButton` | a separate zip per game |
| [Kingmaker Fixes](../kingmaker-fixes) | Kingmaker | `Mods/KingmakerFixes` | replaces `Mods/ConsoleCustomPortraits`, delete that folder; portraits go to `~/.local/share/Steam/steamapps/compatdata/640820/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Kingmaker/Portraits/` |
| [Time Control and Deadlines](../kingmaker-time-control) | Kingmaker | `Mods/TimeControl` | |
| [Belt Consumables Refill](../belt-consumables-refill) | Kingmaker, WotR | `Mods/BeltConsumablesRefill`, `mods/BeltConsumablesRefill` | a separate zip per game |
| [Camera Rotation and Compass](../kingmaker-gamepad-camera-rotation) | Kingmaker | `Mods/GamepadCameraRotation` | the compass needs WotR installed (found automatically). Turn off camera rotation in Bag of Tricks |
| [Level 1 Companions & Free Respec](../level1-companions) | Kingmaker, WotR | `Mods/Level1Companions`, `mods/Level1Companions` | a separate zip per game (`-kingmaker`, `-wotr`). In WotR delete `mods/lvl1companions` first |
| [Portrait Scroll Fix (gamepad)](../wotr-portrait-scroll-fix) | WotR | `mods/PortraitScrollFix` | portraits go to the `Portraits` folder inside the Proton prefix, see the mod README |
| [WotR Fixes](../wotr-fixes) | WotR | `mods/WotrFixes` | |
| [Buff It 2 The Limit (Groups)](https://github.com/sergusaev/wrath-epic-buffing) | WotR | `mods/BuffIt2TheLimit` | replaces the original Buff It 2 The Limit and BubbleBuffs: remove those first |
| [Buff It 2 The Limit (Groups) for Kingmaker](https://github.com/sergusaev/wrath-epic-buffing) | Kingmaker | `Mods/PadBuffsKingmaker` | |

## 3. Logs

| What | Where |
|---|---|
| Kingmaker: UMM and mods | `~/.config/unity3d/Owlcat Games/Pathfinder Kingmaker/Player.log`, `Kingmaker_Data/Managed/UnityModManager/Log.txt` |
| WotR: UMM and mods | `~/.local/share/Steam/steamapps/compatdata/1184370/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Wrath Of The Righteous/Player.log`, `Wrath_Data/Managed/UnityModManager/Log.txt` |

Lines of the mods start with the mod id in brackets, e.g. `[GamepadCameraRotation]`; the buff menu writes `[PAD]`.

## 4. Troubleshooting

| Symptom | Cause and fix |
|---|---|
| Kingmaker: no `[Manager]` lines in the log | the launch options are not `./run.sh %command%`, or `run.sh` is not executable (`chmod +x`), or `executable_name`/`target_assembly` in it are wrong |
| Kingmaker: mods load, but the UMM window never appears | `UIStartingPoint` in `Config.xml`; remove it (1.2) |
| WotR: no traces of UMM in the logs | the launch options lack `WINEDLLOVERRIDES="winhttp=n,b"`, or `winhttp.dll` / `doorstop_config.ini` are missing in the game folder |
| WotR asks to switch to keyboard and mouse | `startup.json` is missing or was restored by a file check (1.3) |
| No "Unity Mod Manager" item in the menus | Unity Mod Manager in Game Menus is not in the mods folder of that game, or the game started without UMM (see the rows above) |
| A mod is not loaded | look at its status in the UMM window and at the log (section 3) |
| Kingmaker mod folder is ignored | on Linux the folder is `Mods` with a capital M and the file is `Info.json` |

## Removing

Clear the launch options of the game. That alone turns UMM off; the files can stay. To remove everything: `run.sh`, `libdoorstop.so` and `Kingmaker_Data/Managed/UnityModManager` for Kingmaker; `winhttp.dll`, `doorstop_config.ini`, `startup.json` (restore `startup.json.bak` if there was one) and `Wrath_Data/Managed/UnityModManager` for WotR; the mods folders.
