# Level 1 Companions & Free Respec (Pathfinder: Kingmaker and Wrath of the Righteous)

[Русская версия](README.ru.md)

A Unity Mod Manager mod for both Pathfinder games. Story companions join the party at level 1 and the player picks every level after that; the built-in respec also takes them back to level 1. A button in the mod window respecs at any time, for free, and the WotR respec window can be closed before every level is spent.

The idea comes from [Level 1 Companions](https://www.nexusmods.com/pathfinderwrathoftherighteous/mods/679) by CascadingDragon for WotR. This mod is written from scratch, adds the respec button and the way out of the respec window, and keeps the companion's name, voice and looks through the respec; in WotR it replaces that mod (see [Replacing lvl1companions](#replacing-lvl1companions-in-wotr)).

## Installing

1. Unity Mod Manager must be installed in the game: [Steam Deck guide](../docs/steam-deck.md), [Windows guide](../docs/windows.md).
2. Download the zip for your game from [Releases](https://github.com/sergusaev/pathfinder-mods/releases):
   - `Level1Companions-<version>-kingmaker.zip` for Kingmaker;
   - `Level1Companions-<version>-wotr.zip` for Wrath of the Righteous.

   The two DLLs are not interchangeable: each is compiled against its own game.
3. Unpack it into the mods folder, so the result is `Mods/Level1Companions/Info.json` (`mods/` in the Steam Deck guide for WotR, which runs through Proton and ignores case).
4. Start the game. The UMM window (Shift+F10, R4 in the Steam Deck layout) lists "Level 1 Companions & Free Respec" with a green status; its settings show the respec button and the list of patched companion blueprints.

When updating, delete `*.cache` in the mod folder before starting the game.

## Tutorial: a companion built by hand

1. **Turn off auto level up.** Settings → Difficulty → Auto Level Up: "Off" (or "Main character only"). Otherwise the game levels companions up by their recommended build as soon as they have the experience.
2. **Recruit as usual.** A companion recruited with the mod installed joins at level 1, with the class, stats and level 1 feats of the blueprint, and with the experience of the level it would have joined at without the mod. The level-up button appears on the portrait at once: pick the levels yourself.
3. **Companions already in the party** keep their levels. Take them back to level 1 with the respec button below or with the game's respec.
4. **Respec at any time.** Open the UMM window, the mod's settings, and press **Respec (free)**:
   - a story companion goes back to the preset level 1 of its blueprint: class, stats and level 1 abilities stay as the story made them, and the character editor opens for the rest of the levels. A respec below level 1 is deliberately not offered: in both games it breaks the companion's story setup and the editor itself;
   - the main character and mercenaries are rebuilt from scratch with everything open to change, as by the game's own respec.

   The UMM window closes and the game's character selection opens, the same window the respec dialog uses. Pick the character; in WotR confirm the respec window. Then the character editor opens. No gold is spent, the respec counter does not grow and no time passes; the party gets a rest, as after the dialog respec.
5. **Confirm the editor.** Closing the editor before the first level is confirmed cancels the respec in the game's own way.
6. **Spend the levels now or later.** After a respec Kingmaker just leaves the level-up button on the portrait. WotR shows the respec window after the first level is confirmed, with buttons for the next level and the next mythic level; without the mod its Complete button is enabled only once every level is spent, and closing the editor returns to that window. With the mod Complete is available as soon as the respec is applied: the levels left are taken later with the portrait button, as after any experience gain.

The button is unavailable in combat, in dialogs and with game windows open; the line under it tells why. The selection lists the main character, mercenaries and companions in the active party (not those sent away, who left or died in the story), whose level is above the level the respec goes back to. Temporary story NPCs (Tartuccio in Kingmaker) are left out: they have no level limit and the game would rebuild them from scratch. The UMM log tells who was listed and why.

## What a respec keeps

- **A story companion's respec changes only class and abilities.** The editor has no portrait, race, appearance, alignment, voice or name steps for a story companion.
- The mod keeps the name, gender, voice, custom model and birthday. The voice of a polymorph (such as Ulbrig's griffon aspect) is not kept: it belongs to the form's buff, which the respec takes off. The game's respec itself resets them to the blueprint's values.
- The editor preview shows the companion's own model, not the bare chargen doll the gamepad interface of both games puts there.
- Kingmaker: the mod also keeps the alignment with its shift history.
- WotR: the game keeps the alignment itself. Mythic levels are not touched by the mod: the respec keeps the mythic experience, and the mythic levels come back through the level up after the respec.
- The main character and mercenaries get the stock respec.

## Behavior and limits

- **The WotR respec window** closes with Complete as soon as the respec is applied, also for a respec from a dialog or ToyBox. The story's forced level-up window is not changed.
- **No experience is lost.** With "Only active companions receive experience" the game does not raise a recruit's experience to the main character's, and the companion would join with the experience of its preset levels. The mod gives it the experience of the level it would have joined at without the mod. During a respec this is off: the game carries the character's experience over itself.
- **Level 1 stays preset**: class, stats and level 1 feats come from the companion blueprint.
- **Mercenaries are not affected**: they are built in the character editor anyway. Enemies and story NPCs before they join are not affected either, as they use separate blueprints.
- **Saves.** Companions already in the party do not change: preset levels are only given when a character is created. Removing the mod is safe; companions recruited with it stay as the player built them.
- Kingmaker: players report that respeccing Ekundayo breaks the bonfire scene of his companion quest. Respec him after it.
- Only character levels are affected. Kingmaker has no mythic paths; in WotR mythic ranks come from the story as before.

## Replacing lvl1companions in WotR

lvl1companions (CascadingDragon) and this mod change the same level limit of the same blueprints, so only one of them is needed. With the game closed, delete `mods/lvl1companions` (or move it out of the mods folder) and install this mod. Saves are not affected: neither mod stores anything in them. ToyBox stays: its respec works as before.

## Troubleshooting

| Symptom | Cause and fix |
|---|---|
| A new companion joins at its usual level | the mod is not loaded (no green status in UMM), or the blueprint is not in the list in the mod window; send the UMM log |
| The companion levels up by itself | auto level up is on in the difficulty settings |
| "Nobody can be respecced" | the character must be in the active party, and a story companion must be above level 1 |
| "Not available now" | close dialogs and game windows, leave combat |
| In WotR the respec window does not close, Complete is disabled | the mod is not loaded; without it the only way out is to spend every level |
| The selection window does not appear after the button | close the UMM window by hand; the status line says so too |

Logs: the UMM log (`<game>/<Game>_Data/Managed/UnityModManager/Log.txt`) and the game log; the mod's lines start with `[Level1Companions]`. At start it writes every companion blueprint it patched or skipped with the reason; for each respec it writes the list of characters after a story companion's respec, `kept name, voice, looks, birthday and alignment`, if a field had to be restored after the copy, the list `fixed after the copy: <field> <found> -> <kept>`, and on leaving the WotR respec window with levels left, `window closed with levels left`.

## How it works

The mod is built from the same sources for both games: `src/*.cs` are shared, `src/kingmaker/` and `src/wotr/` hold what exists in one game only, and `#if KINGMAKER` / `#if WOTR` mark the differences inside shared files.

- **Level limit.** `ClassLevelLimit.LevelLimit` on a companion blueprint is how far `AddClassLevels.LevelUp` gives preset levels. Above it, player units only get level plans (the recommended build used by auto level up). `Player.RespecCompanion` recreates the companion from its blueprint with the same limit, and `RespecCompanion.CanRespec` allows a respec only above it. The mod sets the limit to 1.
  - Kingmaker: a postfix on `LibraryScriptableObject.LoadDictionary` patches every `*_Companion` blueprint and `AchievementsRoot.FollowersCompanions` with a limit above 1, except mercenaries (`BlueprintRoot.CustomCompanion`, `CharGen.CustomCompanions`), pregens and animal companions.
  - WotR loads blueprints on demand, so a postfix on `BlueprintsCache.Init` patches a fixed list: the 39 blueprints of lvl1companions plus the act 1 companions (Camellia, Lann, Seelah, Wenduag and their DLC1 versions; blueprints whose limit is already 1 or lower are only logged). A companion may name another blueprint for the respec (`ReplaceUnitBlueprintForRespec`), which gets the limit too. `MythicLevelLimit` is not touched.
- **Experience.** Adding a class level raises `Experience` to the experience table value of the reached level. A prefix/postfix pair on `AddClassLevels.LevelUp` (Kingmaker: `(UnitDescriptor, int, bool)`, WotR: the static `(AddClassLevels, UnitDescriptor, int, UnitFact)`, mythic classes skipped) tracks the level the original limit would have given and raises `Experience` to it. While a respec runs (`Player.RespecCompanion`, or `RespecWindowVM` open in WotR) it does nothing: the game builds the new unit and the editor previews from the blueprint and carries the experience over itself.
- **Respec button.** It raises `ICharacterSelectorHandler.HandleSelectCharacter`, as the `RespecCompanion` dialog action does. In Kingmaker the selection's confirmation calls `Player.RespecCompanion`; in WotR it raises `IRespecInitiateUIHandler.HandleRespecInitiate`, whose window calls it. The callback rests the party like `FinishRespecialization` with `ForFree`, without the day. A story companion's respec goes to its level limit (1), the main character's and mercenaries' to 0, as `RespecCompanion.CanRespec` allows.
- **Identity.** `Player.RespecCompanion` creates a new unit and copies it onto the old one as JSON (`UnitEntityData.PrepareRespec`, then `JsonConvert.PopulateObject`), except inventory, body, UI settings, buffs and non class features. A prefix on `PrepareRespec` writes the companion's snapshot (name, gender, voice, left-handedness, custom model, birthday; Kingmaker also the doll and the alignment) onto the new unit before the copy; the success callback checks the result and logs it.
- **Kingmaker gamepad editor** (`src/kingmaker/Chargen.cs`): postfixes on `CharGenVM.NeedVoicePhase` / `NeedNamePhase` hide those phases for a story companion's respec; a postfix on `CharcaterServiceContext.HandleLevelUpStart` puts the companion's model into the doll room, and `CharGenDollRoom.HandleDollStateUpdated` is blocked until `CharGenVM` is disposed, so the preview is not replaced by the chargen doll. WotR hides the voice and name phases of a story companion itself, but its class phase shows the chargen doll whenever the level up has a `DollState`, which a respec creates for a story companion too. `src/wotr/Chargen.cs` notes the level up when `CharGenClassPhaseVM.DollState` is read, and a prefix on `DollRoom.BindDollState` shows the level up preview unit (`DollRoom.SetupInfo`) instead of that `DollState`.
- **WotR respec window** (`src/wotr/RespecWindow.cs`): `RespecWindowVM.UpdateProperties` sets `IsFinished` only when the unit has nothing left to level, and `Complete` checks the same. Once the respec is applied (`CanStop` false, the first level confirmed) a postfix sets `IsFinished` and a prefix on `Complete` closes the window (`m_EndAction`). The forced level-up window (`IsRespec` false) is not touched.
- **The selection** takes `Player.PartyCharacters` only: in WotR `RemoteCompanions` also holds companions who left or died in the story.

## Building

The [repository README](../README.md#building) has the tools and settings. This mod is built for both games:

```bash
./level1-companions/build.sh                          # both games: build/kingmaker/ and build/wotr/
./level1-companions/build.sh --game wotr              # one game
./level1-companions/build.sh --game kingmaker --install   # build and install into Kingmaker; the game must be closed
```

The game's assemblies are copied into `refs/kingmaker/` and `refs/wotr/`, so both games must be installed with UMM on the game machine for the full build.
