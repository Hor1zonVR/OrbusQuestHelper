using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusQuestHelper;

[BepInPlugin("com.horizon.orbus.questhelper", "OrbusQuestHelper", "0.1.0")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger = null!;

    public override void Load()
    {
        Logger = Log;
        AddComponent<QuestUiWatcher>();
        Log.LogInfo("[QuestHelper] v0.1.0 diagnostics-only build loaded.");
        Log.LogInfo("[QuestHelper] Read-only Missions and Journal UI inspection enabled.");
        Log.LogInfo("[QuestHelper] Ctrl+Alt+K = manual snapshot (focused Windows game).");
        Log.LogInfo("[QuestHelper] Reports: BepInEx/config/OrbusQuestHelper/Diagnostics/");
    }
}
