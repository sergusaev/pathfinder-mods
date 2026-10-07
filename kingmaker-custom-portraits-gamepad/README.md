# Custom Portraits Gamepad Selection Fix (Pathfinder: Kingmaker)

[Русская версия](README.ru.md)

A Unity Mod Manager mod. In the gamepad (console) interface the character creation of Kingmaker offers only the built-in portraits: the game does not read the `Portraits` folder with custom portraits there. The mod appends all portraits from that folder to the end of the console portrait list.

## Where portraits go

Each portrait is a separate subfolder of `Portraits` with `Small.png` (185×242), `Medium.png` (330×432) and `Fulllength.png` (692×1024). Subfolder names are free; nested subfolders are not seen by the game.

- Linux (native build, Steam Deck): `~/.config/unity3d/Owlcat Games/Pathfinder Kingmaker/Portraits/`
- Windows: `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Kingmaker\Portraits\`

## Behaviour

- Custom portraits come after the built-in ones, sorted by folder name.
- Choosing and saving work the standard way. Saves do not depend on the mod: if it is removed, characters keep their custom portraits.
- Images load lazily, so the first opening of the portrait step may pause while thumbnails load.
- The UMM log gets `Added custom portraits: N`; errors go there too.

Tested on Kingmaker 2.1.7b (native Linux build, Steam Deck), UMM 0.32.4, Harmony 2.3.6.

## How it works

- The console portrait step (`Kingmaker.UI._ConsoleUI.CharGen.Phases.CharGenPortraitPhaseVM`) builds its list only from `BlueprintRoot.CharGen.Portraits`. The mouse interface (`CharBPortraitSelector`) loads custom portraits through `CustomPortraitsManager`.
- A Harmony postfix on the `CharGenPortraitPhaseVM(LevelUpController)` constructor takes `CustomPortraitsManager.Instance.GetExistingCustomPortraitIds()`, sorts them by name, creates a `BlueprintPortrait` (`ScriptableObject.CreateInstance`) with `Data = new PortraitData(id)` for each and adds a `CharGenPortraitSelectorItemVM` to `SelectorGroupVM.VisibleCollection`. The blueprints are cached by id.
- `UnitUISettings.SetPortrait` stores a blueprint whose `Data.IsCustom` is set as `m_CustomPortrait` (the `PortraitData` itself), not as a blueprint reference, which is why saves stay independent of the mod.

## Building

See the [repository README](../README.md#building): `./build.sh`, `./build.sh --install`.
