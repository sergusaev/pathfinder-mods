using Kingmaker.Localization;
using Kingmaker.Localization.Shared;

namespace GamepadCameraRotation
{
    // WotR InGameMenuTexts.CameraRotateMode / CameraMoveMode texts; locales WotR lacks fall back to English.
    static class Strings
    {
        internal static string RotateCamera
        {
            get
            {
                switch (LocalizationManager.CurrentLocale)
                {
                    case Locale.ruRU: return "Вращение камеры";
                    case Locale.deDE: return "Kamera drehen";
                    case Locale.frFR: return "Pivoter la caméra";
                    case Locale.zhCN: return "旋转镜头";
                    default: return "Rotate Camera";
                }
            }
        }

        internal static string MoveCamera
        {
            get
            {
                switch (LocalizationManager.CurrentLocale)
                {
                    case Locale.ruRU: return "Движение камеры";
                    case Locale.deDE: return "Bewege die Kamera";
                    case Locale.frFR: return "Déplacer la caméra";
                    case Locale.zhCN: return "移动镜头";
                    default: return "Move Camera";
                }
            }
        }
    }
}
