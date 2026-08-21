using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Godot;

namespace HydraTextClient.Scripts.Mapper;

public partial class MapNavigator : ScrollContainer
{
    [Export] public MapControl Container;
    [Export] public PackedScene MapLocation;
    public MapLoader Loader;
    public Maps CoreMap;
    public string MapPath;
    public List<MapLocation> Locations = [];

    public Vector2 GetMapSize => Container.MapImage.Texture.GetSize();

    public void UpdateNodes()
    {
        foreach (var node in Locations) node.QueueUpdate = true;
    }
    
    public void SetupMap(MapLoader loader, Maps map, string packPath)
    {
        Loader = loader;
        CoreMap = map;
        MapPath = packPath;
        SetMapName(map.MapName);
        SetImage(map.ImageName);
        foreach (var loc in map.Nodes) CreateLocationNode(loc);
    }

    public void SetMapName(string name)
    {
        if (CoreMap is null) return;
        if (CoreMap!.MapName != name) CoreMap.MapName = name;
        Name = CoreMap.MapName is "" ? "Default Map" : CoreMap.MapName;
    }

    public void SetImage(string name)
    {
        if (CoreMap is null) return;
        if (CoreMap!.ImageName != name) CoreMap.ImageName = name;
        var image = ImageTexture.CreateFromImage(Image.LoadFromFile($"{MapPath}{name}"));
        Container.MapImage.Texture = image;
        Container.ResetZoom();
    }
    
    private void CreateLocationNode(MapNode loc)
    {
        var node = MapLocation.Instantiate<MapLocation>();
        node.RawNodeData = loc;

        node.SetNodeSize(new Vector2(Math.Abs(loc.W), Math.Abs(loc.H)));
        node.SetData(this);
        if (UpdateLocationGroup(node)) return; // return if slot data doesn't match
        node.OnEntered += () => Loader.AddHoverLocation(node);
        node.OnExited += () => Loader.RemoveHoverLocation(node);
        node.OnSelected += () => Loader.AddSelectedLocation(node);
        node.OnUnSelected += () => Loader.RemoveSelectedLocation(node);
        
        Container.MapImage.AddChild(node);
        Locations.Add(node);
        node.SetPos(new Vector2(loc.X, loc.Y));
    }
    
    public bool UpdateLocationGroup(MapLocation node)
    {
        if (Loader.LocationGroupingMap.TryGetValue(node.LocationGroup, out var group))
        {
            if (group.SlotDataKey is not ("" or null) && !Loader.IsInEditMode)
            {
                if (Loader.Client!.SlotData.TryGetValue(group.SlotDataKey, out var slotDataVal)
                    && !group.CompareDataValue(slotDataVal)) return true;
                GD.PrintErr($"Slot data key is invalid: [{group.SlotDataKey}]");
            }

            if (Loader.ItemImageLoader.TryGet(group.MappedIcon, out var img))
            {
                node.Texture = img;
                node.SetImage(group.MappedIcon);
                node.HasCustomImage = true;
            }
            else
            {
                node.SetImage("");
                GD.PrintErr($"Location Icon not found for: [{group.MappedIcon}]");
            }
        }
        else node.SetImage("");
        return false;
    }
}