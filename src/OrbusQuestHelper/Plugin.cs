using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusQuestHelper;

[BepInPlugin("com.horizon.orbus.questhelper", "OrbusQuestHelper", "0.3.2")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger = null!;
    internal static ConfigEntry<bool> RegionGroupingEnabled = null!;

    public override void Load()
    {
        Logger = Log;
        RegionGroupingEnabled = Config.Bind(
            "Missions",
            "EnableRegionTaskGrouping",
            true,
            "Organise the EXISTING native Missions icons by Region > Activity. " +
            "Client-side only. Unknown regions stay visible under Unassigned Region. " +
            "Never changes progress, rewards, purchases or quest completion.");

        // The experimental v0.2.x Weekly/Regular/Top navigation is REMOVED.
        // No direct PlayerMenu or PowerUI interop references are used at
        // compile time, because different community clients use incompatible
        // Document/Element vs HtmlDocument/HtmlElement APIs.
        string powerUiVariant = PowerUiRuntime.FindType("PowerUI.Document") != null
            ? "legacy Document/Element"
            : PowerUiRuntime.FindType("PowerUI.HtmlDocument") != null
                ? "modern HtmlDocument/HtmlElement"
                : "not yet resolved";

        Log.LogInfo("[QuestHelper] v0.3.2 Region > Activity grouping enabled=" +
            RegionGroupingEnabled.Value);
        Log.LogInfo("[QuestHelper] PowerUI runtime: " + powerUiVariant);
        Log.LogInfo("[QuestHelper] Native original mission icons and event handlers preserved.");
        Log.LogInfo("[QuestHelper] PowerUI native vendor grid; legacy DOM remains detached; rows cleared and icons verified.");
        Log.LogInfo("[QuestHelper] Older navigation buttons are not part of this version.");
        Log.LogInfo("[QuestHelper] EnableRegionTaskGrouping=false restores vanilla layout.");

        AddComponent<MissionMenuOrganizer>();
    }
}
