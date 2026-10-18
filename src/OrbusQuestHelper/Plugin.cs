using BepInEx;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusQuestHelper;

[BepInPlugin("com.horizon.orbus.questhelper", "OrbusQuestHelper", "0.1.2")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger = null!;

    public override void Load()
    {
        Logger = Log;
        AddComponent<QuestUiWatcher>();
        Log.LogInfo("[QuestHelper] v0.1.2: read-only UI discovery loaded.");
        Log.LogInfo("[QuestHelper] Improved native component and SuperTextMesh scanning.");
        Log.LogInfo("[QuestHelper] Auto scans no longer require an Overworld scene; all captures append to one session report.");
        Log.LogInfo("[QuestHelper] Reports: BepInEx/config/OrbusQuestHelper/Diagnostics/");
    }
}
