using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Kingmaker;
using Kingmaker.GameModes;
using Kingmaker.UI;
using Newtonsoft.Json;
using UnityEngine;

namespace TimeControl
{
    class TrackedTimer
    {
        public string Key;
        public string Name;
        // Own deadline: this many hours before the timer runs out; alerts count from it.
        public int DeadlineHours;
        public List<int> Alerts = new List<int>();
        public List<int> Fired = new List<int>();
        // Hours left at the last check, to tell a timer that ran out from one the player resolved.
        public double LastRemaining = double.MaxValue;
        public bool Critical;
    }

    class TrackingData
    {
        public List<TrackedTimer> Tracked = new List<TrackedTimer>();
        // Auto-tracked deadlines the player stopped tracking.
        public List<string> Dismissed = new List<string>();
    }

    // Tracked deadlines and their alerts, kept per playthrough (Player.GameId) in the mod folder.
    static class Tracker
    {
        internal static readonly int[] AlertPresets = { 720, 336, 168, 72, 24, 12, 6, 1, 0 };
        internal static readonly int[] DeadlinePresets = { 0, 1, 6, 12, 24, 48, 72, 168, 336 };
        const float ScanInterval = 0.5f;

        static TrackingData s_Data = new TrackingData();
        static string s_GameId;
        static TimeSpan s_LastTime;
        static float s_NextScan;
        static bool s_Rebase;

        internal static List<GameTimer> Current = new List<GameTimer>();

        internal static IEnumerable<TrackedTimer> Tracked
        {
            get { return s_Data.Tracked; }
        }

        internal static TrackedTimer Get(string key)
        {
            return s_Data.Tracked.FirstOrDefault(t => t.Key == key);
        }

        internal static bool IsTracked(string key)
        {
            return Get(key) != null;
        }

        internal static void Tick()
        {
            var player = Game.Instance.Player;
            if (string.IsNullOrEmpty(player.GameId)) return;
            if (player.GameId != s_GameId)
            {
                Load(player.GameId);
                s_LastTime = player.GameTime;
                s_Rebase = true;
                s_NextScan = 0;
            }
            // An earlier save of the same playthrough: alerts already passed there are not shown again.
            if (player.GameTime < s_LastTime - TimeSpan.FromHours(1)) s_Rebase = true;
            // Under a window of another mod of the collection alerts wait: they come once it closes.
            if (Time.unscaledTime < s_NextScan || Skipper.Running || ModWindows.ModWindow.OtherOpen(Main.Mod.Info.Id)) return;
            s_NextScan = Time.unscaledTime + ScanInterval;
            Scan();
        }

        // The clock went back: alerts still ahead are armed again on the next scan.
        internal static void Rearm()
        {
            s_Rebase = true;
        }

        internal static void Scan()
        {
            var player = Game.Instance.Player;
            TimeSpan now = player.GameTime;
            double elapsed = Math.Max(0, (now - s_LastTime).TotalHours);
            s_LastTime = now;
            Current = Timers.Collect();
            bool changed = false;

            if (Main.Settings.AutoTrackCritical)
            {
                foreach (var t in Current)
                {
                    if (!t.Critical || t.Remaining <= TimeSpan.Zero || IsTracked(t.Key) || s_Data.Dismissed.Contains(t.Key)) continue;
                    Add(t, true);
                    changed = true;
                }
            }

            var alerts = new List<Alert>();
            foreach (var tracked in s_Data.Tracked.ToList())
            {
                var t = Current.FirstOrDefault(x => x.Key == tracked.Key);
                if (t == null)
                {
                    // Gone: it ran out when it was about due, otherwise the player resolved it or it was cancelled.
                    if (!s_Rebase && tracked.Alerts.Contains(0) && !tracked.Fired.Contains(0) && tracked.LastRemaining <= elapsed + 2)
                        alerts.Add(new Alert { Key = tracked.Key, Text = string.Format(S.Gone, tracked.Name), Final = true });
                    Remove(tracked.Key);
                    changed = true;
                    continue;
                }
                tracked.Name = t.Name;
                tracked.LastRemaining = t.Remaining.TotalHours;
                // Another save or a rewind: the alerts still ahead are armed again, the ones already passed are not shown.
                if (s_Rebase)
                {
                    MarkPassed(tracked, tracked.LastRemaining);
                    changed = true;
                    continue;
                }
                double left = tracked.LastRemaining - tracked.DeadlineHours;
                int crossed = int.MaxValue;
                foreach (int a in tracked.Alerts)
                {
                    if (tracked.Fired.Contains(a) || left > a) continue;
                    tracked.Fired.Add(a);
                    crossed = Math.Min(crossed, a);
                    changed = true;
                }
                if (crossed == int.MaxValue || s_Rebase) continue;
                string text;
                if (left <= 0)
                    text = tracked.DeadlineHours > 0 ? string.Format(S.DeadlineDue, t.Short) : string.Format(S.Due, t.Short);
                else
                    text = tracked.DeadlineHours > 0 ? string.Format(S.LeftToDeadline, t.Short, Timers.Period(TimeSpan.FromHours(left))) : t.Line;
                if (t.Name != t.Short) text += "\n    <size=19>" + t.Name + "</size>";
                alerts.Add(new Alert { Key = tracked.Key, Text = text });
            }
            s_Rebase = false;
            if (changed || alerts.Count > 0) Save();
            if (alerts.Count > 0)
            {
                Main.Log("alerts: " + string.Join(" | ", alerts.Select(a => a.Text).ToArray()));
                AlertWindow.Show(alerts);
            }
        }

