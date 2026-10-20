using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;

namespace OrbusQuestHelper;

/// <summary>
/// Phase 1 of Quest Tracking: inspect the game's OWN current mission records
/// on the Unity main thread, once per session after the native Missions HTML
/// exists. Strictly read-only: no quest writes, no prefab/HUD, no DOM edits.
/// This exists so we can verify which progress fields are actually usable
/// on Community and Spades before adding Track / Untrack or a live counter.
/// </summary>
internal static class MissionTrackingProbe
{
    private static readonly Regex MissionIds = new(
        @"(?:^|\s)data-missionid-(?<id>[A-Za-z0-9_-]+)(?=[\s'""<>]|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(100));
    private static readonly string[] MissionFields =
        { "status", "progress", "checklist" };
    private static readonly string[] ObjectiveFields =
        { "progress", "current", "count", "amount", "required",
          "target", "total", "completed", "status" };

    private static bool _ran;
    internal static bool HasRun => _ran;

    // Deterministic, limited and independent of the user's particular
    // missions or of the current activity/region sorting.
    internal static IReadOnlyList<string> SampleMissionIds(string html, int max = 3)
    {
        if (html.Length > 1_000_000 || max <= 0) return Array.Empty<string>();
        return MissionIds.Matches(html).Cast<Match>()
            .Select(match => match.Groups["id"].Value)
            .Where(id => id.Length > 0 && id.Length <= 100 &&
                !IsGeneratedWeeklyId(id))
            .Distinct(StringComparer.Ordinal)
            .Take(Math.Min(max, 3))
            .ToArray();
    }

    private static bool IsGeneratedWeeklyId(string id)
    {
        // Spades weekly quest IDs start with a long timestamp-like prefix.
        int digits = 0;
        while (digits < id.Length && char.IsDigit(id[digits])) digits++;
        return digits >= 10 && digits < id.Length && id[digits] == '-';
    }

    internal static void TryRun()
    {
        if (_ran) return;
        string? original = MissionHtmlPatch.OriginalMissionsHtmlForTracking;
        if (string.IsNullOrWhiteSpace(original)) return;
        _ran = true; // never repeatedly probe native interop during gameplay

        try
        {
            var ids = SampleMissionIds(original);
            Type? playerType =
                PowerUiRuntime.FindType("MagicalActual.PlayerCharacter") ??
                PowerUiRuntime.FindType("PlayerCharacter");
            Type? missionType = PowerUiRuntime.FindType("MagicalActual.Mission") ??
                PowerUiRuntime.FindType("Mission");

            Plugin.Logger.LogInfo("[QuestHelper.TrackProbe] Initial read-only " +
                "mission data check: sample=" + ids.Count +
                "; PlayerCharacter=" + (playerType?.FullName ?? "not found") +
                "; Mission=" + (missionType?.FullName ?? "not found") +
                ". No tracking HUD or quest-state changes are active.");

            if (playerType == null || ids.Count == 0)
            {
                Plugin.Logger.LogInfo("[QuestHelper.TrackProbe] Native " +
                    "GetMission data requires edition-specific lookup. " +
                    "Not inventing quest progress.");
                return;
            }

            object? player = PowerUiRuntime.Get(playerType, "Instance");
            string[] methodShapes = playerType.GetMethods(
                BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.Instance | BindingFlags.Static)
                .Where(m => m.Name == "GetMission")
                .Take(5)
                .Select(m => m.Name + "(" + string.Join(",",
                    m.GetParameters().Select(p => p.ParameterType.Name)) + ")")
                .ToArray();

            Plugin.Logger.LogInfo("[QuestHelper.TrackProbe] Native accessor: " +
                "Instance=" + (player == null ? "unavailable" : "available") +
                "; GetMission overloads=[" + string.Join("; ", methodShapes) + "].");

            if (player == null || methodShapes.Length == 0) return;
            foreach (string id in ids)
            {
                try
                {
                    // Read-only getter used in the existing MissionInfoReader.
                    // Never invoke BuyVendorMission, TurnInMission, etc.
                    object? mission = PowerUiRuntime.Call(player, "GetMission", id);
                    if (mission == null)
                    {
                        Plugin.Logger.LogInfo("[QuestHelper.TrackProbe] id=" + id +
                            " GetMission(string)=unavailable");
                        continue;
                    }

                    string fields = string.Join("; ", MissionFields.Select(
                        name => name + "=" + DescribeValue(
                            PowerUiRuntime.Get(mission, name))));
                    var objectives = PowerUiRuntime.Enumerate(
                        PowerUiRuntime.Get(mission, "checklist"), 5);
                    Plugin.Logger.LogInfo("[QuestHelper.TrackProbe] id=" + id +
                        " type=" + mission.GetType().FullName +
                        "; " + fields +
                        "; checklistSample=" + objectives.Count);

                    if (objectives.Count > 0)
                    {
                        object first = objectives[0];
                        var objectiveFields = ObjectiveFields
                            .Select(name => (Name: name,
                                Value: PowerUiRuntime.Get(first, name)))
                            .Where(item => item.Value != null)
                            .Take(7)
                            .Select(item => item.Name + "=" +
                                DescribeValue(item.Value))
                            .ToArray();
                        Plugin.Logger.LogInfo("[QuestHelper.TrackProbe] id=" + id +
                            " firstObjectiveType=" + first.GetType().FullName +
                            " fields=[" + string.Join("; ", objectiveFields) + "]");
                    }
                }
                catch (Exception ex)
                {
                    Plugin.Logger.LogWarning("[QuestHelper.TrackProbe] id=" + id +
                        " native read failed safely: " + ex.GetType().Name);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[QuestHelper.TrackProbe] Read-only " +
                "inspection unavailable: " + ex.GetType().Name);
        }
    }

    private static string DescribeValue(object? value)
    {
        if (value == null) return "unavailable";
        Type type = value.GetType();
        // Never log arbitrary strings, descriptions, player names or objects.
        if (value is string text) return "string(" + Math.Min(text.Length, 10000) + ")";
        if (value is bool boolean) return boolean.ToString();
        if (value is byte or sbyte or short or ushort or int or uint or long
            or ulong or float or double or decimal)
            return Convert.ToString(value, CultureInfo.InvariantCulture) ?? type.Name;
        if (type.IsEnum) return type.Name + "(" +
            Convert.ToInt64(value, CultureInfo.InvariantCulture) + ")";
        return type.FullName ?? type.Name;
    }
}
