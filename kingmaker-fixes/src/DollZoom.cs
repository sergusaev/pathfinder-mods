using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Assets.Console.GamepadInput;
using Kingmaker.UI.ServiceWindow;
using Kingmaker.UI._ConsoleUI.Common;
using Kingmaker.UI._ConsoleUI.Inventory;
using Rewired;
using UniRx;
using UnityEngine;
using Kingmaker.Localization;
using Kingmaker.Localization.Shared;
using UnityModManagerNet;

namespace KingmakerFixes
{
    // The character doll zooms with the mouse wheel and the right mouse button of the keyboard and mouse interface only;
    // in the gamepad interface the right stick turns it, but nothing zooms it. Here the right stick up and down zooms it,
    // and so does the mouse wheel over the doll in either interface. The camera moves toward the doll's head, as the
    // game's wheel zoom does.
    static class DollZoom
    {
        // RewiredConsts.Action.RightStickX / RightStickY.
        internal const int RightStickX = 2;
        internal const int RightStickY = 3;
        internal const float DeadZone = 0.15f;
        // Wheel notches per second with the stick pushed all the way.
        const float Speed = 8f;

        static readonly FieldInfo ActionsUsed = AccessTools.Field(typeof(InputLayer), "m_ActionsUsed");
        static readonly FieldInfo RoomController = AccessTools.Field(typeof(DollRoom), "m_CharacterController");
        // DollCamera.Current stays empty in the gamepad interface, so the camera is taken from the room itself.
        static readonly FieldInfo RoomCamera = AccessTools.Field(typeof(DollRoom), "m_Camera");
        internal static int GameScrolledFrame = -1;
        static bool s_Failed;

        static DollCamera Camera(DollRoom room)
        {
            return room == null ? null : RoomCamera?.GetValue(room) as DollCamera;
        }

        // In wheel notches. The keyboard and mouse doll room runs its DollCamera and zooms with it; the gamepad one keeps
        // the component switched off, so there the camera is moved by GamepadZoom.
        static void Zoom(DollCamera camera, float notches)
        {
            if (camera.isActiveAndEnabled) camera.Zoom(notches);
            else GamepadZoom.By(camera, notches);
        }

        static void ZoomByStick(DollCamera camera, float y)
        {
            Zoom(camera, y * Speed * Mathf.Min(Time.unscaledDeltaTime, 0.1f));
        }

        internal static void OnUpdate(UnityModManager.ModEntry modEntry, float dt)
        {
            try
            {
                var game = Game.Instance;
                var room = game?.UI?.Common?.DollRoom;
                if (room == null || !room.IsVisible)
                {
                    GamepadZoom.Reset();
                    return;
                }
                var camera = Camera(room);
                if (camera == null) return;
                GamepadZoom.Hold(camera);
                if (ModWindows.ModWindow.AnyOpen) return;
                Wheel(room, camera);
                if (game.IsControllerGamepad && GamePad.HasInstance) Stick(camera);
            }
            catch (Exception e)
            {
                if (s_Failed) return;
                s_Failed = true;
                Main.Log.Error("doll zoom: " + e);
            }
        }

        // The game zooms by the wheel only when the doll's own area gets the scroll event; the gamepad interface
        // covers it, so there the wheel did nothing. A frame the game has zoomed itself is left to it.
        static void Wheel(DollRoom room, DollCamera camera)
        {
            float wheel = Input.mouseScrollDelta.y;
            if (wheel == 0f || GameScrolledFrame == Time.frameCount) return;
            var controller = RoomController?.GetValue(room) as DollRoomCharacterController;
            var area = controller != null && controller.isActiveAndEnabled ? controller.transform as RectTransform : null;
            if (area == null) return;
            var canvas = area.GetComponentInParent<Canvas>();
            canvas = canvas != null ? canvas.rootCanvas : null;
            var eye = canvas == null || canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            if (!RectTransformUtility.RectangleContainsScreenPoint(area, Input.mousePosition, eye)) return;
            Zoom(camera, wheel);
        }

        // The stick is read directly: the game's cursor bind passes a purely vertical push on only while the horizontal
        // axis is active too, so up and down alone never reached the inventory. Zooms in windows that do not use the
        // vertical axis themselves (the appearance page of character creation) and in the inventory on the doll page
        // with the description off, where InventoryStick keeps the game from scrolling the stash. The race and class pages
        // scroll their text with it and are left alone.
        static void Stick(DollCamera camera)
        {
            var player = GamePad.Instance.Player;
            var top = GamePad.Instance.CurrentInputLayer;
            if (player == null || top == null) return;
            var used = ActionsUsed?.GetValue(top) as Dictionary<int, BindDescription>;
            if (used == null) return;
            if (used.ContainsKey(RightStickY) && !(IsInventoryLayer(top) && InventoryZooms(InventoryStick.Current))) return;
            float x = player.GetAxis(RightStickX), y = player.GetAxis(RightStickY);
            if (Mathf.Abs(y) >= DeadZone && Mathf.Abs(y) >= Mathf.Abs(x)) ZoomByStick(camera, y);
        }

        static bool IsInventoryLayer(InputLayer layer)
        {
            return layer.ContextName == "InventoryInputContext" || layer.ContextName == "InventoryAppearanceSettingsContext";
        }

        // Only the doll page shows the doll; on the stash page the stick keeps scrolling the list.
        internal static bool InventoryZooms(InventoryView view)
        {
            if (view == null) return false;
            var dollPage = DollPage(view);
            var description = DescriptionMode;
            return dollPage != null && dollPage.Value && description != null && !description.Value;
        }

        internal static IReadOnlyReactiveProperty<bool> DollPage(InventoryView view)
        {
            return InventoryStick.DollPage?.GetValue(view) as IReadOnlyReactiveProperty<bool>;
        }

