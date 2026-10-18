# Windows: Unity Mod Manager and the mods

[Русская версия](windows.ru.md)

This guide installs Unity Mod Manager (UMM) into **Pathfinder: Kingmaker** and **Pathfinder: Wrath of the Righteous** (WotR) on a Windows 10 or 11 PC. UMM loads through Doorstop; Windows needs no launch options in Steam. Checked on Windows 10: Kingmaker 2.1.7b, WotR 2.7, UMM 0.32.4.

How Doorstop works, why it beats the Assembly method and what broke UMM is in [a separate page](umm.md).

## 1. Before you start

1. Install the games and start each once up to the main menu.
2. Close the games: they must be closed while their files change.
3. Find each game's folder: Steam → right-click the game → **Manage** → **Browse local files**. File Explorer opens; click the address bar and copy the path. It is usually `C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Kingmaker`, or on another drive, e.g. `F:\SteamLibrary\steamapps\common\Pathfinder Kingmaker`. The WotR folder is called `Pathfinder Second Adventure`.
4. Open **PowerShell**: Start → type `PowerShell` → Windows PowerShell. Paste with a right-click or Ctrl+V. No administrator rights are needed unless the games are under `Program Files`; if they are, run PowerShell as administrator.

There are two ways to install UMM. **Way A** (PowerShell commands) repeats what is tested on our PC and gives the right settings at once. **Way B** (the UMM installer with windows) is more familiar, but section 3 is a must after it.

## 2A. UMM with PowerShell commands

### Game folders

Paste into PowerShell with your paths from item 1.3. The variables live in this PowerShell window only: if you closed it, set them again.

```powershell
$KM   = "C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Kingmaker"
$WOTR = "C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure"
Test-Path "$KM\Kingmaker.exe", "$WOTR\Wrath.exe"
```

Both output lines must be `True`. `False` means the path is wrong.

### Download UMM

