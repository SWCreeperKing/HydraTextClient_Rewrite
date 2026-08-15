using System;
using System.Collections.Generic;
using System.Linq;
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

public struct Maps(string mapName, string imageName, string tab = "", params List<MapNode> nodes)
{
    public string MapName = mapName;
    public string ImageName = imageName;
    public string Tab = tab;
    public List<MapNode> Nodes = nodes;
}

public struct MapNode(float x, float y, float w, float h, string group = "",
    params List<string> locationChecks)
{
    public string LocationGroup = group;
    public List<string> Locations = locationChecks;
    public float X = x;
    public float Y = y;
    public float W = w;
    public float H = h;
}

public struct LocationGroup(string name, string mapIcon, string openIcon = "", string closeIcon = "")
{
    public string GroupName = name;
    public string MappedIcon = mapIcon;
    public string AvailableIcon = openIcon;

    public string CollectedIcon = closeIcon;
    // todo: add slot data conditions here
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