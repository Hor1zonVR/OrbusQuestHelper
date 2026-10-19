using System;
using System.Collections.Generic;
using System.Linq;

namespace OrbusQuestHelper;

/// <summary>
/// Side-effect-free classifier for original game mission descriptors.
/// This never guesses a geographic region from a level, UI section position,
/// vendor or mission frequency. The live DOM integration will supply data.
/// </summary>
internal static class MissionGrouping
{
    internal sealed record Result(
        string Region,
        string Activity,
        bool RegionVerified,
        bool ActivityVerified);

    private static readonly (string Name, string[] Keys)[] Regions =
    {
        ("Highsteppe", new[] { "highsteppe", "highstepe" }),
        ("Flooded Rainforest", new[] { "flooded rainforest", "flooded rain forest", "rainforest" }),
        ("Lamavora", new[] { "lamavora" }),
        ("Hulthine's Basin", new[] { "hulthine", "hulthine's basin" }),
        ("Wastelands", new[] { "wasteland", "wastelands" })
    };

    // Task categories are objective-based. "Weekly" is not a task or region.
    // Prefer the game's own Mission.type when it clearly names an activity;
    // supplement with descriptive text only if a category is unambiguous.
    private static readonly (string Name, string[] Keys)[] Activities =
    {
        ("Monster Hunting", new[] { "monster", "hunt", "defeat", "kill", "slay", "combat" }),
        ("Fishing", new[] { "fish", "fishing", "lure" }),
        ("Gathering", new[] { "gather", "gathering", "harvest", "herb", "mine ore" }),
        ("Salvaging", new[] { "salvag", "scavenge" }),
        ("Critter Capture", new[] { "critter", "capture" }),
        ("Dragon Racing", new[] { "dragon race", "dragon racing", "race course" }),
        ("Public Events", new[] { "public event", "world event", "event mission" }),
        ("Dungeons", new[] { "dungeon", "shard", "crypt", "broken halls" }),
        ("PvP", new[] { "pvp", "battleground" })
    };

    public static Result Classify(string? objectiveZone, string? missionType,
        string? description)
    {
        var zone = Normalise(objectiveZone);
        var type = Normalise(missionType);
        var text = Normalise(description);

        // Objective.zone is stronger evidence than generic mission text.
        var explicitZones = Identify(Regions, zone);
        var detectedZones = explicitZones.Count != 0 ? explicitZones :
            Identify(Regions, text);

        string region;
        bool regionKnown;
        if (detectedZones.Count == 1)
        {
            region = detectedZones[0];
            regionKnown = true;
        }
        else if (detectedZones.Count > 1)
        {
            region = "Multiple Regions";
            regionKnown = true;
        }
        else
        {
            region = "Unassigned Region";
            regionKnown = false;
        }

        // A clear mission.type wins over textual keywords; a description
        // mentioning fishing AND defeating a monster should not be treated as
        // an unambiguous single task when no type is provided.
        var explicitActivities = Identify(Activities, type);
        var textActivities = explicitActivities.Count != 0 ? explicitActivities :
            Identify(Activities, text);
        string activity = textActivities.Count == 1 ? textActivities[0] :
            "Other Tasks";
        return new Result(region, activity, regionKnown,
            textActivities.Count == 1);
    }

    private static string Normalise(string? s) =>
        (s ?? string.Empty).Trim().ToLowerInvariant();

    private static List<string> Identify(
        (string Name, string[] Keys)[] options, string haystack)
    {
        var results = new List<string>();
        if (haystack.Length == 0) return results;
        foreach (var option in options)
        {
            if (option.Keys.Any(key =>
                haystack.IndexOf(key, StringComparison.OrdinalIgnoreCase) >= 0))
                results.Add(option.Name);
        }
        return results;
    }
}
