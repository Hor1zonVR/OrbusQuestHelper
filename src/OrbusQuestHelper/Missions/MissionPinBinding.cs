using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace OrbusQuestHelper;

/// <summary>
/// Attaches native VR event listeners to our own Star / Pin controls only.
/// Original mission buttons, hover handlers, quest states and rewards are
/// never intercepted. Callbacks queue changes until next Unity Update.
/// </summary>
internal static class MissionPinBinding
{
    private static readonly Regex IdPattern = new(
        @"(?:^|\s)data-missionid-(?<id>[A-Za-z0-9_-]+)(?=\s|['""]|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));

    private static readonly List<object> Delegates = new();
    private static object? _firstControl;
    private static int _attached;
    private static bool _warned;

    internal static void TryBind()
    {
        if (!MissionHtmlPatch.IsTransformEnabled) return;
        string? original = MissionHtmlPatch.OriginalMissionsHtmlForTracking;
        if (string.IsNullOrWhiteSpace(original) || original.Length > 1_000_000)
            return;

        try
        {
            object? document = PowerUiRuntime.PlayerMenuDocument();
            MatchCollection matches = IdPattern.Matches(original);
            string[] ids = matches.Cast<Match>()
                .Select(m => m.Groups["id"].Value)
                .Distinct(StringComparer.Ordinal)
                .Take(300).ToArray();
            if (ids.Length == 0) return;

            object? first = PowerUiRuntime.FindInsideMissions(
                document, "qh-fav-" + ids[0]);
            if (first == null) return;

            // Only run the O(mission-count) bindings when the actual
            // generated buttons have been replaced by a new native page.
            if (PowerUiRuntime.Same(first, _firstControl) &&
                _attached == ids.Length * 2) return;

            _firstControl = first;
            _attached = 0;
            Delegates.Clear();
            int errors = 0;
            foreach (string id in ids)
            {
                object? star = PowerUiRuntime.FindInsideMissions(
                    document, "qh-fav-" + id);
                if (star == null)
                {
                    if (errors++ < 3)
                        Plugin.Logger.LogWarning("[QuestHelper/Pins] Star " +
                            "control missing for one mission.");
                }
                else if (PowerUiRuntime.BindMouseDown(
                        star, () => MissionPinStore.RequestFavourite(id),
                        Delegates, out string starError))
                    _attached++;
                else if (errors++ < 3)
                    Plugin.Logger.LogWarning("[QuestHelper/Pins] Star listener " +
                        "unavailable: " + starError);

                object? pin = PowerUiRuntime.FindInsideMissions(
                    document, "qh-pin-" + id);
                if (pin == null)
                {
                    if (errors++ < 3)
                        Plugin.Logger.LogWarning("[QuestHelper/Pins] Pin " +
                            "control missing for one mission.");
                }
                else if (PowerUiRuntime.BindMouseDown(
                        pin, () => MissionPinStore.RequestPin(id),
                        Delegates, out string pinError))
                    _attached++;
                else if (errors++ < 3)
                    Plugin.Logger.LogWarning("[QuestHelper/Pins] Pin listener " +
                        "unavailable: " + pinError);
            }

            if (!_warned || _attached == ids.Length * 2)
            {
                Plugin.Logger.LogInfo("[QuestHelper/Pins] Native Star/Pin " +
                    "actions bound=" + _attached + "/" + (ids.Length * 2) +
                    ". Limit=" + MissionPinStore.MaxPinned + " pinned missions.");
                _warned = _attached != ids.Length * 2;
            }
        }
        catch (Exception ex)
        {
            if (_warned) return;
            _warned = true;
            Plugin.Logger.LogWarning("[QuestHelper/Pins] Native controls " +
                "could not be bound: " + ex.GetType().Name);
        }
    }
}
