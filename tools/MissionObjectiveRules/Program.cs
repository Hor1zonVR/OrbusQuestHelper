using OrbusQuestHelper;

int tests = 0;
void AssertEq<T>(T actual, T expected, string name)
{
    if (!Equals(actual, expected))
        throw new Exception(name + ": got [" + actual + "], expected [" +
            expected + "]");
    Console.WriteLine("PASS " + name);
    ++tests;
}

string Resolve(string id, string checklist, string description,
    string missionType = "")
    => MissionObjectiveLabels.Resolve(id, checklist, description,
        missionType, out _);

AssertEq(Resolve("capturestrangerfestival2024", "",
    "Now that it's known that something happened"), "Capture Lizards",
    "verified lizards story alias");
AssertEq(Resolve("jester16", "", "Keeping a stockpile of potions"),
    "Use Ruined Potions", "verified ruined potions story alias");
AssertEq(Resolve("capture10", "Capture 5 Desert Lizards (3/5)", ""),
    "Capture Desert Lizards", "native capture objective and count stripping");
AssertEq(Resolve("capture10", "Lizards", ""), "Capture Lizards",
    "short native noun prefixed by activity");
AssertEq(Resolve("hunter1", "", "Long ago there was a tribe that..."),
    "Defeat monsters", "no narrative descriptions on HUD");
AssertEq(Resolve("jester16", "", "Please save our lands"),
    "Use potions", "activity fallback tells player what to do");
AssertEq(Resolve("unmapped-id", "", "A mysterious story about the past."),
    "Complete mission objective", "unknown mission remains honest");
AssertEq(MissionObjectiveLabels.CountUnit("capture10"),
    " captured", "capture count verb");
AssertEq(MissionObjectiveLabels.CountUnit("hunter1"),
    " defeated", "hunter count verb");

AssertEq(MissionGoalPolicy.Choose(98, 10000, true, 100, 1000, 10000),
    (100, "Bronze"), "98 goes to Bronze 100, not lifetime 10000");
AssertEq(MissionGoalPolicy.Choose(100, 10000, true, 100, 1000, 10000),
    (1100, "Silver"), "Bronze to Silver cumulative 100+1000");
AssertEq(MissionGoalPolicy.Choose(1100, 10000, true, 100, 1000, 10000),
    (0, ""), "Gold sum beyond checklist cap never shown as target");
AssertEq(MissionGoalPolicy.Choose(98, 10000, false, 100, 1000, 10000),
    (0, ""), "unverified tier scale never claims 98/10000");
AssertEq(MissionGoalPolicy.Choose(3, 8, false, double.NaN,
    double.NaN, double.NaN), (8, ""),
    "ordinary 3/8 objective remains intact");
AssertEq(MissionGoalPolicy.Choose(10, 10000, true, double.NaN,
    double.NaN, double.NaN), (0, ""),
    "no native reward threshold means no fabricated milestone");

AssertEq(MissionGoalPolicy.Choose(0, 10000, true, 10, 8, 12),
    (10, "Bronze"), "capture16 Bronze 10");
AssertEq(MissionGoalPolicy.Choose(10, 10000, true, 10, 8, 12),
    (18, "Silver"), "capture16 Silver 10+8");
AssertEq(MissionGoalPolicy.Choose(18, 10000, true, 10, 8, 12),
    (30, "Gold"), "capture16 Gold 10+8+12");
AssertEq(MissionGoalPolicy.Choose(4, 10000, true, 4, 3, 4),
    (7, "Silver"), "capture10 Silver 4+3");
AssertEq(MissionGoalPolicy.Choose(7, 10000, true, 4, 3, 4),
    (11, "Gold"), "capture10 Gold 4+3+4");
AssertEq(MissionGoalPolicy.Choose(0, 10000, true,
    double.NaN, double.NaN, 100), (100, "Gold"),
    "event mission with only a Gold requirement");
// Local-only mission file import: synthetic content, no copied game assets.
string temp = Path.Combine(Path.GetTempPath(),
    "qh-mission-rules-" + Guid.NewGuid().ToString("N"));
