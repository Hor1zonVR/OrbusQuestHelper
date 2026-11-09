using System;

namespace OrbusQuestHelper;

/// <summary>
/// Community-server capture definitions transcribed into original, compact
/// display metadata from the user's 16 supplied server records (Nov 9, 2026).
/// The records had NO mission ID, so resolve by the unique description
/// prefix and type, never by the array index or guessed "captureN" ID.
/// This is only a read-only label and reward-tier fallback, not quest state.
/// </summary>
internal static class OfficialCaptureObjectives
{
    internal sealed record Definition(
        string DescriptionPrefix, string Action, string Region,
        int Bronze, int Silver, int Gold, string[] WeightedIds,
        string[] Zones, int ChecklistMax);

    private static readonly Definition[] Definitions =
    {
        new("There is a mouse infestation in the underground Market area", "Capture mice",
            "Highsteppe Market", 6, 3, 5,
            new[] { "mouse:1", "mouse-rare:3" },
            new[] { "ZoneHighsteppe" }, 10000),
        new("The Parathime Finch has been seen along Traders Road", "Capture Parathime Finches",
            "Highsteppe / Traders Road", 6, 3, 5,
            new[] { "bird:1", "bird-rare:3" },
            new[] { "Zone3" }, 10000),
        new("There are markings of the Critters Cenn needs captured", "Capture any critters near Highsteppe",
            "Highsteppe outskirts", 6, 3, 5,
            Array.Empty<string>(),
            new[] { "Zone3" }, 100),
        new("Cenn thinks the squirrels might be the key", "Capture squirrels",
            "Highsteppe outskirts", 6, 3, 5,
            new[] { "squirrel:1", "squirrel-rare:3" },
            new[] { "Zone3" }, 10000),
        new("Head into the Flooded Rainforest and see what Critters", "Capture any Rainforest critters",
            "Flooded Rainforest", 8, 5, 7,
            Array.Empty<string>(),
            new[] { "Zone4" }, 100),
        new("Now that you know there are Obnobi Lizards", "Capture Obnobi Lizards",
            "Flooded Rainforest", 10, 8, 12,
            new[] { "lizard:1", "lizard-rare:3" },
            new[] { "Zone4" }, 10000),
        new("Head into the Lamavora Battlefield to see what kind", "Capture any Lamavora critters",
            "Lamavora Battlefield", 7, 6, 8,
            Array.Empty<string>(),
            new[] { "Zone1" }, 100),
        new("Inside the Lamavora Battlefield there are skunks", "Capture skunks",
            "Lamavora Battlefield", 8, 6, 6,
            new[] { "skunk:1", "skunk-rare:3" },
            new[] { "Zone1" }, 10000),
        new("Luckily the birds aren't affected by the essence", "Capture Green Crested Fowl",
            "Lamavora Battlefield", 8, 6, 6,
            new[] { "bird2:1", "bird2-rare:3" },
            new[] { "Zone1" }, 10000),
        new("Hulthine's Basin doesn't contain any running critters", "Capture Hulthine's Basin birds",
            "Hulthine's Basin", 6, 5, 6,
            new[] { "bird3:1", "bird3-rare:3" },
            new[] { "Zone2" }, 10000),
        new("Cenn isn't being picky today", "Capture any critters anywhere",
            "Any zone", 10, 7, 8,
            Array.Empty<string>(),
            Array.Empty<string>(), 100),
        new("Cenn thinks there may be something with reptile species", "Capture lizards and toads",
            "Any zone", 10, 8, 8,
            new[] { "lizard:1", "corrupt2:3" },
            Array.Empty<string>(), 10000),
        new("Venture into the Wastelands as this is where", "Capture any Wastelands critters",
            "Wastelands", 8, 5, 8,
            Array.Empty<string>(),
            new[] { "Zone5" }, 100),
        new("You have discovered some mutated creatures there", "Capture hairless skunks",
            "Wastelands", 8, 5, 8,
            new[] { "corrupt1:1", "corrupt1-rare:3" },
            new[] { "Zone5" }, 10000),
        new("Return to the Wastelands to retrieve the mutated toads", "Capture mutated toads",
            "Wastelands", 8, 5, 8,
            new[] { "corrupt2:1", "corrupt2-rare:3" },
            new[] { "Zone5" }, 10000),
        new("Cenn thinks you are doing well, but his specimens", "Capture any critters anywhere",
            "Any zone", 15, 10, 10,
            Array.Empty<string>(),
            Array.Empty<string>(), 100),
    };

    internal static int KnownCount => Definitions.Length;

    internal static bool TryFind(string? type, string? description,
        out Definition definition)
    {
        definition = null!;
        if (!string.Equals(type?.Trim(), "capture",
                StringComparison.OrdinalIgnoreCase) ||
            string.IsNullOrWhiteSpace(description))
            return false;

        // Normalize curly apostrophes only. Matching is intentionally
        // conservative: an unknown or revised server definition falls
        // back to the generic category, rather than a guessed creature.
        string normalized = description.TrimStart()
            .Replace('\u2019', '\u0027');
        foreach (Definition entry in Definitions)
        {
            if (!normalized.StartsWith(entry.DescriptionPrefix,
                StringComparison.OrdinalIgnoreCase))
                continue;
            definition = entry;
            return true;
        }
        return false;
    }
}
