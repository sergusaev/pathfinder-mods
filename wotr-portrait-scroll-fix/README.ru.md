# Portrait Scroll Fix (gamepad) — Pathfinder: Wrath of the Righteous

[English version](README.md)

Мод для Unity Mod Manager. В режиме геймпада при создании персонажа список на вкладке «Пользовательские» (свои портреты) не прокручивается за курсором: курсор уходит за край экрана, а правый стик список не листает. Мод исправляет и то, и другое.

## Что делает мод

- **Список следует за фокусом** при крестовине, левом стике, одиночных нажатиях и автоповторе, до концов списка в обе стороны.
- **Правый стик** на вкладке своих портретов двигает курсор по рядам: порог наклона 0,5, первый шаг сразу, автоповтор через 0,35 с с интервалом 0,09 с. На других вкладках стик работает как раньше.
- Ошибки пишутся в лог UMM и игре не мешают: при исключении слежение за фокусом отключается.

Проверено на WotR (Proton, Steam Deck), UMM 0.32.4, Harmony 2.0.4 из `Wrath_Data/Managed`.

## На Steam Deck

Ничего назначать не нужно: мод работает на стандартных кнопках геймпада. Портреты копируются в режиме рабочего стола (Dolphin или Konsole) в папку ниже. Установка Unity Mod Manager — [справка по Steam Deck](../docs/steam-deck.ru.md).

## Куда класть портреты

Каждый портрет — отдельная подпапка в `Portraits` с `Small.png` (185×242), `Medium.png` (330×432) и `Fulllength.png` (692×1024). Вложенные подпапки игра не видит.

- Steam Deck / Proton: `~/.local/share/Steam/steamapps/compatdata/1184370/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Wrath Of The Righteous/Portraits/`
- Windows: `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Wrath Of The Righteous\Portraits\`

## Почему список не прокручивался

- Свои портреты в консольном интерфейсе (`CharGenCustomPortraitGroupConsoleView`) лежат в виртуальном списке `VirtualListGridVertical`. Навигация геймпада — сетка по 8 в ряд из `m_VirtualList.Elements`, а в фокусе оказывается `VirtualListElement` — обычный объект, не `MonoBehaviour`.
- `CharGenPortraitPhaseDetailedConsoleView.SetSelected` прокручивает только внешний `ScrollRect` и только к `MonoBehaviour`, так что для своих портретов он ничего не делает.
- Штатный `ScrollController.ForceScrollToElement` → `TryPinToElement` работает только с элементами, которые список уже раскладывал (`WasUpdatedAtLeastOnes`): видимые ряды плюс один запасной, дальше прокрутка не идёт.
- Правый стик (`CharGenPortraitPhaseDetailedConsoleView.Scroll`) крутит внешний `ScrollRect`, а виртуального списка в нём нет.

## Как устроено

- Постфикс на `SetSelected` вешает на экран компонент `FocusFollower`. Каждый кадр в `LateUpdate` он находит элемент в фокусе в цепочке навигации и вызывает `ScrollController.ScrollTowards(element.Data, speed)`. Этот путь определяет направление и для неразложенных элементов (по индексу относительно `TopVisibleIndex`) и останавливается, когда элемент стал видимым. Скорость — 45 px за кадр для элементов с view и 220 px для дальних.
- Префикс на `Scroll` на вкладке своих портретов шагает по навигационной сетке через `HandleUp`/`HandleDown`.

Заметки для сборки под WotR:

- Ссылаться на `0Harmony.dll` из `Wrath_Data/Managed` (2.0.4), а не на более новый из UMM, чтобы мод не требовал Harmony новее загруженного. `build-mod.sh` так и делает.
- `AccessTools.FieldRefAccess` с `mcs` не компилируется, поля читаются через `FieldInfo.GetValue`.
- `GetUpValidEntity`/`GetDownValidEntity` защищённые, поэтому используются публичные `HandleUp`/`HandleDown`.
- `IScrollController.ForceScrollToElement`/`ScrollTowards` принимают `IVirtualListElementData` (`element.Data`), а не сам элемент.

## Сборка

См. [README репозитория](../README.ru.md#сборка): `./build.sh`, `./build.sh --install`. При ручном обновлении удалить старый `PortraitScrollFix.dll.*.cache` в папке мода.
