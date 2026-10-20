using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace OrbusQuestHelper;

/// <summary>
/// Pure Missions HTML transformer. It never reads or writes PowerUI objects,
/// Unity objects or game files. Offline tests use the same implementation
/// that the optional native setter integration invokes. Every
/// original mission icon is copied byte-for-byte.
/// </summary>
internal static class MissionHtmlTransformer
{
    internal const string Marker = "<!-- QuestHelper:RegionTask:v1 -->";
    // V2 uses the same one-time marker because both variants are generated
    // from unmodified game HTML, and repeated parsing must remain blocked.
    internal const string RegionControlId = "questhelper-view-region";
    internal const string ActivityControlId = "questhelper-view-activity";
    internal enum GroupView { Region, Activity }
    private const int MaxInputLength = 1_000_000;
    private const int MaxNodes = 4000;
    private const int MaxMissions = 300;
    private const string NativeHeadingStyle =
        "color: #FFFFFF; font-size: 20px;margin-bottom:5px";

    private static readonly Regex AttributePattern = new(
        @"(?<key>[A-Za-z_:][\w:.-]*)\s*=\s*(?:(?<q>['""])(?<value>.*?)\k<q>|(?<value>[^\s>]+))",
        RegexOptions.Compiled | RegexOptions.Singleline | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(150));

    private static readonly Regex MissionIdPattern = new(
        @"(?:^|\s)data-missionid-(?<id>[A-Za-z0-9_-]+)(?=\s|$)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant,
        TimeSpan.FromMilliseconds(150));

