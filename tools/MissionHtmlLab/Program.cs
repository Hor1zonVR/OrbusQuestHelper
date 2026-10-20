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
        ("Does not share mission counters between calls", TestRepeatedCalls),
        ("Handles the actual captured vendor-reputation structure", TestCapturedStructure),
        ("Preserves a native new-missions announcement", TestKeepsAnnouncement),
        ("Rejects unfamiliar nested content without dropping it", TestUnknownNestedNode),
        ("Refuses HTML writes outside the native Missions container", TestStrictTarget),
        ("Rejects empty or non-mission HTML in Missions container", TestUnpopulatedTarget),
        ("Targeted runtime transformation remains lossless", TestTargetedRealShape),
        ("Region view stays flat but orders tasks together", TestFlatRegionView),
        ("Activity view stays flat and combines regions", TestFlatActivityView),
        ("Switch labels use compact native paragraph and unique IDs", TestViewSwitchMarkup),
        ("All fishing variants appear under one Fishing heading", TestCombinedFishing),
        ("Spades weekly missions preserve native obfuscated handlers", TestSpadesWeeklyBothViews),
        ("Unknown weekly icon remains visible without invented task", TestSpadesUnknownWeekly)
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
    "<p style='color: #FFFFFF; font-size: 20px;margin-bottom:5px'>Your Level Missions:</p>" +
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
    Assert(!result.Contains(">Critter Capture</p>", StringComparison.Ordinal),
        "Region view must not add a nested activity subheading");
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
        .Replace("Your Level Missions:", "<button onclick='claim()'>Your Level Missions:</button>",
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
static string PlayerMissionTile(string id, string rank = "none") =>
    "<div class=\"inventory-box-lowres mission-" + rank +
    " data-boxtype-playermission vendor-mission data-missionid-" + id +
    "\" style=\"z-index:60\" onmouseover=\"PlayerMenu.missionHover\"" +
    " onmouseout=\"PlayerMenu.missionStopHover\">" +
    "<img src=\"synthetic-icon/" + id +
    "\" style=\"width:100%;height:100%;\"/></div>";

static string CapturedShape(params string[] tiles) =>
    "<div style=\"padding-left: 20px; padding-right: 20px; padding-top: 10px;\">" +
    "<div class=\"vendor\"><div class=\"vendor-reputation\">" +
    "<p style=\"color: #FFFFFF; font-size: 20px;margin-bottom:5px\">" +
    "<img src=\"synthetic-icon/notice\" style=\"margin-right:5px;width:20px;height:20px;\"/>" +
    "New Missions Available in Highsteppe</p>" +
    "<p style=\"color: #FFFFFF; font-size: 20px;margin-bottom:5px\">" +
    "Your Level Missions:</p>" +
    string.Concat(tiles) +
    "</div></div></div>";

static void TestCapturedStructure()
{
    // Same container/heading/mission-tile structure as the private generic
    // capture, but constructed from synthetic test data only.
    string[] tiles = Enumerable.Range(1, 49)
        .Select(i => PlayerMissionTile("mock-" + i,
            i % 5 == 0 ? "bronze" : "none")).ToArray();
    string source = CapturedShape(tiles);
    VerifyMarkup(source, tiles);
    Assert(MissionHtmlTransformer.TryTransform(source,
        MissionHtmlTransformer.FromCatalog, out string output, out var report),
        "Captured-shaped HTML should transform: " + report.Reason);
    Assert(report.OriginalIcons == 49 && report.OutputIcons == 49,
        "Exactly 49 native-style icons must survive");
    Assert(output.Contains("<div class=\"vendor\"><div class=\"vendor-reputation\">"),
        "All original scroll/layout wrappers must stay byte-for-byte");
    Assert(output.EndsWith("</div></div></div>", StringComparison.Ordinal),
        "All original closing wrappers must be preserved");
    Assert(!output.Contains("Your Level Missions:", StringComparison.Ordinal),
        "Generic level headings should be replaced by Region -> Task");
    Assert(output.Contains("Unassigned Region", StringComparison.Ordinal),
        "Unmapped IDs must be included under fallback region");
}
static void TestKeepsAnnouncement()
{
    string[] tiles = { PlayerMissionTile("hunter3", "silver"),
        PlayerMissionTile("capture7", "bronze") };
    string source = CapturedShape(tiles);
    string notice = "<img src=\"synthetic-icon/notice\" " +
        "style=\"margin-right:5px;width:20px;height:20px;\"/>" +
        "New Missions Available in Highsteppe";
    Assert(MissionHtmlTransformer.TryTransform(source,
        MissionHtmlTransformer.FromCatalog, out string output, out _),
        "Expected safe transformation");
    Assert(output.Split(new[] { notice }, StringSplitOptions.None).Length == 2,
        "Informational announcement must remain untouched exactly once");
    Assert(output.Contains("mission-silver") && output.Contains("mission-bronze"),
        "Original mission progress/rank CSS classes must not change");
    Assert(output.Contains("onmouseover=\"PlayerMenu.missionHover\""),
        "Original native mouse hover callback must remain");
}
static void TestUnknownNestedNode()
{
    string source = CapturedShape(PlayerMissionTile("hunter3"),
        PlayerMissionTile("capture7"))
        .Replace("Your Level Missions:</p>", "Your Level Missions:</p>" +
            "<div class='unknown-quest-widget'>Do not delete</div>",
            StringComparison.Ordinal);
    Assert(!MissionHtmlTransformer.TryTransform(source,
        MissionHtmlTransformer.FromCatalog, out string output, out _),
        "Unknown nested element must trigger fail-closed response");
    Assert(output == source, "Unsafe input must remain untouched");
}

static void TestStrictTarget()
{
    string[] tiles = { PlayerMissionTile("capture7"), PlayerMissionTile("hunter3") };
    string source = CapturedShape(tiles);
    foreach (string other in new[] { "achievements-container", "journal",
        "missions-tab", "lorequest-content", "", "Missions-Container" })
    {
        Assert(!MissionHtmlTransformer.TryTransformForTarget(other, source,
            out string output, out _),
            "Must not rewrite native HTML in unrelated DOM target " + other);
        Assert(output == source,
            "Unrelated target's original HTML must not change");
    }
}
static void TestUnpopulatedTarget()
{
    foreach (string source in new[] { "", "<p>Loading...</p>",
        "<div class='vendor'>No missions yet</div>" })
    {
        Assert(!MissionHtmlTransformer.TryTransformForTarget(
            "missions-container", source, out string result, out _),
            "Blank/unpopulated missions must not transform");
        Assert(result == source, "Must preserve vanilla loading state");
    }
}
static void TestTargetedRealShape()
{
    string[] tiles = { PlayerMissionTile("capture7", "silver"),
        PlayerMissionTile("battle12"), PlayerMissionTile("hunter21"),
        PlayerMissionTile("jester24", "bronze") };
    string source = CapturedShape(tiles);
    Assert(MissionHtmlTransformer.TryTransformForTarget(
        "missions-container", source, out string grouped, out var stats),
        "Targeted generic-client markup should be recognised: " + stats.Reason);
    Assert(stats.OriginalIcons == 4 && stats.OutputIcons == 4,
        "All four synthetic original mission elements must survive");
    Assert(grouped.Contains("data-missionid-capture7") &&
        grouped.Contains("data-missionid-battle12") &&
        grouped.Contains("data-missionid-hunter21") &&
        grouped.Contains("data-missionid-jester24"),
        "Must retain all captured-template mission IDs");
    foreach (var t in tiles)
        Assert(grouped.Contains(t, StringComparison.Ordinal),
            "Each icon template must be byte-identical");
    Assert(!MissionHtmlTransformer.TryTransformForTarget(
        "missions-container", grouped, out string again, out _),
        "Targeted HTML transformation should be idempotent");
    Assert(grouped == again, "Idempotent attempt must not change HTML");
}

static void TestFlatRegionView()
{
    string a = PlayerMissionTile("hunter3");
    string b = PlayerMissionTile("capture7");
    string c = PlayerMissionTile("capture5");
    string d = PlayerMissionTile("battle12");
    string source = CapturedShape(a, b, c, d);
    Assert(MissionHtmlTransformer.TryTransform(source,
        MissionHtmlTransformer.FromCatalog,
        MissionHtmlTransformer.GroupView.Region,
        out string output, out var report), "Region grouping failed: " + report.Reason);
    Assert(output.Contains("<b>Highsteppe</b>", StringComparison.Ordinal),
        "Native regional heading missing");
    Assert(!output.Contains(">Critter Capture</p>", StringComparison.Ordinal) &&
        !output.Contains(">Monster Hunting</p>", StringComparison.Ordinal),
        "Region grouping should have NO inner activity headings");
    Assert(output.IndexOf(b, StringComparison.Ordinal) <
        output.IndexOf(a, StringComparison.Ordinal),
        "Capture quests should remain adjacent before monster hunting");
    foreach (var tile in new[] { a,b,c,d })
        Assert(output.Contains(tile, StringComparison.Ordinal), "Tile changed/lost");
}
static void TestFlatActivityView()
{
    string a = PlayerMissionTile("capture7");      // Highsteppe
    string b = PlayerMissionTile("capture17");     // Lamavora
    string c = PlayerMissionTile("capture21");     // Hulthine
    string d = PlayerMissionTile("hunter3");
    string source = CapturedShape(a, b, c, d);
    Assert(MissionHtmlTransformer.TryTransform(source,
        MissionHtmlTransformer.FromCatalog,
        MissionHtmlTransformer.GroupView.Activity,
        out string output, out var report), "Activity grouping failed: " + report.Reason);
    Assert(output.Contains("<b>Critter Capture</b>", StringComparison.Ordinal),
        "One shared activity heading should contain capture missions across regions");
    Assert(!output.Contains("<b>Highsteppe</b>", StringComparison.Ordinal) &&
        !output.Contains("<b>Lamavora</b>", StringComparison.Ordinal),
        "Activity grouping should not add regional subheadings");
    Assert(output.IndexOf(a, StringComparison.Ordinal) <
        output.IndexOf(b, StringComparison.Ordinal) &&
        output.IndexOf(b, StringComparison.Ordinal) <
        output.IndexOf(c, StringComparison.Ordinal),
        "Regional capture tiles should stay adjacent under one activity");
    foreach (var tile in new[] { a,b,c,d })
        Assert(output.Contains(tile, StringComparison.Ordinal), "Tile changed/lost");
}
static void TestViewSwitchMarkup()
{
    string source = CapturedShape(PlayerMissionTile("capture7"),
        PlayerMissionTile("battle12"));
    foreach (var view in new[] {
        MissionHtmlTransformer.GroupView.Region,
        MissionHtmlTransformer.GroupView.Activity })
    {
        Assert(MissionHtmlTransformer.TryTransform(source,
            MissionHtmlTransformer.FromCatalog, view,
            out string output, out _), "Group mode failed");
        Assert(output.Contains("id='" +
            MissionHtmlTransformer.RegionControlId + "'", StringComparison.Ordinal) &&
            output.Contains("id='" +
            MissionHtmlTransformer.ActivityControlId + "'", StringComparison.Ordinal),
            "Missing in-game view selector nodes");
        Assert(output.Contains("View: ", StringComparison.Ordinal),
            "Missing native view controls row");
        Assert(output.Contains("font-size: 20px", StringComparison.Ordinal),
            "Selector text should match game's native font size");
        Assert(output.IndexOf("View: ", StringComparison.Ordinal) <
            output.IndexOf("data-missionid-", StringComparison.Ordinal),
            "Selector must appear above the mission tiles");
    }
}
static void TestCombinedFishing()
{
    string a = PlayerMissionTile("fishing18"); // Catch Fish
    string b = PlayerMissionTile("fishing20"); // Fishing by Weight
    string c = PlayerMissionTile("capture7");
    string source = CapturedShape(a,b,c);
    Assert(MissionHtmlTransformer.TryTransform(source,
        MissionHtmlTransformer.FromCatalog,
        MissionHtmlTransformer.GroupView.Activity,
        out string output, out var report),
        "Mixed fishing variants must transform: " + report.Reason);
    Assert(output.Split(new[] { "<b>Fishing</b>" },
        StringSplitOptions.None).Length == 2,
        "There must be exactly ONE fishing group heading");
    Assert(!output.Contains(">Catch Fish</p>", StringComparison.Ordinal) &&
        !output.Contains(">Fishing by Weight</p>", StringComparison.Ordinal),
        "Do not show separate fishing subtype headings");
    Assert(output.Contains(a, StringComparison.Ordinal) &&
        output.Contains(b, StringComparison.Ordinal),
        "Both original fishing tiles must be retained verbatim");
    Assert(report.OriginalIcons == 3 && report.OutputIcons == 3,
        "Fishing regrouping must preserve the full mission set");
}


static string SpadesMissionTile(string id, string icon) =>
    "<div class='inventory-box-lowres mission-none data-boxtype-playermission vendor-mission " +
    "data-missionid-" + id + "' style='z-index:60' onmouseover='ng.eqj' onmouseout='ng.eqo'>" +
    "<img src='" + icon + "' style='width:100%;height:100%;' /></div>";

static string SpadesShape(params string[] weeklyAndLevels)
{
    // The first 8 are the weekly entries. These are synthetic entries
    // reproducing the structure of the user's private Spades capture.
    return "<div style='padding-left: 20px; padding-right: 20px; padding-top: 10px;'>" +
        "<div class='vendor'><div class='vendor-reputation'>" +
        "<p style='color: #FFFFFF; font-size: 20px;margin-bottom:5px'>" +
        "<img src='ui_icons/Weak' />New Missions Available in Highsteppe</p>" +
        "<p>Your Weekly Missions:</p>" +
        string.Concat(weeklyAndLevels.Take(8)) +
        "<p>Your Level Missions:</p>" +
        string.Concat(weeklyAndLevels.Skip(8)) +
        "</div></div></div>";
}

static void TestSpadesWeeklyBothViews()
{
    var week = new[]
    {
        SpadesMissionTile("1792454400001-7-3", "ui_icons/inv/SalvagingMissions"),
        SpadesMissionTile("1792454400000-7-0", "ui_icons/inv/SalvagingMissions"),
        SpadesMissionTile("1792454400000-5-1", "ui_icons/inv/EventMissions"),
        SpadesMissionTile("1792454400001-5-2", "ui_icons/inv/EventMissions"),
        SpadesMissionTile("1792454400000-6-0", "ui_icons/inv/Fish_016"),
        SpadesMissionTile("1792454400001-6-12", "ui_icons/inv/Fish_016"),
        SpadesMissionTile("1792454400000-3-1", "ui_icons/Madness"),
        SpadesMissionTile("1792454400001-3-4", "ui_icons/Madness")
    };
    string[] baseIds =
    {
        "race8","race12","event12","battle5","battle8","battle10","battle11",
        "battle12","fishing11","fishing15","capturestrangerfestival2024",
        "event15","battle13","battle14","battle15","race13","race15","event16",
        "event17","event18","fishing18","hunter20","fishing20","jester19",
        "jester20","jester21","hunter21","event19","event20","event21",
        "capture23","capture24","capture19","capture17","hunter24","hunter25",
        "jester23","jester24","jester25","capture5","capture16","capture14",
        "capture10","capture7","capture21","capture18","capture22","capture25",
        "capture3","capture12","capture20","jester16","jester11","jester17"
    };
    var level = baseIds.Select((id,i) => SpadesMissionTile(id,
        i % 2 == 0 ? "ui_icons/inv/SunflowerSeeds" :
            "ui_icons/inv/SalvagingMissions")).ToArray();
    string[] all = week.Concat(level).ToArray();
    Assert(all.Length == 62, "Fixture must model all 62 Spades quest tiles");
    string html = SpadesShape(all);
    foreach (var view in new[] {
        MissionHtmlTransformer.GroupView.Region,
        MissionHtmlTransformer.GroupView.Activity
    })
    {
        Assert(MissionHtmlTransformer.TryTransformForTarget(
            "missions-container", html, view, out string output, out var report),
            "Spades layout must transform: " + report.Reason);
        Assert(report.OriginalIcons == 62 && report.OutputIcons == 62,
            "All 62 native mission tiles must survive without duplication");
        foreach (var tile in all)
            Assert(output.Contains(tile, StringComparison.Ordinal),
                "Spades tile including obfuscated handlers changed");
        Assert(output.Contains("New Missions Available in Highsteppe",
            StringComparison.Ordinal) &&
            !output.Contains("Your Level Missions:", StringComparison.Ordinal),
            "Original availability notice must be preserved; old heading removed");
        if (view == MissionHtmlTransformer.GroupView.Region)
        {
            Assert(output.Contains("<b>Weekly Missions</b>",
                StringComparison.Ordinal),
                "Region view needs one Weekly Missions group");
            int firstLevel = level.Select(tile =>
                output.IndexOf(tile, StringComparison.Ordinal)).Min();
            Assert(week.All(tile => output.IndexOf(tile,
                StringComparison.Ordinal) < firstLevel),
                "All weekly mission icons must stay inside the first Weekly group");
        }
        else
        {
            Assert(output.Contains("<b>Fishing</b>", StringComparison.Ordinal),
                "Fishing activity needs combined weekly fishing heading");
            int fishHeading = output.IndexOf("<b>Fishing</b>",
                StringComparison.Ordinal);
            int fishWeekly = output.IndexOf(week[4], StringComparison.Ordinal);
            int fishLevel = output.IndexOf(level[8], StringComparison.Ordinal);
            Assert(fishHeading >= 0 && fishWeekly > fishHeading &&
                fishLevel > fishHeading, "Weekly/level fishing must share heading");
        }
    }
}

static void TestSpadesUnknownWeekly()
{
    var weekly = Enumerable.Range(0, 8).Select(i =>
        SpadesMissionTile("1792454400000-9-" + i,
            "ui_icons/inv/UnknownWeeklyType")).ToArray();
    string html = SpadesShape(weekly.Concat(new[] {
        SpadesMissionTile("hunter3","ui_icons/Madness"),
        SpadesMissionTile("capture7","ui_icons/inv/SunflowerSeeds")
    }).ToArray());
    Assert(MissionHtmlTransformer.TryTransformForTarget("missions-container",
        html, MissionHtmlTransformer.GroupView.Activity,
        out string result, out var report),
        "Unknown weekly type should not cause quest loss: " + report.Reason);
    Assert(result.Contains("<b>Weekly Tasks</b>", StringComparison.Ordinal),
        "Unidentified weekly activity should stay visible, not be mislabelled");
    foreach (var tile in weekly)
        Assert(result.Contains(tile, StringComparison.Ordinal),
            "Unrecognised weekly tile must be copied byte-for-byte");
}

static void TestRepeatedCalls()
{
    string[] tiles = Enumerable.Range(1, 80)
        .Select(i => Tile("synthetic-" + i)).ToArray();
    for (int i = 0; i < 10; i++)
        VerifyMarkup(Native(tiles), tiles);
}