        // The item description switch of the gamepad inventory (InventoryVM.IsDescriptionMode).
        internal static IReactiveProperty<bool> DescriptionMode
        {
            get { return Game.Instance?.Player?.UISettings?.InventoryDescriptionMode; }
        }
    }

    // The gamepad doll room has the game's DollCamera switched off: it never zooms, and DollCamera.Current stays empty.
    // Here the camera itself is moved toward the head the room points it at (DollCamera.LookAt), turning to it on the
    // way as the game's zoom does, and put back where it stood when the doll is hidden.
    static class GamepadZoom
    {
        static readonly FieldInfo Target = AccessTools.Field(typeof(DollCamera), "m_Target");
        // Wheel notches from the doll's full view to the closest one, and the part of the way to the head that is.
        const float Notches = 10f;
        const float Reach = 0.7f;
        // How fast the camera catches up with the zoom asked for, per second: a wheel notch glides instead of jumping.
        const float Smooth = 10f;

        static Transform s_Camera;
        static Vector3 s_Position;
        static Quaternion s_Rotation;
        static float s_Zoom, s_Goal;

        internal static void By(DollCamera camera, float notches)
        {
            if (s_Camera != camera.transform)
            {
                Reset();
                s_Camera = camera.transform;
                s_Position = s_Camera.position;
                s_Rotation = s_Camera.rotation;
            }
            s_Goal = Mathf.Clamp01(s_Goal + notches / Notches);
        }

        // Every frame while the doll is shown: the doll turns, so the head is followed.
        internal static void Hold(DollCamera camera)
        {
            if (s_Camera == null || s_Camera != camera.transform) return;
            s_Zoom = Mathf.Lerp(s_Zoom, s_Goal, 1f - Mathf.Exp(-Smooth * Mathf.Min(Time.unscaledDeltaTime, 0.1f)));
            if (Mathf.Abs(s_Zoom - s_Goal) < 0.001f) s_Zoom = s_Goal;
            var head = Target?.GetValue(camera) as Transform;
            if (head == null || s_Zoom <= 0f)
            {
                s_Camera.SetPositionAndRotation(s_Position, s_Rotation);
                return;
            }
            var position = Vector3.Lerp(s_Position, head.position, s_Zoom * Reach);
            var look = Quaternion.LookRotation(head.position - position);
            s_Camera.SetPositionAndRotation(position, Quaternion.Slerp(s_Rotation, look, s_Zoom));
        }

        internal static void Reset()
        {
            if (s_Camera != null) s_Camera.SetPositionAndRotation(s_Position, s_Rotation);
            s_Camera = null;
            s_Zoom = s_Goal = 0f;
        }
    }

    // A wheel notch the game has zoomed with itself. In the gamepad interface the game gets the scroll event too, but
    // its switched-off camera does nothing with it, so that one is not counted.
    [HarmonyPatch(typeof(DollRoomCharacterController), nameof(DollRoomCharacterController.OnScroll))]
    static class GameWheel
    {
        static void Postfix()
        {
            var camera = DollCamera.Current;
            if (camera != null && camera.isActiveAndEnabled) DollZoom.GameScrolledFrame = Time.frameCount;
        }
    }

    // The gamepad inventory turns the doll with the stick left and right, and with up and down scrolls the item
    // description, or the stash list behind the doll page. On the doll page with the description off, up and down zoom
    // the doll instead (DollZoom.Stick), so here the game's stash scrolling is skipped.
    [HarmonyPatch(typeof(InventoryView), "OnRightStick")]
    static class InventoryStick
    {
        internal static readonly FieldInfo DollPage = AccessTools.Field(typeof(InventoryView), "m_IsDollPage");
        internal static InventoryView Current;

        static bool Prefix(InventoryView __instance, Vector2 vector)
        {
            try
            {
                if (Mathf.Abs(vector.y) <= Mathf.Abs(vector.x)) return true;
                return !DollZoom.InventoryZooms(__instance);
            }
            catch (Exception e)
            {
                Main.Log.Error("doll zoom, inventory stick: " + e);
                return true;
            }
        }
    }

    // A hint "right stick Zoom" in the hint row of the gamepad inventory, shown while up and down zoom. A custom hint is
    // only a picture of the button: nothing is bound in the window's input layer.
    [HarmonyPatch(typeof(InventoryView), "CreateInputs")]
    static class InventoryHint
    {
        static readonly FieldInfo Hints = AccessTools.Field(typeof(InventoryView), "m_ConsoleHintsWidget");

        static string ZoomLabel
        {
            get { return LocalizationManager.CurrentLocale == Locale.ruRU ? "Масштаб" : "Zoom"; }
        }

        static void Postfix(InventoryView __instance)
        {
            try
            {
                InventoryStick.Current = __instance;
                // The game remembers the R3 item description between openings; the inventory opens with it off, so the
                // stick zooms the doll at once.
                if (DollZoom.DescriptionMode != null) DollZoom.DescriptionMode.Value = false;
                var hints = Hints?.GetValue(__instance) as ConsoleHintsWidget;
                var dollPage = DollZoom.DollPage(__instance);
                var description = DollZoom.DescriptionMode;
                if (hints == null || dollPage == null || description == null)
                {
                    Main.Log.Error("doll zoom, inventory hint: the hint row or the doll page flag is not there, no hint");
                    return;
                }
                var shown = dollPage.CombineLatest(description, (doll, describing) => doll && !describing).ToReactiveProperty();
                hints.CreateCustomHint(new List<int> { DollZoom.RightStickY }, ZoomLabel, shown);
            }
            catch (Exception e)
            {
                Main.Log.Error("doll zoom, inventory hint: " + e);
            }
        }
    }
}
