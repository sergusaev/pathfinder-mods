# Gamepad Camera Rotation (Pathfinder: Kingmaker)

[Русская версия](README.ru.md)

A Unity Mod Manager mod for the gamepad (console) interface of Pathfinder: Kingmaker. The console version of Kingmaker cannot rotate the camera at all. The mod adds rotation and zoom with the right stick and brings the camera, turn-based and inspect controls, their on-screen hints and the compass in line with the console interface of Pathfinder: Wrath of the Righteous.

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
- Offsets of the hints around the compass and of the camera mode hint, in case they overlap at your resolution.
- The Wrath of the Righteous folder, if it is not found automatically.

## Compatibility

- Requires the gamepad interface; with mouse and keyboard the mod does nothing.
- Turn off camera rotation in Bag of Tricks, otherwise both mods rotate the camera.
- Tested on Kingmaker 2.1.7b (native Linux build, Steam Deck), UMM 0.32.4, Harmony 2.3.6.

## How it works

- `InputRemap.cs` — a postfix on `InGameInputLayerView.Bind` edits the bindings of the in-game input layer: turn-based moves from R3 to D-pad Up, zoom is removed from D-pad Up, inspect fires once per press, R3 release toggles the camera mode. The hint views are registered again so their icons follow the new buttons; the camera mode hint is a copy of the menu hint.
- `Main.cs` — rotate mode handling: the right stick turns `CameraRig` and changes the zoom of `CameraZoom`, while the original pan handlers are blocked by prefixes. The area's default yaw is taken from `CameraRig.SetRotation`.
- `Compass.cs` — a postfix on `InGameClockView.Bind` rebuilds the clock block with the WotR geometry and turns the compass every frame.
- `UnityBundle.cs`, `WotrSprites.cs` — a minimal reader of UnityFS bundles (LZ4 blocks, serialized files with type trees) that finds the sprites, their atlas rectangles and the atlas texture data.
- `LocalMapFix.cs` — fixed map orientation, re-render on yaw change, the camera footprint outline.
- `Strings.cs` — hint texts.

Errors are written to the UMM log and never break the game.

## Building

See the [repository README](../README.md#building): `./build.sh`, `./build.sh --install`.
