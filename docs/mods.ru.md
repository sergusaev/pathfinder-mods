# Установка модов вручную

[English version](mods.md)

Справка о том, как скачать мод из релиза, поставить, обновить и удалить его руками, на Steam Deck и на Windows. Unity Mod Manager к этому моменту уже должен стоять в игре: [Steam Deck](steam-deck.ru.md), [Windows](windows.ru.md).

## Порядок и независимость

Моды ставятся **в любом порядке и в любом наборе**, ни одному не нужен другой ([почему](umm.ru.md#моды-независимы-друг-от-друга)). Удобнее всего начать с **Unity Mod Manager in Game Menus**: он даёт пункт UMM в меню игры. Остальные — по вкусу.

## Какой файл скачать

Все моды лежат на страницах релизов: [pathfinder-mods](https://github.com/sergusaev/pathfinder-mods/releases) и [меню баффов](https://github.com/sergusaev/wrath-epic-buffing/releases). Откройте нужный релиз и скачайте zip из блока **Assets** (не «Source code»).

- У мода для одной игры один zip: `<Мод>-<версия>.zip`.
- У мода для двух игр два zip: `<Мод>-<версия>-kingmaker.zip` и `<Мод>-<версия>-wotr.zip`. Они не взаимозаменяемы: каждая DLL собрана под свою игру.
- Внутри каждого zip одна папка с именем мода, в ней `Info.json` и DLL (у меню баффов WotR — ещё картинки и ресурсы). Эта папка целиком кладётся в папку `Mods` игры.

| Мод | Игра | Файл в релизе | Папка в `Mods` |
|---|---|---|---|
| [Unity Mod Manager in Game Menus](../umm-menu-button/README.ru.md) | обе | `UmmMenuButton-…-kingmaker.zip` / `…-wotr.zip` | `UmmMenuButton` |
| [Action Bar Rows](../action-bar-rows/README.ru.md) | обе | `ActionBarRows-…-kingmaker.zip` / `…-wotr.zip` | `ActionBarRows` |
| [Belt Bar](../belt-bar/README.ru.md) | обе | `BeltBar-…-kingmaker.zip` / `…-wotr.zip` | `BeltBar` |
| [Belt Consumables Refill](../belt-consumables-refill/README.ru.md) | обе | `BeltConsumablesRefill-…-kingmaker.zip` / `…-wotr.zip` | `BeltConsumablesRefill` |
| [Level 1 Companions & Free Respec](../level1-companions/README.ru.md) | обе | `Level1Companions-…-kingmaker.zip` / `…-wotr.zip` | `Level1Companions` |
| [Success Chance](../success-chance/README.ru.md) | обе | `SuccessChance-…-kingmaker.zip` / `…-wotr.zip` | `SuccessChance` |
| [Kingmaker Fixes](../kingmaker-fixes/README.ru.md) | Kingmaker | `KingmakerFixes-….zip` | `KingmakerFixes` |
| [Camera Rotation and Compass](../kingmaker-gamepad-camera-rotation/README.ru.md) | Kingmaker | `GamepadCameraRotation-….zip` | `GamepadCameraRotation` |
| [Time Control and Deadlines](../kingmaker-time-control/README.ru.md) | Kingmaker | `TimeControl-….zip` | `TimeControl` |
| [Buff It 2 The Limit (Groups) для Kingmaker](https://github.com/sergusaev/wrath-epic-buffing/blob/gamepad/pad-docs/README.ru.md#kingmaker) | Kingmaker | `PadBuffsKingmaker-….zip` (метка `kingmaker-v…`) | `PadBuffsKingmaker` |
| [WotR Fixes](../wotr-fixes/README.ru.md) | WotR | `WotrFixes-….zip` | `WotrFixes` |
| [Buff It 2 The Limit (Groups)](https://github.com/sergusaev/wrath-epic-buffing/blob/gamepad/pad-docs/README.ru.md) | WotR | `BuffIt2TheLimit-Pad-….zip` (метка `v…-pad.N`) | `BuffIt2TheLimit` |

## Что заменяет что

Если в `Mods` уже есть мод из левой колонки, уберите его папку **до** установки нового (при закрытой игре), иначе одно и то же будет сделано дважды.

| Старый мод | Чем заменён | Что сделать |
|---|---|---|
| `ConsoleCustomPortraits` (Custom Portraits Gamepad Selection Fix) | Kingmaker Fixes | удалить `Mods/ConsoleCustomPortraits` |
| `PortraitScrollFix` (Portrait Scroll Fix (gamepad), WotR) | WotR Fixes | удалить `Mods/PortraitScrollFix` |
| `lvl1companions` (Level 1 Companions от CascadingDragon, WotR) | Level 1 Companions & Free Respec | удалить `Mods/lvl1companions` |
| Buff It 2 The Limit (оригинал), BubbleBuffs (WotR) | Buff It 2 The Limit (Groups) | удалить их папки; настройки оригинала Buff It 2 The Limit сохраняются, настройки BubbleBuffs не читаются |

Совместная работа со сторонними модами:

- **Bag of Tricks** (Kingmaker): с Camera Rotation and Compass выключите в Bag of Tricks поворот камеры; с Belt Consumables Refill — «Auto Equip Consumables». Кнопку UMM из Bag of Tricks в меню заменяет Unity Mod Manager in Game Menus сам.
- **ToyBox** (WotR): с Belt Consumables Refill выключите «Refill consumables in belt slots if in inventory».

## Установка на Steam Deck

1. Перейти в режим рабочего стола и скачать zip браузером: по умолчанию он попадёт в папку `~/Downloads` («Загрузки»).
2. Открыть Konsole и вставить команды, подставив имя своего файла. Папки игр — как в [справке для Steam Deck](steam-deck.ru.md#2-найти-папки-игр); если игра на карте microSD, поменяйте первую строку.

Для Kingmaker:

```bash
KM="$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"
cd ~/Downloads
unzip -o BeltBar-1.0.1-kingmaker.zip -d "$KM/Mods/"
ls "$KM/Mods/BeltBar"
```

Для WotR:

```bash
WOTR="$HOME/.local/share/Steam/steamapps/common/Pathfinder Second Adventure"
cd ~/Downloads
unzip -o BeltBar-1.0.1-wotr.zip -d "$WOTR/Mods/"
ls "$WOTR/Mods/BeltBar"
```

`ls` должен показать `Info.json` и DLL мода. Ключ `-o` разрешает перезаписать старые файлы без вопросов.

**Без Konsole, мышью в Dolphin:** правая кнопка на zip → **Распаковать** → **Распаковать архив в…** → выбрать папку `Mods` игры (адрес — в [таблице путей](steam-deck.ru.md#9-где-что-лежит); скрытые папки — Ctrl+H).

## Установка на Windows

**Мышью:** правая кнопка на zip → **Извлечь все…** → в поле пути указать папку `Mods` игры, например `F:\SteamLibrary\steamapps\common\Pathfinder Kingmaker\Mods` → **Извлечь**. Должна получиться папка `Mods\BeltBar\` с `Info.json` внутри, без лишнего уровня `Mods\BeltBar-1.0.1-kingmaker\BeltBar\`. Если получился лишний уровень — перенесите внутреннюю папку на уровень выше.

**PowerShell** (`$KM` и `$WOTR` — из [справки для Windows](windows.ru.md#папки-игр)):

```powershell
cd "$env:USERPROFILE\Downloads"
tar -xf BeltBar-1.0.1-kingmaker.zip -C "$KM\Mods"
tar -xf BeltBar-1.0.1-wotr.zip -C "$WOTR\Mods"
Get-ChildItem "$KM\Mods\BeltBar" -Name
```

## Проверка

Запустите игру и откройте окно UMM (пункт меню или окно при старте), вкладка **Mods**: мод в списке, статус зелёный. Галочки включения модов работают только в главном меню игры.

## Обновление

Обновление ставится так же, поверх старого, при закрытой игре. Затем удалите кэш UMM в папке мода, иначе может загрузиться старая DLL.

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

Кэш — файлы вида `BeltBar.dll.2354.cache`. Мышью их можно просто удалить в файловом менеджере.

Настройки и данные мода в его папке (`Settings.xml`, у Time Control — папка `Tracking` с отслеживаемыми таймерами) при обновлении сохраняются: в zip их нет. Поэтому при обновлении не удаляйте папку мода целиком.

## Удаление

При закрытой игре удалите папку мода из `Mods`, вместе с ней удалятся его настройки. Наши моды ничего не пишут в сохранения, поэтому старые сохранения после удаления загружаются нормально; что именно остаётся (например, уже собранные вручную спутники Level 1 Companions) — в README мода.

## Если мод не работает

| Признак | Причина и решение |
|---|---|
| Мода нет в списке UMM | папка не на своём месте: нужно `Mods/<Мод>/Info.json`. Лишний уровень папок (`Mods/<Мод>/<Мод>/Info.json`) или папка рядом с `Mods`, а не внутри, — UMM мод не увидит |
| Мода нет в списке, на Steam Deck | в папке игры две папки модов, `Mods` и `mods`: Proton видит только одну. Перенесите всё в `Mods`, вторую удалите |
| Красный статус | zip не для этой игры (`-kingmaker` / `-wotr`) или UMM слишком старый. Причина — в логе игры ([пути](steam-deck.ru.md#9-где-что-лежит), [Windows](windows.ru.md#6-где-что-лежит)), строки с id мода |
| WotR: красные все моды этой коллекции сразу, в логе игры `Patching exception` и `__args` | старые версии модов с загрузчиком из UMM 0.33.2 (Doorstop 4): с ним WotR берёт свой Harmony 2.0.4, а его старые версии модов не понимают. Обновить все моды до текущих релизов (Belt Bar 1.0.2, Belt Consumables Refill 1.0.2, Level 1 Companions 1.0.4, Success Chance 1.1.1, UMM in Game Menus 1.0.3, WotR Fixes 1.1.1, меню баффов `v1.21.2-pad.9` и новее) |
| После обновления ведёт себя как старая версия | не удалён `*.cache` — см. «Обновление» |
| Мод в списке, но ничего не делает в Kingmaker с геймпадом | в `Config.xml` UMM есть `UIStartingPoint` ([Steam Deck](steam-deck.ru.md#4-unity-mod-manager-для-kingmaker), [Windows](windows.ru.md#3-убрать-uistartingpoint-у-kingmaker)) |
