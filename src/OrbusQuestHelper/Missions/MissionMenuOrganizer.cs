using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OrbusQuestHelper;

/// <summary>
/// IL2CPP-injectable shell. Keep PowerUI wrappers out of these fields/method
/// signatures: the two community builds export different PowerUI CLR types.
/// </summary>
public sealed class MissionMenuOrganizer : MonoBehaviour
{
    public MissionMenuOrganizer(IntPtr pointer) : base(pointer) { }

    private float _nextWarning;

    private void Update()
    {
        try { MissionRegionEngine.Tick(); }
        catch (Exception ex)
        {
            if (Time.realtimeSinceStartup < _nextWarning) return;
            _nextWarning = Time.realtimeSinceStartup + 20f;
            Plugin.Logger.LogWarning("[QuestHelper] Region organiser: " +
                ex.GetType().Name + " " + ex.Message);
        }
    }

    private void OnDisable()
    {
        try { MissionRegionEngine.Stop(); }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[QuestHelper] Cleanup: " + ex.Message);
        }
    }
}

/// <summary>
/// Manages a single client-side layout pass. Original mission icons (and native
/// mouse event handlers) are moved, NOT cloned or replaced. All server/state
/// operations are strictly read-only. The original DOM is restored on exit.
/// </summary>
internal static class MissionRegionEngine
{
    private const float Interval = 2f;
    private const string ContainerId = "missions-container";
    private const string GroupRootId = "questhelper-region-groups";
    private const string ArchiveId = "questhelper-original-sections";

    private sealed class Tile
    {
        public object Node = null!;
        public string Id = "";
        public MissionInfoReader.Details Data = null!;
    }

