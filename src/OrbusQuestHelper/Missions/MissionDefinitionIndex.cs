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
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder))
            return (0, 0);

        var found = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        var duplicated = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (string path in Directory.GetFiles(folder, "*.json",
                     SearchOption.TopDirectoryOnly).OrderBy(p => p).Take(64))
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
                    string? readable = ReadableTarget(template);
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

    private static string? ReadableTarget(string? template) =>
        template?.ToLowerInvariant() switch
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
