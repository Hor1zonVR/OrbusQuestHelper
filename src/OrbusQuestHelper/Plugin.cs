using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusQuestHelper;

[BepInPlugin("com.horizon.orbus.questhelper", "OrbusQuestHelper", "0.3.5")]
public sealed class Plugin : BasePlugin
{
    internal const bool NativeMissionEditingSuspended = true;
    internal static ManualLogSource Logger = null!;
    internal static ConfigEntry<bool> RegionGroupingEnabled = null!;

    public override void Load()
    {
        Logger = Log;
        RegionGroupingEnabled = Config.Bind(
            "Missions",
            "EnableRegionTaskGrouping",
            false,
            "DISABLED IN v0.3.5 SAFE MODE. Future experimental Region > Activity UI " +
            "may be re-enabled only after PowerUI DOM mutation has been verified. " +
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

        Log.LogWarning("[QuestHelper] v0.3.5 SAFETY HOTFIX: native Missions layout editing is completely disabled.");
        Log.LogWarning("[QuestHelper] v0.3.4 repeatedly duplicated original mission nodes on failed insertBefore rollback (56 -> 112 -> 224 -> 448).");
        Log.LogWarning("[QuestHelper] Existing EnableRegionTaskGrouping=true settings are ignored in this version.");
        Log.LogInfo("[QuestHelper] PowerUI runtime: " + powerUiVariant);
        Log.LogInfo("[QuestHelper] No mission UI components registered; vanilla Missions layout stays untouched.");
        // DO NOT AddComponent<MissionMenuOrganizer>() here. The old PowerUI
        // appendChild operation did not preserve the original DOM nodes when
        // reinserting them. Editing is quarantined until the game's actual
        // menu DOM / render transaction semantics are understood.
    }
}
