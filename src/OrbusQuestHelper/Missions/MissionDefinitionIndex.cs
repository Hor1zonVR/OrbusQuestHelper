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

    internal static int EntryCount => Actions.Count;

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

    internal static (int Loaded, int Duplicates) LoadFromDirectory(string? folder)
    {
        Actions.Clear();
        LocalItemNames.Clear();
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
        var duplicated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

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
                    if (!Field(mission, "id", out string id) ||
                        id.Length == 0 || id.Length > 100 ||
                        !id.All(c => char.IsLetterOrDigit(c) ||
                            c == '-' || c == '_'))
                        continue;
                    if (!Field(mission, "type", out string type))
                        continue;
                    string action = BuildAction(type, mission);
                    if (action.Length == 0) continue;
                    if (found.ContainsKey(id)) duplicated.Add(id);
                    else found[id] = action;
                }
            }
            catch (Exception)
            {
                // A version-mismatched or malformed local file must never
                // interrupt the VR plugin's normal startup or mission UI.
            }
        }

        foreach (var pair in found)
            if (!duplicated.Contains(pair.Key))
                Actions[pair.Key] = pair.Value;

        return (Actions.Count, duplicated.Count);
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