    private static readonly Dictionary<string, int> RegionOrder =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Highsteppe"] = 0,
            ["Lamavora"] = 1,
            ["Hulthine's Basin"] = 2,
            ["Flooded Rainforest"] = 3,
            ["Wastelands"] = 4,
            ["Battlegrounds"] = 5,
            ["Dungeons"] = 6,
            ["Weekly Missions"] = -1,
            ["Unassigned Region"] = 99
        };

    internal sealed record Report(
        bool Changed, string Reason, int OriginalIcons, int OutputIcons,
        int ClassifiedRegions, int ClassifiedTasks);

    internal sealed record Category(string Region, string Task);

    private sealed class Node
    {
        public string Name = "";
        public string Class = "";
        public int Start;
        public int OpenEnd;
        public int CloseStart;
        public int End;
        public Node? Parent;
        public readonly List<Node> Children = new();
    }

    private sealed record MissionTile(string Id, string OriginalHtml,
        Category Category);

    /// <summary>
    /// Target-specific fail-closed entrypoint for the optional PowerUI setter
    /// prefix. Never transform the Journal, achievements or other inventory
    /// HTML even if they happen to contain mission-style CSS classes.
    /// </summary>
    internal static bool TryTransformForTarget(
        string? elementId, string? html, out string transformed,
        out Report report) =>
        TryTransformForTarget(elementId, html, GroupView.Region,
            out transformed, out report);

    internal static bool TryTransformForTarget(
        string? elementId, string? html, GroupView view,
        out string transformed, out Report report)
    {
        transformed = html ?? string.Empty;
        if (!string.Equals(elementId, "missions-container",
                StringComparison.Ordinal))
        {
            report = new Report(false, "Not the original Missions container",
                0, 0, 0, 0);
            return false;
        }

        if (html == null || html.Length < 500 ||
            html.Length > MaxInputLength ||
            !html.Contains("vendor-mission", StringComparison.Ordinal) ||
            !html.Contains("data-missionid-", StringComparison.Ordinal))
        {
            report = new Report(false, "Unpopulated or unsupported native Missions HTML",
                0, 0, 0, 0);
            return false;
        }

        return TryTransform(html, FromCatalog, view, out transformed, out report);
    }

    internal static bool TryTransform(
        string original,
        Func<string, Category?> categorize,
        out string transformed,
        out Report report) =>
        TryTransform(original, categorize, GroupView.Region,
            out transformed, out report);

    internal static bool TryTransform(
        string original,
        Func<string, Category?> categorize,
        GroupView view,
        out string transformed,
        out Report report)
    {
        transformed = original;
        report = new Report(false, "Not processed", 0, 0, 0, 0);
        if (categorize == null || original == null)
        {
            report = report with { Reason = "Missing original HTML or categorizer" };
            return false;
        }
        if (original.Length == 0 || original.Length > MaxInputLength)
        {
            report = report with { Reason = "Empty or oversized HTML" };
            return false;
        }
        if (original.Contains(Marker, StringComparison.Ordinal))
        {
            report = report with { Reason = "Already transformed: idempotence guard" };
            return false;
        }

        if (!TryParse(original, out List<Node> nodes, out string parseReason))
        {
            report = report with { Reason = parseReason };
            return false;
        }

        // Real generic-client capture (2026-10-20): #missions-container
        // contains a padding div > .vendor > .vendor-reputation, and the
        // original mission tiles are DIRECT children of vendor-reputation.
        // Older/synthetic templates place them directly inside .vendor.
        // Keep ALL existing outer wrappers untouched, including scroll bounds.
        List<Node> lists = nodes.Where(n =>
            n.Name.Equals("div", StringComparison.OrdinalIgnoreCase) &&
            (HasClass(n.Class, "vendor-reputation") ||
             HasClass(n.Class, "vendor")) &&
            n.Children.Any(c => HasClass(c.Class, "vendor-mission"))).ToList();
        if (lists.Count != 1)
        {
            report = report with { Reason =
                "Expected exactly one native mission-list element; found " +
                lists.Count };
            return false;
        }

        Node vendor = lists[0];
        var tiles = new List<MissionTile>();
        var originalTileHtml = new List<string>();
        var retainedAnnouncements = new List<string>();
        int cursor = vendor.OpenEnd;
        int missionCount = 0;
        bool isWeeklySection = false;

        foreach (Node child in vendor.Children.OrderBy(n => n.Start))
        {
            if (!string.IsNullOrWhiteSpace(original.Substring(cursor,
                    child.Start - cursor)))
            {
                report = report with { Reason =
                    "Unexpected text between native mission/heading elements" };
                return false;
            }
            cursor = child.End;

            if (child.Name.Equals("p", StringComparison.OrdinalIgnoreCase))
            {
                // Headings have no interactive behaviour. If a new build adds
                // interactive markup to them, refuse to remove those nodes.
                string oldHeading = original.Substring(child.Start,
                    child.End - child.Start);
                if (oldHeading.Contains("onmousedown", StringComparison.OrdinalIgnoreCase) ||
                    oldHeading.Contains("onclick", StringComparison.OrdinalIgnoreCase) ||
                    oldHeading.Contains("<button", StringComparison.OrdinalIgnoreCase))
                {
                    report = report with { Reason = "Interactive native heading; unsupported template" };
                    return false;
                }
                // The original page may contain informational banners with
                // their own images, e.g. 'New Missions Available in Highsteppe'.
                // Preserve those exact original <p> nodes above the grouped
                // missions. Only remove the known generic Weekly/Level headings.
                // The latest Spades build inserts weekly mission tiles
                // before the Level Missions section. Preserve this native
                // categorisation even though their IDs change each week.
                string section = HeadingText(oldHeading);
                if (section.Equals("Your Weekly Missions", StringComparison.OrdinalIgnoreCase) ||
                    section.Equals("Weekly Missions", StringComparison.OrdinalIgnoreCase) ||
                    section.Equals("Available Weekly Missions", StringComparison.OrdinalIgnoreCase))
                    isWeeklySection = true;
                else if (section.Equals("Your Level Missions", StringComparison.OrdinalIgnoreCase) ||
                    section.Equals("Your Missions", StringComparison.OrdinalIgnoreCase) ||
                    section.Equals("Available Level Missions", StringComparison.OrdinalIgnoreCase))
                    isWeeklySection = false;

                if (!IsReplaceableSectionHeading(oldHeading))
                    retainedAnnouncements.Add(oldHeading);
                continue;
            }

            if (child.Name != "div" || !HasClass(child.Class, "vendor-mission"))
            {
                report = report with { Reason =
                    "Unexpected native child inside vendor wrapper: " + child.Name };
                return false;
            }

            var idMatch = MissionIdPattern.Match(child.Class);
            if (!idMatch.Success || ++missionCount > MaxMissions)
            {
                report = report with { Reason =
                    "Unrecognised mission ID or too many mission icons" };
                return false;
            }

            string id = idMatch.Groups["id"].Value;
            string untouched = original.Substring(child.Start, child.End - child.Start);
            originalTileHtml.Add(untouched);

            // Unassigned missions still appear in the output. Missing workbook
            // mappings can never cause an icon to disappear.
            Category category;
            try
            {
                category = categorize(id) ??
                    new Category("Unassigned Region", "Other Tasks");
            }
            catch
            {
                report = report with { Reason = "Mission category lookup failed" };
                return false;
            }
            if (string.IsNullOrWhiteSpace(category.Region) ||
                string.IsNullOrWhiteSpace(category.Task))
                category = new Category("Unassigned Region", "Other Tasks");

            if (isWeeklySection)
            {
                // Classify weekly *activities* using their existing icon
                // identity, never their volatile time-stamped mission ID.
                // The location of a generated weekly objective is not
                // provided in its tile, so never invent one.
                string task = WeeklyActivityFromIcon(untouched);
                category = new Category("Weekly Missions", task);
            }
            tiles.Add(new MissionTile(id, untouched, category));
        }

        if (!string.IsNullOrWhiteSpace(original.Substring(cursor,
                vendor.CloseStart - cursor)) || tiles.Count < 2)
        {
            report = report with { Reason =
                "Unexpected trailing wrapper text or insufficient mission icons" };
            return false;
        }

        // Everything OUTSIDE this single known list remains unchanged.
        var replacement = new StringBuilder(original.Length + 8192);
        replacement.Append(original, 0, vendor.OpenEnd);
        replacement.Append(Marker);
        foreach (string announcement in retainedAnnouncements)
            replacement.Append(announcement);
        AppendViewSwitch(replacement, view);
        if (view == GroupView.Region)
        {
            foreach (var region in tiles.GroupBy(t => t.Category.Region)
                .OrderBy(g => RegionOrder.TryGetValue(g.Key, out int n) ? n : 90)
                .ThenBy(g => g.Key, StringComparer.Ordinal))
            {
                AppendHeading(replacement, region.Key, true);
                // Activity types still appear next to each other, but there
                // are NO inner activity headings and no extra blank rows.
                foreach (var tile in region.OrderBy(t => ActivityHeading(t.Category.Task),
                    StringComparer.Ordinal).ThenBy(t => t.Category.Task,
                    StringComparer.Ordinal))
                    replacement.Append(tile.OriginalHtml);
            }
        }
        else if (view == GroupView.Activity)
        {
            foreach (var activity in tiles.GroupBy(t =>
                    ActivityHeading(t.Category.Task))
                .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                AppendHeading(replacement, activity.Key, true);
                // Within one activity, similar regions remain adjacent;
                // do not render any region subsections.
                foreach (var tile in activity.OrderBy(t =>
                    RegionOrder.TryGetValue(t.Category.Region, out int n) ? n : 90)
                    .ThenBy(t => t.Category.Region, StringComparer.Ordinal))
                    replacement.Append(tile.OriginalHtml);
            }
        }
        else
        {
            report = report with { Reason = "Unrecognised group view" };
            return false;
        }
        replacement.Append(original, vendor.CloseStart,
            original.Length - vendor.CloseStart);
        string candidate = replacement.ToString();

        // Our parser never modifies the icon string. Still explicitly validate
        // the complete mission MULTISET, including repeated IDs/templates.
        // Comparing just a count would have missed the old duplication bug.
        if (candidate.Length > MaxInputLength * 2 ||
            !TryParse(candidate, out var outputNodes, out _) ||
            !SameTileMultiset(originalTileHtml,
                outputNodes.Where(n => HasClass(n.Class, "vendor-mission"))
                    .Select(n => candidate.Substring(n.Start, n.End - n.Start))))
        {
            report = report with { Reason =
                "Post-transform parse or lossless original-icon validation failed",
                OriginalIcons = tiles.Count };
            return false;
        }

        // Protect the native PowerUI click/hover attributes without rewriting
        // their values or making new callbacks.
        transformed = candidate;
        report = new Report(true, "Lossless grouped HTML prepared",
            tiles.Count, tiles.Count,
            tiles.Count(t => t.Category.Region != "Unassigned Region"),
            tiles.Count(t => t.Category.Task != "Other Tasks"));
        return true;

    }

    internal static Category FromCatalog(string missionId)
    {
        if (!MissionCatalog.TryGet(missionId, out string region,
                out string activity))
            return new Category("Unassigned Region", "Other Tasks");
        return new Category(
            string.IsNullOrEmpty(region) ? "Unassigned Region" : region,
            string.IsNullOrEmpty(activity) ? "Other Tasks" : activity);
    }

    /// <summary>
    /// Broad activity view labels. A player looking for fishing quests needs
    /// both the catch-count and fish-by-weight variants in one place.
    /// Original mission IDs and task metadata remain unchanged.
    /// </summary>
    private static string ActivityHeading(string task) => task switch
    {
        "Catch Fish" or "Fishing by Weight" => "Fishing",
        "Battleground Captures" or "Battleground Kills" or
        "Battleground Matches" or "Battleground Victories" => "Battlegrounds",
        _ => task
    };

    private static string WeeklyActivityFromIcon(string tileHtml)
    {
        if (tileHtml.Contains("ui_icons/inv/Fish_016", StringComparison.OrdinalIgnoreCase))
            return "Fishing";
        if (tileHtml.Contains("ui_icons/inv/EventMissions", StringComparison.OrdinalIgnoreCase))
            return "Public Events";
        if (tileHtml.Contains("ui_icons/Madness", StringComparison.OrdinalIgnoreCase))
            return "Monster Hunting";
        if (tileHtml.Contains("ui_icons/inv/SalvagingMissions", StringComparison.OrdinalIgnoreCase))
            return "Salvaging Equipment";
        return "Weekly Tasks";
    }

    private static string HeadingText(string markup)
    {
        string plain = Regex.Replace(markup, "<[^>]*>", " ");
        return Regex.Replace(plain, @"\\s+", " ").Trim().TrimEnd(':').Trim();
    }

    private static bool IsReplaceableSectionHeading(string originalHeading)
    {
        // Never drop a native informational heading containing its own art,
        // image or rich markup (the generic client uses such a banner).
        if (originalHeading.Contains("<img", StringComparison.OrdinalIgnoreCase))
            return false;

        string label = HeadingText(originalHeading);

        return label.Equals("Your Level Missions", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("Your Weekly Missions", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("Your Missions", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("Weekly Missions", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("Available Level Missions", StringComparison.OrdinalIgnoreCase) ||
            label.Equals("Available Weekly Missions", StringComparison.OrdinalIgnoreCase);
    }

    private static void AppendViewSwitch(StringBuilder output, GroupView view)
    {
        // One compact, native-font row. Event handlers are attached to these
        // two existing elements after PowerUI renders them; no inline JS, and
        // no DOM node reparenting. A config fallback remains available.
        output.Append("<p style='color: #FFFFFF; font-size: 20px;margin-bottom:5px'>");
        output.Append("View: ");
        AppendViewLabel(output, "Region", RegionControlId, view == GroupView.Region);
        output.Append(" | ");
        AppendViewLabel(output, "Activity", ActivityControlId, view == GroupView.Activity);
        output.Append("</p>");
    }

    private static void AppendViewLabel(StringBuilder output, string label,
        string id, bool selected)
    {
        output.Append("<span id='").Append(id).Append("'>");
        if (selected) output.Append("<b>");
        output.Append(label);
        if (selected) output.Append("</b>");
        output.Append("</span>");
    }

    private static void AppendHeading(StringBuilder output, string value, bool region)
    {
        // Same <p> tag and in-game parchment title CSS as the game's own
        // mission template. No global styles, custom font, coloured panels,
        // event handlers or synthetic quest icons.
        string escaped = value.Replace("&", "&amp;")
            .Replace("<", "&lt;").Replace(">", "&gt;");
        output.Append("<p style='").Append(NativeHeadingStyle).Append("'>");
        if (region) output.Append("<b>");
        output.Append(escaped);
        if (region) output.Append("</b>");
        output.Append("</p>");
    }

    private static bool SameTileMultiset(IEnumerable<string> originals,
        IEnumerable<string> outputs)
    {
        static Dictionary<string, int> Count(IEnumerable<string> items)
        {
            var counts = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string item in items)
            {
                counts.TryGetValue(item, out int old);
                counts[item] = old + 1;
            }
            return counts;
        }
        Dictionary<string, int> a = Count(originals), b = Count(outputs);
        return a.Count == b.Count && a.All(kv =>
            b.TryGetValue(kv.Key, out int count) && count == kv.Value);
    }

    private static bool HasClass(string all, string wanted) =>
        (" " + all + " ").Contains(" " + wanted + " ", StringComparison.Ordinal);

    private static bool TryParse(string html, out List<Node> all, out string reason)
    {
        all = new List<Node>();
        var stack = new Stack<Node>();
        reason = "";
        int i = 0;
        while (i < html.Length)
        {
            int start = html.IndexOf('<', i);
            if (start < 0) break;
            if (html.AsSpan(start).StartsWith("<!--".AsSpan(),
                StringComparison.Ordinal))
            {
                int endComment = html.IndexOf("-->", start + 4,
                    StringComparison.Ordinal);
                if (endComment < 0) { reason = "Unterminated comment"; return false; }
                i = endComment + 3;
                continue;
            }
            if (html.AsSpan(start).StartsWith("<!".AsSpan(),
                StringComparison.Ordinal))
            {
                reason = "Unsupported declaration"; return false;
            }
            int end = FindTagEnd(html, start);
            if (end < 0) { reason = "Unterminated HTML tag"; return false; }
            int pos = start + 1;
            bool closing = pos < end && html[pos] == '/';
            if (closing) pos++;
            while (pos < end && char.IsWhiteSpace(html[pos])) pos++;
            int nameStart = pos;
            while (pos < end && (char.IsLetterOrDigit(html[pos]) ||
                   html[pos] == '-' || html[pos] == ':'))
                pos++;
            if (pos == nameStart)
            {
                reason = "Unsupported HTML token"; return false;
            }
            string name = html.Substring(nameStart, pos - nameStart)
                .ToLowerInvariant();

            if (closing)
            {
                if (stack.Count == 0 || stack.Peek().Name != name)
                {
                    reason = "Mismatched closing tag: " + name;
                    return false;
                }
                Node node = stack.Pop();
                node.CloseStart = start;
                node.End = end + 1;
            }
            else
            {
                string tag = html.Substring(start, end - start + 1);
                var node = new Node
                {
                    Name = name,
                    Start = start,
                    OpenEnd = end + 1,
                    Parent = stack.Count > 0 ? stack.Peek() : null,
                    Class = Attribute(tag, "class")
                };
                all.Add(node);
                if (all.Count > MaxNodes)
                {
                    reason = "Too many HTML elements";
                    return false;
                }
                node.Parent?.Children.Add(node);
                bool voidTag = name is "img" or "br" or "hr" or "input" or
                    "meta" or "link" or "source" or "wbr";
                bool selfClosing = tag.TrimEnd('>').TrimEnd()
                    .EndsWith("/", StringComparison.Ordinal);
                if (voidTag || selfClosing)
                {
                    node.CloseStart = end + 1;
                    node.End = end + 1;
                }
                else
                {
                    if (name is "script" or "style" or "textarea")
                    {
                        reason = "Unsupported embedded raw-text element";
                        return false;
                    }
                    stack.Push(node);
                }
            }
            i = end + 1;
        }
        if (stack.Count != 0)
        {
            reason = "Unclosed native markup: " + stack.Peek().Name;
            return false;
        }
        return true;
    }

    private static string Attribute(string openingTag, string key)
    {
        foreach (Match m in AttributePattern.Matches(openingTag))
            if (string.Equals(m.Groups["key"].Value, key,
                    StringComparison.OrdinalIgnoreCase))
                return m.Groups["value"].Value;
        return "";
    }

    private static int FindTagEnd(string html, int start)
    {
        char quote = '\0';
        for (int i = start + 1; i < html.Length; i++)
        {
            char c = html[i];
            if (quote != '\0')
            {
                if (c == quote) quote = '\0';
            }
            else if (c is '\'' or '"') quote = c;
            else if (c == '>') return i;
        }
        return -1;
    }
}
