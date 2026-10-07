# Portrait Scroll Fix (gamepad) — Pathfinder: Wrath of the Righteous

[Русская версия](README.ru.md)

A Unity Mod Manager mod. In the gamepad interface the "Custom" tab of the character creation portraits does not scroll after the cursor: the cursor leaves the screen and the right stick does not move the list. The mod fixes both.

## What it does

- **The list follows the focus** for the D-pad, the left stick, single presses and auto-repeat, all the way to both ends of the list.
- **The right stick** on the custom portraits tab moves the cursor by rows: threshold 0.5, the first step at once, auto-repeat after 0.35 s every 0.09 s. On other tabs the stick works as before.
- Errors go to the UMM log and never break the game: on an exception the focus follower switches itself off.

Tested on WotR (Proton, Steam Deck), UMM 0.32.4, Harmony 2.0.4 from `Wrath_Data/Managed`.

## Where portraits go

Each portrait is a separate subfolder of `Portraits` with `Small.png` (185×242), `Medium.png` (330×432) and `Fulllength.png` (692×1024). Nested subfolders are not seen by the game.

- Steam Deck / Proton: `~/.local/share/Steam/steamapps/compatdata/1184370/pfx/drive_c/users/steamuser/AppData/LocalLow/Owlcat Games/Pathfinder Wrath Of The Righteous/Portraits/`
- Windows: `%USERPROFILE%\AppData\LocalLow\Owlcat Games\Pathfinder Wrath Of The Righteous\Portraits\`

## Why the list did not scroll

- Custom portraits in the console interface (`CharGenCustomPortraitGroupConsoleView`) live in a virtual list `VirtualListGridVertical`. Gamepad navigation is a grid of 8 per row built from `m_VirtualList.Elements`, and the focused object is a `VirtualListElement`, a plain object rather than a `MonoBehaviour`.
- `CharGenPortraitPhaseDetailedConsoleView.SetSelected` scrolls only the outer `ScrollRect` and only to a `MonoBehaviour`, so for custom portraits it does nothing.
- The standard `ScrollController.ForceScrollToElement` → `TryPinToElement` only works with elements the list has already laid out (`WasUpdatedAtLeastOnes`): the visible rows plus one spare, never further.
- The right stick (`CharGenPortraitPhaseDetailedConsoleView.Scroll`) scrolls the outer `ScrollRect`, which does not contain the virtual list.

## How it works

- A postfix on `SetSelected` attaches a `FocusFollower` component to the screen. Every `LateUpdate` it finds the focused element in the navigation chain and calls `ScrollController.ScrollTowards(element.Data, speed)`. That path finds the direction even for elements that are not laid out (by index relative to `TopVisibleIndex`) and stops once the element is visible. Speed: 45 px per frame for elements with a view, 220 px for distant ones.
- A prefix on `Scroll` on the custom portraits tab steps the navigation grid with `HandleUp`/`HandleDown`.

Notes for building against WotR:

- Reference `0Harmony.dll` from `Wrath_Data/Managed` (2.0.4), not the newer one from UMM, so the mod never requires a newer Harmony than the one loaded. `build-mod.sh` does this.
- `AccessTools.FieldRefAccess` does not compile with `mcs`; fields are read through `FieldInfo.GetValue`.
- `GetUpValidEntity`/`GetDownValidEntity` are protected, so the public `HandleUp`/`HandleDown` are used.
- `IScrollController.ForceScrollToElement`/`ScrollTowards` take `IVirtualListElementData` (`element.Data`), not the element.

## Building

See the [repository README](../README.md#building): `./build.sh`, `./build.sh --install`. When updating by hand, delete the old `PortraitScrollFix.dll.*.cache` in the mod folder.
