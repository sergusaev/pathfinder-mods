using System;
using System.Collections.Generic;

namespace SuccessChance
{
    // Distribution of a d20 roll after the d20 modifiers of ModifyD20.
    static class D20
    {
        // pmf[1..20]. "failed" is the game's own test of a roll for "reroll only if failed".
        internal static double[] Pmf(List<D20Modifier> modifiers, Func<int, bool> failed)
        {
            var pmf = new double[21];
            if (modifiers == null || modifiers.Count == 0)
            {
                for (int d = 1; d <= 20; d++) pmf[d] = 0.05;
                return pmf;
            }
            // Each modifier applies with its chance; enumerate which apply. The game lets each applying modifier set
            // the roll in turn, so the last one wins; a fixed roll (Replace) wins over any reroll.
            int n = Math.Min(modifiers.Count, 8);
            for (int mask = 0; mask < (1 << n); mask++)
            {
                double p = 1;
                D20Modifier reroll = null, replace = null;
                for (int i = 0; i < n; i++)
                {
                    var m = modifiers[i];
                    bool on = (mask & (1 << i)) != 0;
                    p *= on ? m.Chance / 100.0 : 1 - m.Chance / 100.0;
                    if (!on) continue;
                    if (m.Replace) replace = m; else reroll = m;
                }
                if (p <= 0) continue;
                if (replace != null && replace.Roll >= 1 && replace.Roll <= 20)
                {
                    pmf[replace.Roll] += p;
                    continue;
                }
                var one = reroll == null ? Uniform() : Rerolled(reroll, failed);
                for (int d = 1; d <= 20; d++) pmf[d] += p * one[d];
            }
            return pmf;
        }

        static double[] Uniform()
        {
            var pmf = new double[21];
            for (int d = 1; d <= 20; d++) pmf[d] = 0.05;
            return pmf;
        }

        // The first die f, then (unless "only if failed" and f did not fail) n more dice, the best or the worst kept.
        static double[] Rerolled(D20Modifier m, Func<int, bool> failed)
        {
            var pmf = new double[21];
            int n = Math.Max(0, m.Rerolls);
            for (int f = 1; f <= 20; f++)
            {
                if (n == 0 || (m.OnlyIfFailed && !failed(f)))
                {
                    pmf[f] += 0.05;
                    continue;
                }
                for (int x = 1; x <= 20; x++)
                {
                    double px;
                    if (m.TakeBest)
                        // P(max = x) = P(max <= x) - P(max <= x-1), max <= x when f <= x and every other die <= x.
                        px = (f <= x ? Math.Pow(x / 20.0, n) : 0) - (f <= x - 1 ? Math.Pow((x - 1) / 20.0, n) : 0);
                    else
                        // P(min = x) = P(min >= x) - P(min >= x+1).
                        px = (f >= x ? Math.Pow((21 - x) / 20.0, n) : 0) - (f >= x + 1 ? Math.Pow((20 - x) / 20.0, n) : 0);
                    pmf[x] += 0.05 * px;
                }
            }
            return pmf;
        }

        internal static double P(double[] pmf, Func<int, bool> pred)
        {
            double s = 0;
            for (int d = 1; d <= 20; d++) if (pred(d)) s += pmf[d];
            return s;
        }

        internal static string Describe(List<D20Modifier> modifiers)
        {
            if (modifiers == null || modifiers.Count == 0) return "";
            var parts = new List<string>();
            foreach (var m in modifiers)
            {
                string what = m.Replace ? "roll " + m.Roll
                    : (m.TakeBest ? "best of " : "worst of ") + (m.Rerolls + 1) + (m.OnlyIfFailed ? " if failed" : "");
                if (m.Chance < 100) what += " " + m.Chance + "%";
                parts.Add((m.Source ?? "?") + ": " + what);
            }
            return string.Join("; ", parts.ToArray());
        }
    }
}
