using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using BepInEx.Unity.IL2CPP;

namespace OrbusQuestHelper;

[BepInPlugin("com.horizon.orbus.questhelper", "OrbusQuestHelper", "0.2.1")]
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
            "Enable optional client-side Weekly / Regular / Top navigation inside the existing Missions tab. " +
            "Leaves server mission status, rewards, claim and turn-in actions unchanged.");

        // The build uses an Assembly-CSharp.dll from MODTEST, whereas the game
        // may load a different generated interop from a RebornManager instance.
        // Report the runtime location and type availability, rather than
        // assuming all generated assemblies have matching signatures.
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(a => string.Equals(a.GetName().Name,
                    "Assembly-CSharp", StringComparison.OrdinalIgnoreCase))
                .ToArray();
            Log.LogInfo("[QuestHelper] Runtime Assembly-CSharp count=" +
                assemblies.Length);
            foreach (Assembly assembly in assemblies)
            {
                Log.LogInfo("[QuestHelper] Runtime Assembly-CSharp path=" +
                    assembly.Location);
                string[] typeNames =
                {
                    "PowerUI.Document",
                    "PowerUI.Element",
                    "PowerUI.OnDomEvent",
                    "PlayerMenu"
                };
                foreach (string type in typeNames)
                {
                    try
                    {
                        var loadedType = assembly.GetType(type, false);
                        Log.LogInfo("[QuestHelper] Runtime type " + type +
                            ": " + (loadedType != null ? "present" : "MISSING"));
                    }
                    catch (Exception ex)
                    {
                        Log.LogWarning("[QuestHelper] Runtime type " + type +
                            " inspection failed: " + ex.GetType().Name +
                            " " + ex.Message);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Log.LogWarning("[QuestHelper] Assembly inspection failed: " +
                ex.GetType().Name + " " + ex.Message);
        }

        // Intentionally register ONLY the lightweight Unity-compatible shim.
        // The engine with PowerUI.Element fields is not an injected class.
        AddComponent<MissionMenuOrganizer>();
        Log.LogInfo("[QuestHelper] v0.2.1: PowerUI Missions navigation shim loaded.");
        Log.LogInfo("[QuestHelper] Awaiting live PlayerMenu document.");
        Log.LogInfo("[QuestHelper] Configuration: com.horizon.orbus.questhelper.cfg");
    }
}
