# Portrait Scroll Fix (gamepad) — Pathfinder: Wrath of the Righteous

Мод для Unity Mod Manager. В режиме геймпада при создании персонажа список на вкладке «Пользовательские» (свои портреты) не прокручивается за курсором: курсор уходит за край экрана, и правый стик его не листает. Мод это исправляет.

## Причина

- Свои портреты в консольном интерфейсе (`CharGenCustomPortraitGroupConsoleView`) лежат в виртуальном списке `VirtualListGridVertical`. Навигация геймпада — сетка по 8 в ряд из `m_VirtualList.Elements`, а в фокусе оказывается `VirtualListElement`, обычный объект, не `MonoBehaviour`.
- `CharGenPortraitPhaseDetailedConsoleView.SetSelected` прокручивает только внешний `ScrollRect` и только к `MonoBehaviour`, так что для своих портретов он ничего не делает.
- Штатный `ScrollController.ForceScrollToElement` → `TryPinToElement` работает только с элементами, которые список уже раскладывал (`WasUpdatedAtLeastOnes`). Это видимые ряды плюс один запасной, дальше прокрутка не идёт.
- Правый стик (`CharGenPortraitPhaseDetailedConsoleView.Scroll`) крутит внешний `ScrollRect`, а виртуального списка в нём нет.

## Что делает мод

- **Слежение за фокусом.** Постфикс на `SetSelected` вешает на экран компонент `FocusFollower`. Каждый кадр в `LateUpdate` он находит в цепочке навигации элемент в фокусе и вызывает `ScrollController.ScrollTowards(element.Data, speed)`. Этот путь определяет направление и для неразложенных элементов (по индексу относительно `TopVisibleIndex`) и останавливается, когда элемент попал в видимую зону. Скорость 45 px за кадр для элементов с view и 220 px для дальних. Работает одинаково для крестовины, левого стика, нажатия и удержания с автоповтором, до концов списка в обе стороны.
- **Правый стик.** Префикс на `Scroll` на вкладке своих портретов двигает курсор по рядам через `HandleUp`/`HandleDown` навигационной сетки. Порог наклона 0,5, первый шаг сразу, автоповтор через 0,35 с с интервалом 0,09 с. На других вкладках стик работает штатно.
- Ошибки пишутся в лог UMM, игре не мешают: при исключении `FocusFollower` отключается.

Проверено на WotR (Proton, Steam Deck), UMM 0.32.4, Harmony 2.0.4 из `Wrath_Data/Managed`, режим `ForceControllerMode: gamepad` в `startup.json`.

## Где лежат портреты

Каждый портрет — отдельная подпапка в `Portraits` с `Small.png` (185×242), `Medium.png` (330×432), `Fulllength.png` (692×1024). Вложенные подпапки игра не видит.

- Steam Deck / Proton: `~/.local/share/Steam/steamapps/compatdata/1184370/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Wrath Of The Righteous/Portraits/`
- Windows: `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Wrath Of The Righteous\Portraits\`

## Сборка

Нужен Mono с компилятором `mcs` (на Mac: `brew install mono`). Сборка идёт против DLL игры, их сначала копируют с деки в `./refs`. Эту папку не публиковать: в ней сборки игры.

```bash
cd ~/Dev/pathfinder-mods/wotr-portrait-scroll-fix
./build.sh --fetch            # скопировать сборки игры и UMM с деки, затем собрать
./build.sh                    # пересобрать, refs уже есть
./build.sh --install          # собрать и установить на деку (игра должна быть закрыта)
```

Адрес деки по умолчанию `deck@steamdeck.local`, переопределяется так: `DECK=deck@<ip> ./build.sh --install`.

Ручная сборка без скрипта:

```bash
mcs -target:library -nostdlib -out:PortraitScrollFix.dll \
  -r:refs/<все DLL из Wrath_Data/Managed> \
  -r:refs/umm/UnityModManager.dll \
  src/Main.cs
```

Подводные камни:
- Ссылаться на `0Harmony.dll` из `Wrath_Data/Managed` (2.0.4), а не на более новый из UMM: собранный мод требует не выше той версии, что точно загружена.
- В zsh список `-r:` нельзя передавать через переменную, поэтому ссылки идут через response-файл (`@refs.rsp`).
- Ссылаться надо на все сборки игры, иначе `mcs` не видит базовые классы (`isActiveAndEnabled` и т.п.).
- `AccessTools.FieldRefAccess` с `mcs` не компилируется, поля читаются через `FieldInfo.GetValue`.
- `GetUpValidEntity`/`GetDownValidEntity` защищённые, для шага используются публичные `HandleUp`/`HandleDown`.
- `IScrollController.ForceScrollToElement`/`ScrollTowards` принимают `IVirtualListElementData` (`element.Data`), а не сам элемент.

## Установка вручную

1. Должен быть установлен Unity Mod Manager. На деке: `winhttp.dll` + `doorstop_config.ini` в папке игры, параметры запуска `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
2. Создать `<игра>/mods/PortraitScrollFix/` и положить туда `Info.json` и `PortraitScrollFix.dll`.
3. При обновлении удалить старый `PortraitScrollFix.dll.*.cache` из этой папки.
4. Запустить игру. В окне UMM (Shift+F10) мод «Portrait Scroll Fix (gamepad)» должен быть зелёным.

## Файлы

- `src/Main.cs` — код мода.
- `Info.json` — манифест UMM.
- `build.sh` — сборка и установка на деку.
