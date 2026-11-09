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
}
finally
{
    Directory.Delete(temp, true);
}

Console.WriteLine("Mission objective and milestone checks: " + tests + " passed.");
