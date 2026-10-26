# Моды Pathfinder

[English version](README.md)

Небольшие моды для [Unity Mod Manager](https://www.nexusmods.com/site/mods/21) для **Pathfinder: Kingmaker** и **Pathfinder: Wrath of the Righteous**, написаны и проверены на Steam Deck и Windows. Работают и с геймпадом, и с клавиатурой и мышью и открываются из меню самой игры.

| Мод | Игра | Что делает |
|---|---|---|
| [Action Bar Rows](action-bar-rows/README.ru.md) | Kingmaker и Wrath of the Righteous | Геймпад: до трёх рядов ячеек на панели действий, над панелью следующие страницы; L3 переключает ряды, число хранится у каждого персонажа |
| [Belt Bar](belt-bar/README.ru.md) | Kingmaker и Wrath of the Righteous | Геймпад: кнопка пояса на панели действий справа от кнопки оружия; открывает над панелью ряд ячеек пояса, чтобы зелье, свиток или жезл применялись с панели без контекстного меню |
| [Belt Consumables Refill](belt-consumables-refill/README.ru.md) | Kingmaker и Wrath of the Righteous | Израсходованный расходник на поясе заменяется таким же из инвентаря партии, ячейка показывает остаток; убирает способности «Заряды: 0», оставленные автопополнением Bag of Tricks / ToyBox |
| [Camera Rotation and Compass](kingmaker-gamepad-camera-rotation/README.ru.md) | Kingmaker | Геймпад: поворот и приближение камеры правым стиком, компас WotR вместо песочных часов, раскладка WotR для режима камеры, пошагового режима и осмотра. Клавиатура и мышь: поворот средней кнопкой мыши, компас в часах |
| [Kingmaker Fixes](kingmaker-fixes/README.ru.md) | Kingmaker | Исправления ошибок самой игры: без ложной строки «невосприимчивость благодаря установкам сложности» в журнале боя, без пачки ошибок при входе на глобальную карту, персонажи не замолкают после загрузки локации, «переброс при неудаче» на атаках больше не перебрасывает попадания, в режиме геймпада над юнитом больше не пропадает полоска здоровья, свои портреты из папки `Portraits` при создании персонажа в режиме геймпада, заклинание касанием или его зелье больше не показывает название над головой дважды. Заменяет Custom Portraits Gamepad Selection Fix |
| [Level 1 Companions & Free Respec](level1-companions/README.ru.md) | Kingmaker и Wrath of the Righteous | Сюжетные спутники вступают на 1-м уровне, дальше прокачка полностью ручная; бесплатная кнопка респека до 1-го уровня, имя, голос и внешность спутника сохраняются, а окно респека WotR можно закрыть с нераспределёнными уровнями. В WotR заменяет lvl1companions |
| [Scrolls («Свитки»)](learn-scrolls/README.ru.md) | Kingmaker и Wrath of the Righteous | Учит волшебников, магусов и других заклинателей группы, переписывающих заклинания, сразу всем свиткам из инвентаря — по правилам самой игры, после её окна подтверждения со списком; школа специализации и запретная помечены, заклинания запретных школ можно не изучать |
| [Success Chance](success-chance/README.ru.md) | Kingmaker и Wrath of the Righteous | В пошаговом бою над врагом под курсором: шанс попасть, крита, преодолеть сопротивление магии и провала спасброска цели, с уроном — по собственным правилам игры, до того как вы действуете |
| [Time Control and Deadlines](kingmaker-time-control/README.ru.md) | Kingmaker | Дробные коэффициенты длительности дел королевства, времени в пути и на привале; список всех таймеров, в том числе скрытых («До события X осталось D дней H часов»); отслеживание со своим сроком и оповещениями в стиле игры; перемотка времени, которая не перескакивает срок провала |
| [Unity Mod Manager in Game Menus](umm-menu-button/README.ru.md) | Kingmaker и Wrath of the Righteous | Пункт «Unity Mod Manager» в главном меню и Esc-меню, в стиле игры: менеджер модов открывается из меню игры на любой платформе |
| [WotR Fixes](wotr-fixes/README.ru.md) | Wrath of the Righteous | Исправления ошибок самой игры: заклинание касанием или его зелье больше не показывает название над головой дважды, боевой текст стоит над головой, а не справа от неё, список своих портретов прокручивается за курсором геймпада и правым стиком; необязательные настройки для ровных кадров на Steam Deck (кадр раз в N обновлений экрана, инкрементальная сборка мусора). Заменяет Portrait Scroll Fix (gamepad) |

Buff It 2 The Limit (Groups) — меню групп баффов для мода автобаффов Buff It 2 The Limit, для WotR и Kingmaker. Живёт в отдельном форке: [sergusaev/wrath-epic-buffing](https://github.com/sergusaev/wrath-epic-buffing).

## Установка

Для установки ничего собирать не нужно: готовые моды лежат на странице [Releases](https://github.com/sergusaev/pathfinder-mods/releases). Порядок такой:

1. Поставить Unity Mod Manager в игру по справке для своей платформы:
   - [Steam Deck](docs/steam-deck.ru.md) — обе игры под Proton;
   - [Windows](docs/windows.ru.md).
2. Поставить моды: [установка модов вручную](docs/mods.ru.md) — какой zip скачать, куда распаковать, как обновить и удалить, что заменяет что.

Моды ставятся в любом порядке и в любом наборе, ни одному не нужен другой. Как UMM попадает в игру, что значат параметры запуска и что ломало UMM на Steam Deck — в [отдельной справке](docs/umm.ru.md).

## Для продвинутых

Дальше — сборка из исходников и установка модов скриптами. Для обычной установки это не нужно.

### Сборка из исходников

#### 1. Программы

| Программа | Зачем |
|---|---|
| Mono с `mcs` (компилятор C#, 6.12) | компиляция |
| Bash | скрипт `build.sh` |
| Git | скачать репозиторий |
| `ssh` | копирование файлов игры с другой машины и установка на неё (Steam Deck) |

Установка одним блоком:

**Windows** — в PowerShell:

```powershell
winget install -e --id Git.Git
winget install -e --id Mono.Mono
```

Все дальнейшие команды на Windows выполняются в **Git Bash** (Пуск → Git Bash), не в PowerShell. Mono не добавляет себя в `PATH` — `build-mod.sh` находит его в `C:\Program Files\Mono\bin` сам; `ssh` входит в Git for Windows.

**Linux (Debian, Ubuntu)**:

```bash
sudo apt install mono-devel git openssh-client
```

В других дистрибутивах пакет Mono называется `mono-complete` или `mono`.

**macOS** (с [Homebrew](https://brew.sh)):

```bash
brew install mono
```

**Steam Deck** для сборки не подходит: системный раздел SteamOS только для чтения, Mono на него не ставится. Собирайте на другом компьютере и ставьте на деку по SSH ([ниже](#автоматическая-установка)).

#### 2. Репозиторий

```bash
git clone https://github.com/sergusaev/pathfinder-mods.git
cd pathfinder-mods
git config core.filemode false
```

`core.filemode false` нужен на Windows: там нет признака исполняемого файла, и без этой настройки git показывает все `.sh` изменёнными. На Linux и macOS она не мешает.

#### 3. Где сборка берёт файлы игры

Моды компилируются против сборок самой игры. При первой сборке мода `build.sh` копирует из игры `<Игра>_Data/Managed/*.dll`, а из UMM — `UnityModManager.dll` и `0Harmony.dll` в папку `refs/` мода. Отсюда два условия:

- игра с установленным UMM должна быть на этой машине или на машине, доступной по SSH;
- для мода двух игр нужны обе игры (или ключ `--game`, см. ниже).

Скрипт находит игру по переменным:

| Переменная | Смысл | По умолчанию |
|---|---|---|
| `DECK` | адрес SSH машины с игрой, например `deck@steamdeck.local` | пусто: игра на этой машине |
| `KINGMAKER_DIR` | папка Kingmaker на машине с игрой | `~/.local/share/Steam/steamapps/common/Pathfinder Kingmaker` |
| `WOTR_DIR` | папка WotR на машине с игрой | `~/.local/share/Steam/steamapps/common/Pathfinder Second Adventure` |

Значения по умолчанию — раскладка Steam на Linux и Steam Deck. На Windows задайте свои пути в файле `local.env` рядом с `build-mod.sh` (git его не отслеживает). Пример для игр в `F:\SteamLibrary`, в Git Bash:

```bash
cat > local.env <<'EOF'
KINGMAKER_DIR="/f/SteamLibrary/steamapps/common/Pathfinder Kingmaker"
WOTR_DIR="/f/SteamLibrary/steamapps/common/Pathfinder Second Adventure"
EOF
```

В Git Bash диск `F:` пишется как `/f`, а обратная косая черта — прямой. Кавычки обязательны: в путях пробелы.

Если файлы игры в `refs/` устарели (игра обновилась), скопируйте их заново ключом `--fetch`. `refs/` содержит файлы игры, поэтому исключена из git; публиковать её нельзя.

#### 4. Команды

```bash
./kingmaker-fixes/build.sh             # собрать в kingmaker-fixes/build/
./kingmaker-fixes/build.sh --fetch     # сначала заново скопировать файлы игры, потом собрать
```

Мод двух игр (в его `build.sh` задано `GAMES="kingmaker wotr"`) собирается из одних исходников под каждую игру:

```bash
./belt-bar/build.sh                    # обе игры по очереди: build/kingmaker/, build/wotr/
./belt-bar/build.sh --game wotr        # только одна игра
```

Без `--game` ключи `--fetch` и `--install` действуют на все игры мода. Компилятор получает `-define:KINGMAKER` или `-define:WOTR`; `src/*.cs` общие, `src/kingmaker/` и `src/wotr/` компилируются только для своей игры, `shared/*.cs` входят в каждый мод. Сборки на macOS и Windows совпадают байт в байт.

### Автоматическая установка

`--install` собирает мод и сразу кладёт `Info.json` и DLL в папку `Mods` игры, заодно удаляя кэш UMM (`*.cache`). Игра должна быть закрыта: если она запущена, скрипт откажется ставить. UMM в игре уже должен стоять ([Steam Deck](docs/steam-deck.ru.md), [Windows](docs/windows.ru.md)).

**На этот же компьютер** (пути — из `local.env` или по умолчанию):

```bash
./belt-bar/build.sh --install
./kingmaker-fixes/build.sh --install
```

**На Steam Deck по SSH.** Один раз включить SSH на деке. В режиме рабочего стола в Konsole:

```bash
passwd                              # задать пароль пользователю deck (если ещё не задан)
sudo systemctl enable --now sshd    # включить SSH-сервер, в том числе после перезагрузки
```

Затем один раз на машине сборки (на Windows — в Git Bash) создать ключ и передать его деке, чтобы не вводить пароль каждый раз:

```bash
[ -f ~/.ssh/id_ed25519 ] || ssh-keygen -t ed25519 -N "" -f ~/.ssh/id_ed25519
ssh-copy-id deck@steamdeck.local     # спросит пароль деки один раз
ssh deck@steamdeck.local true && echo OK
```

Если имя `steamdeck.local` не находится, используйте IP-адрес деки (Настройки → Интернет → ваша сеть) — например, `deck@192.168.1.72`.

После этого установка на деку:

```bash
DECK=deck@steamdeck.local ./belt-bar/build.sh --install
```

`DECK=` в начале команды действует только на эту команду и важнее `local.env`: пути игр из `local.env` (они для этого компьютера) при этом не используются, берутся папки деки по умолчанию. Если игры на деке на карте microSD, добавьте их пути в ту же команду: `DECK=deck@steamdeck.local KINGMAKER_DIR="/run/media/deck/<карта>/steamapps/common/Pathfinder Kingmaker" ./kingmaker-fixes/build.sh --install`. Если ставите только на деку, можно записать `DECK=deck@steamdeck.local` в `local.env` и не повторять.

### Грабли

| Признак | Причина и решение |
|---|---|
| `$'\r': command not found` при запуске `.sh` | скрипт с концами строк Windows (CRLF). `.gitattributes` держит `*.sh` в LF; клону, сделанному до его появления, нужно один раз обновить скрипты в Git Bash: `git ls-files -z '*.sh' \| xargs -0 rm && git checkout -- '*.sh'` |
| git показывает все `.sh` изменёнными | не выполнен `git config core.filemode false` (раздел 2). Настройка хранится в `.git/config`: при копировании `.git` с другой машины она возвращается к старой |
| `mcs: command not found` | Mono не установлен, или установлен не в `C:\Program Files\Mono` |
| сборка ищет игру в `~/.local/share/Steam/…` на Windows | нет `local.env` с путями (раздел 3); на Windows умолчания не подходят |
| `Kingmaker.exe is running, close the game before installing` | игра запущена — закрыть и повторить |
| `local.env` скопирован с другой машины | в нём её `DECK` и пути — проверьте его |
| мод поставился, а в игре старая версия | установка без скрипта: удалить `*.cache` ([подробнее](docs/mods.ru.md#обновление)) |


## Лицензия

[MIT](LICENSE). Pathfinder: Kingmaker и Pathfinder: Wrath of the Righteous — игры Owlcat Games; файлов игр в репозитории нет.
