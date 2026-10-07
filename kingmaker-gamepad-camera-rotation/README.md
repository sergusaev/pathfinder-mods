# Camera Rotation and Compass (Pathfinder: Kingmaker)

[Русская версия](README.ru.md)

A Unity Mod Manager mod that lets you rotate the camera in Pathfinder: Kingmaker and adds the compass of Pathfinder: Wrath of the Righteous, in both interfaces of the game. Kingmaker cannot rotate the camera with a gamepad at all. With a gamepad (console interface) the mod adds rotation and zoom with the right stick and brings the camera, turn-based and inspect controls, their on-screen hints and the compass in line with the console interface of WotR. With keyboard and mouse it rotates the camera as WotR does and puts the compass into the game's clock (see [Keyboard and mouse](#keyboard-and-mouse)).

## Controls

| Button | Kingmaker before | With the mod (as in WotR) |
|---|---|---|
| R3 (click the right stick) | toggle turn-based mode | toggle the camera mode: move ↔ rotate |
| Right stick, move mode | pan the camera | pan the camera |
| Right stick, rotate mode | — | left/right rotates the camera, up/down zooms |
| D-pad Up | zoom in (repeats while held) | toggle turn-based mode |
| D-pad Down | inspect (repeats while held) | inspect (once per press) |

- The camera mode hint sits above the compass and names the mode the button switches to: "Rotate Camera" while moving, "Move Camera" while rotating. The texts are WotR's own (English, Russian, German, French, Chinese; other languages use English).
- The turn-based mode hint stays where it was and now shows the D-pad Up icon.
- Holding R3 does nothing: WotR skips time with it, Kingmaker has no such action and the mod does not add one.
- On the local map R3 also toggles the mode, and the right stick rotates the camera in rotate mode. The console map of Kingmaker has no hint bar, so there is no hint there.

## Keyboard and mouse

| Input | Action |
|---|---|
| Middle mouse button, drag | rotate the camera |
| Alt + middle mouse button, drag | move the camera (the Kingmaker default for the middle button) |
| Alt+A / Alt+D, held | rotate left / right |
| F1 | turn the camera north (the default direction of the area) |
| Wheel | zoom, as in the game |
| Click on the compass | turn north; hovering shows a tooltip on the game's parchment |

- The keys are set in the mod settings (UMM window). A key that the game also uses is shown there with the game's action, so a conflict is visible at once.
- The compass takes the place of the hourglass in the round window of the clock next to the system buttons; its scale and offset are set in the settings.
- The keys do nothing during cutscenes and dialogues, while typing, with the UMM window or the buff menu of Buff It 2 The Limit (Groups) open.
- The PC local map gets the same rotated outline of the visible area as the console map.

## Compass

The hourglass in the lower left corner is replaced by the WotR console compass. It is built with WotR's layout: the same position, size, layers and hints around it (Pause, Cursor, Menu, Highlight and the camera mode). The arrow and the rings turn with the camera; north is the default camera direction of the current area.

The compass graphics belong to Wrath of the Righteous and are not shipped with the mod. On the first start the mod finds the installed WotR, reads the sprites from its `Bundles/ui` bundle and saves them as PNG files into `Compass/` inside the mod folder. Later starts use those files.

- **WotR is searched** next to Kingmaker in the same Steam library, then in all Steam libraries from `libraryfolders.vdf` (SteamOS/Linux and Windows). For any other location enter the WotR folder in the mod settings.
- **Without WotR** the hourglass stays and everything else works; the UMM log explains why.
- To extract the sprites again, delete the `Compass` folder in the mod folder.
- Nothing else is needed on the device: the mod reads the bundle with its own code, and the GPU decodes the BC7 atlas.

## Local map

In Kingmaker the local map follows the camera direction but only re-renders when the area changes, so after a rotation the picture and the frame of the visible area no longer matched. As in WotR, the map now keeps a fixed orientation (the default direction of the area), and the frame of the visible area is drawn as the exact footprint of the camera on the ground: it turns with the camera and becomes a trapezoid when the camera is tilted.

## Settings (UMM window)

- Rotation speed (degrees per second) and zoom speed; inverting either axis.
- Keyboard and mouse: mouse rotation speed, the rotate and north keys, the compass in the clock (on/off, scale, offset).
- Offsets of the hints around the compass and of the camera mode hint, in case they overlap at your resolution.
- The Wrath of the Righteous folder, if it is not found automatically.

## On the Steam Deck

The mod uses the standard gamepad buttons, so it needs no Steam Input changes. Its settings live in the UMM window, which needs the mouse: bind R4 to the UMM hotkey and the right trackpad to the mouse as described in [the Steam Deck guide](../docs/steam-deck.md#2-steam-input-layout). The compass is found automatically when WotR is in any Steam library of the deck.

## Compatibility

- Works in both interfaces of the game: gamepad (console) and keyboard and mouse.
- Until 1.2.0 the mod was called Gamepad Camera Rotation; its Id and folder `GamepadCameraRotation` stay, so updates go over the old installation.
- Turn off camera rotation in Bag of Tricks, otherwise both mods rotate the camera.
- Tested on Kingmaker 2.1.7b (native Linux build on the Steam Deck; Windows), UMM 0.32.4, Harmony 2.3.6.

## How it works

- `InputRemap.cs` — a postfix on `InGameInputLayerView.Bind` edits the bindings of the in-game input layer: turn-based moves from R3 to D-pad Up, zoom is removed from D-pad Up, inspect fires once per press, R3 release toggles the camera mode. The hint views are registered again so their icons follow the new buttons; the camera mode hint is a copy of the menu hint.
- `Main.cs` — rotate mode handling: the right stick turns `CameraRig` and changes the zoom of `CameraZoom`, while the original pan handlers are blocked by prefixes. The area's default yaw is taken from `CameraRig.SetRotation`.
- `Compass.cs` — a postfix on `InGameClockView.Bind` rebuilds the clock block with the WotR geometry and turns the compass every frame.
- `UnityBundle.cs`, `WotrSprites.cs` — a minimal reader of UnityFS bundles (LZ4 blocks, serialized files with type trees) that finds the sprites, their atlas rectangles and the atlas texture data.
- `PcMode.cs` — keyboard and mouse: middle-button drag after the game's own scroll tick, Alt+A / Alt+D and the north key, the compass in the clock, its tooltip, key conflicts with the game's bindings.
- `LocalMapFix.cs` — fixed map orientation, re-render on yaw change, the camera footprint outline.
- `Strings.cs` — hint texts.

Errors are written to the UMM log and never break the game.

## Building

See the [repository README](../README.md#building): `./build.sh`, `./build.sh --install`.
