using System;

namespace OrbusQuestHelper;

/// <summary>
/// Reward tiers in the official Community Edition mission definitions use
/// INCREMENTAL requirements: Bronze x, Silver +y, Gold +z.
/// A checklist target such as 10,000 is frequently just an upper bound.
/// Only use cumulative tier requirements when native mission.progress and
/// checklist.killCount agree; otherwise display count without a fake goal.
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
            double cumulative = 0;
            double[] increments = { bronze, silver, gold };
            string[] names = { "Bronze", "Silver", "Gold" };
            for (int i = 0; i < increments.Length; ++i)
            {
                double increment = increments[i];
                if (!double.IsFinite(increment) || increment <= 0)
                    continue;
                cumulative += increment;
                if (cumulative <= current || cumulative > checklistTarget)
                    continue;

                int next = (int)Math.Ceiling(cumulative);
                if (next <= current) continue;
                return next >= LargeLifetimeTotal
                    ? (0, names[i]) : (next, names[i]);
            }
        }

        // The full checklist count is not a trusted next tier. Only show
        // modest finite checklist targets directly (e.g. 3/8).
        return checklistTarget >= LargeLifetimeTotal
            ? (0, "") : (checklistTarget, "");
    }
}
