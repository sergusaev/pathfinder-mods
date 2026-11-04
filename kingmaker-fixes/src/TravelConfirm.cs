using System;
using System.Reflection;
using HarmonyLib;
using Kingmaker;
using Kingmaker.Blueprints.Root;
using Kingmaker.Globalmap;
using Kingmaker.Globalmap.State;
using Kingmaker.UI;
using Kingmaker.UI._ConsoleUI.GlobalMap;
using Kingmaker.UI.Common;
using Kingmaker.Utility;

namespace KingmakerFixes
{
    // Gamepad global map: A on a location chosen with the D-pad or the stick sets the party off at once
    // (GlobalMapUiCommonPartVM.TryEnterLocation -> TravelByDirection / TravelToMapPosition), while with the mouse a click
    // first asks, saying how long the journey takes. The same question is asked here, with the game's own text and the
    // game's own estimate (GlobalMapRules.GetPathTime); on Yes the game's code runs as it did. Going on along a journey
    // stopped half way is asked too, with the time left. A journey of one stretch, to the next point, is not asked.
    // Entering the location the party stands in, stopping a journey, the choice of an entrance and a path the game cannot find are left to the game.
    [HarmonyPatch(typeof(GlobalMapUiCommonPartVM), nameof(GlobalMapUiCommonPartVM.TryEnterLocation))]
    static class TravelConfirm
    {
        static readonly PropertyInfo Selected = AccessTools.Property(typeof(GlobalMapUiCommonPartVM), "SelectedLocation");
        static readonly FieldInfo Hovered = AccessTools.Field(typeof(GlobalMapUiCommonPartVM), "m_HoveredLocation");
        internal static readonly FieldInfo MultiEntrance = AccessTools.Field(typeof(GlobalMapRules), "m_IsMultiEntrance");
        static bool s_Confirmed;
        static bool s_Open;

        static bool Prefix(GlobalMapUiCommonPartVM __instance)
        {
            if (s_Confirmed) return true;
            if (s_Open) return false;
            try
            {
                string text = Question(__instance);
                if (text == null) return true;
                s_Open = true;
                // A frame or two later: the window takes A on its press as the map does, so shown in the same press it
                // took that press as its Yes at once and closed before it was seen.
                DelayedInvoker.InvokeInFrames(() =>
                {
                    try
                    {
                        UIUtility.ShowMessageBox(text, DialogMessageBoxBase.BoxType.Dialog, button =>
                        {
                            s_Open = false;
                            if (button != DialogMessageBoxBase.BoxButton.Yes) return;
                            s_Confirmed = true;
                            try { __instance.TryEnterLocation(); }
                            finally { s_Confirmed = false; }
                        });
                    }
                    catch (Exception e)
                    {
                        s_Open = false;
                        Main.Log.Error("travel confirmation window: " + e);
                    }
                }, 2);
                return false;
            }
            catch (Exception e)
            {
                s_Open = false;
                Main.Log.Error("travel confirmation: " + e);
                return true;
            }
        }

        static string Skip(string why)
        {
            Main.Log.Log("travel confirmation not asked: " + why);
            return null;
        }

        // The question for a journey A would start now, or null when A does something else or the journey is one stretch.
        static string Question(GlobalMapUiCommonPartVM vm)
        {
            var game = Game.Instance;
            var map = game.Player.GlobalMap;
            var rules = GlobalMapRules.Instance;
            if (!game.IsControllerGamepad || map == null || rules == null || Selected == null || Hovered == null) return null;
            if (rules.PathsVisual == null || rules.PathsVisual.LockControls) return Skip("controls locked");
            MapTravelData travel = map.TravelData;
            MapTravelData path;
            if (travel != null && !travel.Finished)
            {
                // A stops a journey under way; on one stopped half way (B, or an event on the road) it goes on along the
                // same route (GlobalMapRules.OnBreak, which does nothing in a cutscene or on the map of entrances):
                // asked as well, with the time from here to its end.
                if (travel.Walking) return null;
                if (game.CutsceneLock || (MultiEntrance != null && (bool)MultiEntrance.GetValue(rules))) return Skip("journey kept by the game");
                if (travel.To == null) return Skip("stopped journey without an end");
                path = rules.CalculatePathToPosition(travel.To) ?? travel;
            }
            else
            {
                // No check of the map of entrances here: the game sets off from it too.
                var current = map.CurrentPosition.Location != null ? rules.GetLocationObject(map.CurrentPosition.Location) : null;
                var selected = Selected.GetValue(vm, null) as GlobalMapLocation;
                var hovered = Hovered.GetValue(vm) as GlobalMapLocation;
                if (current != null && current == selected) return null;
                path = hovered != null ? rules.CalculatePathByDirection(hovered)
                    : selected != null ? rules.CalculatePathToPosition(new MapPosition(selected.Blueprint)) : null;
            }
            if (path == null) return Skip("no path");
            // A single stretch to the next point (a step with the stick) goes without the question, as many of them in a row
            // would each ask; only a journey through at least one point on the way is asked.
            if (path.Path.Count <= 1) return null;

            var texts = game.BlueprintRoot.LocalizedTexts.UserInterfacesText.GlobalMap;
            TimeSpan time = rules.GetPathTime(path);
            string span;
            if (time > TimeSpan.Zero)
            {
                span = BlueprintRoot.Instance.Calendar.GetPeriodString(time);
                if (string.IsNullOrEmpty(span)) span = texts.GlobalMapShortJourney;
            }
            else span = texts.GlobalMapUnknownJouneyDialogDesc;
            return string.Format(texts.GlobalMapJouneyDialogDesc, span);
        }
    }
}
