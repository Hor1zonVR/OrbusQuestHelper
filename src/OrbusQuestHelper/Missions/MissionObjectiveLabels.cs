using System;
using System.Text.RegularExpressions;

namespace OrbusQuestHelper;

/// <summary>
/// Converts game-owned checklist text into concise ACTION labels.
/// The mission catalogue knows activity categories, not every exact species
/// or item: when the native objective has no text, show the verified activity
/// instead of presenting a story paragraph as an objective.
/// </summary>
internal static class MissionObjectiveLabels
{
    private static readonly Regex Markup = new(
        @"<[^>]*>", RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));
    private static readonly Regex Counter = new(
        @"\s*\(?\d+\s*/\s*\d+\)?\s*$",
        RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));
    private static readonly Regex Action = new(
        @"^(Kill|Defeat|Hunt|Capture|Catch|Trap|Collect|Gather|Harvest|Drink|Brew|Use|Complete|Win|Participate in|Salvage|Find|Fish|Race)\s+(?:\d+\s+)?(.+)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    private static string Normalize(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        string text = raw.Length > 400 ? raw.Substring(0, 400) : raw;
        try
        {
            text = Markup.Replace(text, " ");
            text = Counter.Replace(text, "");
            text = Regex.Replace(text, @"(?<=[a-z])(?=[A-Z])", " ");
            text = Regex.Replace(text, @"[_\s]+", " ").Trim(' ', ':', ';');
        }
        catch { return ""; }
        return text.Length > 60 ? text.Substring(0, 57).TrimEnd() + "..." : text;
    }

    private static string ActionFromNative(string? raw, bool checklist)
    {
        string text = Normalize(raw);
        if (text.Length == 0) return "";
        Match action = Action.Match(text);
        if (action.Success)
        {
            string verb = action.Groups[1].Value;
            string thing = action.Groups[2].Value.TrimEnd('.', '!', ' ');
            return Limit(verb + " " + thing);
        }
        // A short checklist label can be a creature/item name with no verb.
        // Do not use an unstructured Mission.description paragraph here.
        if (checklist && text.Length <= 34 &&
            !text.Contains('.') && !text.Contains('!') &&
            !text.Contains('?') && !text.Contains(','))
            return text;
        return "";
    }

    private static string Limit(string text) =>
        text.Length <= 46 ? text : text.Substring(0, 43).TrimEnd() + "...";

    // Old substring-only aliases were dangerously broad: "Keeping a
    // stockpile" is used for several unrelated BREW potions, yet it was
    // once labelled "Ruined Potions". These exceptional aliases are
    // restricted to a verified mission ID and text combination.
    private static string? VerifiedAlias(string missionId, string? story)
    {
        string description = (story ?? "").Trim()
            .Replace('\u2019', '\u0027');
        if (string.Equals(missionId, "capturestrangerfestival2024",
                StringComparison.OrdinalIgnoreCase) &&
            description.StartsWith("Now that it's known",
                StringComparison.OrdinalIgnoreCase))
            return "Lizards";
        if (string.Equals(missionId, "jester29",
                StringComparison.OrdinalIgnoreCase) &&
            description.StartsWith("It's said that Ruined Potions",
                StringComparison.OrdinalIgnoreCase))
            return "Ruined Potions";
        return null;
    }

    private static string ActivityFor(string missionId, string? missionType)
    {
        if (MissionCatalog.TryGet(missionId, out _, out string activity))
            return activity;
        return (missionType ?? "").Trim().ToLowerInvariant() switch
        {
            "kill" or "kills" or "hunter" or "hunt" => "Monster Hunting",
            "capture" => "Critter Capture",
            "fishing" or "fish" or "fishcaught" => "Catch Fish",
            "fishcaughtweight" => "Fishing by Weight",
            "harvest" or "gather" => "Gathering",
            "brewpotion" => "Brewing Potions",
            "consume" => "Using Potions",
            "equipmentsalvage" => "Salvaging Equipment",
            "earndram" => "Earning Dram",
            "eventparticipation" => "Public Events",
            "eventgold" => "Gold Events",
            "brew" or "brewing" => "Brewing Potions",
            "potion" or "use" => "Using Potions",
            "event" => "Public Events",
            "race" => "Dragon Racing",
            _ => ""
        };
    }

    internal static string Resolve(string missionId, string? checklist,
        string? missionDescription, string? missionType, out string source)
    {
        string activity = ActivityFor(missionId, missionType);
        // Native checklist entries are the most specific source of
        // sub-objectives, especially Monster killlist missions with
        // three separate enemies and independent progress counters.
        string native = ActionFromNative(checklist, true);
        if (native.Length > 0)
        {
            source = "checklist-action";
            Match m = Action.Match(native);
            return m.Success ? native :
                Limit(VerbFor(activity) + " " + native).Trim();
        }

        bool officialKnown = OfficialPriorityMissions.TryFind(missionId,
            missionType, missionDescription, out var priority);
        bool exactOfficial = officialKnown &&
            !string.IsNullOrWhiteSpace(missionDescription) &&
            OfficialPriorityMissions.HashDescription(missionDescription) ==
                priority.DescriptionHash;
        if (exactOfficial)
        {
            // Exact source description beats a local importer's generic
            // "Defeat monsters" label for an otherwise opaque enemy ID.
            source = "server-priority-description";
            return Limit(priority.Action);
        }

        // A custom local server may repurpose an established ID while
        // changing its story/target. Local server content should take
        // precedence over a weak ID-only built-in match in that case.
        if (MissionDefinitionIndex.TryGetAction(missionId,
                missionDescription, out string known))
        {
            source = "local-server-definition";
            return Limit(known);
        }

        if (officialKnown)
        {
            source = "server-priority-id";
            return Limit(priority.Action);
        }

        if (OfficialCaptureObjectives.TryFind(missionType,
                missionDescription, out var capture))
        {
            source = "server-capture-definition";
            return Limit(capture.Action);
        }

        string? alias = VerifiedAlias(missionId, missionDescription);
        if (alias != null)
        {
            source = "verified-quest-alias";
            return Limit(VerbFor(activity) + " " + alias).Trim();
        }

        native = ActionFromNative(missionDescription, false);
        if (native.Length > 0)
        {
            source = "mission-action";
            return native;
        }

        source = activity.Length > 0 ? "catalogue-activity"
            : "unclassified";
        return ActivityLabel(activity);
    }

    private static string VerbFor(string activity) => activity switch
    {
        "Monster Hunting" => "Defeat",
        "Critter Capture" => "Capture",
        "Catch Fish" or "Fishing by Weight" => "Catch",
        "Gathering" => "Gather",
        "Brewing Potions" => "Brew",
        "Using Potions" => "Use",
        "Salvaging Equipment" => "Salvage",
        _ => ""
    };

    private static string ActivityLabel(string activity) => activity switch
    {
        "Monster Hunting" => "Defeat monsters",
        "Critter Capture" => "Capture critters",
        "Catch Fish" => "Catch fish",
        "Fishing by Weight" => "Catch fish by weight",
        "Gathering" => "Gather materials",
        "Brewing Potions" => "Brew potions",
        "Using Potions" => "Use potions",
        "Salvaging Equipment" => "Salvage equipment",
        "Dragon Racing" => "Complete dragon races",
        "Public Events" => "Complete public events",
        "Gold Events" => "Earn event gold",
        "Battleground Matches" => "Play battleground matches",
        "Battleground Victories" => "Win battleground matches",
        "Battleground Kills" => "Defeat battleground opponents",
        "Battleground Captures" => "Capture battleground objectives",
        "Earning Dram" => "Earn dram",
        _ => "Complete mission objective"
    };

    internal static string CountUnit(string id,
        string? missionType = null, string? missionDescription = null)
    {
        if (OfficialPriorityMissions.TryFind(id, missionType,
                missionDescription, out var priority))
            return priority.Unit;
        string activity = ActivityFor(id, missionType);
        return activity switch
        {
            "Monster Hunting" or "Battleground Kills" => " defeated",
            "Critter Capture" or "Battleground Captures" => " captured",
            "Catch Fish" => " caught",
            "Fishing by Weight" => " weight caught",
            "Public Events" => " events completed",
            "Gold Events" => " Gold events completed",
            "Earning Dram" => " Dram earned",
            "Gathering" => " gathered",
            "Brewing Potions" => " brewed",
            "Using Potions" => " used",
            "Salvaging Equipment" => " salvaged",
            "Battleground Victories" => " wins",
            "Dragon Racing" or "Battleground Matches" => " completed",
            _ => " progress"
        };
    }
}
