using System;
using System.Collections.Generic;

namespace OrbusQuestHelper;

/// <summary>
/// Region and activity identifiers transcribed from the user's OrbusVR Data
/// workbook, Missions sheet (298 rows). Only IDs with unambiguous categories
/// are included. This table contains no proprietary descriptions or rewards.
/// '969' denotes missing zone information, NOT "multiple regions".
/// One duplicated/ambiguous ID (ltmkill3) is intentionally excluded.
/// </summary>
internal static class MissionCatalog
{
    // Format missionId:zoneCode+taskCode. ? = not explicitly zoned.
    // Zones: S Highsteppe, C Highsteppe City, L Lamavora, H Hulthine,
    // R Rainforest, W Wastelands, B Battlegrounds, D Dungeons.
    private const string Data = @"
battle10:BL battle11:BP battle12:BV battle13:BL battle14:BT battle15:BV battle16:BP battle17:BP battle18:BL battle19:BL
battle20:BP battle21:BL battle22:BT battle23:BL battle24:BP battle25:BV battle26:BP battle27:BT battle28:BP battle29:BL
battle5:BP battle8:BL capture10:SC capture12:SC capture14:RC capture16:RC capture17:LC capture18:CC capture19:LC capture20:LC
capture21:HC capture22:?C capture23:CC capture24:?C capture25:?C capture26:WC capture27:WC capture28:WC capture29:?C capture3:CC
capture5:SC capture7:SC capturestrangerfestival2024:?C event11:SG event12:RE event14:RE event15:RE event16:SE event17:LE event18:LG
event19:LE event20:LG event21:LE event22:LG event23:SE event24:RE event25:HE event26:?E event27:WE event28:LE
event29:?G event7:SE event9:SE fishing11:?F fishing13:?W fishing15:?F fishing18:RF fishing20:HW fishing22:?F fishing24:?F
fishing25:SW fishing26:?W fishing27:?F fishing28:?W fishing29:?W fishing6:SW fishing9:?F hunter1:SK hunter10:DK hunter11:DK
hunter12:SK hunter13:?K hunter14:SK hunter15:?K hunter16:LK hunter17:LK hunter18:LK hunter19:LK hunter2:SK hunter20:?K
hunter21:DK hunter22:WK hunter23:WK hunter24:WK hunter25:LK hunter26:LK hunter27:?K hunter28:SK hunter29:LK hunter3:SK
hunter30:HK hunter4:SK hunter5:SK hunter6:SK hunter7:?K hunter8:?K hunter9:?K jester11:?D jester13:RH jester14:?D
jester16:DU jester17:RH jester19:LH jester20:LH jester21:HH jester23:?D jester24:WH jester25:LU jester26:?D jester27:?B
jester28:WH jester29:CU jester8:SH jester9:?S ltmcapture1:LC ltmevent1:SG ltmevent2:HG ltmfishing1:HW ltmfishing2:?F ltmharvest1:LH
ltmkil1:?K ltmkill2b:?K race1:?R race10:?R race12:?R race13:?R race15:?R race17:?R race18:?R race19:?R
race21:?R race22:?R race23:?R race24:?R race25:?R race26:?R race27:?R race28:?R race29:?R race4:?R
race6:?R race8:?R
";

    private static readonly Dictionary<string, string> Codes = Load();

    internal static bool TryGet(string id, out string region,
        out string activity)
    {
        region = "";
        activity = "";
        if (!Codes.TryGetValue(id, out string? code) || code.Length != 2)
            return false;
        region = code[0] switch
        {
            'S' or 'C' => "Highsteppe",
            'L' => "Lamavora",
            'H' => "Hulthine's Basin",
            'R' => "Flooded Rainforest",
            'W' => "Wastelands",
            'B' => "Battlegrounds",
            'D' => "Dungeons",
            _ => ""
        };
        activity = code[1] switch
        {
            'K' => "Monster Hunting",
            'C' => "Critter Capture",
            'R' => "Dragon Racing",
            'E' => "Public Events",
            'G' => "Gold Events",
            'F' => "Catch Fish",
            'W' => "Fishing by Weight",
            'H' => "Gathering",
            'B' => "Brewing Potions",
            'U' => "Using Potions",
            'D' => "Earning Dram",
            'S' => "Salvaging Equipment",
            'P' => "Battleground Matches",
            'V' => "Battleground Victories",
            'L' => "Battleground Kills",
            'T' => "Battleground Captures",
            _ => ""
        };
        return true;
    }

    private static Dictionary<string, string> Load()
    {
        var result = new Dictionary<string, string>(
            StringComparer.OrdinalIgnoreCase);
        foreach (string entry in Data.Split(
            new[] { ' ', '\r', '\n', '\t' },
            StringSplitOptions.RemoveEmptyEntries))
        {
            int sep = entry.LastIndexOf(':');
            if (sep <= 0 || entry.Length - sep != 3) continue;
            string id = entry.Substring(0, sep);
            string code = entry.Substring(sep + 1);
            result[id] = code;
        }
        return result;
    }
}
