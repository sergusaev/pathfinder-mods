# Моды Pathfinder для геймпада

[English version](README.md)

Небольшие моды для [Unity Mod Manager](https://www.nexusmods.com/site/mods/21), которые делают **Pathfinder: Kingmaker** и **Pathfinder: Wrath of the Righteous** удобнее на геймпаде (консольный интерфейс). Написаны и проверены на Steam Deck.

| Мод | Игра | Что делает |
|---|---|---|
| [Gamepad Camera Rotation](kingmaker-gamepad-camera-rotation/README.ru.md) | Kingmaker | Поворот и приближение камеры правым стиком, компас WotR вместо песочных часов, раскладка WotR для режима камеры, пошагового режима и осмотра |
| [Custom Portraits Gamepad Selection Fix](kingmaker-custom-portraits-gamepad/README.ru.md) | Kingmaker | Свои портреты из папки `Portraits` появляются при создании персонажа в режиме геймпада |
| [Portrait Scroll Fix (gamepad)](wotr-portrait-scroll-fix/README.ru.md) | Wrath of the Righteous | Список своих портретов прокручивается за курсором геймпада и правым стиком |

Меню геймпада для мода автобаффов Buff It 2 The Limit — в отдельном форке: [sergusaev/wrath-epic-buffing](https://github.com/sergusaev/wrath-epic-buffing).

## Установка

**Steam Deck:** по шагам в [docs/steam-deck.ru.md](docs/steam-deck.ru.md) — Unity Mod Manager для обеих игр, раскладка Steam Input (окно UMM на R4, меню баффов на L5, мышь на правом трекпаде) и моды.

**Windows без установщика UMM** (например, по SSH): [docs/windows.ru.md](docs/windows.ru.md) — UMM для обеих игр командами PowerShell и моды.

На других системах:

1. Установить [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) для игры.
2. Скачать zip мода со страницы [Releases](https://github.com/sergusaev/pathfinder-mods/releases).
3. Распаковать в `<игра>/Mods/`, чтобы получилось `Mods/<ModId>/Info.json`. Именно `Mods` указан в `Config.xml` UMM для обеих игр. На Linux регистр важен для нативного Kingmaker; WotR идёт через Proton, где регистр не важен, поэтому папка `mods` из справки для Steam Deck тоже работает.
4. Запустить игру: мод появится в окне UMM с зелёным статусом.

## Сборка

### Сторонние программы

| Программа | Зачем | macOS | Linux | Windows |
|---|---|---|---|---|
| Mono с `mcs` (компилятор C#, 6.12) | компиляция | `brew install mono` | `sudo apt install mono-devel` (Debian/Ubuntu; в других дистрибутивах `mono-complete`) | `winget install Mono.Mono` |
| Bash | `build.sh`, `release.sh` | встроен | встроен | Git for Windows: `winget install Git.Git`, скрипты запускать из **Git Bash** |
| `ssh` | копирование сборок игры с другой машины и установка на неё (`DECK`) | встроен | `openssh-client` | входит в Git for Windows |
| `zip` или bsdtar | упаковка zip для релиза | встроен (`tar`) | `sudo apt install zip` | `C:\Windows\System32\tar.exe`, используется сам |
| GitHub CLI `gh` | только `release.sh --publish` | `brew install gh` | [cli.github.com](https://cli.github.com/) | `winget install GitHub.cli` |

После установки `gh` один раз войти: `gh auth login`. Mono для Windows не добавляет себя в `PATH`; `build-mod.sh` сам находит его в `C:\Program Files\Mono\bin` и переводит пути для `mcs.exe`. Сборки на macOS и Windows совпадают байт в байт.

**Игра.** Моды компилируются против сборок самой игры: `build.sh` при первом запуске (или с `--fetch`) копирует `<Игра>_Data/Managed/*.dll`, а из UMM — `UnityModManager.dll` и `0Harmony.dll` в `<мод>/refs`, с этой машины или по SSH. Unity Mod Manager в игре должен быть уже установлен. В `refs/` лежат файлы игры, поэтому папка исключена из git, публиковать её нельзя.

**Steam Deck как машина с игрой.** Один раз включить SSH в режиме рабочего стола (Konsole): `passwd` — задать пароль пользователю `deck`, затем `sudo systemctl enable --now sshd`; с машины сборки — `ssh-copy-id deck@steamdeck.local`, после этого `DECK=deck@steamdeck.local` в `local.env`.

### Команды

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

На Windows игра обычно лежит в `C:/Program Files (x86)/Steam/steamapps/common/`: задать `KINGMAKER_DIR` / `WOTR_DIR` и запускать скрипты из Git Bash. Два шага один раз после клонирования на Windows или после копирования рабочей копии с macOS или Linux:

- `git config core.filemode false` в каждом клоне. В Windows нет признака исполняемого файла, и без этой настройки git показывает все `.sh` изменёнными. Настройка хранится в `.git/config`: при копировании `.git` с другой машины возвращается старое значение, её надо выставить снова.
- `local.env`, скопированный с другой машины, хранит её `DECK` и пути к играм; проверьте его.

## Выпуск релиза

```bash
./release.sh kingmaker-gamepad-camera-rotation              # собрать и упаковать в dist/GamepadCameraRotation-<версия>.zip
./release.sh kingmaker-gamepad-camera-rotation --publish    # то же, затем тег и релиз на GitHub
./release.sh wotr-portrait-scroll-fix --publish --notes notes.md
```

Версия берётся из `Info.json` мода; перед публикацией её нужно поднять — скрипт откажется публиковать уже выпущенную версию, незакоммиченные изменения или невыложенный `main`. Тег — имя папки без префикса игры, например `gamepad-camera-rotation-v1.1.0`. Для публикации нужен [GitHub CLI](https://cli.github.com/) (`gh auth login`). Папка `dist/` в git не попадает.

## Лицензия

[MIT](LICENSE). Pathfinder: Kingmaker и Pathfinder: Wrath of the Righteous — игры Owlcat Games; файлов игр в репозитории нет.
