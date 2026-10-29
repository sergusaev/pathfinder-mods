using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Assets.Console.GamepadInput;
using Kingmaker.UI.ServiceWindow;
using Kingmaker.UI._ConsoleUI.CharGen.Common;
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
    // and so does the mouse wheel over the doll in either interface. The picture is enlarged around the doll's head (Lens).
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
    // Here the zoom asked for glides and goes to Lens.
    static class GamepadZoom
    {
        internal static readonly FieldInfo Target = AccessTools.Field(typeof(DollCamera), "m_Target");
        // Wheel notches from the doll's full view to the closest one.
        const float Notches = 10f;
        // How fast the zoom catches up with the one asked for, per second: a wheel notch glides instead of jumping.
        const float Smooth = 10f;

        static DollCamera s_Camera;
        static float s_Zoom, s_Goal;

        internal static void By(DollCamera camera, float notches)
        {
            if (s_Camera != camera)
            {
                Reset();
                s_Camera = camera;
            }
            s_Goal = Mathf.Clamp01(s_Goal + notches / Notches);
        }

        // Every frame while the doll is shown.
        internal static void Hold(DollCamera camera)
        {
            if (s_Camera == null || s_Camera != camera) return;
            s_Zoom = Mathf.Lerp(s_Zoom, s_Goal, 1f - Mathf.Exp(-Smooth * Mathf.Min(Time.unscaledDeltaTime, 0.1f)));
            if (Mathf.Abs(s_Zoom - s_Goal) < 0.001f) s_Zoom = s_Goal;
            Lens.Apply(camera, s_Zoom);
        }

        internal static void Reset()
        {
            Lens.Release();
            s_Camera = null;
            s_Zoom = s_Goal = 0f;
        }
    }

    // The doll is zoomed by enlarging a part of the picture around its head as it stood when the zoom began; the camera
    // stays where the room put it. The game moved the camera toward the head bone and turned it there: the backdrop slid
    // along, a tall doll showed the black above it, and the camera shook with the head in the idle animation. The part
    // enlarged is kept inside the picture, so no edge of the backdrop shows and a head the full view shows stays whole.
    static class Lens
    {
        // Enlargement at full zoom.
        const float Most = 3f;
        // The part is centred this far above the head bone (at the base of the skull), about at the eyes.
        const float AboveHead = 0.12f;

        static DollCamera s_Owner;
        static Camera s_Camera;
        static bool s_Anchored;
        static Vector2 s_Anchor;

        // zoom: 0 the full view, 1 the closest.
        internal static void Apply(DollCamera owner, float zoom)
        {
            if (owner != s_Owner)
            {
                Release();
                s_Owner = owner;
                s_Camera = owner.GetComponent<Camera>() ?? owner.GetComponentInChildren<Camera>(true);
                if (s_Camera == null) Main.Log.Error("doll zoom: no camera on " + owner.name);
            }
            if (s_Camera == null) return;
            s_Camera.ResetProjectionMatrix();
            if (zoom <= 0f)
            {
                s_Anchored = false;
                return;
            }
            if (!s_Anchored)
            {
                var head = GamepadZoom.Target?.GetValue(owner) as Transform;
                var point = head != null ? s_Camera.WorldToViewportPoint(head.position + Vector3.up * AboveHead) : new Vector3(0.5f, 0.5f, 1f);
                if (point.z <= 0f) point = new Vector3(0.5f, 0.5f, 1f);
                s_Anchor = new Vector2(point.x * 2f - 1f, point.y * 2f - 1f);
                s_Anchored = true;
                Main.Log.Log($"doll zoom: {(owner.isActiveAndEnabled ? "keyboard and mouse" : "gamepad")} interface, head at {point.x:0.00}, {point.y:0.00} of the view");
            }
            // The part shown is centre ± 1/z in clip space, kept within ±1; on screen p' = z (p - centre).
            float z = Mathf.Lerp(1f, Most, zoom);
            float room = 1f - 1f / z;
            float cx = Mathf.Clamp(s_Anchor.x, -room, room), cy = Mathf.Clamp(s_Anchor.y, -room, room);
            var enlarge = Matrix4x4.identity;
            enlarge[0, 0] = z;
            enlarge[1, 1] = z;
            enlarge[0, 3] = -z * cx;
            enlarge[1, 3] = -z * cy;
            s_Camera.projectionMatrix = enlarge * s_Camera.projectionMatrix;
        }

        internal static void Release()
        {
            if (s_Camera != null) s_Camera.ResetProjectionMatrix();
            s_Owner = null;
            s_Camera = null;
            s_Anchored = false;
        }
    }

    // Character creation keeps the doll on screen from page to page, so the zoom of the appearance page stayed on the
    // pages after it. Each page with the doll binds its own DollCharacterController and unbinds it on leaving; the
    // camera goes back to its place on both.
    [HarmonyPatch]
    static class CharGenPageZoomReset
    {
        static IEnumerable<MethodBase> TargetMethods()
        {
            yield return AccessTools.Method(typeof(DollCharacterController), nameof(DollCharacterController.Bind));
            yield return AccessTools.Method(typeof(DollCharacterController), nameof(DollCharacterController.Unbind));
        }

        static void Postfix()
        {
            try
            {
                GamepadZoom.Reset();
            }
            catch (Exception e)
            {
                Main.Log.Error("doll zoom, character creation page: " + e);
            }
        }
    }

    // The game's doll camera of the keyboard and mouse interface (DollCamera.DirtyZoom) moves toward the head and turns
    // to it, the turn following the zoom asked for and the position gliding after it. A wheel notch is added every frame
    // until a coroutine clears it, one or two frames by the order of updates, and the zoom goes past its limits while the
    // wheel turns and springs back after. Here a notch is added once, the zoom stays within its limits, the camera stays
    // put and Lens enlarges the picture, as in the gamepad interface.
    [HarmonyPatch(typeof(DollCamera), "DirtyZoom")]
    static class SmoothDollCamera
    {
        static readonly FieldInfo CurrentField = AccessTools.Field(typeof(DollCamera), "m_CurrentZoom");
        static readonly FieldInfo SmoothedField = AccessTools.Field(typeof(DollCamera), "m_CurrentSmoothZoom");
        static readonly FieldInfo DeltaField = AccessTools.Field(typeof(DollCamera), "m_DeltaZoom");
        static readonly FieldInfo MinField = AccessTools.Field(typeof(DollCamera), "m_MinZoom");
        static readonly FieldInfo MaxField = AccessTools.Field(typeof(DollCamera), "m_MaxZoom");
        static readonly FieldInfo SmoothField = AccessTools.Field(typeof(DollCamera), "m_SmoothZoom");
        static readonly FieldInfo StartField = AccessTools.Field(typeof(DollCamera), "m_StartPosition");
        static readonly FieldInfo StartRotationField = AccessTools.Field(typeof(DollCamera), "m_StartRotation");
        static bool s_Logged, s_Failed;

        static bool Prefix(DollCamera __instance)
        {
            if (s_Failed) return true;
            try
            {
                float min = (float)MinField.GetValue(__instance), max = (float)MaxField.GetValue(__instance);
                float smooth = (float)SmoothField.GetValue(__instance);
                if (!s_Logged)
                {
                    s_Logged = true;
                    Main.Log.Log($"doll camera: zoom {min}..{max}, smoothing {smooth}");
                }
                if (min == max) return true;
                float zoom = Mathf.Clamp((float)CurrentField.GetValue(__instance) + (float)DeltaField.GetValue(__instance), Mathf.Min(min, max), Mathf.Max(min, max));
                float glide = zoom;
                if (smooth != 0f && __instance.isActiveAndEnabled)
                {
                    float k = 1f - Mathf.Exp(-smooth * Mathf.Min(Time.unscaledDeltaTime, 0.1f));
                    glide = Mathf.Lerp((float)SmoothedField.GetValue(__instance), zoom, k);
                    if (Mathf.Abs(glide - zoom) < 0.0005f) glide = zoom;
                }
                CurrentField.SetValue(__instance, zoom);
                DeltaField.SetValue(__instance, 0f);
                SmoothedField.SetValue(__instance, glide);
                // The full view is at m_MaxZoom (ZoomNormalized 1), the closest at m_MinZoom.
                float normalized = Mathf.Clamp01((glide - min) / Mathf.Abs(max - min));
                __instance.transform.SetPositionAndRotation((Vector3)StartField.GetValue(__instance), (Quaternion)StartRotationField.GetValue(__instance));
                Lens.Apply(__instance, 1f - normalized);
                return false;
            }
            catch (Exception e)
            {
                s_Failed = true;
                Main.Log.Error("doll camera: " + e + ", the game's zoom is back");
                return true;
            }
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
