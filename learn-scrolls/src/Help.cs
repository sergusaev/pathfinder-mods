using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
#if KINGMAKER
using Kingmaker.Blueprints.Console;
#else
using Owlcat.Runtime.UI.ConsoleTools.GamepadInput;
#endif
using TMPro;
using UnityEngine;

namespace LearnScrolls
{
    // The help tab, as the help pages of the buff menu and of Time Control. Markup, one element per line: "# " section, "- " bullet,
    // "{A}| text" button line, "**bold**", "[+good+]" and "[-bad-]" (bold, in the colours of the lists) anywhere; "@pad " and
    // "@kb " lines only with gamepad or keyboard hints. {LIKE} and {DISLIKE} leave room for the marks of the lists, laid
    // over the text by the window (Window.PlaceMarks).
    // Buttons {A} {B} {X} {Y} {LB} {RB} {UP} {DOWN} {LEFT} {RIGHT} {START} become the sprites of the game's console tutorials
    // (bracketed letters without them), keys in keyboard mode.
    static class Help
    {
        static bool s_Resolved;
        static TMP_SpriteAsset s_Asset;
        static string s_Prefix;
        // The size of the button sprites against the text. Kingmaker's at 135% fill its line; WotR's own come out larger and
        // run into each other, so on its first help they are measured and brought to the same height (FitIcons).
        static float s_IconPercent = 135f;
        static bool s_IconsMeasured;
        const float IconHeight = 1.2f;

        static readonly Regex Bold = new Regex(@"\*\*(.+?)\*\*");
        static readonly Regex Good = new Regex(@"\[\+(.+?)\+\]");
        static readonly Regex Bad = new Regex(@"\[-(.+?)-\]");
        static readonly Regex Mark = new Regex(@"\{(LIKE|DISLIKE)\}");
        // The room of a mark: non-breaking spaces under a link the window finds the place of.
        const string MarkRoom = "\u00A0\u00A0\u00A0\u00A0\u00A0\u00A0";
        static readonly Regex Token = new Regex(@"\{(A|B|X|Y|LB|RB|UP|DOWN|LEFT|RIGHT|START)\}");

        internal static TMP_SpriteAsset IconAsset
        {
            get
            {
                Resolve();
                return s_Asset;
            }
        }