try
{
    string missions = Path.Combine(temp, "missions");
    string items = Path.Combine(temp, "items");
    Directory.CreateDirectory(missions);
    Directory.CreateDirectory(items);
    File.WriteAllText(Path.Combine(items, "item-serverdata.txt"),
        "id,name,icon\n100,Healing Potion,potion.png\n43,Blade Cactus,cactus.png\n");
    File.WriteAllText(Path.Combine(missions, "LevelMissions.json"),
        """
        [
          {"id":"capture10","type":"capture",
           "bronze":{"requirement":4},"silver":{"requirement":3},
           "gold":{"requirement":4},
           "checklist":[{"ids":[{"id":"squirrel"},{"id":"squirrel-rare"}]}]},
          {"id":"hunter1","type":"kills",
           "checklist":[{"ids":[{"id":"sapling"}]}]},
          {"id":"jester16","type":"consume",
           "checklist":[{"ids":[{"id":"100"}]}]},
          {"id":"jester17","type":"harvest",
           "checklist":[{"ids":[{"id":"43"}]}]},
          {"id":"fakeDup","type":"capture","checklist":[{"ids":[]}]}
        ]
        """);
    File.WriteAllText(Path.Combine(missions, "OtherMissions.json"),
        """[{"id":"fakeDup","type":"kills","checklist":[{"ids":[]}]}]""");
    var (loaded, ambiguous) = MissionDefinitionIndex.LoadFromDirectory(missions);
    AssertEq(loaded, 4, "four unique IDs imported");
    AssertEq(ambiguous, 1, "colliding definitions skipped");
    AssertEq(Resolve("capture10", "", "An unrelated story"),
        "Capture squirrels", "target from local server critter IDs");
    AssertEq(Resolve("hunter1", "", "An unrelated story"),
        "Defeat Sapling Stafrushers", "target from verified local monster ID");
    AssertEq(Resolve("jester16", "", "An unrelated story"),
        "Drink Healing Potions", "numeric item 100 resolved to potion");
    AssertEq(Resolve("jester17", "", "An unrelated story"),
        "Gather Blade Cactus", "numeric item 43 resolved to herb");
    AssertEq(Resolve("fakeDup", "", "An unrelated story"),
        "Complete mission objective", "ambiguous ID not guessed");
    AssertEq(MissionDefinitionIndex.TryGetTierIncrements("capture10",
        out double b, out double s, out double g), true,
        "fixed mission has local tier increments");
    AssertEq(MissionGoalPolicy.Choose(4, 10000, true, b, s, g),
        (7, "Silver"), "local authored tiers accumulate correctly");
    AssertEq(MissionDefinitionIndex.TryGetTierIncrements("fakeDup",
        out _, out _, out _), false, "duplicate id has no tier fallback");
}
finally
{
    Directory.Delete(temp, true);
}

AssertEq(MissionGoalPolicy.Choose(12, 100, true, 3, 3, 6),
    (0, ""), "hunter1 Gold finished does not fall through to /100");
AssertEq(MissionGoalPolicy.Choose(3, 100, false, 3, 3, 6),
    (0, ""), "different progress scales do not claim /100");

string[] captureDescriptions =
{
    "There is a mouse infestation in the underground Market area",
    "The Parathime Finch has been seen along Traders Road",
    "There are markings of the Critters Cenn needs captured",
    "Cenn thinks the squirrels might be the key",
    "Head into the Flooded Rainforest and see what Critters",
    "Now that you know there are Obnobi Lizards",
    "Head into the Lamavora Battlefield to see what kind",
    "Inside the Lamavora Battlefield there are skunks",
    "Luckily the birds aren't affected by the essence",
    "Hulthine's Basin doesn't contain any running critters",
    "Cenn isn't being picky today",
    "Cenn thinks there may be something with reptile species",
    "Venture into the Wastelands as this is where",
    "You have discovered some mutated creatures there",
    "Return to the Wastelands to retrieve the mutated toads",
    "Cenn thinks you are doing well, but his specimens"
};
string[] captureLabels =
{
    "Capture mice", "Capture Parathime Finches",
    "Capture any critters near Highsteppe", "Capture squirrels",
    "Capture any Rainforest critters", "Capture Obnobi Lizards",
    "Capture any Lamavora critters", "Capture skunks",
    "Capture Green Crested Fowl", "Capture Hulthine's Basin birds",
    "Capture any critters anywhere", "Capture lizards and toads",
    "Capture any Wastelands critters", "Capture hairless skunks",
    "Capture mutated toads", "Capture any critters anywhere"
};
int[] bronzeRequirements =
    { 6, 6, 6, 6, 8, 10, 7, 8, 8, 6, 10, 10, 8, 8, 8, 15 };
int[] silverIncrements =
    { 3, 3, 3, 3, 5, 8, 6, 6, 6, 5, 7, 8, 5, 5, 5, 10 };
int[] goldIncrements =
    { 5, 5, 5, 5, 7, 12, 8, 6, 6, 6, 8, 8, 8, 8, 8, 10 };
