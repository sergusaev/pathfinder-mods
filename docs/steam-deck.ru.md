# Steam Deck: от чистой установки до работающих модов

[English version](steam-deck.md)

Справка настраивает Unity Mod Manager (UMM) для **Pathfinder: Kingmaker** и **Pathfinder: Wrath of the Righteous** на Steam Deck и ставит моды из этого репозитория и форк [Buff It 2 The Limit (Groups)](https://github.com/sergusaev/wrath-epic-buffing). Каждый шаг выполнен и проверен на Steam Deck со SteamOS, Kingmaker 2.1.7b и UMM 0.32.4.

Игры настраиваются по-разному:

| | Kingmaker | Wrath of the Righteous |
|---|---|---|
| Как запускается | нативная сборка для Linux | сборка для Windows через Proton |
| Как грузится UMM | Doorstop для Linux: `libdoorstop.so` + `run.sh` | Doorstop для Windows: `winhttp.dll` |
| Папка модов | `Mods` (регистр важен) | `mods` |
| Параметры запуска | `./run.sh %command%` | `WINEDLLOVERRIDES="winhttp=n,b" %command%` |

## 0. Перед началом

- Установить игры и один раз запустить каждую, чтобы Steam создал их папки и префикс Proton для WotR.
- Перейти в режим рабочего стола (Steam → Питание → Переключиться на рабочий стол) и открыть **Konsole**. Все команды ниже вводятся там; каждый блок можно вставить целиком (Ctrl+Shift+V).
- Команды рассчитаны на игры во внутренней библиотеке Steam. Если игра на карте microSD, поменяйте путь в первой строке блока, например `/run/media/deck/<имя карты>/steamapps/common/...`.
- Пока меняете файлы игры, она должна быть закрыта.

## 1. Unity Mod Manager

### 1.1 Скачать UMM

Автор распространяет UMM на [Nexus Mods](https://www.nexusmods.com/site/mods/21) в виде установщика для Windows, а он не подходит для нативного Kingmaker. Те же файлы есть в пакете обновления, который скачивает сам UMM (ссылка — в [Repository_beta_13.json](https://github.com/newman55/unity-mod-manager/blob/master/Repository_beta_13.json) [репозитория UMM](https://github.com/newman55/unity-mod-manager)):

```bash
mkdir -p ~/umm && cd ~/umm
curl -L -o umm_update.zip "https://www.dropbox.com/s/wt18wcq5six02ku/umm_update.zip?dl=1"
python3 -m zipfile -e umm_update.zip package
curl -L -o libdoorstop.so https://raw.githubusercontent.com/newman55/unity-mod-manager/master/lib/libdoorstop_x64.so
curl -L -o run.sh https://raw.githubusercontent.com/newman55/unity-mod-manager/master/lib/run.sh
ls package
```

В `package` должны быть `UnityModManager.dll`, `0Harmony.dll`, `dnlib.dll` и `winhttp_x64.dll`.

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

В Steam: Kingmaker → ⚙ → Свойства → Параметры запуска: `./run.sh %command%`.

`UIStartingPoint` в `Config.xml` пропущен намеренно: в интерфейсе геймпада Kingmaker не вызывает `MainMenu.Start`, и окно UMM не появилось бы никогда.

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

В Steam: Wrath of the Righteous → ⚙ → Свойства → Параметры запуска: `WINEDLLOVERRIDES="winhttp=n,b" %command%`.

`startup.json` держит WotR в режиме геймпада: без него на любую клавишу (в том числе с экранной клавиатуры Steam) игра спрашивает «Перейти на клавиатуру и мышь?». Интерфейс клавиатуры и мыши в WotR при этом недоступен. «Проверка целостности файлов» в Steam может вернуть исходный `startup.json` — тогда записать его снова.

### 1.4 Проверка

Запустить игру в игровом режиме. При старте появится окно UMM (`ShowOnStart`), вкладка Mods пока пустая. Если окно не появилось — см. [Неполадки](#4-неполадки).

## 2. Установка модов

Скачать zip-архивы со страниц релизов: [релизы pathfinder-mods](https://github.com/sergusaev/pathfinder-mods/releases) и [релизы Buff It 2 The Limit (Groups)](https://github.com/sergusaev/wrath-epic-buffing/releases). В каждом архиве одна папка с `Info.json` и DLL; распаковать её в папку модов игры, чтобы получилось `Mods/<ModId>/Info.json`:

```bash
KM="$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"
WOTR="$HOME/.local/share/Steam/steamapps/common/Pathfinder Second Adventure"
cd ~/Downloads
python3 -m zipfile -e GamepadCameraRotation-1.1.0.zip "$KM/Mods/"            # example for Kingmaker
python3 -m zipfile -e PortraitScrollFix-1.1.0.zip "$WOTR/mods/"              # example for WotR
```

При обновлении мода удалить файлы `*.cache` в его папке до запуска игры.

| Мод | Игра | Папка | Примечания |
|---|---|---|---|
| [Unity Mod Manager in Game Menus](../umm-menu-button/README.ru.md) | Kingmaker, WotR | `Mods/UmmMenuButton`, `mods/UmmMenuButton` | отдельный zip для каждой игры |
| [Kingmaker Fixes](../kingmaker-fixes/README.ru.md) | Kingmaker | `Mods/KingmakerFixes` | заменяет `Mods/ConsoleCustomPortraits`, ту папку удалить; портреты кладутся в `~/.local/share/Steam/steamapps/compatdata/640820/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Kingmaker/Portraits/` |
| [Time Control and Deadlines](../kingmaker-time-control/README.ru.md) | Kingmaker | `Mods/TimeControl` | |
| [Belt Consumables Refill](../belt-consumables-refill/README.ru.md) | Kingmaker, WotR | `Mods/BeltConsumablesRefill`, `mods/BeltConsumablesRefill` | отдельный zip для каждой игры |
| [Camera Rotation and Compass](../kingmaker-gamepad-camera-rotation/README.ru.md) | Kingmaker | `Mods/GamepadCameraRotation` | компасу нужна установленная WotR (находится сама). В Bag of Tricks выключить поворот камеры |
| [Level 1 Companions & Free Respec](../level1-companions/README.ru.md) | Kingmaker, WotR | `Mods/Level1Companions`, `mods/Level1Companions` | отдельный zip для каждой игры (`-kingmaker`, `-wotr`). В WotR сначала удалить `mods/lvl1companions` |
| [Portrait Scroll Fix (gamepad)](../wotr-portrait-scroll-fix/README.ru.md) | WotR | `mods/PortraitScrollFix` | портреты — в папку `Portraits` внутри префикса Proton, см. README мода |
| [WotR Fixes](../wotr-fixes/README.ru.md) | WotR | `mods/WotrFixes` | |
| [Buff It 2 The Limit (Groups)](https://github.com/sergusaev/wrath-epic-buffing/blob/gamepad/pad-docs/README.ru.md) | WotR | `mods/BuffIt2TheLimit` | заменяет оригинальные Buff It 2 The Limit и BubbleBuffs — их сначала удалить |
| [Buff It 2 The Limit (Groups) для Kingmaker](https://github.com/sergusaev/wrath-epic-buffing/blob/gamepad/pad-docs/README.ru.md#kingmaker) | Kingmaker | `Mods/PadBuffsKingmaker` | |

## 3. Логи

| Что | Где |
|---|---|
| Kingmaker: UMM и моды | `~/.config/unity3d/Owlcat Games/Pathfinder Kingmaker/Player.log`, `Kingmaker_Data/Managed/UnityModManager/Log.txt` |
| WotR: UMM и моды | `~/.local/share/Steam/steamapps/compatdata/1184370/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Wrath Of The Righteous/Player.log`, `Wrath_Data/Managed/UnityModManager/Log.txt` |

Строки модов начинаются с id мода в скобках, например `[GamepadCameraRotation]`; меню баффов пишет `[PAD]`.

## 4. Неполадки

| Признак | Причина и решение |
|---|---|
| Kingmaker: в логе нет строк `[Manager]` | параметры запуска не `./run.sh %command%`, или `run.sh` не исполняемый (`chmod +x`), или в нём неверные `executable_name`/`target_assembly` |
| Kingmaker: моды грузятся, а окно UMM не появляется | в `Config.xml` есть `UIStartingPoint`; убрать (1.2) |
| WotR: в логах нет следов UMM | в параметрах запуска нет `WINEDLLOVERRIDES="winhttp=n,b"`, или в папке игры нет `winhttp.dll` / `doorstop_config.ini` |
| WotR предлагает перейти на клавиатуру и мышь | нет `startup.json` или его вернула проверка файлов (1.3) |
| В меню нет пункта «Unity Mod Manager» | Unity Mod Manager in Game Menus нет в папке модов этой игры, или игра запустилась без UMM (см. строки выше) |
| Мод не загрузился | см. его статус в окне UMM и лог (раздел 3) |
| Kingmaker не видит папку мода | на Linux папка называется `Mods` с заглавной M, а файл — `Info.json` |

## Удаление

Очистить параметры запуска игры — одного этого хватает, чтобы выключить UMM; файлы могут остаться. Чтобы убрать всё: для Kingmaker — `run.sh`, `libdoorstop.so` и `Kingmaker_Data/Managed/UnityModManager`; для WotR — `winhttp.dll`, `doorstop_config.ini`, `startup.json` (вернуть `startup.json.bak`, если он был) и `Wrath_Data/Managed/UnityModManager`; папки модов.
