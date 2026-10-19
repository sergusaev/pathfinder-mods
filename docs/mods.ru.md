# Установка модов вручную

[English version](mods.md)

Справка о том, как скачать мод из релиза, поставить, обновить и удалить его руками, на Steam Deck и на Windows. Unity Mod Manager к этому моменту уже должен стоять в игре: [Steam Deck](steam-deck.ru.md), [Windows](windows.ru.md).

## Порядок и независимость

Моды ставятся **в любом порядке и в любом наборе**, ни одному не нужен другой ([почему](umm.ru.md#моды-независимы-друг-от-друга)). Удобнее всего начать с **Unity Mod Manager in Game Menus**: он даёт пункт UMM в меню игры. Остальные — по вкусу.

## Какой файл скачать

Все моды лежат на страницах релизов: [pathfinder-mods](https://github.com/sergusaev/pathfinder-mods/releases) и [меню баффов](https://github.com/sergusaev/wrath-epic-buffing/releases). Самые новые релизы — вверху страницы, у каждого мода свой: первый сверху релиз с именем нужного мода и есть его свежая версия. Качать zip из блока **Assets** (не «Source code»). Команды ниже находят и скачивают свежую версию сами.

- У мода для одной игры один zip: `<Имя>-<версия>.zip`.
- У мода для двух игр два zip: `<Имя>-<версия>-kingmaker.zip` и `<Имя>-<версия>-wotr.zip`. Они не взаимозаменяемы: каждая DLL собрана под свою игру.
- Внутри каждого zip одна папка мода, в ней `Info.json` и DLL (у меню баффов WotR — ещё картинки и ресурсы). Эта папка целиком кладётся в папку `Mods` игры.

| Мод | Игра | Имя файла (`MOD`) | Папка в `Mods` |
|---|---|---|---|
| [Unity Mod Manager in Game Menus](../umm-menu-button/README.ru.md) | обе | `UmmMenuButton` | `UmmMenuButton` |
| [Action Bar Rows](../action-bar-rows/README.ru.md) | обе | `ActionBarRows` | `ActionBarRows` |
| [Belt Bar](../belt-bar/README.ru.md) | обе | `BeltBar` | `BeltBar` |
| [Belt Consumables Refill](../belt-consumables-refill/README.ru.md) | обе | `BeltConsumablesRefill` | `BeltConsumablesRefill` |
| [Level 1 Companions & Free Respec](../level1-companions/README.ru.md) | обе | `Level1Companions` | `Level1Companions` |
| [Success Chance](../success-chance/README.ru.md) | обе | `SuccessChance` | `SuccessChance` |
| [Kingmaker Fixes](../kingmaker-fixes/README.ru.md) | Kingmaker | `KingmakerFixes` | `KingmakerFixes` |
| [Camera Rotation and Compass](../kingmaker-gamepad-camera-rotation/README.ru.md) | Kingmaker | `GamepadCameraRotation` | `GamepadCameraRotation` |
| [Time Control and Deadlines](../kingmaker-time-control/README.ru.md) | Kingmaker | `TimeControl` | `TimeControl` |
| [Buff It 2 The Limit (Groups) для Kingmaker](https://github.com/sergusaev/wrath-epic-buffing/blob/gamepad/pad-docs/README.ru.md#kingmaker) | Kingmaker | `PadBuffsKingmaker` (релизы меню баффов) | `PadBuffsKingmaker` |
| [WotR Fixes](../wotr-fixes/README.ru.md) | WotR | `WotrFixes` | `WotrFixes` |
| [Buff It 2 The Limit (Groups)](https://github.com/sergusaev/wrath-epic-buffing/blob/gamepad/pad-docs/README.ru.md) | WotR | `BuffIt2TheLimit-Pad` (релизы меню баффов) | `BuffIt2TheLimit` |

В командах ниже мод задают три строки:

- `MOD` — имя файла из таблицы;
- `GAME` — `-kingmaker` или `-wotr` для мода двух игр; для мода одной игры пусто (`GAME=` / `$GAME = ""`);
- `REPO` — `sergusaev/pathfinder-mods`, а для обоих меню баффов — `sergusaev/wrath-epic-buffing`.

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

Игра закрыта. Режим рабочего стола → Konsole (вставка — Ctrl+Shift+V). Каждый блок вставляется целиком; первые строки поменяйте под свой мод.

**1. Скачать.** Командой — она сама найдёт свежую версию:

```bash
MOD=BeltBar                       # имя файла из таблицы
GAME=-kingmaker                   # -kingmaker / -wotr; у мода одной игры пусто: GAME=
REPO=sergusaev/pathfinder-mods    # меню баффов: sergusaev/wrath-epic-buffing
cd ~/Downloads
URL=$(curl -s "https://api.github.com/repos/$REPO/releases?per_page=100" | grep -o "https://[^\"]*/$MOD-[0-9][^/\"]*$GAME\.zip" | head -1)
echo "$URL"
curl -L -O "$URL"
F=$(basename "$URL")
```

`echo` должен показать ссылку на zip. Пустая строка — опечатка в `MOD`/`GAME`/`REPO` или нет интернета.

Или браузером: zip попадёт в `~/Downloads` («Загрузки»). Тогда вместо блока выше:

```bash
MOD=BeltBar; GAME=-kingmaker      # как в блоке выше
F=$(ls -t ~/Downloads/$MOD-*$GAME*.zip | head -1)
echo "$F"
```

**2. Распаковать в `Mods`.** В той же Konsole, сразу после шага 1. Папки игр — как в [справке для Steam Deck](steam-deck.ru.md#2-найти-папки-игр); если игра на карте microSD, поменяйте первую строку.

```bash
G="$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"   # WotR: …/common/Pathfinder Second Adventure
D=$(unzip -Z1 "$F" | head -1 | cut -d/ -f1)
unzip -o "$F" -d "$G/Mods/"
rm -f "$G/Mods/$D/"*.cache
ls -l "$G/Mods/$D"
```

`ls` должен показать `Info.json` и DLL мода (дата файлов — из zip, то есть дата сборки релиза, а не сегодняшняя). `D` — папка мода внутри zip, `-o` перезаписывает старые файлы без вопросов, `rm` удаляет кэш UMM (см. «Обновление»).

**Без Konsole, мышью в Dolphin:** правая кнопка на zip → **Распаковать** → **Распаковать архив в…** → выбрать папку `Mods` игры (адрес — в [таблице путей](steam-deck.ru.md#9-где-что-лежит); скрытые папки — Ctrl+H). При обновлении затем удалить кэш (см. «Обновление»).

## Установка на Windows

Игра закрыта.

**Мышью:** правая кнопка на zip → **Извлечь все…** → в поле пути указать папку `Mods` игры, например `F:\SteamLibrary\steamapps\common\Pathfinder Kingmaker\Mods` → **Извлечь**. Должна получиться папка `Mods\<Мод>\` с `Info.json` внутри, без лишнего уровня `Mods\<Мод>-<версия>-kingmaker\<Мод>\`. Если получился лишний уровень — перенесите внутреннюю папку на уровень выше.

**PowerShell**, те же два шага, что на деке. `$KM` и `$WOTR` — из [справки для Windows](windows.ru.md#папки-игр).

1. Скачать свежую версию:

```powershell
$MOD = "BeltBar"; $GAME = "-kingmaker"; $REPO = "sergusaev/pathfinder-mods"
$A = (Invoke-RestMethod "https://api.github.com/repos/$REPO/releases?per_page=100").assets | Where-Object name -Match "^$MOD-\d.*$GAME\.zip$" | Select-Object -First 1
$A.name
$F = "$env:USERPROFILE\Downloads\$($A.name)"
curl.exe -L -o $F $A.browser_download_url
```

Или, если zip уже скачан браузером: `$MOD = "BeltBar"; $GAME = "-kingmaker"` и затем `$F = (Get-ChildItem "$env:USERPROFILE\Downloads\$MOD-*$GAME*.zip" | Sort-Object LastWriteTime | Select-Object -Last 1).FullName`.

2. Распаковать в `Mods`:

```powershell
$G = $KM     # WotR: $G = $WOTR
$D = (tar -tf $F | Select-Object -First 1).Split('/')[0]
tar -xf $F -C "$G\Mods"
Remove-Item "$G\Mods\$D\*.cache" -ErrorAction SilentlyContinue
Get-ChildItem "$G\Mods\$D"
```

## Проверка

Запустите игру и откройте окно UMM (пункт меню или окно при старте), вкладка **Mods**: мод в списке, статус зелёный. Галочки включения модов работают только в главном меню игры.

## Обновление

Обновление ставится так же, как установка, поверх старого, при закрытой игре: команды выше скачивают свежую версию и удаляют кэш UMM. Кэш — файлы вида `BeltBar.dll.2354.cache`: UMM может загрузить их вместо новой DLL. Если ставили мышью, удалите их в файловом менеджере.

Настройки и данные мода в его папке (`Settings.xml`, у Time Control — папка `Tracking` с отслеживаемыми таймерами) при обновлении сохраняются: в zip их нет. Поэтому при обновлении не удаляйте папку мода целиком.

## Удаление

При закрытой игре удалите папку мода из `Mods`, вместе с ней удалятся его настройки. Наши моды ничего не пишут в сохранения, поэтому старые сохранения после удаления загружаются нормально; что именно остаётся (например, уже собранные вручную спутники Level 1 Companions) — в README мода.

## Если мод не работает

| Признак | Причина и решение |
|---|---|
| Мода нет в списке UMM | папка не на своём месте: нужно `Mods/<Мод>/Info.json`. Лишний уровень папок (`Mods/<Мод>/<Мод>/Info.json`) или папка рядом с `Mods`, а не внутри, — UMM мод не увидит |
| Мода нет в списке, на Steam Deck | в папке игры две папки модов, `Mods` и `mods`: Proton видит только одну. Перенесите всё в `Mods`, вторую удалите |
| Красный статус | zip не для этой игры (`-kingmaker` / `-wotr`) или UMM слишком старый. Причина — в логе игры ([пути](steam-deck.ru.md#9-где-что-лежит), [Windows](windows.ru.md#6-где-что-лежит)), строки с id мода |
| WotR: красные все моды этой коллекции сразу, в логе игры `Patching exception` и `__args` | старые версии модов с загрузчиком из UMM 0.33.2 (Doorstop 4): с ним WotR берёт свой Harmony 2.0.4, а его старые версии модов не понимают. Обновить все моды до свежих релизов (нужны версии не ниже: Belt Bar 1.0.2, Belt Consumables Refill 1.0.2, Level 1 Companions 1.0.4, Success Chance 1.1.1, UMM in Game Menus 1.0.3, WotR Fixes 1.1.1, меню баффов `v1.21.2-pad.9` и новее) |
| После обновления ведёт себя как старая версия | не удалён `*.cache` — см. «Обновление» |
| Мод в списке, но ничего не делает в Kingmaker с геймпадом | в `Config.xml` UMM есть `UIStartingPoint` ([Steam Deck](steam-deck.ru.md#4-unity-mod-manager-для-kingmaker), [Windows](windows.ru.md#3-убрать-uistartingpoint-у-kingmaker)) |