AssertEq(OfficialCaptureObjectives.KnownCount, 16,
    "sixteen user-supplied capture missions are mapped");
for (int i = 0; i < captureDescriptions.Length; i++)
{
    string description = captureDescriptions[i] + " (unique text trailing)";
    AssertEq(OfficialCaptureObjectives.TryFind("capture", description,
        out var definition), true, "server capture match " + (i + 1));
    AssertEq(definition.Action, captureLabels[i],
        "server capture action " + (i + 1));
    AssertEq(definition.Bronze, bronzeRequirements[i],
        "server Bronze requirement " + (i + 1));
    AssertEq(definition.Silver, silverIncrements[i],
        "server Silver increment " + (i + 1));
    AssertEq(definition.Gold, goldIncrements[i],
        "server Gold increment " + (i + 1));
    AssertEq(MissionObjectiveLabels.Resolve("unknown-id", "", description,
        "capture", out _), captureLabels[i],
        "server source beats story and unspecified mission ID " + (i + 1));
    int max = definition.ChecklistMax;
    AssertEq(MissionGoalPolicy.Choose(
        0, max, true, definition.Bronze,
        definition.Silver, definition.Gold),
        (definition.Bronze, "Bronze"),
        "native-stage Bronze count " + (i + 1));
    AssertEq(MissionGoalPolicy.Choose(
        definition.Bronze, max, true, definition.Bronze,
        definition.Silver, definition.Gold),
        (definition.Bronze + definition.Silver, "Silver"),
        "native-stage Silver cumulative " + (i + 1));
    AssertEq(MissionGoalPolicy.Choose(
        definition.Bronze + definition.Silver, max, true,
        definition.Bronze, definition.Silver, definition.Gold),
        (definition.Bronze + definition.Silver + definition.Gold,
            "Gold"),
        "native-stage Gold cumulative " + (i + 1));
}
AssertEq(OfficialCaptureObjectives.TryFind(
    "kills", captureDescriptions[0], out _), false,
    "capture target cannot accidentally relabel a hunting mission");
AssertEq(OfficialCaptureObjectives.TryFind(
    "capture", "Unrecognized capture mission", out _), false,
    "unknown capture mission never guessed");
AssertEq(OfficialCaptureObjectives.TryFind(
    "capture", captureDescriptions[0], out var mice), true,
    "mouse weighted-ID entry exists");
AssertEq(mice.WeightedIds.Contains("mouse:1"), true,
    "normal mouse awards one capture point");
AssertEq(mice.WeightedIds.Contains("mouse-rare:3"), true,
    "rare mouse awards three capture points");

// Reborn-server definition arrays may omit the ID entirely; prove that
// local server imports can match such records using the description.
string tempNoId = Path.Combine(Path.GetTempPath(),
    "qh-capture-definitions-" + Guid.NewGuid().ToString("N"));
try
{
    string missionFolder = Path.Combine(tempNoId, "missions");
    Directory.CreateDirectory(missionFolder);
    File.WriteAllText(Path.Combine(missionFolder, "CaptureMissions.json"),
        """
        [{
          "type":"capture",
          "description":"Inside the Lamavora Battlefield there are skunks running loose, capture them and bring them to Cenn for investigation.",
          "bronze":{"requirement":8},
          "silver":{"requirement":6},
          "gold":{"requirement":6},
          "checklist":[{"ids":[{"id":"skunk","value":1},{"id":"skunk-rare","value":3}],
                        "zones":["Zone1"],"timesToKill":10000}]
        }]
        """);
    var loaded = MissionDefinitionIndex.LoadFromDirectory(missionFolder);
    AssertEq(loaded.Loaded, 1, "server definition without ID imports");
    AssertEq(MissionDefinitionIndex.TryGetAction(
        "", "Inside the Lamavora Battlefield there are skunks running loose, capture them and bring them to Cenn for investigation.",
        out var action), true,
        "ID-less source resolved by full description");
    AssertEq(action, "Capture skunks",
        "server ID-less skunk type IDs translate to action");
    AssertEq(MissionDefinitionIndex.TryGetTierIncrements(
        "", "Inside the Lamavora Battlefield there are skunks running loose, capture them and bring them to Cenn for investigation.",
        out double b2, out double s2, out double g2), true,
        "server ID-less tier requirements loaded");
    AssertEq((b2, s2, g2), (8d, 6d, 6d),
        "server ID-less tier values preserved exactly");
}
finally
{
    Directory.Delete(tempNoId, true);
}

Console.WriteLine("Mission objective and milestone checks: " + tests + " passed.");
