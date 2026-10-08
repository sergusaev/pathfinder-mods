# Моды Pathfinder

[English version](README.md)

Небольшие моды для [Unity Mod Manager](https://www.nexusmods.com/site/mods/21), которые делают **Pathfinder: Kingmaker** и **Pathfinder: Wrath of the Righteous** удобнее на геймпаде (консольный интерфейс), а где указано — и с клавиатурой и мышью. Написаны и проверены на Steam Deck и Windows.

| Мод | Игра | Что делает |
|---|---|---|
| [Camera Rotation and Compass](kingmaker-gamepad-camera-rotation/README.ru.md) | Kingmaker | Геймпад: поворот и приближение камеры правым стиком, компас WotR вместо песочных часов, раскладка WotR для режима камеры, пошагового режима и осмотра. Клавиатура и мышь: поворот средней кнопкой мыши и Alt+A / Alt+D, компас в часах |
| [Custom Portraits Gamepad Selection Fix](kingmaker-custom-portraits-gamepad/README.ru.md) | Kingmaker | Свои портреты из папки `Portraits` появляются при создании персонажа в режиме геймпада |
| [Level 1 Companions & Free Respec](level1-companions/README.ru.md) | Kingmaker и Wrath of the Righteous | Сюжетные спутники вступают на 1-м уровне, дальше прокачка полностью ручная; бесплатная кнопка респека до 1-го уровня, имя, голос и внешность спутника сохраняются, а окно респека WotR можно закрыть с нераспределёнными уровнями. В WotR заменяет lvl1companions |
| [Portrait Scroll Fix (gamepad)](wotr-portrait-scroll-fix/README.ru.md) | Wrath of the Righteous | Список своих портретов прокручивается за курсором геймпада и правым стиком |

Buff It 2 The Limit (Groups) — меню групп баффов для мода автобаффов Buff It 2 The Limit; работает с геймпадом, клавиатурой и мышью в WotR и Kingmaker. Живёт в отдельном форке: [sergusaev/wrath-epic-buffing](https://github.com/sergusaev/wrath-epic-buffing).

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

**Моды для обеих игр.** Мод, у которого в `build.sh` вместо `GAME=` задано `GAMES="kingmaker wotr"` ([Level 1 Companions & Free Respec](level1-companions/README.ru.md)), собирается из одних исходников под каждую игру:

```bash
./level1-companions/build.sh                              # все игры по очереди: build/kingmaker/, build/wotr/
./level1-companions/build.sh --game wotr                  # только одна игра
./level1-companions/build.sh --game kingmaker --install   # собрать и установить в одну игру
```

Компилятор получает `-define:KINGMAKER` или `-define:WOTR`; `src/*.cs` общие, `src/kingmaker/` и `src/wotr/` компилируются только для своей игры. Сборки игры копируются в `refs/<игра>/`, поэтому для полной сборки на машине с игрой должны быть установлены обе игры с UMM. Без `--game` ключи `--fetch` и `--install` действуют на все игры.

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
- Концы строк. Git for Windows выписывает текстовые файлы с CRLF (по умолчанию `core.autocrlf=true`), а Bash на скрипте с CRLF падает (`$'\r': command not found`). `.gitattributes` держит `*.sh` в LF на любой машине; клону, сделанному до его появления, нужно один раз обновить скрипты в Git Bash: `git ls-files -z '*.sh' | xargs -0 rm && git checkout -- '*.sh'`. `release.sh` отрезает `\r`, когда читает `Info.json` и `build.sh`, так что CRLF в них не мешает.

## Выпуск релиза

```bash
./release.sh kingmaker-gamepad-camera-rotation              # собрать и упаковать в dist/GamepadCameraRotation-<версия>.zip
./release.sh kingmaker-gamepad-camera-rotation --publish    # то же, затем тег и релиз на GitHub
./release.sh wotr-portrait-scroll-fix --publish --notes notes.md
```

Мод для обеих игр упаковывается в отдельный zip для каждой игры, `dist/<ModId>-<версия>-kingmaker.zip` и `dist/<ModId>-<версия>-wotr.zip`, оба прикладываются к одному релизу (тег `level1-companions-v1.0.0`).

Версия берётся из `Info.json` мода; перед публикацией её нужно поднять — скрипт откажется публиковать уже выпущенную версию, незакоммиченные изменения или невыложенный `main`. Тег — имя папки без префикса игры, например `gamepad-camera-rotation-v1.1.0`. Для публикации нужен [GitHub CLI](https://cli.github.com/) (`gh auth login`). Папка `dist/` в git не попадает.

## Лицензия

[MIT](LICENSE). Pathfinder: Kingmaker и Pathfinder: Wrath of the Righteous — игры Owlcat Games; файлов игр в репозитории нет.
