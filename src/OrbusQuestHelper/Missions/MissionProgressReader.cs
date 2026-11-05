using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace OrbusQuestHelper;

/// <summary>
/// Read-only player mission checklist access, using class/field mappings
/// verified against BOTH the un-obfuscated Community dump and the
/// Renewed/Spades obfuscated dump. No network calls or quest state changes.
/// </summary>
internal static class MissionProgressReader
{
    internal sealed record ObjectiveLine(
        string MissionId, string Name, int Current, int Required);

    private static readonly Regex Markup = new(
        @"<[^>]*>", RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(50));

    internal static object? CurrentCharacter()
    {
        Type? type = PowerUiRuntime.FindType("MagicalActual.PlayerCharacter");
        if (type != null)
        {
            object? character = PowerUiRuntime.Get(type, "Instance");
            if (character != null) return character;
        }

        // Confirmed mapping: MagicalActual.PlayerCharacter -> ceh;
        // static PlayerCharacter.Instance -> ceh.blfr.
        type = PowerUiRuntime.FindType("ceh");
        return type == null ? null : PowerUiRuntime.Get(type, "blfr");
    }

    internal static object? CurrentTopHud()
    {
        object? player = CurrentCharacter();
        if (player == null) return null;

        // PlayerCharacter.myPlayerAvatar -> ceh.betm (type cbn).
        object? avatar = PowerUiRuntime.Get(player, "myPlayerAvatar") ??
                         PowerUiRuntime.Get(player, "betm");
        // PlayerAvatar.topHud -> cbn.bebt (type clf).
        return PowerUiRuntime.Get(avatar, "topHud") ??
               PowerUiRuntime.Get(avatar, "bebt");
    }

    internal static object? CurrentLevelLabel()
    {
        object? hud = CurrentTopHud();
        // TopHud.levelLabel -> clf.bhch (type ft).
        return PowerUiRuntime.Get(hud, "levelLabel") ??
               PowerUiRuntime.Get(hud, "bhch");
    }

    internal static object? ReadMission(string id)
    {
        if (!IsValid(id)) return null;
        object? player = CurrentCharacter();
        if (player == null) return null;

        // Community PlayerCharacter.GetMission(string);
        // Spades ceh.ljn(string), matched by field/method layout.
        object? mission = PowerUiRuntime.Call(player, "GetMission", id) ??
                          PowerUiRuntime.Call(player, "ljn", id);
        if (mission != null) return mission;

        // Defensive read-only fallback: PlayerCharacter.myMissions ->
        // ceh.bfad -> MagicalActual.Missions.missions.
        object? all = PowerUiRuntime.Get(player, "myMissions") ??
                      PowerUiRuntime.Get(player, "bfad");
        foreach (object entry in PowerUiRuntime.Enumerate(
                     PowerUiRuntime.Get(all, "missions"), 220))
        {
            if (string.Equals(PowerUiRuntime.Text(entry, "id"),
                id, StringComparison.Ordinal))
                return entry;
        }
        return null;
    }

    private static bool IsValid(string? id) =>
        !string.IsNullOrEmpty(id) && id.Length <= 100 &&
        id.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');

    private static bool TryNumber(object? source, out double value)
    {
        value = 0;
        if (source == null) return false;
        try
        {
            value = Convert.ToDouble(source, CultureInfo.InvariantCulture);
            return double.IsFinite(value);
        }
        catch { return false; }
    }

    private static string CleanObjectiveName(string raw)
    {
        string value = raw;
        try { value = Markup.Replace(value, " "); }
        catch { return "Objective"; }
        value = Regex.Replace(value, @"\s+", " ").Trim();
        // Never infer an animal name from the mission ID. Use only the
        // game's own checklist description; make it compact for the HUD.
        if (value.Length > 38) value = value.Substring(0, 35) + "...";
        return value.Length == 0 ? "Objective" : value;
    }

    internal static List<ObjectiveLine> ReadPinnedObjectives(
        IReadOnlyList<string> ids, out int resolvedMissions)
    {
        resolvedMissions = 0;
        var result = new List<ObjectiveLine>();
        foreach (string id in ids.Take(MissionPinStore.MaxPinned))
        {
            object? mission = ReadMission(id);
            if (mission == null) continue;
            resolvedMissions++;

            // The Community AND Renewed/Spades MagicalActual.ChecklistItem
            // both retain: description, timesToKill, killCount, completed.
            foreach (object item in PowerUiRuntime.Enumerate(
                         PowerUiRuntime.Get(mission, "checklist"), 12))
            {
                if (!TryNumber(PowerUiRuntime.Get(item, "timesToKill"),
                        out double target) ||
                    !TryNumber(PowerUiRuntime.Get(item, "killCount"),
                        out double current) ||
                    target <= 0 || target > 100000)
                    continue;

                int required = (int)Math.Round(target);
                int captured = (int)Math.Clamp(Math.Floor(current), 0, required);
                string description = CleanObjectiveName(
                    PowerUiRuntime.Text(item, "description"));

                result.Add(new ObjectiveLine(id, description, captured, required));
                if (result.Count >= 9) return result;
            }
        }
        return result;
    }
}
