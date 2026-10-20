using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusQuestHelper;

[BepInPlugin("com.horizon.orbus.questhelper", "OrbusQuestHelper", "0.3.6")]
public sealed class Plugin : BasePlugin
{
    // Hard safety lock on the old, broken DOM-reparenting implementation.
    // Its Unity component is NEVER registered in this version.
    internal const bool NativeMissionEditingSuspended = true;

    internal static ManualLogSource Logger = null!;
    internal static ConfigEntry<bool> RegionGroupingEnabled = null!;
    internal static ConfigEntry<bool> CaptureMissionHtmlOnce = null!;

    public override void Load()
    {
        Logger = Log;
        RegionGroupingEnabled = Config.Bind(
            "Missions", "EnableRegionTaskGrouping", false,
            "UNAVAILABLE IN v0.3.6: old native DOM reparenting is blocked " +
            "regardless of this setting to prevent mission duplication/freezes.");

        CaptureMissionHtmlOnce = Config.Bind(
            "Investigation", "CaptureMissionHtmlOnce", true,
            "Save one read-only original Missions HTML snapshot under " +
            "BepInEx/config/OrbusQuestHelper/Diagnostics when the list is stable. " +
            "Automatically turns off after saving. Never changes in-game UI.");

        string powerUiVariant = PowerUiRuntime.FindType("PowerUI.Document") != null
            ? "legacy Document/Element"
            : PowerUiRuntime.FindType("PowerUI.HtmlDocument") != null
                ? "modern HtmlDocument/HtmlElement"
                : "not yet resolved";

        Log.LogWarning("[QuestHelper] v0.3.6 SAFE MODE: region/task DOM " +
            "reparenting disabled regardless of config.");
        Log.LogInfo("[QuestHelper] PowerUI runtime: " + powerUiVariant);
        Log.LogInfo("[QuestHelper] Offline region/task HTML transformer " +
            "is available in source only, NOT hooked into the game.");
        if (CaptureMissionHtmlOnce.Value)
        {
            AddComponent<MissionHtmlCapture>();
            Log.LogInfo("[QuestHelper] One-shot READ-ONLY Missions HTML " +
                "capture enabled. Original game UI will be unchanged.");
        }
        else
            Log.LogInfo("[QuestHelper] One-shot HTML capture disabled in config.");
    }
}
