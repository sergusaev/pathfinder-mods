using Kingmaker.Localization;
using Kingmaker.Localization.Shared;

namespace ActionBarRows
{
    static class Strings
    {
        internal static string Rows
        {
            get
            {
                switch (LocalizationManager.CurrentLocale)
                {
                    case Locale.ruRU: return "Ряды";
                    case Locale.deDE: return "Reihen";
                    case Locale.frFR: return "Rangées";
                    case Locale.zhCN: return "行";
                    default: return "Rows";
                }
            }
        }
    }
}