The same files as in the installer, from the update package that UMM downloads by itself (its link is in [Repository_beta_13.json](https://github.com/newman55/unity-mod-manager/blob/master/Repository_beta_13.json) of the UMM repository). Windows 10 and 11 have `curl.exe` and `tar.exe`; nothing else is needed.

```powershell
$P = "$env:USERPROFILE\umm"
New-Item -ItemType Directory -Force "$P\package" | Out-Null
curl.exe -L -o "$P\umm_update.zip" "https://www.dropbox.com/s/wt18wcq5six02ku/umm_update.zip?dl=1"
tar -xf "$P\umm_update.zip" -C "$P\package"
Get-ChildItem "$P\package" -Name
```

The list must have `UnityModManager.dll`, `0Harmony.dll`, `dnlib.dll`, `winhttp_x64.dll` and `Harmony`.

### Kingmaker

```powershell
$UMM = "$KM\Kingmaker_Data\Managed\UnityModManager"
New-Item -ItemType Directory -Force $UMM, "$KM\Mods" | Out-Null
Copy-Item "$P\package\UnityModManager.dll", "$P\package\UnityModManager.xml", "$P\package\0Harmony.dll", "$P\package\dnlib.dll", "$P\package\Harmony\1.2\*.dll" $UMM

# Doorstop: the loader and its settings.
Copy-Item "$P\package\winhttp_x64.dll" "$KM\winhttp.dll"
"[General]`r`nenabled = true`r`ntarget_assembly = Kingmaker_Data\Managed\UnityModManager\UnityModManager.dll" | Set-Content "$KM\doorstop_config.ini" -Encoding ASCII

# The game description for UMM. There is NO UIStartingPoint line here, on purpose.
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

# UMM settings: the window on start. An existing file is left alone: it holds which mods are on.
if (-not (Test-Path "$UMM\Params.xml")) {
@'
<?xml version="1.0" encoding="utf-8"?>
<Param xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <CheckUpdates>0</CheckUpdates>
  <ShowOnStart>1</ShowOnStart>
</Param>
'@ | Set-Content "$UMM\Params.xml" -Encoding UTF8
}

Test-Path "$KM\winhttp.dll", "$KM\doorstop_config.ini", "$UMM\UnityModManager.dll"
```

All three output lines are `True`.

### Wrath of the Righteous

```powershell
$UMM = "$WOTR\Wrath_Data\Managed\UnityModManager"
New-Item -ItemType Directory -Force $UMM, "$WOTR\Mods" | Out-Null
Copy-Item "$P\package\UnityModManager.dll", "$P\package\UnityModManager.xml", "$P\package\0Harmony.dll", "$P\package\dnlib.dll" $UMM

Copy-Item "$P\package\winhttp_x64.dll" "$WOTR\winhttp.dll"
"[General]`r`nenabled = true`r`ntarget_assembly = Wrath_Data\Managed\UnityModManager\UnityModManager.dll" | Set-Content "$WOTR\doorstop_config.ini" -Encoding ASCII

# WotR keeps the UIStartingPoint line: one main menu for both modes.
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

if (-not (Test-Path "$UMM\Params.xml")) {
@'
<?xml version="1.0" encoding="utf-8"?>
<Param xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <CheckUpdates>0</CheckUpdates>
  <ShowOnStart>1</ShowOnStart>
</Param>
'@ | Set-Content "$UMM\Params.xml" -Encoding UTF8
}

Test-Path "$WOTR\winhttp.dll", "$WOTR\doorstop_config.ini", "$UMM\UnityModManager.dll"
```

The `startup.json` file of the Steam Deck guide is **not** for the PC: it pins WotR to gamepad mode and turns the mouse off.

After way A go to section 4: the commands above already did section 3.

## 2B. UMM with the installer

1. Download Unity Mod Manager from [Nexus Mods](https://www.nexusmods.com/site/mods/21) (a free account is needed) and unpack the archive into any folder, e.g. `Downloads\UnityModManager`.
2. Run `UnityModManager.exe`.
3. Pick the game in the list and check the path to the game folder.
4. Among the installation methods pick **DoorstopProxy**, not **Assembly** ([why](umm.md#assembly-and-why-we-do-not-use-it)).
5. Click **Install**. Repeat for the other game.
6. Do section 3 for Kingmaker — **required**.

## 3. Remove UIStartingPoint for Kingmaker

The UMM installer writes the `UIStartingPoint` line into Kingmaker's `Config.xml`. Because of it the UMM window is never created in gamepad mode: the UMM menu item does not open the window, and the mods are visible but do nothing ([details](umm.md#configxml-and-the-uistartingpoint-line)). Delete the line (`$KM` is from section 2A):

```powershell
$C = "$KM\Kingmaker_Data\Managed\UnityModManager\Config.xml"
(Get-Content $C) | Where-Object { $_ -notmatch 'UIStartingPoint' } | Set-Content $C -Encoding UTF8
Select-String -Path $C -Pattern 'UIStartingPoint'
```

The last command must print nothing. Repeat this section after **every** reinstall or update of UMM with the installer. Leave WotR's line alone.

## 4. Check

Start the game. The UMM window appears at start; its **Mods** tab is empty for now. If there is no window, open `Log.txt` in `<Game>_Data\Managed\UnityModManager`:

- no file, or no fresh `[Manager]` lines — UMM does not start: check that `winhttp.dll` and `doorstop_config.ini` are in the game folder;
- there are lines — UMM works; look for the cause under [troubleshooting](#7-troubleshooting).

**Kingmaker and a gamepad.** If a gamepad is connected at launch, Kingmaker starts in the gamepad (console) interface. For the mouse and keyboard interface, disconnect the gamepad before starting the game.

## 5. Installing the mods

How to download, install, update and remove the mods is in the [mod installation guide](mods.md). The first one to install is [Unity Mod Manager in Game Menus](../umm-menu-button): with it the UMM window opens from the game's menus.

## 6. Where things are

| | Kingmaker | WotR |
|---|---|---|
| Game folder | from item 1.3 | from item 1.3 |
| UMM | `<game folder>\Kingmaker_Data\Managed\UnityModManager\` | `<game folder>\Wrath_Data\Managed\UnityModManager\` |
| Mods | `<game folder>\Mods\` | `<game folder>\Mods\` |
| UMM log | `…\UnityModManager\Log.txt` | `…\UnityModManager\Log.txt` |
| Game data | `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Kingmaker\` | `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Wrath Of The Righteous\` |
| Game and mod log | `output_log.txt` in the game data | `Player.log` and `GameLogFull.txt` in the game data |
| Saves | `Saved Games\` in the game data | `Saved Games\` in the game data |
| Custom portraits | `Portraits\` in the game data | `Portraits\` in the game data |

A path with `%USERPROFILE%` can be pasted right into File Explorer's address bar. `AppData` is hidden but opens by the pasted path.

## 7. Troubleshooting

| Symptom | Cause and fix |
|---|---|
| No UMM window, no fresh lines in `Log.txt` | no `winhttp.dll` or `doorstop_config.ini` in the game folder, or a wrong path in `target_assembly` of `doorstop_config.ini` — repeat section 2A for this game |
| Kingmaker, gamepad mode: the UMM menu item opens nothing, the mods do nothing | `Config.xml` has `UIStartingPoint` — section 3 |
| UMM vanished after a game update | UMM was installed with Assembly — reinstall with way A, or way B with DoorstopProxy |
| The antivirus deletes `winhttp.dll` | add the game folder to the antivirus exclusions and copy `winhttp.dll` again |
| A mod is not listed, has a red status, or is the old version after an update | see the [mod installation guide](mods.md#if-a-mod-does-not-work) |

## 8. Uninstalling

Delete `winhttp.dll`, `doorstop_config.ini`, the `<Game>_Data\Managed\UnityModManager` and `Mods` folders from the game folder. With the installer you can also click **Uninstall** in it. The saves are not touched.
