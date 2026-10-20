// Syntax/algorithm check ONLY. These minimal stubs do not simulate real
// IL2CPP Harmony installation. Actual game interop requires the user's build.
using System;
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
namespace PowerUI
{
    public sealed class Document { }
    public sealed class Element
    {
        public string id { get; set; } = string.Empty;
        public void set_innerHTML(string html) { }
    }
}
namespace OrbusQuestHelper
{
    internal sealed class StubOption { public bool Value = true; }
    internal sealed class StubLogger
    {
        public void LogWarning(string text) => Console.WriteLine("WARN " + text);
        public void LogInfo(string text) => Console.WriteLine("INFO " + text);
    }
    internal static class Plugin
    {
        internal static StubOption? HtmlRegionTaskGrouping = new();
        internal static StubLogger Logger = new();
    }
    internal static class PowerUiRuntime
    {
        internal static Type? FindType(string fullName) => fullName switch
        {
            "PowerUI.Document" => typeof(PowerUI.Document),
            "PowerUI.Element" => typeof(PowerUI.Element),
            _ => null
        };
        internal static string Text(object owner, string name) =>
            name == "id" && owner is PowerUI.Element element
                ? element.id : string.Empty;
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

        object[] other = { new PowerUI.Element { id = "achievements-container" }, html };
        prefix.Invoke(null, other);
        Require((string)other[1] == html,
            "Prefix must not transform another inventory section");
        Console.WriteLine("PASS: managed prefix argument rewrite, tile preservation, unrelated target untouched");
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
