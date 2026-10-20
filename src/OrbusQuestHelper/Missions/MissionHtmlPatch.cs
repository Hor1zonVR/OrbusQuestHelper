using System;
using System.Linq;
using System.Reflection;
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
    private static int _rewrites;
    private static string? _cachedOriginal;
    private static string? _cachedReplacement;

    internal static void InstallIfEnabled()
    {
        if (Plugin.HtmlRegionTaskGrouping == null ||
            !Plugin.HtmlRegionTaskGrouping.Value)
        {
            Plugin.Logger.LogInfo("[QuestHelper] v0.4.0 HTML organiser is OFF " +
                "(opt in with Investigation.EnableHtmlRegionTaskGrouping=true).");
            return;
        }

        // We have verified the legacy generic build's native call sequence.
        // The FourOfSpades HtmlElement API needs a different verified hook.
        Type? legacy = PowerUiRuntime.FindType("PowerUI.Element");
        if (legacy == null ||
            PowerUiRuntime.FindType("PowerUI.Document") == null)
        {
            Plugin.Logger.LogWarning("[QuestHelper] HTML organiser not installed: " +
                "the supported legacy PowerUI.Element/Document API was not found.");
            return;
        }

        MethodInfo? setter = legacy.GetMethods(
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
                "the native PowerUI.Element.set_innerHTML(string) signature differs.");
            return;
        }

        try
        {
            _harmony = new Harmony("com.horizon.orbus.questhelper.htmlregions");
            _harmony.Patch(setter, prefix: new HarmonyMethod(prefix));
            Plugin.Logger.LogWarning("[QuestHelper] v0.4.0 experimental " +
                "HTML organiser HOOK INSTALLED. No DOM reparenting. " +
                "Only original missions-container HTML will be changed.");
        }
        catch (Exception ex)
        {
            _failed = true;
            Plugin.Logger.LogWarning("[QuestHelper] HTML hook could not install: " +
                ex.GetType().Name + ": " + ex.Message +
                ". Vanilla Missions preserved.");
        }
    }

    // Harmony uses __0 for the original setter's first argument; changing it
    // here means native PowerUI receives the completed grouped HTML instead
    // of parsing and rendering the old flat list first.
    private static void BeforeInnerHtml(object __instance, ref string __0)
    {
        if (_failed || _inPrefix || __instance == null ||
            string.IsNullOrEmpty(__0) ||
            !__0.Contains("data-missionid-", StringComparison.Ordinal) ||
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

            if (!_loggedFirstMatch)
            {
                _loggedFirstMatch = true;
                Plugin.Logger.LogInfo("[QuestHelper] Native Missions innerHTML " +
                    "setter intercepted before rendering; attempting " +
                    "lossless Region > Task grouping.");
            }

            if (_cachedOriginal != null &&
                string.Equals(_cachedOriginal, __0, StringComparison.Ordinal))
            {
                __0 = _cachedReplacement!;
                return;
            }

            if (!MissionHtmlTransformer.TryTransformForTarget(
                    target, __0, out string grouped, out var report))
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
