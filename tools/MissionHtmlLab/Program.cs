using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using OrbusQuestHelper;

if (args.Length == 1 && args[0] == "--self-test")
{
    var tests = new (string Name, Action Check)[]
    {
        ("Preserves every original HTML tile verbatim", TestPreservesAllTiles),
        ("Preserves duplicate mission IDs as separate original icons", TestDuplicateId),
        ("Never transforms the result twice", TestIdempotence),
        ("Rejects unbalanced native HTML", TestInvalidMarkup),
        ("Rejects unfamiliar native wrapper children", TestUnknownWrapper),
        ("Refuses to remove interactive native headings", TestInteractiveHeader),
        ("Rejects oversized/unrecognised input", TestEmptyInput),
        ("Does not share mission counters between calls", TestRepeatedCalls)
    };
    int failures = 0;
    foreach (var (name, check) in tests)
    {
        try
        {
            check();
            Console.WriteLine("PASS " + name);
        }
        catch (Exception e)
        {
            failures++;
            Console.WriteLine("FAIL " + name + ": " + e.Message);
        }
    }
    Console.WriteLine($"{tests.Length - failures}/{tests.Length} checks passed.");
    Environment.ExitCode = failures == 0 ? 0 : 1;
    return;
}
if (args.Length != 4 || args[0] != "--input" || args[2] != "--output")
{
    Console.WriteLine("MissionHtmlLab: offline, no game changes.");
    Console.WriteLine("  dotnet run --project tools/MissionHtmlLab -- --self-test");
    Console.WriteLine("  dotnet run --project tools/MissionHtmlLab -- --input original.html --output grouped.html");
    Environment.ExitCode = 2;
    return;
}
string inputFile = args[1], outputFile = args[3];
if (!File.Exists(inputFile))
{
    Console.Error.WriteLine("Input file not found.");
    Environment.ExitCode = 2;
    return;
}
string original = File.ReadAllText(inputFile);
if (MissionHtmlTransformer.TryTransform(original,
        MissionHtmlTransformer.FromCatalog, out string grouped,
        out MissionHtmlTransformer.Report result))
{
    File.WriteAllText(outputFile, grouped);
    Console.WriteLine("PASS: " + result.Reason);
    Console.WriteLine($"Original icons: {result.OriginalIcons}; Output icons: {result.OutputIcons}");
    Console.WriteLine($"Matched region: {result.ClassifiedRegions}; Matched task: {result.ClassifiedTasks}");
    Console.WriteLine("Preview saved: " + Path.GetFullPath(outputFile));
}
else
{
    // Fail closed. Never replace or overwrite original input.
    Console.WriteLine("NOT TRANSFORMED: " + result.Reason);
    Environment.ExitCode = 1;
}

static void Assert(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}
static string Tile(string id, string label = "")
{
    // Faithful to the native IL2CPP string-literal template, but uses
    // synthetic IDs/icons and contains no proprietary game resource.
    return "<div class='inventory-box-lowres data-boxtype-vendor vendor-mission " +
        "data-missionid-" + id +
        "' onmousedown='PlayerMenu.missionClick' " +
        "onmouseover='PlayerMenu.missionHover' " +
        "onmouseout='PlayerMenu.missionStopHover'>" +
        "<img src='synthetic-icon/" + (label.Length == 0 ? id : label) +
        "' style='width:100%;height:100%;' /></div>";
}
static string Native(params string[] tiles) =>
    "<div class='vendor'>" +
    "<p style='color: #FFFFFF; font-size: 20px;margin-bottom:5px'>Weekly Missions</p>" +
    string.Concat(tiles) +
    "<p style='color: #FFFFFF; font-size: 20px;margin-bottom:5px'>Your Missions</p>" +
    "</div>";
