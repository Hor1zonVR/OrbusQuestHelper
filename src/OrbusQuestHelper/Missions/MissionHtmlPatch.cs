using System;
using System.Linq;
using System.Reflection;
using System.IO;
using System.Text;
using BepInEx;
using HarmonyLib;

namespace OrbusQuestHelper;

/// <summary>
/// Opt-in PRE-RENDER interception of the exact native PowerUI HTML setter.
/// Unlike the old organiser, this never moves or appends a rendered DOM node.
/// Only installed on the known generic client's Element API, and only the
/// original missions-container HTML is eligible for transformation.
/// </summary>
internal static class MissionHtmlPatch
{
    private static Harmony? _harmony;
    private static bool _failed;
    private static bool _inPrefix;
    private static bool _loggedFirstMatch;
    private static bool _modern;
    private static int _modernSamples;
    private static bool _capturedModern;
    private static int _rewrites;
    private static string? _cachedOriginal;
    private static string? _cachedReplacement;
    private static MissionHtmlTransformer.GroupView _view =
        MissionHtmlTransformer.GroupView.Region;
    internal static bool IsAvailable => _harmony != null && !_failed;
    internal static MissionHtmlTransformer.GroupView CurrentView => _view;

    internal static void InstallIfEnabled()
    {
        if (Plugin.HtmlRegionTaskGrouping == null ||
            !Plugin.HtmlRegionTaskGrouping.Value)
        {
            Plugin.Logger.LogInfo("[QuestHelper] v0.4.0 HTML organiser is OFF " +
                "(opt in with Missions.EnableHtmlRegionTaskGrouping=true).");
            return;
        }

        // Persist the preferred default view between game sessions.
        _view = string.Equals(Plugin.GroupingView.Value, "Activity",
            StringComparison.OrdinalIgnoreCase)
            ? MissionHtmlTransformer.GroupView.Activity
            : MissionHtmlTransformer.GroupView.Region;

        // One plugin binary supports both clients. The generic game uses
        // PowerUI.Element, whereas Spades inherits the HTML setter from
        // Dom.Element in Assembly-CSharp-firstpass.dll.
        Type? legacy = PowerUiRuntime.FindType("PowerUI.Element");
        Type? modern = PowerUiRuntime.FindType("Dom.Element");
        bool hasModernDocument =
            PowerUiRuntime.FindType("PowerUI.HtmlDocument") != null;

        _modern = legacy == null && modern != null && hasModernDocument;
        Type? element = _modern ? modern : legacy;
        if (element == null)
        {
            Plugin.Logger.LogWarning("[QuestHelper] No supported PowerUI HTML " +
                "setter class found (legacy PowerUI.Element or modern Dom.Element).");
            return;
        }

        MethodInfo? setter = element.GetMethods(
            BindingFlags.Public | BindingFlags.NonPublic |
            BindingFlags.Instance)
            .FirstOrDefault(m => m.Name == "set_innerHTML" &&
                m.ReturnType == typeof(void) &&
                m.GetParameters().Length == 1 &&
                m.GetParameters()[0].ParameterType == typeof(string));

        MethodInfo? prefix = typeof(MissionHtmlPatch).GetMethod(
            nameof(BeforeInnerHtml), BindingFlags.NonPublic |
            BindingFlags.Static);

        if (setter == null || prefix == null)
        {
            Plugin.Logger.LogWarning("[QuestHelper] HTML organiser not installed: " +
                "the native " + element.FullName +
                ".set_innerHTML(string) signature differs.");
            return;
        }

        try
        {
            _harmony = new Harmony("com.horizon.orbus.questhelper.htmlregions");
            _harmony.Patch(setter, prefix: new HarmonyMethod(prefix));
            Plugin.Logger.LogWarning("[QuestHelper] v0.4.2 " +
                (_modern ? "Spades modern DOM" : "generic legacy PowerUI") +
                " HTML setter hook installed (" + setter.DeclaringType?.FullName +
                "). Old DOM reparenting remains disabled.");
            if (_modern)
                Plugin.Logger.LogInfo("[QuestHelper] SpadesMode=" +
                    Plugin.SpadesMode.Value +
                    " (Observe logs mission writes without changing HTML; " +
                    "Transform is opt-in after capture validation).");
        }
        catch (Exception ex)
        {
            _failed = true;
            Plugin.Logger.LogWarning("[QuestHelper] HTML hook could not install: " +
                ex.GetType().Name + ": " + ex.Message +
                ". Vanilla Missions preserved.");
        }
    }

