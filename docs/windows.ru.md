# Windows: Unity Mod Manager и моды

[English version](windows.md)

Справка ставит Unity Mod Manager (UMM) в **Pathfinder: Kingmaker** и **Pathfinder: Wrath of the Righteous** (WotR) на ПК с Windows 10 или 11. UMM загружается через Doorstop, параметры запуска в Steam на Windows не нужны. Проверено на Windows 10: Kingmaker 2.1.7b, WotR 2.7, UMM 0.32.4.

Как устроен Doorstop, чем он лучше способа Assembly и что ломало UMM — в [отдельной справке](umm.ru.md).

## 1. Перед началом

1. Установить игры и запустить каждую один раз до главного меню.
2. Закрыть игры: пока меняются их файлы, они должны быть закрыты.
3. Узнать папку каждой игры: Steam → правая кнопка на игре → **Управление** → **Просмотреть локальные файлы**. Откроется Проводник; щёлкните по адресной строке и скопируйте путь. Обычно это `C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Kingmaker` или на другом диске, например `F:\SteamLibrary\steamapps\common\Pathfinder Kingmaker`. Папка WotR называется `Pathfinder Second Adventure`.
4. Открыть **PowerShell**: Пуск → ввести `PowerShell` → Windows PowerShell. Вставка в окно — правая кнопка мыши или Ctrl+V. Права администратора не нужны, если игры не лежат в `Program Files`; если лежат — запустите PowerShell от имени администратора.

UMM можно поставить двумя способами. **Способ А** (команды PowerShell) повторяет то, что проверено на нашем ПК, и сразу даёт правильные настройки. **Способ Б** (установщик UMM с окнами) привычнее, но после него обязательно нужен раздел 3.

## 2А. UMM командами PowerShell

### Папки игр

Вставьте в PowerShell, подставив свои пути из пункта 1.3. Переменные живут только в этом окне PowerShell: если закрыли окно, задайте их снова.

```powershell
$KM   = "C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Kingmaker"
$WOTR = "C:\Program Files (x86)\Steam\steamapps\common\Pathfinder Second Adventure"
Test-Path "$KM\Kingmaker.exe", "$WOTR\Wrath.exe"
```

Обе строки вывода должны быть `True`. Если `False` — путь неверный.

### Скачать UMM

