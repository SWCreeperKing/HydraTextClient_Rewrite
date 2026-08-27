using System;
using System.Collections.Generic;
using Godot;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HydraTextClient.Scripts.Mapper;

public struct TabStructure(string name = "", params List<TabStructure> subTabs)
{
    public string Name = name;
    public List<TabStructure> SubTabs = subTabs;
    [JsonIgnore] public string Parent;

    public override int GetHashCode() => HashCode.Combine(Name, SubTabs);
}

public class Maps(string mapName, string imageName, string tab = "", string mapId = "", List<EntranceNode>? entrances = null, params List<MapNode> nodes)
{
    public string MapName = mapName;
    public string MapId = mapId;
    public string ImageName = imageName;
    public string Tab = tab;
    public List<MapNode> Nodes = nodes;
    public List<EntranceNode> Entrances = entrances ?? [];

    [JsonIgnore] public string GetId => MapId is "" or null ? MapName ?? "" : MapId;
}

public class MapNode(float x, float y, float w, float h, string group = "",
    params List<string> locationChecks)
{
    public string LocationGroup = group;
    public List<string> Locations = locationChecks;
    public float X = x;
    public float Y = y;
    public float W = w;
    public float H = h;
}

public class EntranceNode(float x, float y, float w, float h, string entrance)
{
    public string Entrance = entrance;
    public float X = x;
    public float Y = y;
    public float W = w;
    public float H = h;
}

public class LocationGroup(string name, string mapIcon, string openIcon = "", string closeIcon = "")
{
    public const float Tolerance = .00001f;
    public enum DataStorageType { Bool, Number, Text }

    [Flags]
    public enum NumberCompareType // storage value [compare type] data compare 
    {
        NotEqualTo = 0, // defaults to equal to
        EqualTo = 1, GreaterThan = 1 << 1, LessThan = 1 << 2,
    }

    public string GroupName = name;
    public string MappedIcon = mapIcon;
    public string AvailableIcon = openIcon;
    public string CollectedIcon = closeIcon;

    // slot data conditions
    public string SlotDataKey;
    public DataStorageType StoreType;
    public string[] DataCompare = [];
    public bool BoolCompare = true;
    public double NumberCompare;
    public NumberCompareType CompareType;
    public bool MatchAny = true; // false means to match none

    public bool CompareDataValue(object val)
    {
        try
        {
            switch (StoreType)
            {
                case DataStorageType.Text:
                    var text = (string)val;
                    return MatchAny ? DataCompare.Contains(text) : !DataCompare.Contains(text);
                case DataStorageType.Number:
                    var num = (long)val;
                    if (CompareType is NumberCompareType.NotEqualTo) return Math.Abs(num - NumberCompare) > Tolerance;
                    if (CompareType.HasFlag(NumberCompareType.EqualTo)) return Math.Abs(num - NumberCompare) < Tolerance;
                    if (CompareType.HasFlag(NumberCompareType.GreaterThan)) return num > NumberCompare;
                    if (CompareType.HasFlag(NumberCompareType.LessThan)) return num < NumberCompare;
                    return false;
                case DataStorageType.Bool: return (bool)val == BoolCompare;
                default: return false;
            }
        }
        catch (Exception e) { GD.PrintErr($"Error with reading [{SlotDataKey}]", e); }
        return false;
    }
}

#region ugly poptracker imports

public struct PoptrackerManifest
{
    [JsonProperty("name")] public string Name;
    [JsonProperty("game_name")] public string GameName;
    [JsonProperty("author")] public string Author;
    [JsonProperty("package_uid")] public string PackUid;
    [JsonProperty("package_version")] public string PackVersion;
    [JsonProperty("versions_url")] public string PackVersionUrl;
    [JsonProperty("variants")] public Dictionary<string, PoptrackerVariant> PackVariants;
    [JsonProperty("platform")] public string Platform;
    [JsonProperty("platform_override")] public string PlatformOverride;

    [JsonProperty("min_poptracker_version")]
    public string MinTrackerVersion;

    [JsonProperty("target_poptracker_version")]
    public string TargetTrackerVersion;
}

public struct PoptrackerVariant
{
    [JsonProperty("display_name")] public string DisplayName;
    [JsonProperty("flags")] public dynamic Flags;
}

public class PoptrackerLocation
{
    [JsonProperty("name")] public string Name;
    [JsonProperty("short_name")] public string ShortName;
    [JsonProperty("access_rules")] public dynamic _AccessRules;
    [JsonProperty("visibility_rules")] public dynamic _VisibilityRules;
    [JsonProperty("chest_unopened_img")] public string UnopenedImage;
    [JsonProperty("chest_opened_img")] public string OpenedImage;
    [JsonProperty("overlay_background")] public string OverlayBackground; // #(AA)RRGGBB
    [JsonProperty("color")] public string Color;
    [JsonProperty("parent")] public string Parent;