        // The game names its sprites "<pad prefix><action>", e.g. "XBox_Confirm"; the prefix comes from its binding template.
        static void Resolve()
        {
            if (s_Resolved) return;
            s_Resolved = true;
            string gamePrefix = null;
            try
            {
                var tag = new Kingmaker.TextTools.ConsoleBindingTemplate().Generate(false, new List<string> { "Confirm" });
                var match = Regex.Match(tag ?? "", "name=\"(\\w*?)Confirm\"");
                if (match.Success) gamePrefix = match.Groups[1].Value;
            }
            catch (Exception) { }
            var assets = new List<TMP_SpriteAsset>();
            try
            {
                if (TMP_Settings.defaultSpriteAsset != null) assets.Add(TMP_Settings.defaultSpriteAsset);
            }
            catch (Exception) { }
            assets.AddRange(UnityEngine.Resources.FindObjectsOfTypeAll<TMP_SpriteAsset>());
            foreach (var p in new[] { gamePrefix, "Steam_", "XBox_", "PS4_" }.Where(p => p != null).Distinct())
                foreach (var a in assets)
                {
                    try
                    {
                        if (a == null || a.GetSpriteIndexFromName(p + "Confirm") < 0) continue;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                    s_Asset = a;
                    s_Prefix = p;
                    Main.Log("help icons: sprite asset " + a.name + ", prefix " + p);
                    return;
                }
            Main.Log("help icons: no sprite asset, letters instead");
        }

        static string Button(RewiredActionType action)
        {
            if (Window.KeyboardMode) return ModBar.KeyCaps.Inline(Window.KeyLabel(action));
            Resolve();
            if (s_Asset != null && s_Asset.GetSpriteIndexFromName(s_Prefix + action) >= 0) return "<size=" + s_IconPercent + "%><sprite name=\"" + s_Prefix + action + "\"></size>";
            return Letters(Window.PadLabel(action));
        }

        // Measures the first button sprite of the text once; true when the size changed and the text is to be built again.
        internal static bool FitIcons(TMP_Text text)
        {
            if (s_IconsMeasured || s_Asset == null || Window.KeyboardMode) return false;
            text.ForceMeshUpdate();
            var info = text.textInfo;
            for (int i = 0; i < info.characterCount; i++)
            {
                var c = info.characterInfo[i];
                if (c.elementType != TMP_TextElementType.Sprite) continue;
                s_IconsMeasured = true;
                float height = (c.topLeft.y - c.bottomLeft.y) / text.fontSize;
                Main.Log("help icons: " + s_IconPercent + "% is " + height.ToString("0.00") + " of the text size");
#if KINGMAKER
                return false;
#else
                if (height <= 0f) return false;
                float percent = Mathf.Round(s_IconPercent * IconHeight / height);
                if (Mathf.Abs(percent - s_IconPercent) < 5f) return false;
                s_IconPercent = percent;
                return true;
#endif
            }
            return false;
        }

        static string Letters(string text)
        {
            return "<b><color=" + Theme.Accent + ">[" + text + "]</color></b>";
        }

        static string Tokens(string text)
        {
            text = ModBar.KeyCaps.Keys(text);
            if (text.IndexOf('{') < 0) return text;
            return Token.Replace(text, m =>
            {
                switch (m.Groups[1].Value)
                {
                    case "A": return Button(RewiredActionType.Confirm);
                    case "B": return Button(RewiredActionType.Decline);
                    case "X": return Button(RewiredActionType.Func01);
                    case "Y": return Button(RewiredActionType.Func02);
                    case "LB": return Button(RewiredActionType.LeftUp);
                    case "RB": return Button(RewiredActionType.RightUp);
                    case "UP": return Button(RewiredActionType.DPadUp);
                    case "DOWN": return Button(RewiredActionType.DPadDown);
                    case "LEFT": return Button(RewiredActionType.DPadLeft);
                    case "START": return Button(RewiredActionType.Options);
                    default: return Button(RewiredActionType.DPadRight);
                }
            });
        }

        internal static string Build()
        {
            var sb = new StringBuilder();
            foreach (var raw in Strings.Help.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.StartsWith("@pad "))
                {
                    if (Window.KeyboardMode) continue;
                    line = line.Substring(5);
                }
                else if (line.StartsWith("@kb "))
                {
                    if (!Window.KeyboardMode) continue;
                    line = line.Substring(4);
                }
                line = Bold.Replace(line, "<b>$1</b>");
                line = Good.Replace(line, "<b><color=" + Theme.Good + ">$1</color></b>");
                line = Bad.Replace(line, "<b><color=" + Theme.Bad + ">$1</color></b>");
                line = Mark.Replace(line, m => "<link=\"" + m.Groups[1].Value + "\">" + MarkRoom + "</link>");
                if (line.StartsWith("# "))
                {
                    if (sb.Length > 0) sb.Append("<size=50%>\n</size>");
                    sb.Append("<size=125%><b><color=" + Theme.Accent + ">").Append(Theme.Initial(line.Substring(2))).Append("</color></b></size>\n");
                }
                else if (line.StartsWith("- "))
                    sb.Append("<pos=10><color=" + Theme.Accent + ">•</color><pos=32><indent=32>").Append(Tokens(line.Substring(2))).Append("</indent>\n");
                else if (line.StartsWith("{") && line.Contains("|"))
                {
                    int bar = line.IndexOf('|');
                    sb.Append("<pos=10>").Append(Tokens(line.Substring(0, bar).Trim())).Append("<pos=110><indent=110>")
                        .Append(Tokens(line.Substring(bar + 1).Trim())).Append("</indent>\n");
                }
                else sb.Append(Tokens(line)).Append('\n');
            }
            return sb.ToString();
        }
    }
}
