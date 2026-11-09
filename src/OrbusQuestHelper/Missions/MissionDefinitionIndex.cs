using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace OrbusQuestHelper;

/// <summary>
/// Optional read-only mission definition index built from the user's OWN
/// locally installed official Community Edition server content/missions.
/// No Orbus content/assets are shipped with this plugin or uploaded.
/// Definitions are hints for labels ONLY. Live game objects always own counts.
/// </summary>
internal static class MissionDefinitionIndex
{
    private static readonly Dictionary<string, string> Actions =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, string> LocalItemNames =
        new(StringComparer.Ordinal);
    private static readonly Dictionary<string, (double Bronze, double Silver,
        double Gold)> TierIncrements =
        new(StringComparer.OrdinalIgnoreCase);
    // Official mission arrays may omit "id" entirely. Match each
    // version-specific mission description to a unique definition instead
    // of manufacturing an unsafe captureN ID.
    private static readonly Dictionary<string, string> ActionsByDescription =
        new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, (double Bronze, double Silver,
        double Gold)> TiersByDescription =
        new(StringComparer.OrdinalIgnoreCase);

    internal static int EntryCount => Actions.Count +
        ActionsByDescription.Count;

    internal static bool TryGetTierIncrements(string id, out double bronze,
        out double silver, out double gold)
    {
        bronze = silver = gold = double.NaN;
        if (!TierIncrements.TryGetValue(id, out var tiers)) return false;
        bronze = tiers.Bronze;
        silver = tiers.Silver;
        gold = tiers.Gold;
        return true;
    }

    internal static bool TryGetAction(string missionId, out string action)
    {
        if (Actions.TryGetValue(missionId, out string? value))
        {
            action = value;
            return true;
        }
        action = "";
        return false;
    }

    internal static bool TryGetTierIncrements(string? id,
        string? description, out double bronze, out double silver,
        out double gold)
    {
        if (!string.IsNullOrWhiteSpace(description) &&
            TiersByDescription.TryGetValue(description.Trim(),
                out var named))
        {
            (bronze, silver, gold) = named;
            return true;
        }
        return TryGetTierIncrements(id ?? "", out bronze,
            out silver, out gold);
    }

    internal static bool TryGetAction(string? id, string? description,
        out string action)
    {
        if (!string.IsNullOrWhiteSpace(description) &&
            ActionsByDescription.TryGetValue(description.Trim(),
                out string? named))
        {
            action = named;
            return true;
        }
        return TryGetAction(id ?? "", out action);
    }

    internal static (int Loaded, int Duplicates) LoadFromDirectory(string? folder)
    {
        Actions.Clear();
        LocalItemNames.Clear();
        TierIncrements.Clear();
        ActionsByDescription.Clear();
        TiersByDescription.Clear();
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return (0, 0);

        // Official server layout: content/missions/*.json and
        // content/items/item-serverdata.txt. Never copy or publish these.
        // A local-only id -> display name lookup lets a potion id such as
        // "100" resolve to its real item rather than "Drink potions".
        ReadLocalItemNames(Path.Combine(folder, "..", "items",
            "item-serverdata.txt"));

        var found = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        var foundTiers = new Dictionary<string, (double, double, double)>(
            StringComparer.OrdinalIgnoreCase);
        var duplicated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var foundDescriptions = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        var descriptionTiers = new Dictionary<string,
            (double Bronze, double Silver, double Gold)>(
            StringComparer.OrdinalIgnoreCase);
        var duplicatedDescriptions = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);
        var idlessCount = 0;

        string[] files;
        try
        {
            files = Directory.GetFiles(folder, "*.json",
                SearchOption.TopDirectoryOnly);
        }
        catch { return (0, 0); }

