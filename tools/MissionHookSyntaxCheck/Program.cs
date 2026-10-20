// Syntax/algorithm check ONLY. These minimal stubs do not simulate real
// IL2CPP Harmony installation. Actual game interop requires the user's build.
using System;
using System.Collections.Generic;
using System.Reflection;
using OrbusQuestHelper;

namespace HarmonyLib
{
    public sealed class Harmony
    {
        public Harmony(string id) { }
        public void Patch(MethodBase original, HarmonyMethod? prefix = null) { }
    }
    public sealed class HarmonyMethod
    {
        public HarmonyMethod(MethodInfo prefix) { }
    }
}
namespace UnityEngine
{
    public class MonoBehaviour
    {
        public MonoBehaviour(IntPtr pointer) { }
    }
    public static class Time
    {
        public static float realtimeSinceStartup => 0;
    }
}
namespace BepInEx
{
    public static class Paths
    {
        public static string ConfigPath => System.IO.Path.GetTempPath();
    }
}
namespace Dom
{
    public class Element
    {
        public string id = "";
        public string innerHTML = "";
        public object? ownerDocument = new PowerUI.HtmlDocument();
        public void set_innerHTML(string html) => innerHTML = html;
    }
}
namespace PowerUI
{
    public sealed class HtmlDocument { }
    public sealed class Document { }
    public sealed class Element
    {
        public string id { get; set; } = string.Empty;
        public string innerHTML = string.Empty;
        public void set_innerHTML(string html) => innerHTML = html;
    }
}
namespace OrbusQuestHelper
{
    internal sealed class StubOption { public bool Value = true; }
    internal sealed class StubMode { public string Value = "Region"; }
    internal sealed class StubSpadesMode { public string Value = "Observe"; }
    internal sealed class StubLogger
    {
        public void LogWarning(string text) => Console.WriteLine("WARN " + text);
        public void LogInfo(string text) => Console.WriteLine("INFO " + text);
    }
    internal static class Plugin
    {
        internal static StubOption? HtmlRegionTaskGrouping = new();
        internal static StubMode GroupingView = new();
        internal static StubSpadesMode SpadesMode = new();
        internal static StubOption CaptureMissionHtmlOnce = new() { Value = false };
        internal static StubLogger Logger = new();
    }
    internal static class PowerUiRuntime
    {
        internal static bool Modern;
        internal static Type? FindType(string fullName) => fullName switch
        {
            "PowerUI.Document" when !Modern => typeof(PowerUI.Document),
            "PowerUI.Element" when !Modern => typeof(PowerUI.Element),
            "PowerUI.HtmlDocument" when Modern => typeof(PowerUI.HtmlDocument),
            "Dom.Element" when Modern => typeof(Dom.Element),
            _ => null
        };
        internal static object? ObservedMissionsDocument { get; set; }
        internal static object? Get(object owner, string name) =>
            owner is Dom.Element el && name == "ownerDocument"
                ? el.ownerDocument : null;
        internal static readonly PowerUI.Element NativeContainer = new()
            { id = "missions-container" };
        internal static readonly PowerUI.Element RegionButton = new()
            { id = MissionHtmlTransformer.RegionControlId };
        internal static readonly PowerUI.Element ActivityButton = new()
            { id = MissionHtmlTransformer.ActivityControlId };
        internal static readonly Dictionary<string, Action> ClickHandlers = new();
        internal static object? PlayerMenuDocument() => new PowerUI.Document();
        internal static object? ById(object doc, string id) => id switch
        {
            "missions-container" => NativeContainer,
            MissionHtmlTransformer.RegionControlId => RegionButton,
            MissionHtmlTransformer.ActivityControlId => ActivityButton,
            _ => null
        };
        internal static bool Same(object? a, object? b) =>
            a != null && b != null && ReferenceEquals(a, b);
        internal static bool BindMouseDown(object node, Action handler,
            IList<object> keepAlive)
        {
            if (node is not PowerUI.Element element) return false;
            ClickHandlers[element.id] = handler;
            keepAlive.Add(handler);
            return true;
        }
        internal static bool Set(object element, string name, object value)
        {
            if (element is PowerUI.Element native && name == "innerHTML")
            {
                native.innerHTML = (string)value;
                return true;
            }
            return false;
        }
        internal static string Text(object owner, string name)
        {
            if (owner is PowerUI.Element element)
            {
                if (name == "id") return element.id;
                if (name == "innerHTML") return element.innerHTML;
            }
            if (owner is Dom.Element modern)
            {
                if (name == "id") return modern.id;
                if (name == "innerHTML") return modern.innerHTML;
            }
            return string.Empty;
        }
    }
}

