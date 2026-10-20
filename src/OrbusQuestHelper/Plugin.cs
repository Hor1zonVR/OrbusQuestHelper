using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusQuestHelper;

[BepInPlugin("com.horizon.orbus.questhelper", "OrbusQuestHelper", "0.4.2")]
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
    internal static ConfigEntry<bool> CaptureMissionHtmlOnce = null!;

    public override void Load()
    {
        Logger = Log;
        RegionGroupingEnabled = Config.Bind(
            "Missions", "EnableRegionTaskGrouping", false,
            "DEPRECATED, IGNORED: unsafe DOM-reparenting remains hard-disabled. " +
            "Use EnableHtmlRegionTaskGrouping for the new, opt-in HTML implementation.");

        HtmlRegionTaskGrouping = Config.Bind(
            "Missions", "EnableHtmlRegionTaskGrouping", false,
            "Reorganise original Missions HTML as a compact By Region or " +
            "By Activity list before PowerUI renders it. OFF by default. " +
            "Supports generic PowerUI. On Spades the hook defaults " +
            "to read-only Observe mode; no DOM nodes are moved. " +
            "Restart Orbus after changing this setting.");

        GroupingView = Config.Bind(
            "Missions", "GroupingView", "Region",
            "The initial Missions sorting view: Region or Activity. " +
            "The in-game View: Region | Activity selector saves your choice.");

        SpadesMode = Config.Bind(
            "Missions", "SpadesMode", "Observe",
            "Spades' newer DOM setter is currently a compatibility test. " +
            "Observe logs original HTML only; Transform enables the same " +
            "lossless Region/Activity transformer if compatible. " +
            "Do not use Transform until its original markup is validated.");

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

        Log.LogWarning("[QuestHelper] v0.4.2: all old DOM-reparenting " +
            "implementation remains hard-disabled.");
        Log.LogInfo("[QuestHelper] PowerUI runtime: " + variant);
        Log.LogInfo("[QuestHelper] Opt-in pre-render HTML organiser " +
            "enabled=" + HtmlRegionTaskGrouping.Value);

        if (CaptureMissionHtmlOnce.Value)
        {
            AddComponent<MissionHtmlCapture>();
            Log.LogInfo("[QuestHelper] Read-only original Missions HTML " +
                "capture registered.");
        }

        MissionHtmlPatch.InstallIfEnabled();
        if (HtmlRegionTaskGrouping.Value && MissionHtmlPatch.IsTransformEnabled)
        {
            AddComponent<MissionViewSwitcher>();
            Log.LogInfo("[QuestHelper] In-game Region / Activity selector " +
                "active. Starting view=" + GroupingView.Value);
        }
    }
}