        foreach (string path in files.OrderBy(p => p).Take(64))
        {
            try
            {
                var info = new FileInfo(path);
                if (info.Length > 2_000_000) continue;
                using JsonDocument file = JsonDocument.Parse(
                    File.ReadAllText(path));
                if (file.RootElement.ValueKind != JsonValueKind.Array)
                    continue;

                foreach (JsonElement mission in file.RootElement.EnumerateArray())
                {
                    if (!Field(mission, "type", out string type))
                        continue;
                    string action = BuildAction(type, mission);
                    if (action.Length == 0) continue;

                    var tiers = (Bronze: Tier(mission, "bronze"),
                        Silver: Tier(mission, "silver"),
                        Gold: Tier(mission, "gold"));

                    bool hasValidId = Field(mission, "id", out string id) &&
                        id.Length is > 0 and <= 100 &&
                        id.All(c => char.IsLetterOrDigit(c) ||
                            c == '-' || c == '_');
                    if (hasValidId)
                    {
                        if (found.ContainsKey(id)) duplicated.Add(id);
                        else { found[id] = action; foundTiers[id] = tiers; }
                    }

                    if (Field(mission, "description",
                            out string description) &&
                        description.Length is > 0 and <= 2000)
                    {
                        string key = description.Trim();
                        if (foundDescriptions.ContainsKey(key))
                            duplicatedDescriptions.Add(key);
                        else
                        {
                            foundDescriptions[key] = action;
                            descriptionTiers[key] = tiers;
                        }
                        if (!hasValidId) idlessCount++;
                    }
                }
            }
            catch (Exception)
            {
                // A version-mismatched or malformed local file must never
                // interrupt the VR plugin's normal startup or mission UI.
            }
        }

        foreach (var pair in found)
        {
            if (duplicated.Contains(pair.Key)) continue;
            Actions[pair.Key] = pair.Value;
            TierIncrements[pair.Key] = foundTiers[pair.Key];
        }

        foreach (var pair in foundDescriptions)
        {
            if (duplicatedDescriptions.Contains(pair.Key)) continue;
            ActionsByDescription[pair.Key] = pair.Value;
            TiersByDescription[pair.Key] = descriptionTiers[pair.Key];
        }

