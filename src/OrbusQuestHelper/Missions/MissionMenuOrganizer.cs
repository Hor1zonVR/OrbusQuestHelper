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
            ["Battlegrounds"] = 5,
            ["Dungeons"] = 6,
            ["Multiple Regions"] = 80,
            ["Unassigned Region"] = 90
        };

    private static float _nextCheck;
    private static object? _document;
    private static object? _container;
    private static object? _root;
    private sealed class NativePlacement
    {
        public object Node = null!;
        public object Parent = null!;
        public object? NextSibling;
    }

    private sealed class NativeVisibility
    {
        public object Node = null!;
        public string Display = "";
    }

    private static readonly List<NativePlacement> OriginalTileParents = new();
    private static readonly List<object> OriginalChildren = new();
    private static readonly List<NativeVisibility> OriginalVisibility = new();
    private static readonly List<object> RetainedHandlers = new();
    private static readonly HashSet<string> StatusMessages = new();
    private static float _nextHeartbeat;
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

        // currentInventoryPage is a NUMERIC inventory paging value (e.g. 1),
        // not the selected UI tab name. v0.3.0 misread it as a string and
        // prevented Missions grouping from running. Never gate on it.
        object? doc = PowerUiRuntime.PlayerMenuDocument();
        if (doc == null)
        {
            Note("Waiting for live PlayerMenu document.");
            Restore();
            return;
        }
        object? content = PowerUiRuntime.ById(doc, ContainerId);
        if (Time.realtimeSinceStartup >= _nextHeartbeat)
        {
            _nextHeartbeat = Time.realtimeSinceStartup + 20f;
            Plugin.Logger.LogInfo("[QuestHelper] DOM heartbeat: container=" +
                (content == null ? "missing" : "present") +
                ", organisedRoot=" +
                (PowerUiRuntime.ById(doc, GroupRootId) == null
                    ? "absent" : "present") + ".");
        }
        if (content == null)
        {
            Note("Missions container not found in " +
                doc.GetType().FullName + ".");
            Restore();
            return;
        }

        if (_root != null)
        {
            bool intact = PowerUiRuntime.Same(doc, _document) &&
                PowerUiRuntime.Same(content, _container) &&
                PowerUiRuntime.ById(doc, GroupRootId) != null;
            if (intact) return;
            // If the same page lost only our root, always unhide/restore the
            // original wrappers BEFORE dropping handles. A full document
            // replacement is the only case where old handles are stale.
            if (PowerUiRuntime.Same(doc, _document) &&
                PowerUiRuntime.Same(content, _container))
                Restore();
            else
                ClearHandles();
        }

        var nativeIcons = PowerUiRuntime.ByClass(content, "vendor-mission");
        var tiles = new List<Tile>();
        int missingIds = 0;
        foreach (object node in nativeIcons)
        {
            if (!PowerUiRuntime.IsDescendant(node, content)) continue;
            string? id = MissionInfoReader.MissionId(node);
            if (string.IsNullOrWhiteSpace(id))
            {
                missingIds++;
                continue;
            }
            tiles.Add(new Tile
            {
                Node = node,
                Id = id,
                Data = MissionInfoReader.Read(id)
            });
        }
        // It is crucial not to hide original quest icons merely because
        // an unfamiliar PowerUI build omitted their data-missionid class.
        if (missingIds != 0)
        {
            Note("Some original mission tiles have no readable ID (" +
                missingIds + "); leaving vanilla layout untouched.");
            return;
        }

        // Notify on transitions rather than only on the initial empty DOM.
        if (nativeIcons.Count > 0)
            Note("Mission icon DOM populated: " + nativeIcons.Count +
                " native icons; " + tiles.Count + " with readable IDs.");
        // Do not change empty, hidden or unrelated tabs. If the actual menu
        // structure differs between builds, fail open to vanilla instead.
        if (tiles.Count < 2)
        {
            Note("No usable vendor-mission tiles inside missions-container yet.");
            Note(PowerUiRuntime.Summary(content));
            return;
        }

        // Do not depend on numeric currentInventoryPage, and do not use the
        // permanently-present navigation tab as proof of a Missions screen.
        // Positive proof comes from live native mission tiles inside the
        // actual missions-container and a safe reversible DOM transaction.
        Apply(doc, content, tiles);
    }

    private static void Note(string message)
    {
        if (StatusMessages.Add(message))
            Plugin.Logger.LogInfo("[QuestHelper] " + message);
    }

    private static int PixelOr(object node, string property, int fallback)
    {
        try
        {
            object? n = PowerUiRuntime.Get(node, property);
            int actual = n == null ? 0 : Convert.ToInt32(n);
            return actual > 0 ? actual : fallback;
        }
        catch { return fallback; }
    }

    private static void Apply(object document, object content, List<Tile> tiles)
    {
        object? root = PowerUiRuntime.Make(document, "div");
        if (root == null)
        {
            Note("Apply stopped BEFORE changes: createElement(div) returned null.");
            return;
        }

        // v0.3.3 moved old content into a detached archive, but the game's
        // existing render geometry stayed alive at the top of the parchment.
        // This version leaves the original DOM IN PLACE and hides it using
        // the native display property while rendering a replacement first.
        if (!PowerUiRuntime.Set(root, "id", GroupRootId))
        {
            Note("Apply stopped BEFORE changes: cannot set grouping root ID.");
            return;
        }
        if (!PowerUiRuntime.SetDomClass(root, "vendor"))
        {
            Note("Apply stopped BEFORE changes: native vendor CSS class cannot be set.");
            return;
        }

        Note("Missions loaded: preparing region/task groups for " +
            tiles.Count + " original icons.");
        // Build the new layout and all headings before touching the original
        // DOM. A failed element creation should not leave an empty Missions tab.
        var grouped = tiles
            .GroupBy(t => t.Data.Group.Region)
            .OrderBy(g => RegionOrder.TryGetValue(g.Key, out int n) ? n : 70)
            .ThenBy(g => g.Key, StringComparer.Ordinal)
            .ToArray();

        // The legacy PowerUI ElementStyle metadata has set_height but NO
        // set_clear property, and PowerUI.Element has no setAttribute API.
        // Consequently v0.3.2 silently abandoned all layout generation.
        // Instead, make explicit-height native rows to contain floated icons.
        int panelWidth = PixelOr(content, "pixelWidth", 390);
        int iconWidth = tiles.Select(t => PixelOr(t.Node, "pixelWidth", 72))
            .Where(n => n > 0).DefaultIfEmpty(72).Max();
        int iconHeight = tiles.Select(t => PixelOr(t.Node, "pixelHeight", 68))
            .Where(n => n > 0).DefaultIfEmpty(68).Max();
        iconWidth = Math.Clamp(iconWidth, 60, 100);
        iconHeight = Math.Clamp(iconHeight + 18, 85, 140);
        // Slightly conservative: never pack icons tightly against the
        // parchment's right scrollbar.
        int columns = Math.Clamp((panelWidth - 35) / (iconWidth + 12), 1, 5);
        Note("Native grid measurements: panelWidth=" + panelWidth +
            ", iconWidth=" + iconWidth + ", rowHeight=" + iconHeight +
            ", columns=" + columns + ".");

        var destinations = new Dictionary<Tile, object>();
        int headingsWithClicks = 0;

        foreach (var region in grouped)
        {
            object? regionSection = PowerUiRuntime.Make(document, "div");
            object? regionHeader = Heading(document, region.Key, isRegion: true);
            object? regionBody = PowerUiRuntime.Make(document, "div");
            if (regionSection == null || regionHeader == null ||
                regionBody == null)
            {
                Note("Apply stopped BEFORE changes: region section or heading creation failed.");
                return;
            }

            if (!PowerUiRuntime.Append(regionSection, regionHeader) ||
                !PowerUiRuntime.Append(regionSection, regionBody) ||
                !PowerUiRuntime.Append(root, regionSection))
            {
                Note("Apply stopped BEFORE changes: region HTML append failed.");
                return;
            }

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
                    taskBody == null)
                {
                    Note("Apply stopped BEFORE changes: task heading creation failed.");
                    return;
                }

                if (!PowerUiRuntime.Append(taskSection, taskHeader) ||
                    !PowerUiRuntime.Append(taskSection, taskBody) ||
                    !PowerUiRuntime.Append(regionBody, taskSection))
                {
                    Note("Apply stopped BEFORE changes: task HTML append failed.");
                    return;
                }

                if (TryMakeCollapsible(taskHeader, taskBody))
                    headingsWithClicks++;

                Tile[] taskTiles = activity.ToArray();
                for (int first = 0; first < taskTiles.Length; first += columns)
                {
                    object? row = PowerUiRuntime.Make(document, "div");
                    if (row == null)
                    {
                        Note("Apply stopped BEFORE changes: mission row creation failed.");
                        return;
                    }

                    // Both old and new PowerUI ElementStyle export height.
                    // This avoids unsupported clear:both while preventing
                    // floats from overlapping the next task's heading.
                    object? style = PowerUiRuntime.Get(row, "style");
                    if (!PowerUiRuntime.Set(style, "height",
                            iconHeight + "px") ||
                        !PowerUiRuntime.Append(taskBody, row))
                    {
                        Note("Apply stopped BEFORE changes: PowerUI cannot set " +
                            "explicit row height or append native row.");
                        return;
                    }

                    foreach (Tile tile in taskTiles.Skip(first).Take(columns))
                        destinations[tile] = row;
                }
            }
        }

        if (destinations.Count != tiles.Count)
        {
            Note("Apply stopped BEFORE changes: destination count " +
                destinations.Count + "/" + tiles.Count + ".");
            return;
        }
        List<object> originals = PowerUiRuntime.Enumerate(
            PowerUiRuntime.Get(content, "childNodes"), 1024);
        if (originals.Count == 0)
        {
            Note("Apply stopped BEFORE changes: mission container childNodes " +
                "returned no original elements.");
            return;
        }

        // Preserve original nodes, order and inline display styles before
        // any mutation. The original wrappers remain under the native scroll
        // container, so its viewport, clipping and scaling stay unchanged.
        _document = document;
        _container = content;
        _root = root;
        OriginalChildren.Clear();
        OriginalChildren.AddRange(originals);
        OriginalTileParents.Clear();
        OriginalVisibility.Clear();
        foreach (Tile tile in tiles)
        {
            object? parent = PowerUiRuntime.Get(tile.Node, "parentNode");
            if (parent == null)
            {
                Restore();
                Plugin.Logger.LogWarning("[QuestHelper] Original icon parent unavailable; vanilla UI preserved.");
                return;
            }
            OriginalTileParents.Add(new NativePlacement
            {
                Node = tile.Node,
                Parent = parent,
                NextSibling = PowerUiRuntime.Get(tile.Node, "nextSibling")
            });
        }
        _lastCount = 0;

        // First child inside missions-container, never appended below the
        // vanilla header/list. This is the native scrollable viewport.
        if (!PowerUiRuntime.InsertBefore(content, root, originals[0]))
        {
            Restore();
            Plugin.Logger.LogWarning("[QuestHelper] insertBefore unsupported: vanilla Missions preserved.");
            return;
        }

        // Start the user at the first region, not at a leftover scroll
        // offset from the native Weekly/Level mission list.
        PowerUiRuntime.Set(content, "scrollTop", 0);

        // Hide original wrapper/layout nodes WHILE they are attached to the
        // live document, so PowerUI invalidates their existing draw geometry.
        // Avoid hiding tiles that are direct children: those will be moved
        // into the new layout and must remain visible.
        foreach (object child in originals)
        {
            if (tiles.Any(t => PowerUiRuntime.Same(t.Node, child)))
                continue;
            object? style = PowerUiRuntime.Get(child, "style");
            if (style == null)
            {
                // Native childNodes can include whitespace text nodes.
                // They have no style setter and cannot paint quest icons;
                // don't treat those as errors. Non-whitespace text, though,
                // must not be left floating above the new menu.
                string rawText = PowerUiRuntime.Text(child, "textContent");
                if (!string.IsNullOrWhiteSpace(rawText))
                {
                    Restore();
                    Plugin.Logger.LogWarning("[QuestHelper] Visible original text node cannot be hidden; vanilla restored.");
                    return;
                }
                continue;
            }
            string originalDisplay = PowerUiRuntime.Text(style, "display");
            OriginalVisibility.Add(new NativeVisibility
            {
                Node = child,
                Display = originalDisplay
            });
            if (!PowerUiRuntime.Display(child, "none"))
            {
                Restore();
                Plugin.Logger.LogWarning("[QuestHelper] Can't hide original Missions rows; vanilla layout restored.");
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

        // A successful append call is not enough: verify the same number of
        // actual native mission entries still exists inside our new grid.
        int verifiedTiles = PowerUiRuntime.ByClass(root, "vendor-mission").Count;
        if (verifiedTiles != tiles.Count)
        {
            Restore();
            Plugin.Logger.LogWarning("[QuestHelper] Layout verification failed: " +
                verifiedTiles + "/" + tiles.Count +
                " native icons survived the move. Restored vanilla Missions.");
            return;
        }

        int resolved = tiles.Count(t => t.Data.Group.RegionVerified);
        int taskResolved = tiles.Count(t => t.Data.Group.ActivityVerified);
        int foundData = tiles.Count(t => t.Data.Found);
        Plugin.Logger.LogInfo("[QuestHelper] Native vendor grid (first child, original wrappers hidden in place), verified=" +
            verifiedTiles + " mission icons.");
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
        // EXACT same heading conventions found in Orbus's mission template:
        // white, 20px, 5px below. No blue panels or browser-default black.
        PowerUiRuntime.Set(style, "color", "#FFFFFF");
        PowerUiRuntime.Set(style, "fontSize", "20px");
        PowerUiRuntime.Set(style, "marginBottom", "5px");
        PowerUiRuntime.Set(style, "marginTop", "8px");
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
        if (_container == null)
        {
            ClearHandles();
            return;
        }

        try
        {
            // Move native tiles back first (their original VR hover/click
            // events survive), using the next sibling when it still exists.
            foreach (NativePlacement placement in OriginalTileParents)
            {
                if (placement.NextSibling != null &&
                    PowerUiRuntime.Same(
                        PowerUiRuntime.Get(placement.NextSibling, "parentNode"),
                        placement.Parent))
                {
                    if (PowerUiRuntime.InsertBefore(
                        placement.Parent, placement.Node,
                        placement.NextSibling))
                        continue;
                }
                PowerUiRuntime.Append(placement.Parent, placement.Node);
            }

            // Unhide the existing original wrappers. We never detach these,
            // so no second copy of the old menu can enter the render tree.
            foreach (NativeVisibility item in OriginalVisibility)
                PowerUiRuntime.Display(item.Node, item.Display);

            if (_root != null)
            {
                object? rootParent = PowerUiRuntime.Get(_root, "parentNode");
                if (rootParent != null)
                    PowerUiRuntime.Call(rootParent, "removeChild", _root);
            }
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
        _root = null;
        _lastCount = 0;
        OriginalChildren.Clear();
        OriginalTileParents.Clear();
        OriginalVisibility.Clear();
        RetainedHandlers.Clear();
    }
}