        // The timer moved (a coefficient changed): alerts already passed by its new time are not shown.
        internal static void Rebase(GameTimer t)
        {
            var tracked = Get(t.Key);
            if (tracked == null) return;
            tracked.LastRemaining = t.Remaining.TotalHours;
            MarkPassed(tracked, tracked.LastRemaining);
        }

        static void MarkPassed(TrackedTimer tracked, double remaining)
        {
            double left = remaining - tracked.DeadlineHours;
            tracked.Fired = tracked.Alerts.Where(a => left <= a).ToList();
        }

        internal static TrackedTimer Add(GameTimer t, bool auto)
        {
            var tracked = Get(t.Key);
            if (tracked != null) return tracked;
            tracked = new TrackedTimer
            {
                Key = t.Key,
                Name = t.Name,
                Alerts = Main.Settings.DefaultAlerts.ToList(),
                Critical = t.Critical,
                LastRemaining = t.Remaining.TotalHours
            };
            MarkPassed(tracked, tracked.LastRemaining);
            s_Data.Tracked.Add(tracked);
            s_Data.Dismissed.Remove(t.Key);
            if (!auto) Save();
            Main.Log((auto ? "auto-tracking " : "tracking ") + t.Name);
            return tracked;
        }

        internal static void Remove(string key)
        {
            s_Data.Tracked.RemoveAll(t => t.Key == key);
        }

        internal static void Untrack(string key)
        {
            var tracked = Get(key);
            if (tracked == null) return;
            Remove(key);
            if (tracked.Critical && !s_Data.Dismissed.Contains(key)) s_Data.Dismissed.Add(key);
            Save();
        }

        internal static void Changed(TrackedTimer tracked)
        {
            MarkPassed(tracked, tracked.LastRemaining);
            Save();
        }

        static string Folder
        {
            get { return Path.Combine(Main.Mod.Path, "Tracking"); }
        }

        // The game sets JsonConvert.DefaultSettings to its own (only its marked members, references by $id): with them our
        // classes come out empty. Our own settings, every public field.
        static readonly JsonSerializerSettings Json = new JsonSerializerSettings
        {
            ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver(),
            PreserveReferencesHandling = PreserveReferencesHandling.None,
            TypeNameHandling = TypeNameHandling.None,
            ObjectCreationHandling = ObjectCreationHandling.Replace,
        };

        static void Load(string gameId)
        {
            s_GameId = gameId;
            s_Data = new TrackingData();
            try
            {
                string file = Path.Combine(Folder, gameId + ".json");
                if (File.Exists(file)) s_Data = JsonConvert.DeserializeObject<TrackingData>(File.ReadAllText(file), Json) ?? new TrackingData();
            }
            catch (Exception e)
            {
                Main.Error(e, "loading tracked deadlines");
            }
            // Reminders of earlier versions ("rm:" keys) are gone with them.
            if (s_Data.Tracked.RemoveAll(t => t.Key != null && t.Key.StartsWith("rm:")) > 0) Save();
            Main.Log(string.Format("playthrough {0}: {1} tracked", gameId, s_Data.Tracked.Count));
        }

        static void Save()
        {
            if (s_GameId == null) return;
            try
            {
                Directory.CreateDirectory(Folder);
                File.WriteAllText(Path.Combine(Folder, s_GameId + ".json"), JsonConvert.SerializeObject(s_Data, Formatting.Indented, Json));
            }
            catch (Exception e)
            {
                Main.Error(e, "saving tracked deadlines");
            }
        }
    }

    class Alert
    {
        public string Key;
        public string Text;
        public bool Final;
    }
}
