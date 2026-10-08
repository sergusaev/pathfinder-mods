# Steam Deck: from a clean install to working mods

[Русская версия](steam-deck.ru.md)

This guide sets up Unity Mod Manager (UMM) for **Pathfinder: Kingmaker** and **Pathfinder: Wrath of the Righteous** on the Steam Deck, binds the back buttons, and installs the gamepad mods from this repository and the [Buff It 2 The Limit (Groups)](https://github.com/sergusaev/wrath-epic-buffing) fork. Every step was done and checked on a Steam Deck with SteamOS, Kingmaker 2.1.7b and UMM 0.32.4.

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

# The UMM window opens with Shift+F10 (see 1.4) and shows on start.
cat > "$UMM/Params.xml" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<Param xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Hotkey>
    <keyCode>F10</keyCode>
    <modifiers>2</modifiers>
  </Hotkey>
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
  || printf '<?xml version="1.0" encoding="utf-8"?>\n<Param>\n  <Hotkey>\n    <keyCode>F10</keyCode>\n    <modifiers>2</modifiers>\n  </Hotkey>\n  <CheckUpdates>0</CheckUpdates>\n  <ShowOnStart>1</ShowOnStart>\n</Param>\n' > "$UMM/Params.xml"

# Keep WotR in gamepad mode: otherwise every key sent by a back button pops up
# "Switch to keyboard and mouse?". The mouse and keyboard interface of WotR is unavailable while this is set.
[ -f "$WOTR/startup.json" ] && cp "$WOTR/startup.json" "$WOTR/startup.json.bak"
echo '{"ForceControllerMode":"gamepad"}' > "$WOTR/startup.json"
```

In Steam: Wrath of the Righteous → ⚙ → Properties → Launch options: `WINEDLLOVERRIDES="winhttp=n,b" %command%`.

"Verify integrity of game files" in Steam may restore the original `startup.json`; write it again afterwards.

### 1.4 Hotkey of the UMM window

UMM opens its window with Ctrl+F10 by default, but in the desktop mode KDE takes Ctrl+F10 ("Show all windows"), and Ctrl+F11, Shift+Tab and F12 belong to Steam. The `Params.xml` above changes the hotkey to **Shift+F10** (`modifiers`: 1 Ctrl, 2 Shift, 4 Alt). UMM rewrites `Params.xml` when the game exits, so edit it only while the game is closed. The hotkey can also be changed in the UMM window itself (Settings tab).

UMM reacts to the hotkey only if the keys are held for about 0.2 s; a short tap of a button that sends both keys at once is ignored. The R4 binding in the next section takes care of that.

### 1.5 Check

Start the game in the game mode. The UMM window shows up on start (`ShowOnStart`), with the Mods tab listing nothing yet. The UMM window is operated with the mouse: the right trackpad (next section). If the window does not appear, see [Troubleshooting](#5-troubleshooting).

## 2. Steam Input layout

Set this up for each game separately: open the game's page → controller icon → Edit layout (or Steam button → Controller settings while the game runs). The layout of Kingmaker is stored in Steam Cloud and the one of WotR locally; Steam handles both, nothing to do by hand.

| Button | Binding | What for |
|---|---|---|
| **R4** | two commands: Left Shift, then F10 with delays (below) | UMM window |
| **L5** | keyboard key F7, a plain single press, no delays | Buff It 2 The Limit (Groups) menu; tap, hold and double press are recognised by the mod |
| **Right trackpad** | Mouse (as mouse), click = left mouse button | UMM window and mod settings |
| **R5** | right mouse button | context actions in the UMM window |
| **L4** | Enter (optional) | confirming dialogs in mouse mode; not needed with `startup.json` |

R4 step by step:

1. Back buttons → R4 → Keyboard → Left Shift. In the command's settings (gear) set **Fire end delay** to 400 ms.
2. On R4 choose "Add extra command" → Keyboard → F10. In its settings set **Fire start delay** 120 ms and **Fire end delay** 250 ms.

A tap of R4 then holds Shift for 0.4 s and F10 for 0.25 s inside it, long enough for UMM.

Things to avoid:

- **Key chords with Shift on L5.** On a short tap Steam Input does not deliver a key that has a start delay, only the Shift arrives, so the menu key must be a single key without delays.
- **Steam's Long Press and Double Press activators on L5.** With Long Press Steam also sends the regular press key, and Double Press did not reach the game. The buff mod does these gestures itself.
- **L3+R3 in WotR**: the combination opens the game's bug report window.
- The names of the back buttons in a layout file, if you ever edit one: L4 = `button_back_left_upper`, L5 = `button_back_left`, R4 = `button_back_right_upper`, R5 = `button_back_right`. A `…_lower` name does not exist and Steam silently skips such a block.

## 3. Installing the mods

Download the zip files from the release pages: [pathfinder-mods releases](https://github.com/sergusaev/pathfinder-mods/releases) and [Buff It 2 The Limit (Groups) releases](https://github.com/sergusaev/wrath-epic-buffing/releases). Every zip holds one folder with `Info.json` and the DLL; unpack it into the mods folder of the game, so it becomes `Mods/<ModId>/Info.json`:

```bash
KM="$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"
WOTR="$HOME/.local/share/Steam/steamapps/common/Pathfinder Second Adventure"
cd ~/Downloads
python3 -m zipfile -e GamepadCameraRotation-1.1.0.zip "$KM/Mods/"            # example for Kingmaker
python3 -m zipfile -e PortraitScrollFix-1.1.0.zip "$WOTR/mods/"              # example for WotR
```

When a mod is updated, delete `*.cache` files in its folder before starting the game.

| Mod | Game | Folder | Steam Deck notes |
|---|---|---|---|
| [Camera Rotation and Compass](../kingmaker-gamepad-camera-rotation) | Kingmaker | `Mods/GamepadCameraRotation` | R3 switches the camera mode, D-pad Up is turn-based mode. The compass needs WotR installed (found automatically). Turn off camera rotation in Bag of Tricks |
| [Custom Portraits Gamepad Selection Fix](../kingmaker-custom-portraits-gamepad) | Kingmaker | `Mods/ConsoleCustomPortraits` | portraits go to `~/.config/unity3d/Owlcat Games/Pathfinder Kingmaker/Portraits/` |
| [Level 1 Companions & Free Respec](../level1-companions) | Kingmaker, WotR | `Mods/Level1Companions`, `mods/Level1Companions` | a separate zip per game (`-kingmaker`, `-wotr`). Respec button in the UMM window (R4). In WotR delete `mods/lvl1companions` first |
| [Portrait Scroll Fix (gamepad)](../wotr-portrait-scroll-fix) | WotR | `mods/PortraitScrollFix` | portraits go to the `Portraits` folder inside the Proton prefix, see the mod README |
| [Buff It 2 The Limit (Groups)](https://github.com/sergusaev/wrath-epic-buffing) | WotR | `mods/BuffIt2TheLimit` | replaces the original Buff It 2 The Limit and BubbleBuffs: remove those first. Menu on L5, see below |
| [Buff It 2 The Limit (Groups) for Kingmaker](https://github.com/sergusaev/wrath-epic-buffing) | Kingmaker | `Mods/PadBuffsKingmaker` | the same menu; the menu key is F7 out of the box, so L5 → F7 is all it needs. Spells, class abilities, activatables and songs; no scrolls, potions or wands |

**The menu key of Buff It 2 The Limit (Groups) in WotR.** In Kingmaker the menu key is F7 out of the box. In WotR it is the "open buff menu" key of the original mod, which is set in the PC spellbook screen, unavailable in gamepad mode. Start WotR once with the mod, load a save and quit, then set the keys in its settings files (menu = F7, Long group = F6, Important group = F9):

```bash
WOTR="$HOME/.local/share/Steam/steamapps/common/Pathfinder Second Adventure"
cd "$WOTR/mods/BuffIt2TheLimit/UserSettings" && python3 - bi2tl-*.json <<'EOF'
import json, sys
for path in sys.argv[1:]:
    raw = open(path, 'rb').read()
    bom = raw.startswith(b'\xef\xbb\xbf')
    data = json.loads(raw.decode('utf-8-sig'))
    key = lambda k: {'Key': k, 'Ctrl': False, 'Shift': False, 'Alt': False}
    data['OpenBuffMenuKey'] = key('F7')
    data['ShortcutKeys'] = {'Long': key('F6'), 'Quick': key('None'), 'Important': key('F9')}
    open(path, 'wb').write((b'\xef\xbb\xbf' if bom else b'') + json.dumps(data, ensure_ascii=False, indent=2).encode())
    print('updated', path)
EOF
```

Each playthrough has its own settings file, so repeat this for a new game.

## 4. Logs

| What | Where |
|---|---|
| Kingmaker: UMM and mods | `~/.config/unity3d/Owlcat Games/Pathfinder Kingmaker/Player.log`, `Kingmaker_Data/Managed/UnityModManager/Log.txt` |
| WotR: UMM and mods | `~/.local/share/Steam/steamapps/compatdata/1184370/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Wrath Of The Righteous/Player.log`, `Wrath_Data/Managed/UnityModManager/Log.txt` |

Lines of the mods start with the mod id in brackets, e.g. `[GamepadCameraRotation]`; the buff menu writes `[PAD]`.

## 5. Troubleshooting

| Symptom | Cause and fix |
|---|---|
| Kingmaker: no `[Manager]` lines in the log | the launch options are not `./run.sh %command%`, or `run.sh` is not executable (`chmod +x`), or `executable_name`/`target_assembly` in it are wrong |
| Kingmaker: mods load, but the UMM window never appears | `UIStartingPoint` in `Config.xml`; remove it (1.2) |
| WotR: no traces of UMM in the logs | the launch options lack `WINEDLLOVERRIDES="winhttp=n,b"`, or `winhttp.dll` / `doorstop_config.ini` are missing in the game folder |
| WotR asks to switch to keyboard and mouse | `startup.json` is missing or was restored by a file check (1.3) |
| R4 does not open the UMM window | the R4 delays (section 2), or the hotkey in `Params.xml` is not Shift+F10 |
| The buff menu does not open on L5 | L5 must send a single F7 without delays; in WotR the key must also be set in the settings file (section 3) |
| Kingmaker mod folder is ignored | on Linux the folder is `Mods` with a capital M and the file is `Info.json` |

## Removing

Clear the launch options of the game. That alone turns UMM off; the files can stay. To remove everything: `run.sh`, `libdoorstop.so` and `Kingmaker_Data/Managed/UnityModManager` for Kingmaker; `winhttp.dll`, `doorstop_config.ini`, `startup.json` (restore `startup.json.bak` if there was one) and `Wrath_Data/Managed/UnityModManager` for WotR; the mods folders.
