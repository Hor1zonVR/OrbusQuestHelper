using System;

namespace OrbusQuestHelper;

/// <summary>
/// Selects the next server-provided reward threshold only when native
/// mission.progress and checklist.killCount are on a comparable scale.
/// Never presents a huge lifetime/event total as the next small milestone.
/// </summary>
internal static class MissionGoalPolicy
{
    internal const int LargeLifetimeTotal = 2000;

    internal static (int Goal, string Tier) Choose(
        int current, int checklistTarget, bool sameScale,
        double bronze, double silver, double gold)
    {
        if (sameScale)
        {
            double[] thresholds = { bronze, silver, gold };
            string[] tiers = { "Bronze", "Silver", "Gold" };
            for (int i = 0; i < thresholds.Length; ++i)
            {
                double raw = thresholds[i];
                if (!double.IsFinite(raw) || raw <= current ||
                    raw > checklistTarget || raw <= 0)
                    continue;
                int goal = (int)Math.Ceiling(raw);
                if (goal <= current) continue;
                // If the next reward itself requires thousands of actions,
                // show the real accumulated count and the tier name,
                // without an oppressive or misleading /10000 counter.
                return goal >= LargeLifetimeTotal
                    ? (0, tiers[i]) : (goal, tiers[i]);
            }
        }

        return checklistTarget >= LargeLifetimeTotal
            ? (0, "") : (checklistTarget, "");
    }
}
