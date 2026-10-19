using System;
using System.Collections.Generic;
using Il2CppInterop.Runtime;
using UnityEngine;
using PowerUI;

namespace OrbusQuestHelper;

/// <summary>
/// Client-only navigation for the game's own Missions PowerUI DOM.
/// Does not alter mission state, native mission tiles or their event handlers.
/// </summary>
/// <summary>
/// Only Unity-safe primitive fields belong on this injected MonoBehaviour.
/// Il2Cpp ClassInjector reflects every field during AddComponent, so holding a
/// PowerUI.Element/OnDomEvent field here caused the v0.2.0 TypeLoadException.
/// The non-injected engine owns all PowerUI reference types.
/// </summary>
public sealed class MissionMenuOrganizer : MonoBehaviour
{
    public MissionMenuOrganizer(IntPtr pointer) : base(pointer) { }

    private float _nextFailure;

    private void Update()
    {
        try
        {
            MissionMenuHost.Tick();
        }
        catch (Exception ex)
        {
            if (Time.realtimeSinceStartup < _nextFailure) return;
            _nextFailure = Time.realtimeSinceStartup + 20f;
            Plugin.Logger.LogWarning("[QuestHelper] PowerUI engine failure: " +
                ex.GetType().Name + " " + ex.Message);
        }
    }

    private void OnDisable()
    {
        try { MissionMenuHost.Disable(); }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[QuestHelper] UI cleanup: " + ex.Message);
        }
    }
}

internal static class MissionMenuHost
{
    // Static outside injected MonoBehaviour; managed-only PowerUI reference
    // types must never appear in the class the IL2CPP injector registers.
    private static MissionMenuEngine? _engine;

    public static void Tick()
    {
        _engine ??= new MissionMenuEngine();
        _engine.Tick();
    }

    public static void Disable()
    {
        _engine?.Stop();
        _engine = null;
    }
}

internal sealed class MissionMenuEngine
{
    private const string NavId = "questhelper-missions-navigation";
    private const float PollSeconds = 1.5f;
    private static readonly string[] WeeklyIds =
    {
        "PlayerMenu-YourWeeklyMissions-header",
        "PlayerMenu-AvailableWeeklyMissions-header"
    };
    private static readonly string[] RegularIds =
    {
        "PlayerMenu-YourLevelMissions-header",
        "PlayerMenu-YourMissions-header",
        "PlayerMenu-AvailableLevelMissions-header"
    };

    private float _nextPoll;
    private float _nextWarning;
    private IntPtr _documentPtr;
    private Element? _navigation;
    private readonly List<OnDomEvent> _handlers = new();

    private enum Destination { Weekly, Regular, Top }

    public void Tick()
    {
        if (Time.realtimeSinceStartup < _nextPoll) return;
        _nextPoll = Time.realtimeSinceStartup + PollSeconds;
        if (Plugin.NavigationEnabled == null ||
            !Plugin.NavigationEnabled.Value) return;

        try
        {
            // PlayerMenu is the game's real static PowerUI menu controller.
            Document? document = PlayerMenu.document;
            if (document == null) return;

            if (document.Pointer != _documentPtr)
            {
                _documentPtr = document.Pointer;
                _navigation = null;
                _handlers.Clear();
            }

            Element? tab = document.getElementById("missions-tab");
            if (tab == null) return;
            if (document.getElementById(NavId) != null) return;

            InsertNavigation(document, tab);
        }
        catch (Exception ex)
        {
            if (Time.realtimeSinceStartup < _nextWarning) return;
            _nextWarning = Time.realtimeSinceStartup + 15f;
            Plugin.Logger.LogWarning("[QuestHelper] PowerUI menu unavailable: " +
                ex.GetType().Name + " " + ex.Message);
        }
    }

    private static Element? FindFirst(Document document, string[] ids)
    {
        foreach (string id in ids)
        {
            Element? node = document.getElementById(id);
            if (node != null) return node;
        }
        return null;
    }