Те же файлы, что в установщике, из пакета обновления, который UMM скачивает сам (ссылка — в [Repository_beta_13.json](https://github.com/newman55/unity-mod-manager/blob/master/Repository_beta_13.json) репозитория UMM). В Windows 10 и 11 есть `curl.exe` и `tar.exe`, больше ничего не нужно.

```powershell
$P = "$env:USERPROFILE\umm"
New-Item -ItemType Directory -Force "$P\package" | Out-Null
curl.exe -L -o "$P\umm_update.zip" "https://www.dropbox.com/s/wt18wcq5six02ku/umm_update.zip?dl=1"
tar -xf "$P\umm_update.zip" -C "$P\package"
Get-ChildItem "$P\package" -Name
```

В списке должны быть `UnityModManager.dll`, `0Harmony.dll`, `dnlib.dll`, `winhttp_x64.dll` и `Harmony`.

### Kingmaker

```powershell
$UMM = "$KM\Kingmaker_Data\Managed\UnityModManager"
New-Item -ItemType Directory -Force $UMM, "$KM\Mods" | Out-Null
Copy-Item "$P\package\UnityModManager.dll", "$P\package\UnityModManager.xml", "$P\package\0Harmony.dll", "$P\package\dnlib.dll", "$P\package\Harmony\1.2\*.dll" $UMM

# Doorstop: загрузчик и его настройка.
Copy-Item "$P\package\winhttp_x64.dll" "$KM\winhttp.dll"
"[General]`r`nenabled = true`r`ntarget_assembly = Kingmaker_Data\Managed\UnityModManager\UnityModManager.dll" | Set-Content "$KM\doorstop_config.ini" -Encoding ASCII

# Описание игры для UMM. Строки UIStartingPoint здесь НЕТ намеренно.
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

# Настройки UMM: окно при старте. Существующий файл не трогаем, в нём включённые моды.
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

Все три строки вывода — `True`.

### Wrath of the Righteous

```powershell
$UMM = "$WOTR\Wrath_Data\Managed\UnityModManager"
New-Item -ItemType Directory -Force $UMM, "$WOTR\Mods" | Out-Null
Copy-Item "$P\package\UnityModManager.dll", "$P\package\UnityModManager.xml", "$P\package\0Harmony.dll", "$P\package\dnlib.dll" $UMM

Copy-Item "$P\package\winhttp_x64.dll" "$WOTR\winhttp.dll"
"[General]`r`nenabled = true`r`ntarget_assembly = Wrath_Data\Managed\UnityModManager\UnityModManager.dll" | Set-Content "$WOTR\doorstop_config.ini" -Encoding ASCII

# У WotR строка UIStartingPoint остаётся: одно главное меню на оба режима.
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

Файл `startup.json` из справки для Steam Deck на ПК **не нужен**: он закрепляет WotR в режиме геймпада и отключает мышь.

После способа А переходите к разделу 4: раздел 3 уже выполнен командами выше.

## 2Б. UMM установщиком

1. Скачать Unity Mod Manager с [Nexus Mods](https://www.nexusmods.com/site/mods/21) (нужна бесплатная учётная запись) и распаковать архив в любую папку, например `Загрузки\UnityModManager`.
2. Запустить `UnityModManager.exe`.
3. Выбрать игру в списке, проверить путь к папке игры.
4. В способах установки выбрать **DoorstopProxy**, а не **Assembly** ([почему](umm.ru.md#assembly--почему-мы-его-не-используем)).
5. Нажать **Install**. Повторить для второй игры.
6. **Обязательно** выполнить раздел 3 для Kingmaker.

## 3. Убрать UIStartingPoint у Kingmaker

Установщик UMM записывает в `Config.xml` Kingmaker строку `UIStartingPoint`. Из-за неё в режиме геймпада окно UMM не создаётся: пункт UMM в меню не открывает окно, моды видны, но не действуют ([подробно](umm.ru.md#configxml-и-строка-uistartingpoint)). Удалить строку (`$KM` — из раздела 2А):

```powershell
$C = "$KM\Kingmaker_Data\Managed\UnityModManager\Config.xml"
(Get-Content $C) | Where-Object { $_ -notmatch 'UIStartingPoint' } | Set-Content $C -Encoding UTF8
Select-String -Path $C -Pattern 'UIStartingPoint'
```

Последняя команда ничего не должна напечатать. Повторяйте этот раздел после **каждой** переустановки или обновления UMM установщиком. У WotR строку не трогать.

## 4. Проверка

Запустите игру. При старте появится окно UMM, вкладка **Mods** пока пустая. Если окна нет, откройте `Log.txt` в папке `<Игра>_Data\Managed\UnityModManager`:

- файла нет или в нём нет свежих строк `[Manager]` — UMM не запускается: проверьте, что в папке игры есть `winhttp.dll` и `doorstop_config.ini`;
- строки есть — UMM работает, причину ищите в [неполадках](#7-неполадки).

**Kingmaker и геймпад.** Если при запуске подключён геймпад, Kingmaker стартует в интерфейсе для геймпада (консольном). Для интерфейса мыши и клавиатуры отключите геймпад до запуска игры.

## 5. Установка модов

Как скачать, поставить, обновить и удалить моды — в [справке по установке модов](mods.ru.md). Первым стоит поставить [Unity Mod Manager in Game Menus](../umm-menu-button/README.ru.md): с ним окно UMM открывается из меню игры.

## 6. Где что лежит

| | Kingmaker | WotR |
|---|---|---|
| Папка игры | из пункта 1.3 | из пункта 1.3 |
| UMM | `<папка игры>\Kingmaker_Data\Managed\UnityModManager\` | `<папка игры>\Wrath_Data\Managed\UnityModManager\` |
| Моды | `<папка игры>\Mods\` | `<папка игры>\Mods\` |
| Лог UMM | `…\UnityModManager\Log.txt` | `…\UnityModManager\Log.txt` |
| Данные игры | `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Kingmaker\` | `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Wrath Of The Righteous\` |
| Лог игры и модов | `output_log.txt` в данных игры | `Player.log` и `GameLogFull.txt` в данных игры |
| Сохранения | `Saved Games\` в данных игры | `Saved Games\` в данных игры |
| Свои портреты | `Portraits\` в данных игры | `Portraits\` в данных игры |

Путь с `%USERPROFILE%` можно вставить прямо в адресную строку Проводника. Папка `AppData` скрыта, но по вставленному пути открывается.

## 7. Неполадки

| Признак | Причина и решение |
|---|---|
| Окна UMM нет, в `Log.txt` нет свежих строк | нет `winhttp.dll` или `doorstop_config.ini` в папке игры; или в `doorstop_config.ini` неверный путь в `target_assembly` — повторить раздел 2А для этой игры |
| Kingmaker, режим геймпада: пункт UMM в меню ничего не открывает, моды не действуют | в `Config.xml` есть `UIStartingPoint` — раздел 3 |
| UMM пропал после обновления игры | UMM стоял способом Assembly — поставить заново способом А или Б с DoorstopProxy |
| Антивирус удаляет `winhttp.dll` | добавить папку игры в исключения антивируса и повторить копирование `winhttp.dll` |
| Мод не появился в списке, красный статус, старая версия после обновления | см. [справку по установке модов](mods.ru.md#если-мод-не-работает) |

## 8. Удаление

Удалить из папки игры `winhttp.dll`, `doorstop_config.ini`, папки `<Игра>_Data\Managed\UnityModManager` и `Mods`. Если ставили установщиком, можно нажать в нём **Uninstall**. Сохранения не затрагиваются.