static void VerifyMarkup(string input, IEnumerable<string> exactTiles)
{
    bool ok = MissionHtmlTransformer.TryTransform(input,
        MissionHtmlTransformer.FromCatalog, out string output, out var result);
    Assert(ok, "Should transform: " + result.Reason);
    Assert(result.OriginalIcons == result.OutputIcons,
        "Original and output icon counts must match");
    Assert(output.Contains(MissionHtmlTransformer.Marker), "Should have idempotence marker");
    foreach (string tile in exactTiles)
    {
        Assert(output.Split(new[] { tile }, StringSplitOptions.None).Length - 1 ==
            input.Split(new[] { tile }, StringSplitOptions.None).Length - 1,
            "Original mission element was changed or duplicated: " + tile);
    }
}
static void TestPreservesAllTiles()
{
    string[] tiles =
    {
        Tile("hunter3"), Tile("capture7"), Tile("fishing25"), Tile("unmapped-test"),
        Tile("battle10")
    };
    string source = Native(tiles);
    VerifyMarkup(source, tiles);
    bool ok = MissionHtmlTransformer.TryTransform(source,
        MissionHtmlTransformer.FromCatalog, out string result, out var report);
    Assert(ok, "Expected success");
    Assert(result.Contains("<b>Highsteppe</b>"), "Region should be included");
    Assert(result.Contains("Critter Capture"), "Task should be included");
    Assert(result.Contains("Unassigned Region"), "Unknown quest should remain visible");
    Assert(report.ClassifiedRegions >= 3, "Should classify catalog entries");
    Assert(result.IndexOf(Tile("capture7"), StringComparison.Ordinal) <
        result.IndexOf(Tile("hunter3"), StringComparison.Ordinal),
        "Catalog tasks should be grouped rather than original input order");
}
static void TestDuplicateId()
{
    string a = Tile("hunter3", "A"), b = Tile("hunter3", "B");
    VerifyMarkup(Native(a, b, Tile("capture7")), new[] { a, b, Tile("capture7") });
}
static void TestIdempotence()
{
    string input = Native(Tile("hunter3"), Tile("capture7"));
    Assert(MissionHtmlTransformer.TryTransform(input, MissionHtmlTransformer.FromCatalog,
        out string first, out _), "First transform failed");
    Assert(!MissionHtmlTransformer.TryTransform(first, MissionHtmlTransformer.FromCatalog,
        out string second, out _), "Second transform should not run");
    Assert(first == second, "Second pass must leave result unchanged");
}
static void TestInvalidMarkup()
{
    string source = "<div class='vendor'>" + Tile("hunter3") +
        "<div class='vendor-mission'>";
    Assert(!MissionHtmlTransformer.TryTransform(source, MissionHtmlTransformer.FromCatalog,
        out string output, out _), "Malformed HTML must be rejected");
    Assert(output == source, "Malformed HTML must stay unchanged");
}
static void TestUnknownWrapper()
{
    string source = Native(Tile("hunter3"), Tile("capture7"))
        .Replace("</div>", "</div><span class='different-ui'>unsupported</span>",
            StringComparison.Ordinal);
    Assert(!MissionHtmlTransformer.TryTransform(source, MissionHtmlTransformer.FromCatalog,
        out string output, out _), "Unexpected native children must not disappear");
    Assert(output == source, "Source must remain unchanged on refusal");
}
static void TestInteractiveHeader()
{
    string source = Native(Tile("hunter3"), Tile("capture7"))
        .Replace("Weekly Missions", "<button onclick='claim()'>Weekly Missions</button>",
            StringComparison.Ordinal);
    Assert(!MissionHtmlTransformer.TryTransform(source, MissionHtmlTransformer.FromCatalog,
        out string output, out _), "Interactive headings cannot be thrown away");
    Assert(output == source, "Source must remain unchanged");
}
static void TestEmptyInput()
{
    Assert(!MissionHtmlTransformer.TryTransform("",
        MissionHtmlTransformer.FromCatalog, out string output, out _),
        "Empty HTML should not pass");
    Assert(output == "", "Empty HTML must remain empty");
}
static void TestRepeatedCalls()
{
    string[] tiles = Enumerable.Range(1, 80)
        .Select(i => Tile("synthetic-" + i)).ToArray();
    for (int i = 0; i < 10; i++)
        VerifyMarkup(Native(tiles), tiles);
}
