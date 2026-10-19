using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;

namespace OrbusQuestHelper;

/// <summary>
/// Resolves a native vendor mission by the ID already attached to its original
/// HTML tile. Never requests, purchases, turns in, cancels or edits missions.
/// </summary>
internal static class MissionInfoReader
{
    private static readonly Regex IdClass = new(
        @"(?:^|\s)data-missionid-([^\s]+)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    internal sealed record Details(
        string Id, string Type, string Description, string Zone,
        string Checklist, bool Found)
    {
        internal MissionGrouping.Result Group => MissionGrouping.Classify(
            Zone, Type, Description + " " + Checklist);
    }

    internal static string? MissionId(object tile)
    {
        string classes = PowerUiRuntime.Text(tile, "className");
        Match match = IdClass.Match(classes);
        return match.Success ? match.Groups[1].Value.Trim() : null;
    }

    internal static Details Read(string id)
    {
        object? mission = FindNativeMission(id);
        if (mission == null)
            return new Details(id, "", "", "", "", false);

        string type = PowerUiRuntime.Text(mission, "type");
        string description = PowerUiRuntime.Text(mission, "description");
        object? checklist = PowerUiRuntime.Get(mission, "checklist");
        var text = new StringBuilder();
        string objectiveZone = "";

        foreach (object item in PowerUiRuntime.Enumerate(checklist, 80))
        {
            string line = PowerUiRuntime.Text(item, "description");
            if (!string.IsNullOrWhiteSpace(line))
            {
                if (text.Length != 0) text.Append("; ");
                text.Append(line);
            }

            // Some native mission objective types expose a real 'zone' value.
            // A missing zone is *not* evidence that the current zone applies.
            string zone = PowerUiRuntime.Text(item, "zone");
            if (!string.IsNullOrWhiteSpace(zone))
                objectiveZone = objectiveZone.Length == 0
                    ? zone : objectiveZone + " " + zone;
        }

        return new Details(id, type, description, objectiveZone,
            text.ToString(), true);
    }

    private static object? FindNativeMission(string id)
    {
        try
        {
            Type? characterType = PowerUiRuntime.FindType(
                "MagicalActual.PlayerCharacter");
            if (characterType == null) return null;
            object? character = PowerUiRuntime.Get(characterType, "Instance");
            if (character == null) return null;

            // Resolve by the original ID. Prefer GetMission(string), but use
            // the integer overload if a numeric ID is supplied by the game.
            object? mission = PowerUiRuntime.Call(character, "GetMission", id);
            if (mission != null) return mission;
            return int.TryParse(id, out int index)
                ? PowerUiRuntime.Call(character, "GetMission", index)
                : null;
        }
        catch { return null; }
    }
}
