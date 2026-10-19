using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusQuestHelper;

[BepInPlugin("com.horizon.orbus.questhelper", "OrbusQuestHelper", "0.1.6")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger = null!;

    public override void Load()
    {
        Logger = Log;
        // Event-oriented diagnostics replace the old frequent full-scene snapshots.
        AddComponent<UiInteractionWatcher>();
        Log.LogInfo("[QuestHelper] v0.1.6: targeted Player Menu and VR input diagnostics enabled.");
        Log.LogInfo("[QuestHelper] Filtered left/right UI ray targets, Player Menu mesh changes and native input method signatures.");
        Log.LogInfo("[QuestHelper] Ctrl+Alt+K: capture active UI canvases and selectable elements.");
        Log.LogInfo("[QuestHelper] Report: BepInEx/config/OrbusQuestHelper/Diagnostics/*_UiInteractions.txt");
    }
}
