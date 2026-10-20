using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusQuestHelper;

[BepInPlugin("com.horizon.orbus.questhelper", "OrbusQuestHelper", "0.4.3")]
public sealed class Plugin : BasePlugin
{
    // Hard safety lock on ALL v0.3.x DOM-reparenting. That component is never
    // registered. The opt-in HTML prefix operates only on string arguments
    // before the game's own renderer sees them.
    internal const bool NativeMissionEditingSuspended = true;

    internal static ManualLogSource Logger = null!;
    internal static ConfigEntry<bool> RegionGroupingEnabled = null!;
    internal static ConfigEntry<bool> HtmlRegionTaskGrouping = null!;
    internal static ConfigEntry<string> GroupingView = null!;
    internal static ConfigEntry<string> SpadesMode = null!;
    internal static ConfigEntry<bool> AutoOrganiseMissions = null!;
    internal static ConfigEntry<bool> ReadOnlySpadesProbe = null!;
    internal static ConfigEntry<bool> CaptureMissionHtmlOnce = null!;

    public override void Load()
    {
        Logger = Log;
        RegionGroupingEnabled = Config.Bind(
            "Missions", "EnableRegionTaskGrouping", false,
            "DEPRECATED, IGNORED: unsafe DOM-reparenting remains hard-disabled. " +
            "Use EnableHtmlRegionTaskGrouping for the new, opt-in HTML implementation.");

        // Keep legacy settings readable, but do not require an old
        // opt-in flag that was false on every first-time install.
        HtmlRegionTaskGrouping = Config.Bind(
            "Missions", "EnableHtmlRegionTaskGrouping", true,
            "Legacy compatibility setting; v0.4.3 uses automatic setup. " +
            "Nobody needs to edit this value for QuestHelper to work.");

        AutoOrganiseMissions = Config.Bind(
            "Missions", "AutoOrganiseMissions", true,
            "Automatically group Missions on supported Community and " +
            "Spades clients. Enabled at install, including upgrades " +
            "from older QuestHelper versions.");

        ReadOnlySpadesProbe = Config.Bind(
            "Investigation", "ReadOnlySpadesProbe", false,
            "Developer diagnostic only. When true, Spades' HTML hook logs " +
            "its original input without modifying it.");

        GroupingView = Config.Bind(
            "Missions", "GroupingView", "Region",
            "The initial Missions sorting view: Region or Activity. " +
            "The in-game View: Region | Activity selector saves your choice.");

        SpadesMode = Config.Bind(
            "Missions", "SpadesMode", "Auto",
            "Legacy v0.4.2 diagnostic setting, now ignored. " +
            "Both editions are automatically detected. No editing needed.");

        CaptureMissionHtmlOnce = Config.Bind(
            "Investigation", "CaptureMissionHtmlOnce", false,
            "Optional one-shot read-only capture of original Missions HTML " +
            "under BepInEx/config/OrbusQuestHelper/Diagnostics. " +
            "OFF by default; turns itself off when the capture succeeds.");

        string variant = PowerUiRuntime.FindType("PowerUI.Document") != null
            ? "legacy Document/Element"
            : PowerUiRuntime.FindType("PowerUI.HtmlDocument") != null
                ? "modern HtmlDocument/HtmlElement"
                : "not yet resolved";

        Log.LogWarning("[QuestHelper] v0.4.3: all old DOM-reparenting " +
            "implementation remains hard-disabled.");
        Log.LogInfo("[QuestHelper] PowerUI runtime: " + variant);
        Log.LogInfo("[QuestHelper] Automatic pre-render HTML organiser " +
            "enabled=" + AutoOrganiseMissions.Value +
            " (legacy opt-in setting ignored).");

        if (CaptureMissionHtmlOnce.Value)
        {
            if (PowerUiRuntime.FindType("PowerUI.Document") != null)
            {
                AddComponent<MissionHtmlCapture>();
                Log.LogInfo("[QuestHelper] Read-only legacy Missions HTML " +
                    "capture registered.");
            }
            else
            {
                // Spades has no legacy PlayerMenu.document. Its one-shot
                // original HTML is captured by the read-only modern setter
                // prefix, so no extra Unity component is registered.
                Log.LogInfo("[QuestHelper] Spades original HTML capture " +
                    "armed through modern setter observer only.");
            }
        }

        MissionHtmlPatch.InstallIfEnabled();
        if (AutoOrganiseMissions.Value && MissionHtmlPatch.IsTransformEnabled)
        {
            AddComponent<MissionViewSwitcher>();
            Log.LogInfo("[QuestHelper] In-game Region / Activity selector " +
                "active. Starting view=" + GroupingView.Value);
        }
    }
}
