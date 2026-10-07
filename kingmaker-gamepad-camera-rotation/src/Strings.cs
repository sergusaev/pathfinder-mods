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

        static string Pick(string ru, string de, string fr, string zh, string en)
        {
            switch (LocalizationManager.CurrentLocale)
            {
                case Locale.ruRU: return ru;
                case Locale.deDE: return de;
                case Locale.frFR: return fr;
                case Locale.zhCN: return zh;
                default: return en;
            }
        }

        internal static string CameraTitle
        {
            get { return Pick("Камера", "Kamera", "Caméra", "镜头", "Camera"); }
        }

        internal static string PcRotateTip
        {
            get
            {
                return Pick("Поворот: средняя кнопка мыши, {0} / {1}", "Drehen: mittlere Maustaste, {0} / {1}",
                    "Pivoter : bouton central, {0} / {1}", "旋转：鼠标中键，{0} / {1}", "Rotate: middle mouse button, {0} / {1}");
            }
        }

        internal static string PcNorthTip
        {
            get
            {
                return Pick("На север: {0} или щелчок по компасу", "Nach Norden: {0} oder Klick auf den Kompass",
                    "Vers le nord : {0} ou clic sur la boussole", "朝北：{0} 或点击罗盘", "North: {0} or click the compass");
            }
        }

        internal static string PcPanTip
        {
            get
            {
                return Pick("Сдвиг: Alt + средняя кнопка мыши", "Verschieben: Alt + mittlere Maustaste",
                    "Déplacer : Alt + bouton central", "平移：Alt + 鼠标中键", "Pan: Alt + middle mouse button");
            }
        }
    }
}
