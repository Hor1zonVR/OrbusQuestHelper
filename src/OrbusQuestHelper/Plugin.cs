using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusQuestHelper;

// Auto uses the native level HUD when active, otherwise the VR eye camera.
// Users can choose the anchor explicitly in RebornMenu for comparison.
internal enum TrackerAnchor { Auto, LevelHud, Headset }

[BepInPlugin("com.horizon.orbus.questhelper", "OrbusQuestHelper", "0.4.9")]
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
    internal static ConfigEntry<bool> ShowPinnedHud = null!;
    internal static ConfigEntry<TrackerAnchor> HudAnchor = null!;
    internal static ConfigEntry<float> HudSize = null!;

    public override void Load()
    {
        Logger = Log;
        MissionPinStore.Initialize(Config);
        ShowPinnedHud = Config.Bind(
            "HUD", "ShowPinnedMissions", true,
            "Show up to three pinned missions' real checklist progress " +
            "near the level HUD. Does not replace the level display.");
        HudAnchor = Config.Bind(
            "HUD", "TrackerAnchor", TrackerAnchor.Auto,
            new ConfigDescription(
                "Auto follows the visible level HUD; falls back to a small " +
                "headset-attached tracker if that native label is hidden.",
                null, "RebornMenu.Label=Tracker Placement"));
        HudSize = Config.Bind(
            "HUD", "TrackerSize", 1f,
            new ConfigDescription("Size of the pinned mission tracker.",
                new AcceptableValueRange<float>(0.5f, 2f),
                "RebornMenu.Label=Tracker Text Size"));
        // Old v0.3/v0.4 compatibility switches are no longer bound:
        // EnableRegionTaskGrouping, EnableHtmlRegionTaskGrouping, SpadesMode.
        // Their values were ignored by the actual HTML grouping path.
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

        Log.LogWarning("[QuestHelper] v0.4.9: all old DOM-reparenting " +
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
            Log.LogInfo("[QuestHelper] Native Region/Activity tabs, favourites " +
                "and up to three persisted HUD pins are enabled. " +
                "Progress is read from native Mission.checklist only; " +
                "original mission state remains untouched.");
        }
    }
}
