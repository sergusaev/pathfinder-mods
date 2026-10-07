# Custom Portraits Gamepad Selection Fix (Pathfinder: Kingmaker)

[English version](README.md)

Мод для Unity Mod Manager. В режиме геймпада (консольный интерфейс) создание персонажа в Kingmaker предлагает только встроенные портреты: папку `Portraits` со своими портретами игра там не читает. Мод добавляет в конец консольного списка все портреты из этой папки.

## Куда класть портреты

Каждый портрет — отдельная подпапка в `Portraits` с файлами `Small.png` (185×242), `Medium.png` (330×432) и `Fulllength.png` (692×1024). Имена подпапок любые, вложенные подпапки игра не видит.

- Linux (нативная сборка, Steam Deck): `~/.config/unity3d/Owlcat Games/Pathfinder Kingmaker/Portraits/`
- Windows: `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Kingmaker\Portraits\`

## Поведение

- Свои портреты идут после встроенных, по имени папки.
- Выбор и сохранение работают штатно. Сохранения от мода не зависят: если его удалить, у персонажей останутся их портреты.
- Картинки грузятся лениво, поэтому при первом открытии шага бывает пауза, пока подгружаются миниатюры.
- В лог UMM пишется `Added custom portraits: N`, ошибки — туда же.

Проверено на Kingmaker 2.1.7b (нативная Linux-сборка, Steam Deck), UMM 0.32.4, Harmony 2.3.6.

## Как устроено

- Консольный шаг «Портрет» (`Kingmaker.UI._ConsoleUI.CharGen.Phases.CharGenPortraitPhaseVM`) строит список только из `BlueprintRoot.CharGen.Portraits`. Интерфейс для мыши (`CharBPortraitSelector`) грузит свои портреты через `CustomPortraitsManager`.
- Harmony-постфикс на конструктор `CharGenPortraitPhaseVM(LevelUpController)` берёт `CustomPortraitsManager.Instance.GetExistingCustomPortraitIds()`, сортирует по имени, для каждого id создаёт `BlueprintPortrait` (`ScriptableObject.CreateInstance`) с `Data = new PortraitData(id)` и добавляет `CharGenPortraitSelectorItemVM` в `SelectorGroupVM.VisibleCollection`. Блупринты кэшируются по id.
- `UnitUISettings.SetPortrait` для блупринта с `Data.IsCustom` сохраняет в персонаже `m_CustomPortrait` (сам `PortraitData`), а не ссылку на блупринт, поэтому сохранения не зависят от мода.

## Сборка

См. [README репозитория](../README.ru.md#сборка): `./build.sh`, `./build.sh --install`.
