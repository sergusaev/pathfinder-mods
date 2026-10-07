# Windows: Unity Mod Manager and the mods without the installer

[Русская версия](windows.ru.md)

The usual way to get Unity Mod Manager (UMM) on Windows is its installer from [Nexus Mods](https://www.nexusmods.com/site/mods/21). This guide does the same with a few PowerShell commands: useful when the installer is not wanted, or the PC is set up remotely over SSH, where a program with windows cannot be used. Checked on Windows 10 with Kingmaker 2.1.7b and UMM 0.32.4: Kingmaker was set up this way and runs the mods; the commands of section 1 were run as written. WotR on that PC got UMM from the installer, so its block below repeats the Doorstop setup that works for WotR on the Steam Deck.

No launch options are needed on Windows: both games load UMM through Doorstop for Windows (`winhttp.dll` and `doorstop_config.ini` in the game folder).

## 0. Before you start

- Install the games and start each one once.
- Close the games while you change their files.
- Open **PowerShell**. Windows 10 and 11 ship `curl.exe` and `tar.exe`, which is all the commands below need; Python is not required.
- Set the game folders once per PowerShell window. The default Steam library is shown; with another library use its path, e.g. `F:\SteamLibrary\steamapps\common\...`:

```powershell
$KM   = "C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Kingmaker"
$WOTR = "C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure"
```

## 1. Unity Mod Manager

### 1.1 Download UMM

The same files as in the installer, from the update package UMM downloads itself (the link is in [Repository_beta_13.json](https://github.com/newman55/unity-mod-manager/blob/master/Repository_beta_13.json) of the UMM repository):

```powershell
$P = "$env:USERPROFILE\umm"
New-Item -ItemType Directory -Force "$P\package" | Out-Null
curl.exe -L -o "$P\umm_update.zip" "https://www.dropbox.com/s/wt18wcq5six02ku/umm_update.zip?dl=1"
tar -xf "$P\umm_update.zip" -C "$P\package"
Get-ChildItem "$P\package"
```

`package` must contain `UnityModManager.dll`, `0Harmony.dll`, `dnlib.dll` and `winhttp_x64.dll`.

### 1.2 Kingmaker

```powershell
$UMM = "$KM\Kingmaker_Data\Managed\UnityModManager"
New-Item -ItemType Directory -Force $UMM, "$KM\Mods" | Out-Null
Copy-Item "$P\package\UnityModManager.dll", "$P\package\UnityModManager.xml", "$P\package\0Harmony.dll", "$P\package\dnlib.dll", "$P\package\Harmony\1.2\*.dll" $UMM

# Doorstop for Windows: the game loads winhttp.dll from its folder, which loads UMM.
Copy-Item "$P\package\winhttp_x64.dll" "$KM\winhttp.dll"
"[General]`r`nenabled = true`r`ntarget_assembly = Kingmaker_Data\Managed\UnityModManager\UnityModManager.dll" | Set-Content "$KM\doorstop_config.ini" -Encoding ASCII

@'
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
'@ | Set-Content "$UMM\Config.xml" -Encoding UTF8

# The UMM window opens with Shift+F10 and shows on start (modifiers: 1 Ctrl, 2 Shift, 4 Alt).
@'
<?xml version="1.0" encoding="utf-8"?>
<Param xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <Hotkey>
    <keyCode>F10</keyCode>
    <modifiers>2</modifiers>
  </Hotkey>
  <CheckUpdates>0</CheckUpdates>
  <ShowOnStart>1</ShowOnStart>
</Param>
'@ | Set-Content "$UMM\Params.xml" -Encoding UTF8
```

### 1.3 Wrath of the Righteous

Skip this if UMM is already installed for WotR with the installer.

```powershell
$UMM = "$WOTR\Wrath_Data\Managed\UnityModManager"
New-Item -ItemType Directory -Force $UMM, "$WOTR\Mods" | Out-Null
Copy-Item "$P\package\UnityModManager.dll", "$P\package\UnityModManager.xml", "$P\package\0Harmony.dll", "$P\package\dnlib.dll" $UMM
Copy-Item "$P\package\winhttp_x64.dll" "$WOTR\winhttp.dll"
"[General]`r`nenabled = true`r`ntarget_assembly = Wrath_Data\Managed\UnityModManager\UnityModManager.dll" | Set-Content "$WOTR\doorstop_config.ini" -Encoding ASCII

@'
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
'@ | Set-Content "$UMM\Config.xml" -Encoding UTF8
Copy-Item "$KM\Kingmaker_Data\Managed\UnityModManager\Params.xml" $UMM
```

The WotR step of the Steam Deck guide that writes `startup.json` is for the Steam Deck only; do not do it on a PC, it locks WotR into the gamepad interface.

### 1.4 Check

Start the game. The UMM window shows on start with an empty Mods tab; later it opens and closes with Shift+F10. If it does not appear, see `<game>\<Game>_Data\Managed\UnityModManager\Log.txt` and the game log (`%USERPROFILE%\AppData\LocalLow\Owlcat Games\<game>\output_log.txt` for Kingmaker, `Player.log` for WotR).

## 2. Installing the mods

Download the zip files from [pathfinder-mods releases](https://github.com/sergusaev/pathfinder-mods/releases) and [Buff It 2 The Limit (Pad) releases](https://github.com/sergusaev/wrath-epic-buffing/releases). Every zip holds one folder with `Info.json` and the DLL; unpack it into the game's `Mods` folder:

```powershell
cd "$env:USERPROFILE\Downloads"
tar -xf GamepadCameraRotation-1.1.0.zip -C "$KM\Mods"       # example for Kingmaker
tar -xf PortraitScrollFix-1.1.0.zip -C "$WOTR\Mods"         # example for WotR
```

When a mod is updated, delete the `*.cache` files in its folder before starting the game: UMM loads its cached copy of the DLL.

| Mod | Game | Folder | Windows notes |
|---|---|---|---|
| [Gamepad Camera Rotation](../kingmaker-gamepad-camera-rotation) | Kingmaker | `Mods\GamepadCameraRotation` | With keyboard and mouse: the middle mouse button rotates, Alt + middle mouse moves the camera, Alt+A / Alt+D rotate, F1 turns north; the compass is next to the system buttons. The compass needs WotR installed (found automatically in the Steam libraries) |
| [Custom Portraits Gamepad Selection Fix](../kingmaker-custom-portraits-gamepad) | Kingmaker | `Mods\ConsoleCustomPortraits` | only matters with a gamepad; portraits go to `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Kingmaker\Portraits\` |
| [Portrait Scroll Fix (gamepad)](../wotr-portrait-scroll-fix) | WotR | `Mods\PortraitScrollFix` | only matters with a gamepad |
| [Buff It 2 The Limit (Pad)](https://github.com/sergusaev/wrath-epic-buffing) | WotR | `Mods\BuffIt2TheLimit` | replaces the original Buff It 2 The Limit and BubbleBuffs: move those out of `Mods` first. The settings of Buff It 2 The Limit stay (same id and files); the settings of BubbleBuffs (`bubblebuff-*.json`) are not read |
| [Buff It 2 The Limit (Pad) for Kingmaker](https://github.com/sergusaev/wrath-epic-buffing) | Kingmaker | `Mods\PadBuffsKingmaker` | the menu opens with F7 |

**Kingmaker and a connected gamepad.** With a controller connected, Kingmaker may start in the gamepad (console) interface. For the keyboard and mouse interface turn the controller off before starting the game.
