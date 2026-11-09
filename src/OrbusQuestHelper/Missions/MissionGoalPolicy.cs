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
        bool hasNativeTiers =
            double.IsFinite(bronze) && bronze > 0 ||
            double.IsFinite(silver) && silver > 0 ||
            double.IsFinite(gold) && gold > 0;

        if (sameScale && hasNativeTiers)
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
                // A *verified reward tier* may legitimately be 3,000+
                // Dram, or a large fish-weight target. The large number
                // cutoff only protects us from unverified checklist caps
                // such as timesToKill=10000, never real tier thresholds.
                return (next, names[i]);
            }
            // All reachable reward tiers have already been passed. A
            // generic checklist upper bound (even just 100) is not a new
            // mission goal after Gold has been earned.
            return (0, "");
        }

        // When stages exist but progress scales are not comparable,
        // NEVER present the checklist's high protective cap as a goal.
        if (hasNativeTiers) return (0, "");

        // A genuinely un-tiered checklist with a modest total may still
        // have an ordinary finite objective such as 3/8.
        return checklistTarget >= LargeLifetimeTotal
            ? (0, "") : (checklistTarget, "");
    }
}