    private void InsertNavigation(Document document, Element tab)
    {
        Element? weeklyHeader = FindFirst(document, WeeklyIds);
        // Prefer the weekly heading as a host near the top of the native page.
        // Fallback is the actual mission tab; no existing HTML is rebuilt.
        Element host = weeklyHeader ?? tab;

        Element nav = document.createElement("div");
        nav.id = NavId;
        nav.style.display = "block";
        nav.style.backgroundColor = "#18354D";
        nav.style.border = "1px solid #4B9FB5";
        nav.style.padding = "6px";
        nav.style.marginTop = "5px";
        nav.style.marginBottom = "8px";

        Element label = document.createElement("span");
        label.innerHTML = "QUESTHELPER";
        label.style.color = "#A8EAF7";
        label.style.fontSize = "15px";
        label.style.fontWeight = "bold";
        label.style.marginRight = "8px";
        nav.appendChild(label);

        bool weeklyButton = AddButton(document, nav, "Weekly", Destination.Weekly);
        bool regularButton = AddButton(document, nav, "Regular", Destination.Regular);
        bool topButton = AddButton(document, nav, "Top", Destination.Top);

        host.appendChild(nav);
        _navigation = nav;
        Accent(document, WeeklyIds, "#A6EAF7");
        Accent(document, RegularIds, "#F4D58D");
        Plugin.Logger.LogInfo("[QuestHelper] Missions navigation inserted at " +
            (weeklyHeader != null ? "weekly header" : "tab fallback") +
            "; button registration: " + weeklyButton + ", " +
            regularButton + ", " + topButton);
    }

    private bool AddButton(Document document, Element bar,
        string name, Destination target)
    {
        Element button = document.createElement("span");
        button.id = "questhelper-button-" + name.ToLowerInvariant();
        button.innerHTML = name;
        button.style.display = "inline-block";
        button.style.color = "#FFFFFF";
        button.style.fontSize = "16px";
        button.style.backgroundColor = "#315E78";
        button.style.border = "1px solid #7BB7C9";
        button.style.padding = "5px 8px";
        button.style.marginRight = "5px";
        bar.appendChild(button);

        try
        {
            Action<UIEvent> managed = _ => Navigate(target);
            OnDomEvent converted =
                DelegateSupport.ConvertDelegate<OnDomEvent>(managed);
            button.OnMouseDown += converted;
            // Keep managed-to-IL2CPP delegates alive between VR clicks.
            _handlers.Add(converted);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[QuestHelper] " + name +
                " button callback unavailable: " + ex.Message);
            return false;
        }
    }

    private static void Accent(Document document, string[] ids, string color)
    {
        foreach (string id in ids)
        {
            try
            {
                Element? header = document.getElementById(id);
                if (header == null) continue;
                header.style.color = color;
                header.style.fontWeight = "bold";
            }
            catch { }
        }
    }

    private void Navigate(Destination destination)
    {
        try
        {
            Document? document = PlayerMenu.document;
            if (document == null) return;

            Element? scroll = document.getElementById("missions-container")
                ?? document.getElementById("missions-tab");
            if (scroll == null) return;

            if (destination == Destination.Top)
            {
                scroll.scrollTo(0, 0);
                Plugin.Logger.LogInfo("[QuestHelper] Scroll to Missions top.");
                return;
            }

            Element? section = FindFirst(document,
                destination == Destination.Weekly ? WeeklyIds : RegularIds);
            if (section == null)
            {
                Plugin.Logger.LogInfo("[QuestHelper] " + destination +
                    " heading not present in current Missions document.");
                return;
            }

            int y = 0;
            Element? node = section;
            for (int depth = 0; node != null && depth < 30; depth++)
            {
                if (node.Pointer == scroll.Pointer) break;
                y += Math.Max(0, node.offsetTop);
                node = node.parentNode;
            }
            scroll.scrollTo(0, Math.Max(0, y));
            Plugin.Logger.LogInfo("[QuestHelper] Scroll to " +
                destination + " y=" + y);
        }
        catch (Exception ex)
        {
            Plugin.Logger.LogWarning("[QuestHelper] Navigation error: " +
                ex.GetType().Name + " " + ex.Message);
        }
    }

    public void Stop()
    {
        try
        {
            if (_navigation != null && _navigation.parentNode != null)
                _navigation.parentNode.removeChild(_navigation);
        }
        catch { }
        _navigation = null;
        _handlers.Clear();
    }
}
