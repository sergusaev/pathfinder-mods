# Моды Pathfinder для геймпада

[English version](README.md)

Небольшие моды для [Unity Mod Manager](https://www.nexusmods.com/site/mods/21), которые делают **Pathfinder: Kingmaker** и **Pathfinder: Wrath of the Righteous** удобнее на геймпаде (консольный интерфейс). Написаны и проверены на Steam Deck.

| Мод | Игра | Что делает |
|---|---|---|
| [Gamepad Camera Rotation](kingmaker-gamepad-camera-rotation/README.ru.md) | Kingmaker | Поворот и приближение камеры правым стиком, компас WotR вместо песочных часов, раскладка WotR для режима камеры, пошагового режима и осмотра |
| [Custom Portraits Gamepad Selection Fix](kingmaker-custom-portraits-gamepad/README.ru.md) | Kingmaker | Свои портреты из папки `Portraits` появляются при создании персонажа в режиме геймпада |
| [Portrait Scroll Fix (gamepad)](wotr-portrait-scroll-fix/README.ru.md) | Wrath of the Righteous | Список своих портретов прокручивается за курсором геймпада и правым стиком |

Меню геймпада для мода автобаффов Buff It 2 The Limit — в отдельном форке: [sergusaev/wrath-epic-buffing](https://github.com/sergusaev/wrath-epic-buffing).

## Установка мода

1. Установить Unity Mod Manager для игры. На Steam Deck:
   - Kingmaker работает нативно под Linux: метод DoorstopProxy, параметры запуска `./run.sh %command%`.
   - Wrath of the Righteous работает через Proton: `winhttp.dll` и `doorstop_config.ini` в папку игры, параметры запуска `WINEDLLOVERRIDES="winhttp=n,b" %command%`.
2. Собрать мод (ниже) или взять `Info.json` и DLL из релиза.
3. Положить оба файла в `<игра>/Mods/<ModId>/` для Kingmaker или в `<игра>/mods/<ModId>/` для WotR. На Linux регистр имени папки и `Info.json` важен.
4. Запустить игру: мод появится в окне UMM (по умолчанию Ctrl+F10) с зелёным статусом.

## Сборка

Нужно:

- Mono с компилятором `mcs` (macOS: `brew install mono`, Linux: `mono-devel`, Windows: Mono и Git Bash).
- Сама игра: моды компилируются против её сборок. `build.sh` при первом запуске копирует их в `<мод>/refs`. Это файлы игры, поэтому `refs/` исключена из git, публиковать её нельзя.

```bash
./kingmaker-gamepad-camera-rotation/build.sh            # скопировать refs, если их нет, и собрать в build/
./kingmaker-gamepad-camera-rotation/build.sh --fetch    # заново скопировать сборки игры (после обновления игры)
./kingmaker-gamepad-camera-rotation/build.sh --install  # собрать и установить в папку модов игры; игра должна быть закрыта
```

Общая логика всех модов — в [`build-mod.sh`](build-mod.sh). Игру он находит по переменным:

| Переменная | Смысл | По умолчанию |
|---|---|---|
| `DECK` | адрес SSH, если игра на другой машине, например `deck@steamdeck.local` | пусто: игра на этой машине |
| `KINGMAKER_DIR` | папка Kingmaker на машине с игрой | `~/.local/share/Steam/steamapps/common/Pathfinder Kingmaker` |
| `WOTR_DIR` | папка Wrath of the Righteous на машине с игрой | `~/.local/share/Steam/steamapps/common/Pathfinder Second Adventure` |

Личные значения кладутся в `local.env` рядом с `build-mod.sh` (git его не отслеживает), например:

```bash
DECK=deck@steamdeck.local
```

На Windows игра обычно лежит в `C:/Program Files (x86)/Steam/steamapps/common/`: задать `KINGMAKER_DIR` / `WOTR_DIR` и запускать скрипты из Git Bash.

## Лицензия

[MIT](LICENSE). Pathfinder: Kingmaker и Pathfinder: Wrath of the Righteous — игры Owlcat Games; файлов игр в репозитории нет.