    private static readonly Dictionary<string, int> RegionOrder =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Highsteppe"] = 0,
            ["Hulthine's Basin"] = 1,
            ["Flooded Rainforest"] = 2,
            ["Lamavora"] = 3,
            ["Wastelands"] = 4,
            ["Multiple Regions"] = 80,
            ["Unassigned Region"] = 90
        };

    private static float _nextCheck;
    private static object? _document;
    private static object? _container;
    private static object? _archive;
    private static object? _root;
    private static readonly List<object> OriginalChildren = new();
    private static readonly List<object> RetainedHandlers = new();
    private static int _printedUnknownForSession;
    private static int _lastCount;

    internal static void Tick()
    {
        if (Time.realtimeSinceStartup < _nextCheck) return;
        _nextCheck = Time.realtimeSinceStartup + Interval;
        if (Plugin.RegionGroupingEnabled == null ||
            !Plugin.RegionGroupingEnabled.Value)
        {
            Restore();
            return;
        }

        string activePage = PowerUiRuntime.CurrentInventoryPage();
        // A missions-tab button is present on EVERY inventory page, as the
        // v0.2.0 Achievements screenshot demonstrated. Never use that button
        // to decide which page is active.
        if (!string.IsNullOrWhiteSpace(activePage) &&
            (activePage.IndexOf("achievement", StringComparison.OrdinalIgnoreCase) >= 0 ||
             activePage.IndexOf("talent", StringComparison.OrdinalIgnoreCase) >= 0 ||
             activePage.IndexOf("equipment", StringComparison.OrdinalIgnoreCase) >= 0))
        {
            Restore();
            return;
        }

        object? doc = PowerUiRuntime.PlayerMenuDocument();
        if (doc == null) { Restore(); return; }
        object? content = PowerUiRuntime.ById(doc, ContainerId);
        if (content == null) { Restore(); return; }

        if (_root != null)
        {
            bool intact = PowerUiRuntime.Same(doc, _document) &&
                PowerUiRuntime.Same(content, _container) &&
                PowerUiRuntime.ById(doc, GroupRootId) != null;
            if (intact) return;
            // A native menu refresh may replace the subtree. Discard the
            // previous document's element handles; don't mutate stale nodes.
            ClearHandles();
        }

        var tiles = new List<Tile>();
        foreach (object node in PowerUiRuntime.ByClass(content, "vendor-mission"))
        {
            if (!PowerUiRuntime.IsDescendant(node, content)) continue;
            string? id = MissionInfoReader.MissionId(node);
            if (string.IsNullOrWhiteSpace(id)) continue;
            tiles.Add(new Tile
            {
                Node = node,
                Id = id,
                Data = MissionInfoReader.Read(id)
            });
        }

        // Do not change empty, hidden or unrelated tabs. If the actual menu
        // structure differs between builds, fail open to vanilla instead.
        if (tiles.Count < 2) return;

        // A guard against the still-visible Achievements page: missions must
        // be the selected page, or the page identifier must be unavailable.
        if (!string.IsNullOrWhiteSpace(activePage) &&
            activePage.IndexOf("mission", StringComparison.OrdinalIgnoreCase) < 0)
            return;

        Apply(doc, content, tiles);
    }

    private static void Apply(object document, object content, List<Tile> tiles)
    {
        object? root = PowerUiRuntime.Make(document, "div");
        object? archive = PowerUiRuntime.Make(document, "div");
        if (root == null || archive == null) return;

        if (!PowerUiRuntime.Set(root, "id", GroupRootId) ||
            !PowerUiRuntime.Set(archive, "id", ArchiveId))
        {
            Plugin.Logger.LogWarning("[QuestHelper] PowerUI cannot set grouping IDs; vanilla UI preserved.");
            return;
        }

        // Build the new layout and all headings before touching the original
        // DOM. A failed element creation should not leave an empty Missions tab.
        var grouped = tiles
            .GroupBy(t => t.Data.Group.Region)
            .OrderBy(g => RegionOrder.TryGetValue(g.Key, out int n) ? n : 70)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToArray();

        var destinations = new Dictionary<Tile, object>();
        int headingsWithClicks = 0;

        foreach (var region in grouped)
        {
            object? regionSection = PowerUiRuntime.Make(document, "div");
            object? regionHeader = Heading(document, region.Key, isRegion: true);
            object? regionBody = PowerUiRuntime.Make(document, "div");
            if (regionSection == null || regionHeader == null ||
                regionBody == null) return;

            if (!PowerUiRuntime.Append(regionSection, regionHeader) ||
                !PowerUiRuntime.Append(regionSection, regionBody) ||
                !PowerUiRuntime.Append(root, regionSection)) return;

            if (TryMakeCollapsible(regionHeader, regionBody))
                headingsWithClicks++;

            foreach (var activity in region
                .GroupBy(x => x.Data.Group.Activity)
                .OrderBy(g => g.Key, StringComparer.Ordinal))
            {
                object? taskSection = PowerUiRuntime.Make(document, "div");
                object? taskHeader = Heading(document, activity.Key, isRegion: false);
                object? taskBody = PowerUiRuntime.Make(document, "div");
                if (taskSection == null || taskHeader == null ||
                    taskBody == null) return;

                if (!PowerUiRuntime.Append(taskSection, taskHeader) ||
                    !PowerUiRuntime.Append(taskSection, taskBody) ||
                    !PowerUiRuntime.Append(regionBody, taskSection)) return;

                if (TryMakeCollapsible(taskHeader, taskBody))
                    headingsWithClicks++;

                foreach (Tile tile in activity)
                    destinations[tile] = taskBody;
            }
        }

        if (destinations.Count != tiles.Count) return;
        List<object> originals = PowerUiRuntime.Enumerate(
            PowerUiRuntime.Get(content, "childNodes"), 1024);
        if (originals.Count == 0) return;

        // A failure to hide the legacy headings would leave overlapping lists.
        // Test style support on a detached node before any mutations.
        if (!PowerUiRuntime.Display(archive, "none"))
        {
            Plugin.Logger.LogWarning("[QuestHelper] PowerUI display styling unsupported; vanilla Missions preserved.");
            return;
        }

        bool archiveAdded = PowerUiRuntime.Append(content, archive);
        bool rootAdded = archiveAdded && PowerUiRuntime.Append(content, root);
        if (!rootAdded) return;

        _document = document;
        _container = content;
        _archive = archive;
        _root = root;
        OriginalChildren.Clear();
        OriginalChildren.AddRange(originals);
        _lastCount = 0;

        // Move the old native headings, spacers, and original layout wrappers
        // to a hidden archive. Then move the REAL mission tiles into the new
        // region/task wrappers. The original icon objects keep their handlers.
        foreach (object child in originals)
        {
            if (!PowerUiRuntime.Append(archive, child))
            {
                Restore();
                Plugin.Logger.LogWarning("[QuestHelper] Couldn't preserve original Missions markup; restored vanilla UI.");
                return;
            }
        }

        foreach (Tile tile in tiles)
        {
            if (!PowerUiRuntime.Append(destinations[tile], tile.Node))
            {
                Restore();
                Plugin.Logger.LogWarning("[QuestHelper] Couldn't move a native mission icon; restored vanilla UI.");
                return;
            }
            _lastCount++;
        }

        int resolved = tiles.Count(t => t.Data.Group.RegionVerified);
        int taskResolved = tiles.Count(t => t.Data.Group.ActivityVerified);
        int foundData = tiles.Count(t => t.Data.Found);
        Plugin.Logger.LogInfo("[QuestHelper] Region/task layout applied: " +
            tiles.Count + " original mission icons; region known=" + resolved +
            ", activity known=" + taskResolved + ", native mission data=" +
            foundData + ", collapsible headings=" + headingsWithClicks +
            ", groups=" + grouped.Length + ".");

        if (_printedUnknownForSession < 1)
        {
            _printedUnknownForSession++;
            var unresolved = tiles
                .Where(t => !t.Data.Group.RegionVerified)
                .Select(t => t.Id)
                .Distinct()
                .Take(30);
            Plugin.Logger.LogInfo(
                "[QuestHelper] Unassigned mission IDs (up to 30): " +
                string.Join(", ", unresolved));
            Plugin.Logger.LogInfo("[QuestHelper] Unknown mission regions remain visible. " +
                "No mission progress, rewards or native click handlers were changed.");
        }
    }

    private static object? Heading(object document, string label, bool isRegion)
    {
        // No custom fonts, blue bars, coloured panels or Unity Canvas overlays.
        // Standard markup inherits the existing inventory document's fonts,
        // parchment colours, and layout. Orbus already renders <p>/<b>.
        object? heading = PowerUiRuntime.Make(document, "p");
        if (heading == null) return null;
        string escaped = label.Replace("&", "&amp;")
            .Replace("<", "&lt;").Replace(">", "&gt;");
        if (!PowerUiRuntime.Set(heading, "innerHTML",
                (isRegion ? "<b>" : "") + escaped +
                (isRegion ? "</b>" : "")))
            return null;

        object? style = PowerUiRuntime.Get(heading, "style");
        // Indentation only; avoid changing fonts, colours and menu scaling.
        if (!isRegion) PowerUiRuntime.Set(style, "marginLeft", "12px");
        PowerUiRuntime.Set(style, "clear", "both");
        return heading;
    }

    private static bool TryMakeCollapsible(object heading, object body)
    {
        // If the PowerUI build doesn't support managed-to-native DOM events,
        // leave the content expanded rather than making a dead control.
        return PowerUiRuntime.BindMouseDown(heading, () =>
        {
            string state = PowerUiRuntime.Text(
                PowerUiRuntime.Get(body, "style"), "display");
            PowerUiRuntime.Display(body,
                string.Equals(state, "none", StringComparison.OrdinalIgnoreCase)
                    ? "block" : "none");
        }, RetainedHandlers);
    }

    internal static void Stop() => Restore();

    private static void Restore()
    {
        if (_container == null || _archive == null)
        {
            ClearHandles();
            return;
        }

        try
        {
            // Reassemble the original native DOM, in its original order.
            foreach (object node in OriginalChildren)
                PowerUiRuntime.Append(_container, node);
            object? rootParent = PowerUiRuntime.Get(_root, "parentNode");
            if (rootParent != null && _root != null)
                PowerUiRuntime.Call(rootParent, "removeChild", _root);
            object? archiveParent = PowerUiRuntime.Get(_archive, "parentNode");
            if (archiveParent != null)
                PowerUiRuntime.Call(archiveParent, "removeChild", _archive);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[QuestHelper] Restoring original UI: " + ex.Message);
        }
        finally { ClearHandles(); }
    }

    private static void ClearHandles()
    {
        _document = null;
        _container = null;
        _archive = null;
        _root = null;
        _lastCount = 0;
        OriginalChildren.Clear();
        RetainedHandlers.Clear();
    }
}