        // Account for definitions identified only by description.
        return (Actions.Count + idlessCount -
                duplicatedDescriptions.Count, duplicated.Count +
                duplicatedDescriptions.Count);
    }

    private static bool Field(JsonElement node, string name, out string value)
    {
        value = "";
        if (node.ValueKind != JsonValueKind.Object ||
            !node.TryGetProperty(name, out JsonElement item) ||
            item.ValueKind != JsonValueKind.String)
            return false;
        value = item.GetString() ?? "";
        return true;
    }

    private static double Tier(JsonElement mission, string name)
    {
        if (!mission.TryGetProperty(name, out JsonElement tier) ||
            tier.ValueKind != JsonValueKind.Object ||
            !tier.TryGetProperty("requirement", out JsonElement requirement) ||
            requirement.ValueKind != JsonValueKind.Number ||
            !requirement.TryGetDouble(out double value) ||
            !double.IsFinite(value) || value <= 0)
            return double.NaN;
        return value;
    }

    private static string BuildAction(string type, JsonElement mission)
    {
        string verb = type.ToLowerInvariant() switch
        {
            "kills" => "Defeat",
            "capture" => "Capture",
            "harvest" => "Gather",
            "brewpotion" => "Brew",
            "consume" => "Drink",
            "fishcaught" => "Catch",
            "fishcaughtweight" => "Catch",
            "equipmentsalvage" => "Salvage",
            "earndram" => "Earn",
            "racing" => "Complete",
            "eventparticipation" => "Complete",
            "eventgold" => "Earn",
            "battlegroundskills" => "Defeat",
            "battlegroundscaptures" => "Capture",
            "battlegroundsgamesplayed" => "Play",
            "battlegroundsgameswon" => "Win",
            _ => ""
        };
        if (verb.Length == 0) return "";

        string generic = type.ToLowerInvariant() switch
        {
            "kills" => "monsters",
            "capture" => "critters",
            "harvest" => "materials",
            "brewpotion" or "consume" => "potions",
            "fishcaught" or "fishcaughtweight" => "fish",
            "equipmentsalvage" => "equipment",
            "earndram" => "dram",
            "racing" => "dragon races",
            "eventparticipation" => "public events",
            "eventgold" => "event gold",
            "battlegroundskills" => "battleground opponents",
            "battlegroundscaptures" => "battleground objectives",
            "battlegroundsgamesplayed" or "battlegroundsgameswon" =>
                "battleground matches",
            _ => "objectives"
        };

        // The official server definitions include objective ids such as
        // lizard, lizard-rare, squirrel, and monster-template identifiers.
        // Only a small, verified vocabulary is translated; no invented
        // creature or item name is inferred from opaque/numeric IDs.
        if (mission.TryGetProperty("checklist", out JsonElement list) &&
            list.ValueKind == JsonValueKind.Array)
        {
            var targets = new HashSet<string>(StringComparer.Ordinal);
            foreach (JsonElement item in list.EnumerateArray())
            {
                if (!item.TryGetProperty("ids", out JsonElement ids) ||
                    ids.ValueKind != JsonValueKind.Array)
                    continue;
                foreach (JsonElement entry in ids.EnumerateArray())
                {
                    if (!Field(entry, "id", out string template)) continue;
                    string? readable = ReadableTarget(template, type);
                    if (readable != null) targets.Add(readable);
                }
            }
            if (targets.Count == 1)
                generic = targets.First();
            else if (targets.Count == 2 &&
                     targets.All(s => s.Length <= 24))
                generic = string.Join(" or ", targets.OrderBy(x => x));
        }

        return verb + " " + generic;
    }

    private static string? ReadableTarget(string? template, string type)
    {
        if (template != null &&
            LocalItemNames.TryGetValue(template, out string? item))
        {
            // Capture/gather use items as nouns; for drinking/brewing
            // pluralize a counted "Potion" as a useful action label.
            if ((type == "consume" || type == "brewpotion") &&
                item.EndsWith(" Potion", StringComparison.OrdinalIgnoreCase))
                return item + "s";
            return item;
        }
        return template?.ToLowerInvariant() switch
        {
            "lizard" or "lizard-rare" => "lizards",
            "squirrel" or "squirrel-rare" => "squirrels",
            "mouse" or "mouse-rare" => "mice",
            "skunk" or "skunk-rare" => "skunks",
            "frog" or "frog-rare" => "frogs",
            "bird" or "bird-rare" => "Parathime Finches",
            "sapling" => "Sapling Stafrushers",
            "cnrusherexlow" => "Sapling Barkers",
            "cnrusher" => "Stafrushers",
            "cnrusherex" => "Barking Stafrushers",
            _ => null
        };
    }

    private static void ReadLocalItemNames(string path)
    {
        try
        {
            var info = new FileInfo(path);
            if (!info.Exists || info.Length > 4_000_000) return;
            foreach (string line in File.ReadLines(path).Skip(1).Take(10000))
            {
                int first = line.IndexOf(',');
                if (first <= 0 || first > 12) continue;
                string id = line.Substring(0, first).Trim();
                if (!id.All(char.IsDigit)) continue;
                string remainder = line.Substring(first + 1);
                string name = ReadSecondCsvColumn(remainder);
                if (name.Length is > 0 and < 100)
                    LocalItemNames[id] = name;
            }
        }
        catch
        {
            LocalItemNames.Clear();
        }
    }

    private static string ReadSecondCsvColumn(string remainder)
    {
        if (remainder.Length == 0) return "";
        if (remainder[0] != '"')
        {
            int end = remainder.IndexOf(',');
            return (end >= 0 ? remainder.Substring(0, end) :
                remainder).Trim();
        }

        var name = new System.Text.StringBuilder();
        for (int i = 1; i < remainder.Length; i++)
        {
            if (remainder[i] == '"')
            {
                if (i + 1 < remainder.Length && remainder[i + 1] == '"')
                {
                    name.Append('"');
                    i++;
                    continue;
                }
                break;
            }
            name.Append(remainder[i]);
        }
        return name.ToString().Trim();
    }
}