internal static class Program
{
    private static void Main()
    {
        MissionHtmlPatch.InstallIfEnabled();
        string a = Tile("hunter3"), b = Tile("capture7"), c = Tile("battle5");
        string html =
            "<div class='vendor'><div class='vendor-reputation'>" +
            "<p>New Missions Available</p><p>Your Level Missions:</p>" +
            a + b + c + "</div></div>";
        var prefix = typeof(MissionHtmlPatch).GetMethod(
            "BeforeInnerHtml", BindingFlags.NonPublic | BindingFlags.Static)!;
        object[] invoke = { new PowerUI.Element { id = "missions-container" }, html };
        prefix.Invoke(null, invoke);
        string rewritten = (string)invoke[1];
        Require(rewritten.Contains(MissionHtmlTransformer.Marker),
            "Prefix must transform correctly scoped original Missions HTML");
        foreach (var tile in new[] { a, b, c })
            Require(rewritten.Contains(tile, StringComparison.Ordinal),
                "Prefix must preserve each mission tile unchanged");

        // Test manual mode change on the existing exact Missions container,
        // using the saved original HTML rather than the already-sorted one.
        PowerUiRuntime.NativeContainer.innerHTML = rewritten;
        Require(MissionHtmlPatch.ChangeView(MissionHtmlTransformer.GroupView.Activity),
            "Expected the Activity selector to change the displayed page");
        string activityHtml = PowerUiRuntime.NativeContainer.innerHTML;
        Require(activityHtml.Contains("<b>Critter Capture</b>",
            StringComparison.Ordinal), "Activity heading missing");
        Require(!activityHtml.Contains("<b>Highsteppe</b>",
            StringComparison.Ordinal), "Activity view should be flat");
        Require(Plugin.GroupingView.Value == "Activity",
            "Chosen view should persist to config");
        Require(MissionHtmlPatch.ChangeView(MissionHtmlTransformer.GroupView.Region),
            "Expected the Region selector to switch back");
        Require(PowerUiRuntime.NativeContainer.innerHTML.Contains(
            "<b>Highsteppe</b>", StringComparison.Ordinal),
            "Region heading should be restored");
        foreach (var tile in new[] { a,b,c })
            Require(PowerUiRuntime.NativeContainer.innerHTML.Contains(tile),
                "Original mission markup must survive view swaps");

        // Simulated native click event binding on the two generated spans.
        MissionViewBinding.TryBind();
        Require(PowerUiRuntime.ClickHandlers.Count == 2,
            "Both native view selectors must have a click handler");
        PowerUiRuntime.ClickHandlers[MissionHtmlTransformer.ActivityControlId]();
        Require(PowerUiRuntime.NativeContainer.innerHTML.Contains(
            "<b>Critter Capture</b>", StringComparison.Ordinal),
            "Activity button must switch to Activity mode");
        PowerUiRuntime.ClickHandlers[MissionHtmlTransformer.RegionControlId]();
        Require(PowerUiRuntime.NativeContainer.innerHTML.Contains(
            "<b>Highsteppe</b>", StringComparison.Ordinal),
            "Region button must switch back to Region mode");

        object[] other = { new PowerUI.Element { id = "achievements-container" }, html };
        prefix.Invoke(null, other);
        Require((string)other[1] == html,
            "Prefix must not transform another inventory section");

        // Spades uses a different PowerUI class but inherits the native
        // setter from Dom.Element. First confirm Observe leaves HTML untouched.
        PowerUiRuntime.Modern = true;
        Plugin.SpadesMode.Value = "Observe";
        MissionHtmlPatch.InstallIfEnabled();
        object modernElement = new Dom.Element
            { id = "missions-container", innerHTML = "" };
        object[] observe = { modernElement, html };
        prefix.Invoke(null, observe);
        Require((string)observe[1] == html,
            "Spades Observe mode MUST NOT change any original HTML string");
        Require(!MissionHtmlPatch.IsTransformEnabled,
            "Spades Observe mode must not activate mutating UI view controls");
        Require(PowerUiRuntime.ObservedMissionsDocument != null,
            "Observed Spades mission element must reveal its ownerDocument");
        Plugin.SpadesMode.Value = "Transform";
        object[] transform = { modernElement, html };
        prefix.Invoke(null, transform);
        Require(((string)transform[1]).Contains(MissionHtmlTransformer.Marker),
            "Explicit Spades Transform mode should reuse lossless grouping");
        Require(MissionHtmlPatch.IsTransformEnabled,
            "Explicit Spades Transform mode should enable view controls");
        foreach (var tile in new[] { a, b, c })
            Require(((string)transform[1]).Contains(tile, StringComparison.Ordinal),
                "Spades transformation must retain native tiles verbatim");
        Console.WriteLine("PASS: Spades modern Dom.Element hook, Observe is read-only, Transform preserves icons");
        Console.WriteLine("PASS: managed prefix argument rewrite, flat view switching, config persistence, tile preservation, unrelated target untouched");
    }
    private static string Tile(string id) =>
        "<div class='inventory-box-lowres vendor-mission data-missionid-" +
        id + "' onmouseover='PlayerMenu.missionHover'>" +
        "<img src='synthetic/" + id + "'/></div>";
    private static void Require(bool condition, string message)
    {
        if (!condition) throw new Exception(message);
    }
}
