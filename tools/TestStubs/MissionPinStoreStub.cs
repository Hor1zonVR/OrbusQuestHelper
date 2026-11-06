using System;
using System.Collections.Generic;

// Offline test-only substitute. The real MissionPinStore links against
// BepInEx and must never be referenced by synthetic HTML tests.
namespace OrbusQuestHelper;

internal static class MissionPinStore
{
    internal const int MaxPinned = 3;
    internal static IReadOnlyList<string> PinnedIds => Array.Empty<string>();
    internal static bool IsFavourite(string id) => false;
    internal static bool IsPinned(string id) => false;
    internal static bool ApplyPending() => false;
}
