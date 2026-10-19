using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusQuestHelper;

[BepInPlugin("com.horizon.orbus.questhelper", "OrbusQuestHelper", "0.2.0")]
public sealed class Plugin : BasePlugin
{
    internal static ManualLogSource Logger = null!;
    internal static ConfigEntry<bool> NavigationEnabled = null!;

    public override void Load()
    {
        Logger = Log;
        NavigationEnabled = Config.Bind(
            "Missions",
            "EnableQuickNavigation",
            true,
            "Add client-side Weekly / Regular / Top navigation to the PowerUI Missions page. " +
            "Does not change quest data, rewards or existing mission click handlers.");

        AddComponent<MissionMenuOrganizer>();
        Log.LogInfo("[QuestHelper] v0.2.0: PowerUI Missions navigation loaded.");
        Log.LogInfo("[QuestHelper] Original mission tiles, hover and turn-in methods remain untouched.");
        Log.LogInfo("[QuestHelper] Disable through BepInEx/config/com.horizon.orbus.questhelper.cfg");
    }
}
