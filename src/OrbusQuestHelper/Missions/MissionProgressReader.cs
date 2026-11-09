using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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

    private static bool Near(double a, double b) =>
        Math.Abs(a-b) <= Math.Max(0.05, 0.001*Math.Abs(b));

    private static (int Goal, string Tier) FindNextRewardGoal(
        object mission, int current, int checklistTarget, bool oneObjective)
    {
        bool comparable = oneObjective &&
            TryNumber(PowerUiRuntime.Get(mission, "progress"),
                out double progress) &&
            Near(progress, current);

        double Requirement(string tier)
        {
            object? reward = PowerUiRuntime.Get(mission, tier);
            return TryNumber(PowerUiRuntime.Get(reward, "requirement"),
                out double threshold) ? threshold : double.NaN;
        }

        return MissionGoalPolicy.Choose(current, checklistTarget, comparable,
            Requirement("bronze"), Requirement("silver"),
            Requirement("gold"));
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

            // Read the native counters, but never treat a narrative
            // Mission.description as a requested kill/capture task.
            var checklist = PowerUiRuntime.Enumerate(
                PowerUiRuntime.Get(mission, "checklist"), 12);
            string nativeDescription =
                PowerUiRuntime.Text(mission, "description");
            string missionType = PowerUiRuntime.Text(mission, "type");
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
                string displayName = MissionObjectiveLabels.Resolve(
                    id, PowerUiRuntime.Text(item, "description"),
                    nativeDescription, missionType, out string nameSource);

                (int goal, string tier) = FindNextRewardGoal(
                    mission, captured, required, checklist.Count == 1);
                // Zero means "verified count; milestone not available".
                // Never clamp a hidden large target back to 10,000.
                if (goal > 0) goal = Math.Clamp(goal, captured, required);
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
                    goal, goal > 0 ? Math.Max(0, goal - captured) : 0, tier));
                if (result.Count >= 9) return result;
            }
        }
        return result;
    }
}
