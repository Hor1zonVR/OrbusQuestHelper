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
        internal static StubOption? HtmlRegionTaskGrouping = new() { Value = false };
        internal static StubOption? AutoOrganiseMissions = new();
        internal static StubOption ReadOnlySpadesProbe = new() { Value = false };
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
        internal static object? ObservedMissionsContainer { get; set; }
        internal static object? FindInsideMissions(object? doc, string id) =>
            doc != null ? ById(doc, id) :
            id == "missions-container" ? ObservedMissionsContainer : null;
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
        internal static object? Call(object? target, string name, params object?[] args) => null;
        internal static List<object> Enumerate(object? source, int limit = 512) => new();
        internal static bool Same(object? a, object? b) =>
            a != null && b != null && ReferenceEquals(a, b);
        internal static bool BindMouseDown(object node, Action handler,
            IList<object> keepAlive, out string failure)
        {
            failure = "";
            if (node is not PowerUI.Element element)
            {
                failure = "Synthetic node unsupported";
                return false;
            }
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
        // The read-only tracker probe must use ORIGINAL mission IDs and
        // must never sample generated weekly timestamp-like IDs.
        var probeSample = MissionTrackingProbe.SampleMissionIds(
            "<div class='vendor-mission data-missionid-1700000000000-3-0'></div>" +
            html + html);
        Require(probeSample.Count == 3 &&
            probeSample[0] == "hunter3" &&
            probeSample[1] == "capture7" &&
            probeSample[2] == "battle5",
            "Tracking probe must sample unique regular IDs and skip weekly IDs");
        Require(!MissionTrackingProbe.HasRun, "Probe must not run before the first Unity Update");
        MissionTrackingProbe.TryRun();
        Require(MissionTrackingProbe.HasRun, "Tracker metadata probe should run after first HTML transform");
        MissionTrackingProbe.TryRun(); // must remain a single diagnostic pass
        Console.WriteLine("PASS: bounded one-shot read-only tracker discovery and original IDs");
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
        Require(!PowerUiRuntime.NativeContainer.innerHTML.Contains(
            "<b>Critter Capture</b>", StringComparison.Ordinal),
            "DOM event handlers must queue rather than rebuild during dispatch");
        MissionViewBinding.ApplyPending();
        Require(PowerUiRuntime.NativeContainer.innerHTML.Contains(
            "<b>Critter Capture</b>", StringComparison.Ordinal),
            "Activity button must switch to Activity mode after Update");
        PowerUiRuntime.ClickHandlers[MissionHtmlTransformer.RegionControlId]();
        MissionViewBinding.ApplyPending();
        Require(PowerUiRuntime.NativeContainer.innerHTML.Contains(
            "<b>Highsteppe</b>", StringComparison.Ordinal),
            "Region button must switch back to Region mode");
        MissionViewBinding.TryBind();
        Require(PowerUiRuntime.ClickHandlers.Count == 2,
            "Rebinding the same controls must not create additional listeners");

        object[] other = { new PowerUI.Element { id = "achievements-container" }, html };
        prefix.Invoke(null, other);
        Require((string)other[1] == html,
            "Prefix must not transform another inventory section");

        // Spades uses a different PowerUI class but inherits the native
        // setter from Dom.Element. Old v0.4.2 config values MUST NOT prevent
        // the organiser from automatically working on an updated install.
        PowerUiRuntime.Modern = true;
        Plugin.SpadesMode.Value = "Observe"; // stale diagnostic setting
        MissionHtmlPatch.InstallIfEnabled();
        object modernElement = new Dom.Element
            { id = "missions-container", innerHTML = "" };
        object[] modernAuto = { modernElement, html };
        prefix.Invoke(null, modernAuto);
        Require(((string)modernAuto[1]).Contains(MissionHtmlTransformer.Marker),
            "Spades must auto-group missions despite old Observe config");
        Require(MissionHtmlPatch.IsTransformEnabled,
            "Spades must automatically activate safe view controls");
        Require(PowerUiRuntime.ObservedMissionsDocument != null,
            "Observed Spades mission element must reveal its ownerDocument");
        Require(ReferenceEquals(PowerUiRuntime.ObservedMissionsContainer, modernElement),
            "Spades must cache exact observed missions-container for selector fallback");
        foreach (var tile in new[] { a, b, c })
            Require(((string)modernAuto[1]).Contains(tile, StringComparison.Ordinal),
                "Spades auto-group must retain native mission markup verbatim");

        // Explicit investigation-only mode must not rewrite any Spades HTML.
        Plugin.ReadOnlySpadesProbe.Value = true;
        object[] observe = { modernElement, html };
        prefix.Invoke(null, observe);
        Require((string)observe[1] == html,
            "Spades diagnostic Observe mode MUST NOT rewrite HTML");
        Require(!MissionHtmlPatch.IsTransformEnabled,
            "Diagnostic read-only mode must not enable mutating view controls");
        Console.WriteLine("PASS: Spades modern Dom.Element automatic grouping, stale flags ignored, optional diagnostic remains read-only");
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
