using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net.Enums;
using HydraTextClient.Scripts.Hints;
using static HydraTextClient.Scripts.Utility.ColorIdConstants;
using static HydraTextClient.Scripts.Utility.ColorIdConstants.ColorConstant;
using Color = Godot.Color;

namespace HydraTextClient.Scripts.Utility;

[Obsolete("Legacy, replaced with new save system")]
public class LegacyUserData
{
    public static Dictionary<string, ColorConstant> LegacyColorIdToNewIds = new()
    {
        ["background_color"] = UiBackground, ["player_server"] = ServerColor, ["player_generic"] = PlayerNonConnected,
        ["player_color"] = PlayerConnected, ["player_color_offline"] = PlayerListedNonConnected,
        ["item_special"] = SpecialItemColor, ["item_progressive"] = ProgressiveItemColor,
        ["item_useful"] = UsefulItemColor, ["item_normal"] = NormalItemColor, ["item_trap"] = TrapItemColor,
        ["item_bg_special"] = SpecialItemBackgroundColor, ["item_bg_progressive"] = ProgressiveItemBackgroundColor,
        ["item_bg_useful"] = UsefulItemBackgroundColor, ["item_bg_normal"] = NormalItemBackgroundColor,
        ["item_bg_trap"] = TrapItemBackgroundColor, ["location"] = LocationColor, ["entrance"] = EntranceColor,
        ["hint_found"] = FoundColor, ["hint_priority"] = Priority, ["hint_unspecified"] = Unspecified,
        ["hint_no_priority"] = NoPriority, ["hint_avoid"] = Avoid, ["tooltip_bgcolor"] = TooltipColor,
    };

    public Dictionary<string, LegacyColorSetting> UiColorSettings = [];
    public Dictionary<string, LegacyGameData> GameData = [];
    public Dictionary<string, LegacyItemFilter> ItemFilters = [];
    public Dictionary<string, int> FontSizes = [];
    public List<SortObject> HintSortOrder = [];
    public bool[] HintOptions = [false, true, true, true, true];
    public bool[] ItemLogOptions = [true, true, true, true, true];
    public bool ShowFoundHints = false;
    public bool AlwaysOnTop = false;
    public bool ShowNewItems = true;
    public int GlobalFontSize = 20;

    public string Colors
    {
        set
        {
            var raw = value
                     .Split(";;;")
                     .Select(item => item.Split("==="))
                     .ToDictionary(item => item[0], item => new LegacyColorSetting(item[1], new Color(item[2])));

            if (raw is null || raw.Count == 0) return;

            foreach (var key in UiColorSettings.Keys.Where(key => !raw.ContainsKey(key)))
            {
                raw.Add(key, UiColorSettings[key]);
            }

            UiColorSettings = raw;
        }
    }

    public List<LegacyGameData> GameDatas { set => GameData = value.ToDictionary(l => l.SlotName, l => l); }

    public List<string> SlotNames // backwards compatability
    {
        set => GameData = value.ToDictionary(s => s, s => new LegacyGameData(s));
    }

    public Dictionary<string, LegacyColorSetting> ColorSettings // backwards compatability
    {
        set => UiColorSettings = value;
    }
}

public class LegacyGameData(string name)
{
    public string SlotName = name;
    public string GameName = null;
}

public class LegacyItemFilter(long id, string name, string game, ItemFlags flags)
{
    public readonly string Name = name;
    public readonly string Game = game;
    public readonly ItemFlags Flags = flags;
    public readonly string UidCode = MakeUidCode(id, name, game, flags);
    public bool ShowInItemLog = true;
    public bool ShowInHintsTable = true;
    public bool IsSpecial = false;

    public static string MakeUidCode(long id, string name, string game, ItemFlags flags) => $"{id}{name}{game}{flags}";
}

public readonly struct LegacyColorSetting(string settingName, Color color)
{
    public readonly string SettingName = settingName;
    public readonly Color Color = color;
    public readonly string Hex = color.ToHtml();
    public static implicit operator Color(LegacyColorSetting setting) => setting.Color;
    public static implicit operator string(LegacyColorSetting setting) => setting.Hex;
}

[Obsolete("Legacy, replaced with: LocationGrouping & LocationRule")]
public class LocationGroup(string name, string mapIcon, string openIcon = "", string closeIcon = "")
{
    public string GroupName = name;
    public string MappedIcon = mapIcon;
    public string AvailableIcon = openIcon;
    public string CollectedIcon = closeIcon;
    public string SlotDataKey;
    public long StoreType;
    public string[] DataCompare = [];
    public bool BoolCompare = true;
    public double NumberCompare;
    public long CompareType;
    public bool MatchAny = true; // false means to match none
}
