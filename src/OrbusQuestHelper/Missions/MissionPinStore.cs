using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx.Configuration;

namespace OrbusQuestHelper;

/// <summary>
/// Persists favourite IDs and up to three HUD-pinned mission IDs locally.
/// Never modifies Orbus mission/quest state. Store handles double-fired
/// VR click + mousedown by committing pending actions once on Update.
/// </summary>
internal static class MissionPinStore
{
    internal const int MaxPinned = 3;
    private const int MaxFavourites = 80;
    private static ConfigEntry<string>? _favourites;
    private static ConfigEntry<string>? _pinned;
    private static readonly List<string> Favourites = new();
    private static readonly List<string> Pinned = new();
    private static readonly HashSet<string> PendingFavourite = new(StringComparer.Ordinal);
    private static readonly HashSet<string> PendingPin = new(StringComparer.Ordinal);

    internal static void Initialize(ConfigFile config)
    {
        _favourites = config.Bind("Pinned Missions", "Favourites", "",
            "Local mission IDs saved as favourites; edited through the Missions page.");
        _pinned = config.Bind("Pinned Missions", "HudPins", "",
            "Up to three mission IDs displayed beside the level HUD.");
        Favourites.Clear();
        Pinned.Clear();
        ReadIds(_favourites.Value, Favourites, MaxFavourites);
        ReadIds(_pinned.Value, Pinned, MaxPinned);
        Plugin.Logger.LogInfo("[QuestHelper/Pins] Saved favourites=" +
            Favourites.Count + ", HUD pins=" + Pinned.Count + ".");
    }

    private static void ReadIds(string? encoded, List<string> result, int limit)
    {
        if (encoded == null || encoded.Length > 12000) return;
        foreach (string text in encoded.Split(','))
        {
            string id = text.Trim();
            if (!ValidId(id) || result.Contains(id, StringComparer.Ordinal)) continue;
            result.Add(id);
            if (result.Count >= limit) break;
        }
    }

    private static bool ValidId(string? id) =>
        !string.IsNullOrWhiteSpace(id) && id.Length <= 100 &&
        id.All(c => char.IsLetterOrDigit(c) || c == '-' || c == '_');

    internal static IReadOnlyList<string> PinnedIds => Pinned;
    internal static bool IsFavourite(string id) => Favourites.Contains(id);
    internal static bool IsPinned(string id) => Pinned.Contains(id);

    internal static void RequestFavourite(string id)
    {
        if (ValidId(id)) PendingFavourite.Add(id);
    }

    internal static void RequestPin(string id)
    {
        if (ValidId(id)) PendingPin.Add(id);
    }

    // Return true if a visible pin/favourite state changed and the native
    // button glyphs should be refreshed from untouched original HTML.
    internal static bool ApplyPending()
    {
        if (PendingFavourite.Count == 0 && PendingPin.Count == 0)
            return false;

        bool changed = false;
        foreach (string id in PendingFavourite)
        {
            if (Favourites.Remove(id))
                changed = true;
            else if (Favourites.Count < MaxFavourites)
            {
                Favourites.Add(id);
                changed = true;
            }
        }
        foreach (string id in PendingPin)
        {
            if (Pinned.Remove(id))
                changed = true;
            else if (Pinned.Count < MaxPinned)
            {
                Pinned.Add(id);
                changed = true;
            }
            else
                Plugin.Logger.LogInfo("[QuestHelper/Pins] Up to three missions " +
                    "can be pinned. Unpin another one first.");
        }
        PendingFavourite.Clear();
        PendingPin.Clear();

        if (changed)
        {
            if (_favourites != null) _favourites.Value = string.Join(",", Favourites);
            if (_pinned != null) _pinned.Value = string.Join(",", Pinned);
        }
        return changed;
    }
}
