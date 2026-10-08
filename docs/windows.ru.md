# Windows: Unity Mod Manager и моды без установщика

[English version](windows.md)

Обычно UMM на Windows ставят установщиком с [Nexus Mods](https://www.nexusmods.com/site/mods/21). Эта справка делает то же несколькими командами PowerShell. Пригодится, если установщик не нужен или ПК настраивается удалённо по SSH, где программу с окнами не запустить. Проверено на Windows 10 с Kingmaker 2.1.7b и UMM 0.32.4: Kingmaker настроен именно так и работает с модами, команды раздела 1 выполнены в том виде, как написаны. WotR на том ПК получила UMM от установщика, поэтому её блок ниже повторяет настройку Doorstop, которая работает для WotR на Steam Deck.

Параметры запуска на Windows не нужны: обе игры грузят UMM через Doorstop для Windows (`winhttp.dll` и `doorstop_config.ini` в папке игры).

## 0. Перед началом

- Установить игры и один раз запустить каждую.
- Закрыть игры на время изменения их файлов.
- Открыть **PowerShell**. В Windows 10 и 11 есть `curl.exe` и `tar.exe`, больше командам ничего не нужно; Python не требуется.
- Один раз на окно PowerShell задать папки игр. Ниже стандартная библиотека Steam; для другой библиотеки укажите её путь, например `F:\SteamLibrary\steamapps\common\...`:

```powershell
$KM   = "C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Kingmaker"
$WOTR = "C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure"
```

## 1. Unity Mod Manager

### 1.1 Скачать UMM

Те же файлы, что в установщике, из пакета обновления, который UMM скачивает сам (ссылка — в [Repository_beta_13.json](https://github.com/newman55/unity-mod-manager/blob/master/Repository_beta_13.json) репозитория UMM):

```powershell
$P = "$env:USERPROFILE\umm"
New-Item -ItemType Directory -Force "$P\package" | Out-Null
curl.exe -L -o "$P\umm_update.zip" "https://www.dropbox.com/s/wt18wcq5six02ku/umm_update.zip?dl=1"
tar -xf "$P\umm_update.zip" -C "$P\package"
Get-ChildItem "$P\package"
```

В `package` должны быть `UnityModManager.dll`, `0Harmony.dll`, `dnlib.dll` и `winhttp_x64.dll`.

### 1.2 Kingmaker

```powershell
$UMM = "$KM\Kingmaker_Data\Managed\UnityModManager"
New-Item -ItemType Directory -Force $UMM, "$KM\Mods" | Out-Null
Copy-Item "$P\package\UnityModManager.dll", "$P\package\UnityModManager.xml", "$P\package\0Harmony.dll", "$P\package\dnlib.dll", "$P\package\Harmony\1.2\*.dll" $UMM

# Doorstop для Windows: игра грузит winhttp.dll из своей папки, а он загружает UMM.
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

# Окно UMM открывается по Shift+F10 и показывается при старте (modifiers: 1 Ctrl, 2 Shift, 4 Alt).
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

Пропустите, если UMM для WotR уже поставлен установщиком.

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

Шаг из справки для Steam Deck, который пишет `startup.json`, нужен только деке. На ПК его не делать: он закрепляет WotR в интерфейсе геймпада.

### 1.4 Проверка

Запустите игру. Окно UMM появится при старте с пустой вкладкой Mods, дальше открывается и закрывается по Shift+F10. Если окна нет, смотрите `<игра>\<Game>_Data\Managed\UnityModManager\Log.txt` и лог игры (`%USERPROFILE%\AppData\LocalLow\Owlcat Games\<игра>\output_log.txt` у Kingmaker, `Player.log` у WotR).

## 2. Установка модов

Скачайте zip из [релизов pathfinder-mods](https://github.com/sergusaev/pathfinder-mods/releases) и [релизов Buff It 2 The Limit (Groups)](https://github.com/sergusaev/wrath-epic-buffing/releases). В каждом zip одна папка с `Info.json` и DLL; распакуйте её в папку `Mods` игры:

```powershell
cd "$env:USERPROFILE\Downloads"
tar -xf GamepadCameraRotation-1.1.0.zip -C "$KM\Mods"       # пример для Kingmaker
tar -xf PortraitScrollFix-1.1.0.zip -C "$WOTR\Mods"         # пример для WotR
```

При обновлении мода удалите файлы `*.cache` в его папке до запуска игры: UMM грузит свою закэшированную копию DLL.

| Мод | Игра | Папка | На Windows |
|---|---|---|---|
| [Camera Rotation and Compass](../kingmaker-gamepad-camera-rotation) | Kingmaker | `Mods\GamepadCameraRotation` | С клавиатурой и мышью: средняя кнопка мыши поворачивает, Alt + средняя кнопка двигает камеру, Alt+A / Alt+D поворачивают, F1 — на север; компас рядом с системными кнопками. Компасу нужна установленная WotR (находится сама в библиотеках Steam) |
| [Custom Portraits Gamepad Selection Fix](../kingmaker-custom-portraits-gamepad) | Kingmaker | `Mods\ConsoleCustomPortraits` | нужен только с геймпадом; портреты кладутся в `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Kingmaker\Portraits\` |
| [Level 1 Companions & Free Respec](../level1-companions/README.ru.md) | Kingmaker, WotR | `Mods\Level1Companions` | отдельный zip для каждой игры (`-kingmaker`, `-wotr`). Кнопка респека — в окне UMM (Shift+F10). В WotR сначала вынести `lvl1companions` из `Mods` |
| [Portrait Scroll Fix (gamepad)](../wotr-portrait-scroll-fix) | WotR | `Mods\PortraitScrollFix` | нужен только с геймпадом |
| [Buff It 2 The Limit (Groups)](https://github.com/sergusaev/wrath-epic-buffing) | WotR | `Mods\BuffIt2TheLimit` | заменяет оригинальные Buff It 2 The Limit и BubbleBuffs: сначала уберите их из `Mods`. Настройки Buff It 2 The Limit сохраняются (один id и одни файлы); настройки BubbleBuffs (`bubblebuff-*.json`) не читаются |
| [Buff It 2 The Limit (Groups) для Kingmaker](https://github.com/sergusaev/wrath-epic-buffing) | Kingmaker | `Mods\PadBuffsKingmaker` | меню открывается по F7 |

**Kingmaker и подключённый геймпад.** С подключённым контроллером Kingmaker может запуститься в интерфейсе геймпада (консольном). Для интерфейса клавиатуры и мыши выключите контроллер до запуска игры.
