using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Assets.Console.GamepadInput;
using Kingmaker.UI;
using Kingmaker.UI._ConsoleUI.InputLayers.InGameLayer;
using Kingmaker.UI._ConsoleUI.LocalMap;
using Kingmaker.UI._ConsoleUI.Utils.HintTool;
using Rewired;
using UnityEngine;

namespace GamepadCameraRotation
{
    // WotR console layout: R3 release toggles camera rotate mode, D-pad up toggles turn-based mode, D-pad down only inspects.
    [HarmonyPatch(typeof(InGameInputLayerView), "Bind")]
    static class InputRemap
    {
        internal const string CameraHintName = "GCR_MoveCameraHint";

        static readonly FieldInfo LayerField = AccessTools.Field(typeof(InGameInputLayerView), "m_InGameInputLayer");
        static readonly FieldInfo BehaviorField = AccessTools.Field(typeof(InGameInputLayerView), "m_InputBehavior");
        static readonly FieldInfo ToggleModeHintField = AccessTools.Field(typeof(InGameInputLayerView), "m_ToggleModeHint");
        static readonly FieldInfo MenuHintField = AccessTools.Field(typeof(InGameInputLayerView), "m_MenuHint");
        static readonly FieldInfo BindsField = AccessTools.Field(typeof(InputLayer), "m_Binds");
        static readonly MethodInfo ActivateInspect = AccessTools.Method(typeof(InGameInputLayerView), "OnActivateInspect");

        static GenericHintView s_CameraHint;

        static void Postfix(InGameInputLayerView __instance)
        {
            try
            {
                var layer = LayerField.GetValue(__instance) as InGameInputLayer;
                var behavior = BehaviorField.GetValue(__instance) as ConsoleInputBehavior;
                if (layer == null || behavior == null) return;
                var binds = (List<BindDescription>)BindsField.GetValue(layer);

                bool bound = layer.LayerBinded.Value;
                if (bound) layer.Unbind();

                BindDescription turnBased = binds.Find(b => b.ActionId == Main.RightStickButton && b.EventType == InputActionEventType.ButtonJustPressed);
                BindDescription zoomIn = binds.Find(b => b.ActionId == Main.DPadUp && b.EventType == InputActionEventType.ButtonRepeating);
                BindDescription inspect = binds.Find(b => b.ActionId == Main.DPadDown && b.EventType == InputActionEventType.ButtonRepeating);

                if (zoomIn != null) binds.Remove(zoomIn);
                if (turnBased != null) turnBased.ActionId = Main.DPadUp;
                if (inspect != null)
                {
                    inspect.EventType = InputActionEventType.ButtonJustPressed;
                    inspect.ActionHandler = e =>
                    {
                        if (InputLayer.CanReceiveInput() && (bool)ActivateInspect.Invoke(__instance, null))
                            Game.Instance.UI.Common.UISound.Play(UISoundType.ButtonClick);
                    };
                }
                InputBindStruct cameraBind = layer.AddButton(e => Main.ToggleRotateMode(), Main.RightStickButton,
                    InputActionEventType.ButtonJustReleased, enableDefaultSound: false);

                if (bound) layer.Bind();

                var toggleHint = ToggleModeHintField.GetValue(__instance) as GenericHintView;
                if (turnBased != null && toggleHint != null)
                    behavior.RegisteredHint(toggleHint, new InputBindStruct(layer, turnBased));

                var menuHint = MenuHintField.GetValue(__instance) as GenericHintView;
                if (menuHint != null)
                {
                    Transform parent = menuHint.transform.parent;
                    Transform old = parent.Find(CameraHintName);
                    if (old != null) UnityEngine.Object.Destroy(old.gameObject);
                    var go = UnityEngine.Object.Instantiate(menuHint.gameObject, parent, false);
                    go.name = CameraHintName;
                    ((RectTransform)go.transform).anchoredPosition = Compass.MoveCameraHintPos;
                    s_CameraHint = go.GetComponent<GenericHintView>();
                    behavior.RegisteredHint(s_CameraHint, cameraBind);
                    UpdateCameraHintLabel();
                }
                Main.Log?.Log("Input remapped, hints parent " + (menuHint != null ? menuHint.transform.parent.name : "none"));
            }
            catch (Exception e)
            {
                Main.Log?.Error(e.ToString());
            }
        }

        internal static void UpdateCameraHintLabel()
        {
            if (s_CameraHint == null) return;
            s_CameraHint.SetLabel(Main.RotateMode ? Strings.MoveCamera : Strings.RotateCamera);
        }
    }

    [HarmonyPatch(typeof(LocalMapView), "GetInputLayer")]
    static class LocalMapInputPatch
    {
        static void Postfix(InputLayer __result)
        {
            __result?.AddButton(e => Main.ToggleRotateMode(), Main.RightStickButton,
                InputActionEventType.ButtonJustReleased, enableDefaultSound: false);
        }
    }
}
