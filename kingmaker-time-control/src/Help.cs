using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Kingmaker.Blueprints.Console;
using TMPro;

namespace TimeControl
{
    // The help tab, as the help page of the buff menu. Markup, one element per line: "# " section, "- " bullet,
    // "{A}| text" button line, "**bold**" anywhere; "@pad " and "@kb " lines only with gamepad or keyboard hints.
    // Buttons {A} {B} {X} {Y} {LB} {RB} {UP} {DOWN} {LEFT} {RIGHT} {START} become the sprites of the game's console tutorials
    // (bracketed letters without them), keys in keyboard mode.
    static class Help
    {
        static bool s_Resolved;
        static TMP_SpriteAsset s_Asset;
        static string s_Prefix;

        static readonly Regex Bold = new Regex(@"\*\*(.+?)\*\*");
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
            if (Ui.Keyboard) return Letters(Modal.KeyName(action));
            Resolve();
            if (s_Asset != null && s_Asset.GetSpriteIndexFromName(s_Prefix + action) >= 0) return "<size=135%><sprite name=\"" + s_Prefix + action + "\"></size>";
            return Letters(Modal.PadName(action));
        }

        static string Letters(string text)
        {
            return "<b><color=" + Theme.Accent + ">[" + text + "]</color></b>";
        }

        static string Tokens(string text)
        {
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
            foreach (var raw in S.HelpBody.Split('\n'))
            {
                string line = raw.TrimEnd('\r');
                if (line.StartsWith("@pad "))
                {
                    if (Ui.Keyboard) continue;
                    line = line.Substring(5);
                }
                else if (line.StartsWith("@kb "))
                {
                    if (!Ui.Keyboard) continue;
                    line = line.Substring(4);
                }
                line = Bold.Replace(line, "<b>$1</b>");
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
