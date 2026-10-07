# Custom Portraits Gamepad Selection Fix (Pathfinder: Kingmaker)

Мод для Unity Mod Manager. В режиме геймпада (консольный интерфейс) создание персонажа в Kingmaker показывает только встроенные портреты, папку `Portraits` с пользовательскими портретами игра там не читает. Мод добавляет в конец консольного списка все портреты из этой папки.

## Как работает

- Консольный шаг «Портрет» (`Kingmaker.UI._ConsoleUI.CharGen.Phases.CharGenPortraitPhaseVM`) строит список только из `BlueprintRoot.CharGen.Portraits`. Интерфейс для мыши (`CharBPortraitSelector`) грузит пользовательские портреты через `CustomPortraitsManager`.
- Harmony-постфикс на конструктор `CharGenPortraitPhaseVM(LevelUpController)` берёт `CustomPortraitsManager.Instance.GetExistingCustomPortraitIds()`, сортирует по имени. Для каждого id создаёт `BlueprintPortrait` (`ScriptableObject.CreateInstance`) с `Data = new PortraitData(id)` и добавляет `CharGenPortraitSelectorItemVM` в `SelectorGroupVM.VisibleCollection`. Блупринты кэшируются по id.
- Выбор и сохранение идут штатным путём. `UnitUISettings.SetPortrait` для блупринта, у которого `Data.IsCustom`, сохраняет в персонаже `m_CustomPortrait` (сам `PortraitData`), а не ссылку на блупринт. Сохранения от мода не зависят: если его удалить, портреты уже созданных персонажей останутся.
- Картинки грузятся лениво (`PortraitData.SmallPortrait` → `EnsureImage`). Только при первом открытии шага бывает пауза, пока подгружаются миниатюры.
- В лог UMM пишется `Added custom portraits: N`, ошибки — туда же.

Проверено на Kingmaker 2.1.7b (нативная Linux-сборка, Steam Deck), UMM 0.32.4, Harmony 2.3.6.

## Где лежат портреты

Каждый портрет — отдельная подпапка в `Portraits` с файлами `Small.png` (185×242), `Medium.png` (330×432), `Fulllength.png` (692×1024). Имена подпапок любые, вложенные подпапки игра не видит.

- Linux (нативная сборка): `~/.config/unity3d/Owlcat Games/Pathfinder Kingmaker/Portraits/`
- Windows: `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Kingmaker\Portraits\`

## Сборка

Нужен Mono с компилятором `mcs` (на Mac: `brew install mono`). Сборка идёт против DLL самой игры, поэтому их сначала копируют с деки в `./refs`. Эту папку не публиковать: в ней сборки игры.

```bash
cd ~/Dev/pathfinder-mods/kingmaker-custom-portraits-gamepad
./build.sh --fetch            # скопировать сборки игры и UMM с деки, затем собрать
./build.sh                    # пересобрать, refs уже есть
./build.sh --install          # собрать и установить на деку (игра должна быть закрыта)
```

Адрес деки по умолчанию `deck@steamdeck.local`, переопределяется так: `DECK=deck@<ip> ./build.sh --install`.

Ручная сборка без скрипта:

```bash
mcs -target:library -nostdlib -out:ConsoleCustomPortraits.dll \
  -r:refs/<все DLL из Kingmaker_Data/Managed, кроме 0Harmony*> \
  -r:refs/umm/0Harmony.dll -r:refs/umm/UnityModManager.dll \
  src/Main.cs
```

Подводные камни `mcs`: в zsh список `-r:` нельзя передавать через переменную, она не разбивается на слова. Поэтому скрипт пишет ссылки в response-файл (`@refs.rsp`). Ссылаться надо на все сборки игры, иначе `mcs` не видит базовые классы Unity.

## Установка вручную

1. Должен быть установлен Unity Mod Manager (на деке метод DoorstopProxy, параметры запуска `./run.sh %command%`).
2. Создать папку `<игра>/Mods/ConsoleCustomPortraits/` (на Linux регистр `Mods` и `Info.json` важен).
3. Положить туда `Info.json` и `ConsoleCustomPortraits.dll`.
4. Запустить игру. В окне UMM (Shift+F10) мод «Custom Portraits Gamepad Selection Fix» должен быть зелёным.

## Файлы

- `src/Main.cs` — код мода.
- `Info.json` — манифест UMM (`Id` = `ConsoleCustomPortraits`, точка входа `ConsoleCustomPortraits.Main.Load`).
- `build.sh` — сборка и установка на деку.
