using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusQuestHelper;

[BepInPlugin("com.horizon.orbus.questhelper", "OrbusQuestHelper", "0.1.5")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger = null!;

    public override void Load()
    {
        Logger = Log;
        // Event-oriented diagnostics replace the old frequent full-scene snapshots.
        AddComponent<UiInteractionWatcher>();
        Log.LogInfo("[QuestHelper] v0.1.5: VR UI pointer/selection diagnostics enabled.");
        Log.LogInfo("[QuestHelper] Left and right VR ray target changes + EventSystem selections.");
        Log.LogInfo("[QuestHelper] Ctrl+Alt+K: capture active UI canvases and selectable elements.");
        Log.LogInfo("[QuestHelper] Report: BepInEx/config/OrbusQuestHelper/Diagnostics/*_UiInteractions.txt");
    }
}
