using System;
using System.Collections.Generic;
using System.IO;
using Godot;

namespace HydraTextClient.Scripts.Mapper;

public partial class MapNavigator : ScrollContainer
{
    [Export] public MapControl Container;
    [Export] public PackedScene MapLocation;
    [Export] public Texture2D DefaultImage;
    public MapLoader Loader;
    public Maps CoreMap;
    public string MapPath;
    public List<MapLocation> Locations = [];
    public string MapId => CoreMap.GetId;

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
        Container.OnRightClick += Loader.RightClickedMap;
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
        if (CoreMap is null)
        {
            LoadImage("");
            return;
        }
        if (CoreMap!.ImageName != name) CoreMap.ImageName = name;
        LoadImage(name);
    }

    private void LoadImage(string name)
    {
        if (name is not "" && !File.Exists($"{MapPath}{name}")) name = "";
        var image = name is "" ? DefaultImage : ImageTexture.CreateFromImage(Image.LoadFromFile($"{MapPath}{name}"));
        Container.MapImage.Texture = image;
        Container.ResetZoom();
    }

    public void EditMapData(string mapName, string image, string mapId)
    {
        SetMapName(mapName);
        SetImage(image);
        CoreMap.MapId = mapId;
    }

    public void CreateNewNode(Vector2 pos)
    {
        MapNode newNode = new(pos.X, pos.Y, 32, 32);
        CoreMap.Nodes.Add(newNode);
        CreateLocationNode(newNode);
    }

    public void CreateNewNode(MapNode nodeData, Vector2 pos)
    {
        nodeData.X = pos.X;
        nodeData.Y = pos.Y;
        CoreMap.Nodes.Add(nodeData);
        CreateLocationNode(nodeData);
    }

    public void DeleteNode(MapLocation node)
    {
        CoreMap.Nodes.Remove(node.RawNodeData);
        Container.MapImage.CallDeferred("remove_child", node);
        Locations.Remove(node);
        node.QueueFree();
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
        node.OnRightClick += () => Loader.RightClickedNode(node);

        try { Container.MapImage.AddChild(node); }
        catch { Container.MapImage.CallDeferred("add_child", node); }
        Locations.Add(node);
        node.Pos = new Vector2(loc.X, loc.Y);
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