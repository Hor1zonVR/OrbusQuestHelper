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
        string MissionId, string Name, int Current, int Required,
        int Remaining, string GoalLabel);

    private static readonly HashSet<string> ReportedPinnedIds =
        new(StringComparer.Ordinal);
    // Read-only stage transition diagnostics. We deliberately log only
    // numeric targets and NEVER mission IDs, descriptions or player data.
    private static readonly Dictionary<string, int> LastPinnedGoal =
        new(StringComparer.Ordinal);

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

    private static string CleanObjectiveName(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        string value = raw;
        try { value = Markup.Replace(value, " "); }
        catch { return ""; }
        if (value.Length > 400) value = value.Substring(0, 400);
        value = Regex.Replace(value, @"(?<=[a-z])(?=[A-Z])", " ");
        value = Regex.Replace(value, @"[_\s]+", " ").Trim();
        // Prefer the game's objective description; fall back to the
        // *parent mission* description if the checklist has none.
        // Never invent animal names from a generic activity or ID.
        value = Regex.Replace(value,
            @"^(?:Capture|Catch|Trap|Collect|Defeat|Kill|Hunt|Find|Drink|Gather)\s+(?:\d+\s+)?",
            "", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant).Trim();
        value = Regex.Replace(value,
            @"\s*\(?\d+\s*/\s*\d+\)?\s*$", "").Trim();
        if (value.Length > 0)
        {
            string[] breaks = { " during ", " to earn ", " for the ", " in order ",
                " around the ", " as part of " };
            foreach (string stop in breaks)
            {
                int i = value.IndexOf(stop, StringComparison.OrdinalIgnoreCase);
                if (i > 7) { value = value.Substring(0, i); break; }
            }
        }
        value = value.TrimEnd(':', ';', ' ');
        if (value.Length > 30) value = value.Substring(0, 27).TrimEnd() + "...";
        return value;
    }

    private static string KnownQuestTitle(string missionDescription)
    {
        // Verified by the player's in-game Missions/quest HUD screenshots.
        // These two distinctive narrative prefixes identify the quests
        // directly. Do not gate this on the rough activity catalogue:
        // some seasonal missions can be categorised differently.
        string narrative = (missionDescription ?? "").Trim()
            .Replace('\u2019', '\u0027');
        if (narrative.StartsWith("Now that it's known",
                StringComparison.OrdinalIgnoreCase))
            return "Lizards";
        if (narrative.StartsWith("Keeping a stockpile",
                StringComparison.OrdinalIgnoreCase))
            return "Ruined Potions";
        return "";
    }

    private static bool Near(double a, double b) =>
        Math.Abs(a-b) <= Math.Max(0.05, 0.001*Math.Abs(b));

    private static (int Goal, string Tier) FindNextRewardGoal(
        object mission, int current, int checklistTarget, bool oneObjective)
    {
        // Bronze/silver/gold are GAME-OWNED requirements for reward tiers.
        // They are not necessarily a checklist target: use them only when
        // mission.progress and checklist.killCount are the same scale.
        if (!oneObjective ||
            !TryNumber(PowerUiRuntime.Get(mission, "progress"), out double progress) ||
            !Near(progress, current))
            return (checklistTarget, "");

        foreach (string tier in new[] { "bronze", "silver", "gold" })
        {
            object? reward = PowerUiRuntime.Get(mission, tier);
            if (!TryNumber(PowerUiRuntime.Get(reward, "requirement"),
                out double threshold) ||
                threshold <= current || threshold > checklistTarget)
                continue;
            int requirement = (int)Math.Ceiling(threshold);
            if (requirement > current)
                return (requirement, tier.Substring(0, 1).ToUpperInvariant() +
                    tier.Substring(1));
        }
        return (checklistTarget, "");
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
            // Many live checklist descriptions are actually empty: get the
            // human-readable title from Mission.description when possible.
            var checklist = PowerUiRuntime.Enumerate(
                PowerUiRuntime.Get(mission, "checklist"), 12);
            string nativeDescription = PowerUiRuntime.Text(mission, "description");
            string title = CleanObjectiveName(nativeDescription);
            string genericType = CleanObjectiveName(
                PowerUiRuntime.Text(mission, "type"));
            string catalogueActivity = "";
            if (MissionCatalog.TryGet(id, out _, out string activity) &&
                !string.Equals(activity, "Other Tasks",
                    StringComparison.OrdinalIgnoreCase))
                catalogueActivity = CleanObjectiveName(activity);
            string knownTitle = KnownQuestTitle(nativeDescription);
            foreach (object item in checklist)
            {
                if (!TryNumber(PowerUiRuntime.Get(item, "timesToKill"),
                        out double target) ||
                    !TryNumber(PowerUiRuntime.Get(item, "killCount"),
                        out double current) ||
                    target <= 0 || target > 100000)
                    continue;

                int required = (int)Math.Round(target);
                int captured = (int)Math.Clamp(Math.Floor(current), 0, required);
                string objectiveName = CleanObjectiveName(
                    PowerUiRuntime.Text(item, "description"));
                // Prefer the specific objective; a mission can include
                // multiple animals. Parent title is only a fallback.
                string displayName = objectiveName.Length > 0 ? objectiveName
                    : knownTitle.Length > 0 ? knownTitle
                    : title.Length > 0 ? title
                    : catalogueActivity.Length > 0 ? catalogueActivity
                    : genericType.Length > 0 ? genericType
                    : "Unlabelled quest";
                string nameSource = objectiveName.Length > 0 ? "checklist"
                    : knownTitle.Length > 0 ? "verified-quest-title"
                    : title.Length > 0 ? "mission"
                    : catalogueActivity.Length > 0 ? "catalogue"
                    : genericType.Length > 0 ? "type" : "unavailable";

                (int goal, string tier) = FindNextRewardGoal(
                    mission, captured, required, checklist.Count == 1);
                goal = Math.Clamp(goal, captured, required);
                if (LastPinnedGoal.TryGetValue(id, out int priorGoal) &&
                    goal != priorGoal)
                {
                    Plugin.Logger.LogInfo("[QuestHelper/HUD] Live pinned reward " +
                        "target changed: previous=" + priorGoal +
                        ",next=" + goal + ",tier=" +
                        (tier.Length > 0 ? tier : "none") +
                        ". Re-read from native mission data.");
                }
                LastPinnedGoal[id] = goal;
                if (ReportedPinnedIds.Add(id))
                {
                    // Player-safe metadata only. Do not log names or
                    // arbitrary mission text; the label's origin and the
                    // target semantics are what we need for diagnostics.
                    Plugin.Logger.LogInfo("[QuestHelper/HUD] Pinned mission " +
                        "titleSource=" + nameSource +
                        ",checklistTarget=" + required +
                        ",displayTarget=" + goal +
                        ",nextRewardTier=" + (tier.Length > 0 ? tier : "none") +
                        ",titleLength=" + displayName.Length + ".");
                }
                result.Add(new ObjectiveLine(id, displayName, captured,
                    goal, goal-captured, tier));
                if (result.Count >= 9) return result;
            }
        }
        return result;
    }
}
