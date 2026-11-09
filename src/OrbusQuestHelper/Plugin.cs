using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusQuestHelper;

// Auto follows the native XP/level HUD. Only explicit Headset mode
// attaches the tracker to the VR eye camera.
internal enum TrackerAnchor { Auto, LevelHud, Headset }

[BepInPlugin("com.horizon.orbus.questhelper", "OrbusQuestHelper", "0.4.14")]
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
    internal static ConfigEntry<string> LocalMissionDefinitionsFolder = null!;
    internal static ConfigEntry<string> SpadesMode = null!;
    internal static ConfigEntry<bool> AutoOrganiseMissions = null!;
    internal static ConfigEntry<bool> ReadOnlySpadesProbe = null!;
    internal static ConfigEntry<bool> CaptureMissionHtmlOnce = null!;
    internal static ConfigEntry<bool> ShowPinnedHud = null!;
    internal static ConfigEntry<TrackerAnchor> HudAnchor = null!;
    internal static ConfigEntry<float> HudSize = null!;
    internal static ConfigEntry<float> HudOffsetX = null!;
    internal static ConfigEntry<float> HudOffsetY = null!;

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
                "Auto follows the real level / XP HUD in world space " +
                "without parenting to it. Headset is optional only; " +
                "neither mode resizes the native level UI.",
                null, "RebornMenu.Label=Tracker Placement"));
        HudSize = Config.Bind(
            "HUD", "TrackerSize", 1f,
            new ConfigDescription("Scale of compact quest text. Default is now " +
                "approximately 25% of the oversized v0.4.9 text.",
                new AcceptableValueRange<float>(0.4f, 1.5f),
                "RebornMenu.Label=Tracker Text Size"));
        HudOffsetX = Config.Bind(
            "HUD", "TrackerOffsetX", 0f,
            new ConfigDescription(
                "Move the tracker horizontally relative to the native level label. " +
                "Expanded to one metre in either direction; old position is preserved.",
                new AcceptableValueRange<float>(-1f, 1f),
                "RebornMenu.Label=Tracker Left / Right (m)"));
        HudOffsetY = Config.Bind(
            "HUD", "TrackerOffsetY", 0f,
            new ConfigDescription(
                "Move the tracker vertically relative to the native level label. " +
                "Expanded to 75 centimetres up and down; old position is preserved.",
                new AcceptableValueRange<float>(-0.75f, 0.75f),
                "RebornMenu.Label=Tracker Up / Down (m)"));
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

        LocalMissionDefinitionsFolder = Config.Bind(
            "Missions", "LocalServerMissionsFolder", "",
            "Optional folder path to your own OrbusVR Community Edition " +
            "server's content/missions JSON directory. Read-only; game " +
            "files are NOT bundled with QuestHelper. Leave blank for " +
            "normal native quest tracking.");
        if (!string.IsNullOrWhiteSpace(LocalMissionDefinitionsFolder.Value))
        {
            var (loaded, duplicateIds) =
                MissionDefinitionIndex.LoadFromDirectory(
                    LocalMissionDefinitionsFolder.Value);
            Log.LogInfo("[QuestHelper/Missions] Loaded " + loaded +
                " local mission definition labels; ambiguous IDs=" +
                duplicateIds + ". Local paths and descriptions not logged.");
        }

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

        Log.LogWarning("[QuestHelper] v0.4.14: all old DOM-reparenting " +
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
        // Pinned missions are independent of the optional Missions HTML
        // organiser. This updater works as soon as gameplay HUD is ready,
        // even when Inventory > Missions has never been opened.
        AddComponent<MissionHudDriver>();
        Log.LogInfo("[QuestHelper/HUD] Always-on pinned mission reader started. " +
            "Local objectives polled independently of Missions UI; " +
            "Spades server refresh still requires a verified native request.");
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