    /// <summary>
    /// Called only when the user presses a native PowerUI view selector.
    /// Re-render exactly ONE existing missions-container via its native
    /// innerHTML setter, from the original HTML string saved by the prefix.
    /// This is not a timed rebuild; no original DOM nodes are manipulated.
    /// </summary>
    internal static bool ChangeView(MissionHtmlTransformer.GroupView requested)
    {
        if (!IsAvailable || _cachedOriginal == null) return false;
        if (requested == _view) return true;

        try
        {
            object? doc = PowerUiRuntime.PlayerMenuDocument();
            if (doc == null) return false;
            object? container = PowerUiRuntime.ById(doc, "missions-container");
            if (container == null) return false;
            string currentMarkup = PowerUiRuntime.Text(container, "innerHTML");
            if (!currentMarkup.Contains(MissionHtmlTransformer.Marker,
                    StringComparison.Ordinal))
                return false;

            if (!MissionHtmlTransformer.TryTransformForTarget(
                    "missions-container", _cachedOriginal, requested,
                    out string newMarkup, out var report) ||
                report.OriginalIcons != report.OutputIcons)
            {
                Plugin.Logger.LogWarning("[QuestHelper] Switch to " + requested +
                    " refused: HTML validation failed. Current view unchanged.");
                return false;
            }

            if (!PowerUiRuntime.Set(container, "innerHTML", newMarkup))
            {
                Plugin.Logger.LogWarning("[QuestHelper] PowerUI native HTML " +
                    "setter unavailable. Current view unchanged.");
                return false;
            }

            _view = requested;
            _cachedReplacement = newMarkup;
            Plugin.GroupingView.Value = requested.ToString();
            Plugin.Logger.LogInfo("[QuestHelper] Missions switched to By " +
                requested + ": " + report.OutputIcons + " original icons preserved.");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[QuestHelper] Unable to switch grouping: " +
                ex.GetType().Name + " " + ex.Message +
                ". Existing Missions HTML left in place.");
            return false;
        }
    }

    private static void ObserveSpadesSetter(object element, string html)
    {
        // Logging-only: this method never writes back to the setter argument
        // or modifies any DOM nodes. Limit observations to prevent log spam.
        string id = PowerUiRuntime.Text(element, "id");
        bool nativeTiles = html.Contains("data-missionid-",
                StringComparison.Ordinal) &&
            html.Contains("vendor-mission", StringComparison.Ordinal);
        bool target = string.Equals(id, "missions-container",
            StringComparison.Ordinal);
        if (!nativeTiles && !target) return;

        if (_modernSamples++ < 4)
        {
            Plugin.Logger.LogInfo("[QuestHelper] Spades original HTML setter: " +
                "elementType=" + element.GetType().FullName +
                " id='" + id + "'" +
                " chars=" + html.Length +
                " missionTileMarkers=" +
                (html.Split(new[] { "data-missionid-" },
                    StringSplitOptions.None).Length - 1) +
                " nativeTemplate=" + nativeTiles +
                " ownerDocument=" +
                (PowerUiRuntime.Get(element, "ownerDocument")?.GetType().FullName ??
                 "not exposed"));
        }

        if (target && nativeTiles)
        {
            PowerUiRuntime.ObservedMissionsDocument =
                PowerUiRuntime.Get(element, "ownerDocument") ??
                PowerUiRuntime.Get(element, "document");
        }

        if (!nativeTiles || _capturedModern ||
            !Plugin.CaptureMissionHtmlOnce.Value)
            return;

        // A private one-shot ORIGINAL string capture. Do not require a
        // PlayerMenu.document reference in the modern client: it is missing
        // from the supplied Spades interop report.
        string dir = Path.Combine(Paths.ConfigPath,
            "OrbusQuestHelper", "Diagnostics");
        Directory.CreateDirectory(dir);
        string path = Path.Combine(dir,
            DateTime.Now.ToString("yyyyMMdd_HHmmss") +
            "_SpadesMissionsOriginal.html");
        File.WriteAllText(path, html, new UTF8Encoding(false));
        _capturedModern = true;
        Plugin.CaptureMissionHtmlOnce.Value = false;
        Plugin.Logger.LogInfo("[QuestHelper] Captured original Spades " +
            "mission-like HTML without changing the UI: " + path);
    }

    // Harmony uses __0 for the original setter's first argument; changing it
    // here means native PowerUI receives the completed grouped HTML instead
    // of parsing and rendering the old flat list first.
    private static void BeforeInnerHtml(object __instance, ref string __0)
    {
        if (_failed || _inPrefix || __instance == null ||
            string.IsNullOrEmpty(__0))
            return;

        // In Observe mode on Spades, NEVER change the original setter string.
        // Report only the first few matching HTML writes, including target ID.
        if (_modern)
        {
            bool missionLike = __0.Length > 150 &&
                (__0.Contains("mission", StringComparison.OrdinalIgnoreCase) ||
                 __0.Contains("quest", StringComparison.OrdinalIgnoreCase));
            if (missionLike)
            {
                try { ObserveSpadesSetter(__instance, __0); }
                catch (Exception ex)
                {
                    if (_modernSamples++ == 0)
                        Plugin.Logger.LogWarning("[QuestHelper] Modern HTML probe: " +
                            ex.GetType().Name + " " + ex.Message);
                }
            }

            if (!string.Equals(Plugin.SpadesMode.Value, "Transform",
                    StringComparison.OrdinalIgnoreCase))
                return;
        }

        if (!__0.Contains("data-missionid-", StringComparison.Ordinal) ||
            !__0.Contains("vendor-mission", StringComparison.Ordinal) ||
            __0.Contains(MissionHtmlTransformer.Marker, StringComparison.Ordinal))
            return;

        try
        {
            _inPrefix = true;
            string target = PowerUiRuntime.Text(__instance, "id");
            if (!string.Equals(target, "missions-container",
                    StringComparison.Ordinal))
                return;

            if (_modern)
                PowerUiRuntime.ObservedMissionsDocument =
                    PowerUiRuntime.Get(__instance, "ownerDocument") ??
                    PowerUiRuntime.Get(__instance, "document");

            if (!_loggedFirstMatch)
            {
                _loggedFirstMatch = true;
                Plugin.Logger.LogInfo("[QuestHelper] Native Missions innerHTML " +
                    "setter intercepted before rendering; attempting " +
                    "lossless compact " + _view + " grouping.");
            }

            if (_cachedOriginal != null &&
                string.Equals(_cachedOriginal, __0, StringComparison.Ordinal))
            {
                __0 = _cachedReplacement!;
                return;
            }

            if (!MissionHtmlTransformer.TryTransformForTarget(
                    target, __0, _view, out string grouped, out var report))
            {
                // Never replace the original argument if the transformer
                // rejects it. Disable this feature for this session to
                // prevent a repeated failing transformation loop.
                _failed = true;
                Plugin.Logger.LogWarning("[QuestHelper] HTML organiser " +
                    "rejected this mission template: " + report.Reason +
                    ". Auto-disabled for this session. Vanilla HTML preserved.");
                return;
            }

            // Additional runtime invariant. The pure transformer has already
            // validated the entire tile-HTML multiset, not just the count.
            if (report.OriginalIcons == 0 ||
                report.OriginalIcons != report.OutputIcons)
            {
                _failed = true;
                Plugin.Logger.LogWarning("[QuestHelper] Mission tile count " +
                    "validation failed. No HTML was changed; grouping suspended.");
                return;
            }

            _cachedOriginal = __0;
            _cachedReplacement = grouped;
            __0 = grouped;
            _rewrites++;
            if (_rewrites == 1 || _rewrites == 5)
                Plugin.Logger.LogInfo("[QuestHelper] Native HTML transformed " +
                    "before render: icons=" + report.OutputIcons +
                    " matchedRegions=" + report.ClassifiedRegions +
                    " view=" + _view +
                    " matchedTasks=" + report.ClassifiedTasks +
                    " nativeWrites=" + _rewrites + ".");
        }
        catch (Exception ex)
        {
            _failed = true;
            Plugin.Logger.LogWarning("[QuestHelper] HTML intercept error: " +
                ex.GetType().Name + ": " + ex.Message +
                ". Returning original mission HTML from now on.");
        }
        finally
        {
            _inPrefix = false;
        }
    }
}
