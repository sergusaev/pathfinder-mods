# Steam Deck: Unity Mod Manager и моды с нуля

[English version](steam-deck.md)

Справка ставит Unity Mod Manager (UMM) в **Pathfinder: Kingmaker** и **Pathfinder: Wrath of the Righteous** (WotR) на Steam Deck. Обе игры работают как Windows-сборки под Proton, UMM загружается через Doorstop. Проверено на SteamOS: Kingmaker 2.1.7b под Proton Experimental, WotR 2.7 под GE-Proton 10, UMM 0.32.4 и 0.33.2.

Что такое Doorstop, Proton и параметры запуска и почему всё сделано именно так — в [отдельной справке](umm.ru.md). Её стоит прочитать хотя бы до раздела про `UIStartingPoint`.

Идите по разделам по порядку. Каждый блок команд можно скопировать и вставить в Konsole целиком.

## 1. Перед началом

1. **Kingmaker — только Windows-сборка.** По умолчанию Steam ставит на деку Linux-сборку Kingmaker, а она не годится (почему — [здесь](umm.ru.md#почему-не-нативная-linux-сборка-kingmaker)). В игровом режиме: Kingmaker → ⚙ → **Свойства** → **Совместимость** → включить «Принудительно использовать выбранный инструмент совместимости Steam Play» → выбрать **Proton Experimental**. Steam заново скачает игру (несколько ГБ). WotR выходит только для Windows, Proton у неё включается сам; менять ничего не нужно.
2. **Запустить каждую игру один раз** до главного меню и выйти. Так Steam создаёт папку с данными игры (префикс Proton), где потом будут логи и сохранения.
3. **Перейти в режим рабочего стола:** кнопка Steam → **Питание** → **Переключиться на рабочий стол**.
4. **Открыть Konsole** (терминал): меню приложений в левом нижнем углу → «Система» → Konsole. Вставка в Konsole — **Ctrl+Shift+V** или правая кнопка мыши → «Вставить». Если клавиатуры нет — Steam + X открывает экранную.
5. Пока меняете файлы игры, игра должна быть закрыта.

## 2. Найти папки игр

Команды ниже рассчитаны на игры во внутренней памяти. Проверьте, где они на самом деле:

```bash
find ~/.local/share/Steam/steamapps/common /run/media -maxdepth 5 -type d \( -name "Pathfinder Kingmaker" -o -name "Pathfinder Second Adventure" \) 2>/dev/null
```

- Если вывод начинается с `/home/deck/.local/share/Steam/` — всё как в справке, ничего не меняйте.
- Если с `/run/media/…` — игра на карте microSD. Тогда в каждом блоке ниже замените первую строку с `KM=` или `WOTR=` на путь из вывода, например `KM="/run/media/deck/SD1TB/steamapps/common/Pathfinder Kingmaker"`.

Папка WotR называется **`Pathfinder Second Adventure`** — это внутреннее имя игры, другой папки нет.

Проверка, что Kingmaker — Windows-сборка:

```bash
KM="$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"
ls "$KM"
```

В списке должны быть `Kingmaker.exe` и `UnityPlayer.dll`. Если видите `UnityPlayer.so`, Proton для Kingmaker не включён — вернитесь к пункту 1 раздела 1. Если видите `run.sh`, `libdoorstop.so` — это остатки прежней установки на Linux-сборку, удалите их (Dolphin → в корзину).

## 3. Скачать UMM

Автор выкладывает UMM на [Nexus Mods](https://www.nexusmods.com/site/mods/21) установщиком для Windows. На деке он не нужен: те же файлы есть в пакете обновления, который UMM скачивает сам (ссылка — в [Repository_beta_13.json](https://github.com/newman55/unity-mod-manager/blob/master/Repository_beta_13.json) репозитория UMM).

```bash
mkdir -p ~/umm && cd ~/umm
curl -L -o umm_update.zip "https://www.dropbox.com/s/wt18wcq5six02ku/umm_update.zip?dl=1"
unzip -o umm_update.zip -d package
ls package
```

В выводе должны быть `UnityModManager.dll`, `0Harmony.dll`, `dnlib.dll`, `winhttp_x64.dll` и папка `Harmony`.

## 4. Unity Mod Manager для Kingmaker

```bash
KM="$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"
UMM="$KM/Kingmaker_Data/Managed/UnityModManager"
mkdir -p "$UMM" "$KM/Mods"

# Сам UMM и Harmony (старым модам нужен Harmony 1.2, он тоже кладётся).
cp ~/umm/package/UnityModManager.dll ~/umm/package/UnityModManager.xml ~/umm/package/0Harmony.dll ~/umm/package/dnlib.dll ~/umm/package/Harmony/1.2/*.dll "$UMM/"

# Doorstop: загрузчик и его настройка.
cp ~/umm/package/winhttp_x64.dll "$KM/winhttp.dll"
printf '[General]\nenabled = true\ntarget_assembly = Kingmaker_Data\\Managed\\UnityModManager\\UnityModManager.dll\n' > "$KM/doorstop_config.ini"

# Описание игры для UMM. Строки UIStartingPoint здесь НЕТ намеренно.
cat > "$UMM/Config.xml" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<Config xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" Name="Pathfinder: Kingmaker">
  <Folder>Pathfinder Kingmaker</Folder>
  <ModsDirectory>Mods</ModsDirectory>
  <ModInfo>Info.json</ModInfo>
  <GameExe>Kingmaker.exe</GameExe>
  <EntryPoint>[UnityEngine.UIModule.dll]UnityEngine.Canvas.cctor:Before</EntryPoint>
  <StartingPoint>[Assembly-CSharp.dll]Kingmaker.GameStarter.Awake:Before</StartingPoint>
  <OldPatchTarget>[Assembly-CSharp.dll]Kingmaker.GameStarter.Awake:Before</OldPatchTarget>
  <GameVersionPoint>[Assembly-CSharp.dll]Kingmaker.GameVersion.GetVersion</GameVersionPoint>
  <MinimalManagerVersion>0.12.0</MinimalManagerVersion>
</Config>
EOF

# Настройки UMM: окно при старте. Существующий файл не трогаем, в нём включённые моды.
[ -f "$UMM/Params.xml" ] || cat > "$UMM/Params.xml" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<Param xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <CheckUpdates>0</CheckUpdates>
  <ShowOnStart>1</ShowOnStart>
</Param>
EOF

ls "$KM/winhttp.dll" "$KM/doorstop_config.ini" "$UMM/UnityModManager.dll"
grep -c UIStartingPoint "$UMM/Config.xml"
```

Последние две команды проверяют результат: три файла должны найтись, а `grep` — напечатать `0`.

**Важно.** Без строки `UIStartingPoint` окно UMM работает в режиме геймпада. Если когда-нибудь поставите UMM заново другим способом (установщиком, из другой справки), он вернёт эту строку, и в режиме геймпада всё сломается снова: пункт UMM в меню не откроется, моды будут видны, но не будут действовать. Подробно — [здесь](umm.ru.md#configxml-и-строка-uistartingpoint). Убрать строку из уже существующего файла:

```bash
KM="$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker"
sed -i '/UIStartingPoint/d' "$KM/Kingmaker_Data/Managed/UnityModManager/Config.xml"
```

## 5. Unity Mod Manager для Wrath of the Righteous

```bash
WOTR="$HOME/.local/share/Steam/steamapps/common/Pathfinder Second Adventure"
UMM="$WOTR/Wrath_Data/Managed/UnityModManager"
mkdir -p "$UMM" "$WOTR/Mods"

cp ~/umm/package/UnityModManager.dll ~/umm/package/UnityModManager.xml ~/umm/package/0Harmony.dll ~/umm/package/dnlib.dll "$UMM/"

cp ~/umm/package/winhttp_x64.dll "$WOTR/winhttp.dll"
printf '[General]\nenabled = true\ntarget_assembly = Wrath_Data\\Managed\\UnityModManager\\UnityModManager.dll\n' > "$WOTR/doorstop_config.ini"

# У WotR строка UIStartingPoint остаётся: здесь одно главное меню на оба режима.
cat > "$UMM/Config.xml" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<Config xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance" Name="Pathfinder: Wrath of the Righteous">
  <Folder>Pathfinder Second Adventure</Folder>
  <ModsDirectory>Mods</ModsDirectory>
  <ModInfo>Info.json</ModInfo>
  <GameExe>Wrath.exe</GameExe>
  <EntryPoint>[UnityEngine.UIModule.dll]UnityEngine.Canvas.cctor:Before</EntryPoint>
  <StartingPoint>[Assembly-CSharp.dll]Kingmaker.GameStarter.Awake:Before</StartingPoint>
  <UIStartingPoint>[Assembly-CSharp.dll]Kingmaker.MainMenu.Start:After</UIStartingPoint>
  <GameVersionPoint>[Assembly-CSharp.dll]Kingmaker.GameVersion.GetVersion</GameVersionPoint>
  <MinimalManagerVersion>0.22.15</MinimalManagerVersion>
</Config>
EOF

[ -f "$UMM/Params.xml" ] || cat > "$UMM/Params.xml" <<'EOF'
<?xml version="1.0" encoding="utf-8"?>
<Param xmlns:xsd="http://www.w3.org/2001/XMLSchema" xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  <CheckUpdates>0</CheckUpdates>
  <ShowOnStart>1</ShowOnStart>
</Param>
EOF

# Держать WotR в режиме геймпада (см. ниже). Старый файл сохраняется в startup.json.bak.
[ -f "$WOTR/startup.json" ] && cp "$WOTR/startup.json" "$WOTR/startup.json.bak"
echo '{"ForceControllerMode":"gamepad"}' > "$WOTR/startup.json"

ls "$WOTR/winhttp.dll" "$WOTR/doorstop_config.ini" "$UMM/UnityModManager.dll" "$WOTR/startup.json"
```

**Зачем `startup.json`.** Без него WotR на любое нажатие клавиши, в том числе с экранной клавиатуры Steam, спрашивает «Перейти на клавиатуру и мышь?». С ним игра всегда в режиме геймпада, режим мыши и клавиатуры недоступен. Если нужен режим мыши, удалите `startup.json`. Проверка целостности файлов в Steam может вернуть исходный файл — тогда запишите его снова последними строками блока.

## 6. Параметры запуска

Для **обеих** игр в Steam задаются одинаковые параметры запуска:

```
WINEDLLOVERRIDES="winhttp=n,b" %command%
```

Что значит каждая часть — [здесь](umm.ru.md#параметры-запуска). Коротко: без них Proton игнорирует `winhttp.dll` из папки игры, и UMM не запускается.

Удобнее всего ввести их в режиме рабочего стола, вставкой, чтобы не ошибиться в кавычках:

1. Открыть Steam на рабочем столе (ярлык Steam или меню приложений → «Игры» → Steam).
2. Библиотека → правая кнопка на игре → **Свойства** → **Общие** → поле **Параметры запуска**.
3. Скопировать строку выше и вставить (Ctrl+V). Закрыть окно — сохраняется само.
4. Повторить для второй игры.

В игровом режиме то же поле: игра → ⚙ → **Свойства** → **Общие** → **Параметры запуска**. Кавычки нужны прямые `"`.

## 7. Проверка

1. Вернуться в игровой режим (ярлык «Return to Gaming Mode» на рабочем столе) и запустить игру.
2. При старте появится окно UMM, вкладка **Mods** пока пустая. Окно управляется мышью: на деке это **правый трекпад**, нажатие на трекпад — щелчок.
3. Если окна нет, проверьте, загрузился ли UMM вообще (в Konsole, после выхода из игры):

```bash
grep -m3 "\[Manager\]" "$HOME/.local/share/Steam/steamapps/common/Pathfinder Kingmaker/Kingmaker_Data/Managed/UnityModManager/Log.txt"
```

Для WotR — тот же путь с `Pathfinder Second Adventure/Wrath_Data`. Есть строки `[Manager] Version: …` — UMM работает, ищите причину в [неполадках](#10-неполадки). Файла нет или строки старые — UMM не запускался: проверьте параметры запуска (раздел 6) и файлы из блоков 4–5.

## 8. Установка модов

Как скачать, поставить, обновить и удалить моды — в [справке по установке модов](mods.ru.md). Первым стоит поставить [Unity Mod Manager in Game Menus](../umm-menu-button/README.ru.md): с ним окно UMM открывается из меню игры (главного и Esc), а окно при старте он выключает.

## 9. Где что лежит

Папок у каждой игры две, и их легко перепутать:

| Что | Где | Пример |
|---|---|---|
| **Папка игры**: файлы игры, UMM, моды, `winhttp.dll` | `~/.local/share/Steam/steamapps/common/<папка игры>/` | `…/common/Pathfinder Kingmaker/Mods/` |
| **Префикс Proton**: всё, что игра пишет как «пользователь Windows»: логи, сохранения, портреты, настройки | `~/.local/share/Steam/steamapps/compatdata/<AppID>/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/<игра>/` | см. таблицу ниже |

Префикс — это поддельный диск `C:` отдельно для каждой игры. Номер папки в `compatdata` — номер игры в Steam (AppID): **640820** у Kingmaker, **1184370** у WotR. UMM и моды в префиксе **не лежат**, их ищите только в папке игры. Если игра на карте microSD, обе папки лежат на карте: `/run/media/…/steamapps/common/…` и `/run/media/…/steamapps/compatdata/…`.

Пути в логах вида `Z:\home\deck\…` или `S:\steamapps\…` — это те же папки Linux, как их видит Wine: буквы дисков у него указывают на папки деки.

| | Kingmaker | WotR |
|---|---|---|
| Папка игры | `~/.local/share/Steam/steamapps/common/Pathfinder Kingmaker` | `~/.local/share/Steam/steamapps/common/Pathfinder Second Adventure` |
| UMM | `…/Kingmaker_Data/Managed/UnityModManager/` | `…/Wrath_Data/Managed/UnityModManager/` |
| Моды | `…/Pathfinder Kingmaker/Mods/` | `…/Pathfinder Second Adventure/Mods/` |
| Лог UMM | `…/UnityModManager/Log.txt` | `…/UnityModManager/Log.txt` |
| Данные игры (префикс) | `~/.local/share/Steam/steamapps/compatdata/640820/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Kingmaker/` | `~/.local/share/Steam/steamapps/compatdata/1184370/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Wrath Of The Righteous/` |
| Лог игры и модов | `output_log.txt` в данных игры | `Player.log` и `GameLogFull.txt` в данных игры |
| Сохранения | `Saved Games/` в данных игры | `Saved Games/` в данных игры |
| Свои портреты | `Portraits/` в данных игры | `Portraits/` в данных игры |

Строки модов в логе игры начинаются с id мода в скобках, например `[BeltBar]`; меню баффов пишет `[PAD]`.

**В Dolphin** (файловый менеджер): папки, имя которых начинается с точки (`.local`), скрыты — **Ctrl+H** показывает их. Путь можно вставить в адресную строку (Ctrl+L). Папку игры проще всего открыть из Steam: игра → ⚙ → **Управление** → **Просмотреть локальные файлы**.

## 10. Неполадки

| Признак | Причина и решение |
|---|---|
| Окна UMM нет, в `Log.txt` нет свежих строк `[Manager]` | UMM не запускается. Проверить параметры запуска (раздел 6, прямые кавычки, есть `%command%`), наличие `winhttp.dll` и `doorstop_config.ini` в папке игры |
| Kingmaker не запускается совсем | в параметрах запуска осталось `./run.sh %command%` от Linux-сборки — заменить (раздел 6) |
| Kingmaker, режим геймпада: пункт UMM в меню ничего не открывает, моды видны, но не действуют; в режиме мыши всё работает | в `Config.xml` есть `UIStartingPoint` — удалить (конец раздела 4) |
| UMM работал и пропал после обновления игры | UMM стоял способом Assembly — поставить заново по разделам 4–5 ([почему](umm.ru.md#assembly--почему-мы-его-не-используем)) |
| В папке `Managed` есть `UnityEngine.UIModule.dll.original_` | следы способа Assembly. В Steam: игра → ⚙ → Свойства → Установленные файлы → **Проверить целостность файлов игры**, затем разделы 4–6 |
| WotR спрашивает «Перейти на клавиатуру и мышь?» | нет `startup.json` или его вернула проверка файлов — конец раздела 5 |
| Мода нет в списке UMM | папка мода не там: должно быть `Mods/<папка мода>/Info.json`, без лишней вложенной папки. Ещё причина — в папке игры две папки модов, `Mods` и `mods`: Proton видит только одну. Перенести всё в `Mods`, вторую удалить |
| Мод в списке с красным статусом | zip не для этой игры (у модов для двух игр zip с `-kingmaker` и `-wotr`) или старая версия UMM. Причина — в логе игры (раздел 9) |
| После обновления мода работает старая версия | удалить `*.cache` в папке мода ([подробнее](mods.ru.md#обновление)) |
| Нет пункта «Unity Mod Manager» в меню игры | не установлен [Unity Mod Manager in Game Menus](../umm-menu-button/README.ru.md) или UMM не запустился (первая строка таблицы) |

## 11. Удаление

- **Выключить UMM на время:** очистить параметры запуска игры. Файлы остаются, игра запускается без модов.
- **Удалить всё:** из папки игры удалить `winhttp.dll`, `doorstop_config.ini`, папки `<Игра>_Data/Managed/UnityModManager` и `Mods`; у WotR ещё `startup.json` (вернуть `startup.json.bak`, если он был). Очистить параметры запуска. Сохранения в префиксе не затрагиваются.

## 12. Для продвинутых: установка с другого компьютера

Моды можно собирать из исходников и ставить на деку по SSH одной командой — см. [README репозитория](../README.ru.md#автоматическая-установка). Там же — как включить SSH на деке.
