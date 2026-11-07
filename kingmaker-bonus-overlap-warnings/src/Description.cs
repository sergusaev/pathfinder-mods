using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using HarmonyLib;
using Kingmaker.Blueprints.Root;
using Kingmaker.EntitySystem.Stats;
using Kingmaker.UI.Tooltip;
using UnityEngine;

namespace BonusOverlapWarnings
{
    // The item's description (the tooltip and the details window, both interfaces), after its stat bonuses: what of
    // the equipped item's bonuses has no effect and because of what, what it overrides.
    [HarmonyPatch(typeof(DescriptionTemplatesItem), "ItemStatBonuses")]
    static class DescriptionOverlaps
    {
        static void Postfix(DescriptionBricksBox box, TooltipData data, ref bool __result)
        {
            try
            {
                var item = data != null ? data.Item : null;
                if (item == null || item.Wielder == null) return;
                // Counted anew: the description may come between the refreshes.
                Overlaps.Clear();
                var report = Overlaps.Of(item);
                if (report == null || report.Verdict == Verdict.None) return;
                // The game's separator comes after the block only when the block says it added something.
                if (__result) box.Add(DescriptionTemplatesBase.Bricks.Separator3);
                var title = report.Verdict == Verdict.Lost ? (report.Extras.Count > 0 ? Strings.TitleLostOwn : Strings.TitleLost)
                    : report.Verdict == Verdict.Reduced ? Strings.TitleReduced
                    : report.Green ? Strings.TitleShared : Strings.TitleOverrides;
                var color = report.Verdict == Verdict.Lost ? "DE4634" : report.Verdict == Verdict.Reduced ? "D99A1E" : "4F8FD6";
                box.Add(DescriptionTemplatesBase.Bricks.SimpleText).SetText("<color=#" + color + "><b>" + title + "</b></color>");
                foreach (var line in report.Lines)
                    box.Add(DescriptionTemplatesBase.Bricks.SimpleText).SetText(Text(line));
                if (report.Extras.Count > 0)
                {
                    var unlisted = Extras.Unlisted(item);
                    var green = Strings.ExtrasLine + (unlisted.Count > 0 ? Strings.Unlisted(string.Join("; ", unlisted.ToArray())) : "");
                    box.Add(DescriptionTemplatesBase.Bricks.SimpleText).SetText("<color=#3E9A4C>" + green + "</color>");
                }
                __result = true;
            }
            catch (Exception e) { Main.Error(e, "overlaps in the description"); }
        }

        static string Text(Line line)
        {
            var texts = LocalizedTexts.Instance;
            var bonus = new StringBuilder();
            bonus.Append(texts.AbilityModifiers.GetName(line.Descriptor)).Append(" +").Append(line.Value).Append(" (");
            if (line.OnItem) bonus.Append(Strings.ItemItself);
            else bonus.Append(Stats(line.Stats));
            bonus.Append(")");
            // The rivals by name only: their values are on them.
            var rivals = string.Join(", ", line.Rivals.Select(r => r.Source.Label).ToArray());
            var rivalsBy = string.Join(", ", line.Rivals.Select(r => r.Source.LabelBy).ToArray());
            var b = "<b>" + bonus + "</b>";
            switch (line.Kind)
            {
                case LineKind.Lost: return Strings.Lost(b, rivalsBy);
                case LineKind.Tie: return Strings.Tie(b, rivals);
                case LineKind.Shared: return Strings.Shared(b, rivals);
                case LineKind.OverridesTie: return Strings.OverridesTie(b, rivals);
                default: return Strings.Overrides(b, rivals);
            }
        }

        static string Name(string text, StatType stat)
        {
            return string.IsNullOrEmpty(text) ? stat.ToString() : text;
        }

        static string Stats(List<StatType> stats)
        {
            var texts = LocalizedTexts.Instance.Stats;
            const int shown = 4;
            var names = stats.Take(stats.Count > shown + 1 ? shown : stats.Count).Select(s => Name(texts.GetText(s), s)).ToArray();
            var text = string.Join(", ", names);
            if (stats.Count > names.Length) text += Strings.AndMore(stats.Count - names.Length);
            return text;
        }
    }
}