    [JsonProperty("children"), JsonConverter(typeof(SingleOrArray<PoptrackerLocation>))]
    public PoptrackerLocation[] Locations;

    [JsonProperty("map_locations"), JsonConverter(typeof(SingleOrArray<PoptrackerMapLocation>))]
    public PoptrackerMapLocation[] MapLocations;

    [JsonProperty("sections")] public PoptrackerSection[] Sections;
}

public struct PoptrackerMapLocation
{
    [JsonProperty("map")] public string MapName;
    [JsonProperty("x")] public float X;
    [JsonProperty("y")] public float Y;
    [JsonProperty("size")] public float Size;
    [JsonProperty("border_thickness")] public float BorderThickness;
    [JsonProperty("shape")] public string Shape;

    [JsonProperty("restrict_visibility_rules")]
    public string[] RestrictedVisibilityRules;

    [JsonProperty("force_invisibility_rules")]
    public string[] ForcedInvisibilityRules;
}

public struct PoptrackerSection
{
    [JsonProperty("name")] public string Name;
    [JsonProperty("clear_as_group")] public bool ClearAsGroup;
    [JsonProperty("chest_unopened_img")] public string UnopenedImage;
    [JsonProperty("chest_opened_img")] public string OpenedImage;
    [JsonProperty("item_count")] public int ItemCount;
    [JsonProperty("hosted_item")] public string HostedItem;
    [JsonProperty("access_rules")] public dynamic AccessRules;
    [JsonProperty("visibility_rules")] public dynamic VisibilityRules;
    [JsonProperty("ref")] public string Reference;
}

public class PoptrackerMap
{
    [JsonProperty("name")] public string Name;
    [JsonProperty("img")] public string Image;
    [JsonProperty("location_size")] public int LocationSize = 24;

    [JsonProperty("location_border_thickness")]
    public int LocationBorderSize = 2;

    [JsonProperty("location_shape")] public string _LocationShape;
}

public class PoptrackerLayout // needed to find the stupid tabs and subtabs ;-;
{
    [JsonProperty("tracker_default")] public PoptrackerLayout DefaultLayout;
    [JsonProperty("tracker_horizontal")] public PoptrackerLayout HorizontalLayout;
    [JsonProperty("tracker_vertical")] public PoptrackerLayout VerticalLayout;
    [JsonProperty("type")] public string Type = ""; // look for tabbed
    [JsonProperty("background")] public string BackgroundColor;
    [JsonProperty("h_alignment")] public string HorizontalAlignment;
    [JsonProperty("v_alignment")] public string VerticalAlignment;
    [JsonProperty("dock")] public string DockType;
    [JsonProperty("orientation")] public string Orientation;
    [JsonProperty("max_height")] public int MaxHeight;
    [JsonProperty("max_width")] public int MaxWidth;
    [JsonProperty("margin")] public string Margin;
    [JsonProperty("item_margin")] public string ItemMargin;
    [JsonProperty("item_size")] public string ItemSize;
    [JsonProperty("item_h_alignment")] public string ItemHorizontalAlignment;
    [JsonProperty("item_v_alignment")] public string ItemVerticalAlignment;
    [JsonProperty("dropshadow")] public bool DropShadow;
    [JsonProperty("text")] public string Text;
    [JsonProperty("header_content")] public dynamic HeaderContent;

    [JsonProperty("content"), JsonConverter(typeof(SingleOrArray<PoptrackerLayout>))]
    public PoptrackerLayout[] Content = [];

    [JsonProperty("tabs"), JsonConverter(typeof(SingleOrArray<PoptrackerLayout>))]
    public PoptrackerLayout[] Tabs = [];

    [JsonProperty("key")] public string KeyReference;
    [JsonProperty("compact")] public bool Compact;

    [JsonProperty("maps"), JsonConverter(typeof(SingleOrArray<string>))]
    public string[] Maps = [];

    [JsonProperty("title")] public string Title = "";
}

public class SingleOrArray<T> : JsonConverter
{
    public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        => throw new NotImplementedException();

    public override bool CanWrite => false;

    public override bool CanConvert(Type objectType)
        => objectType == typeof(List<T>) || objectType == typeof(T[])
                                         || objectType == typeof(T); // dunno what this last one does

    public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
    {
        var token = JToken.Load(reader);
        return token.Type switch
        {
            JTokenType.Array => token.ToObject<T[]>(), JTokenType.Null => null, _ => [token.ToObject<T>()],
        };
    }
}

#endregion