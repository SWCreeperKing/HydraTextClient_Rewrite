using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using CreepyUtil.Archipelago.ApClient;
using Godot;
using HydraTextClient.Scripts.Clients.CircleTracker;
using HydraTextClient.Scripts.Utility.UIHelpers;
using Newtonsoft.Json;

namespace HydraTextClient.Scripts.Mapper;

public partial class MapLoader : Control
{
    [Export] public ButtonAnimation SaveMap;
    [Export] public Control Container;
    [Export] public ItemList List;
    [Export] public PopupPanel LocationPanel;
    [Export] public RichTextLabel LocationPanelText;
    [Export] public PackedScene MapLocation;
    [Export] public PackedScene MapContainer;
    [Export] public PopoutWindow PopoutWindow;
    [Export] public CheckBox AutoTab;
    public MapItemImageLoader ItemImageLoader;
    public List<Maps> MapsList = [];
    public TabStructure Structure;
    public Dictionary<string, TabContainer> MapTabs = [];
    public Dictionary<int, MapLocation> MapLocationMap = [];
    public Dictionary<int, MapNavigator> MapNavMap = [];
    public Dictionary<string, MapNavigator> MapAutoTabbing = [];
    public List<LocationGroup> LocationGroups = [];
    public Action<MapLoader> ExitEvent;
    public ApClient? Client;
    public TrackerPage? Page;
    public Control Parent;
    public bool IsInEditMode;
    private string TrackerName;
    private HashSet<int> SelectedLocation = [];
    private HashSet<int> HoveredLocation = [];
    private Dictionary<string, LocationGroup> LocationGroupingMap = [];
    private bool UpdateItemList;
    private EmptyRichLabelInteractor LocationPopupList;
    private string MapPath;

    public void Setup(string path, string trackerName, Control parent)
    {
        MapPath = path;
        IsInEditMode = parent is not MapTracker;
        Client?.HintsTrackedEvent += UpdateNodes;
        Client?.AddDataStorageListener(
            "Current Map", (_, newValue, _) => CallDeferred("SelectMap", (string)newValue), Scope.Slot
        );

        TrackerName = trackerName;
        Parent = parent;
        MapsList = JsonConvert.DeserializeObject<List<Maps>>(File.ReadAllText($"{path}/atlas.json"));
        Structure = JsonConvert.DeserializeObject<TabStructure>(File.ReadAllText($"{path}/tabs.json"));
        LocationGroups = JsonConvert.DeserializeObject<List<LocationGroup>>(
            File.ReadAllText($"{path}/locationgroups.json")
        );

        if (!IsInEditMode && !CircleTracker.Singleton.Pages.TryGetValue(trackerName, out Page))
        {
            ((MapTracker)parent).UnloadMap(trackerName);
            return;
        }
        if (IsInEditMode) PopoutWindow.HideButton();
        SaveMap.Visible = IsInEditMode;

        ItemImageLoader = new MapItemImageLoader(path);
        Page?.OnLogicUpdated += UpdateNodes;
        foreach (var group in LocationGroups) LocationGroupingMap[group.GroupName] = group;

        Queue<TabStructure> structures = [];
        structures.Enqueue(Structure);

        while (structures.Count != 0)
        {
            var tab = structures.Dequeue();
            foreach (var child in tab.SubTabs) structures.Enqueue(child with { Parent = tab.Name });
            if (MapTabs.ContainsKey(tab.Name)) continue;

            var container = MapTabs[tab.Name] = new TabContainer();
            container.SizeFlagsVertical = SizeFlags.ExpandFill;

            if (tab.Name is "" or null)
            {
                Container.AddChild(container);
                continue;
            }

            container.Name = tab.Name;
            MapTabs[tab.Parent].AddChild(container);
        }

        var nodeId = -1;
        var mapId = -1;
        foreach (var map in MapsList) CreateMap(path, map, ++mapId, ref nodeId);
    }

    public override void _Process(double delta)
    {
        if (!UpdateItemList) return;
        List.Visible = false;
        UpdateItemList = false;

        if (SelectedLocation.Count != 0)
        {
            SetItemList(SelectedLocation.First());
            return;
        }

        if (HoveredLocation.Count == 0) return;
        SetItemList(HoveredLocation.First());
    }

    private void CreateMap(string path, Maps map, int mapId, ref int nodeId)
    {
        var container = MapTabs.GetValueOrDefault(map.Tab, MapTabs[""]);

        var mapContainer = MapAutoTabbing[map.GetId] = MapNavMap[mapId] = MapContainer.Instantiate<MapNavigator>();
        mapContainer.Name = map.MapName is "" ? "Default Map" : map.MapName;
        var image = ImageTexture.CreateFromImage(Image.LoadFromFile($"{path}/maps/{MapsList[mapId].ImageName}"));
        mapContainer.SetImage(image);
        var imageSize = image.GetSize();

        foreach (var loc in map.Nodes) CreateLocationNode(path, loc, mapId, ++nodeId, imageSize, mapContainer);

        container.AddChild(mapContainer);
    }

    private void CreateLocationNode(string path, MapNode loc, int mapId, int nodeId, Vector2 imageSize,
        MapNavigator mapContainer)
    {
        var id = nodeId;
        var node = MapLocation.Instantiate<MapLocation>();
        node.Locations = loc.Locations;
        node.LocationGroup = loc.LocationGroup;
        var nodeSize = new Vector2(Math.Abs(loc.W), Math.Abs(loc.H));

        if (LocationGroupingMap.TryGetValue(loc.LocationGroup, out var group))
        {
            if (ItemImageLoader.TryGet(group.MappedIcon, out var img))
            {
                node.Texture = img;
                node.SetImage(mapId, nodeId, path, group.MappedIcon, nodeSize, this);
                node.HasCustomImage = true;
            }
            else
            {
                node.SetImage(mapId, nodeId, path, "", nodeSize, this);
                GD.PrintErr($"Location Icon not found for: [{group.MappedIcon}]");
            }
        }
        else node.SetImage(mapId, nodeId, path, "", nodeSize, this);
        var popupId = $"{mapId}-{nodeId}";

        node.OnEntered += () =>
        {
            StringBuilder sb = new();
            foreach (var loc in node.Locations.Where(l => Client is null || Client.Locations.Any(kv => kv.Key == l)))
            {
                if (sb.Length != 0) sb.Append('\n');
                sb.Append(loc);
            }

            if (sb.Length == 0) return;

            LocationPanel.Title = popupId;
            LocationPanel.Position = Vector2I.Zero;
            var rect = node.GetGlobalRect();
            LocationPanel.Popup(
                new Rect2I(new Vector2I((int)rect.Position.X, (int)(rect.Position.Y + rect.Size.Y)), Vector2I.Zero)
            );

            LocationPanelText.Text = sb.ToString();
        };
        node.OnExited += () =>
        {
            if (LocationPanel.Title == popupId) LocationPanel.Hide();
        };

        node.OnSelected += () =>
        {
            SelectedLocation.Add(id);
            UpdateItemList = true;
        };
        node.OnUnSelected += () =>
        {
            SelectedLocation.Remove(id);
            UpdateItemList = true;
        };

        mapContainer.Container.MapImage.AddChild(node);
        MapLocationMap[nodeId] = node;

        var nodePos = new Vector2(
            Math.Clamp(loc.X, nodeSize.X / 2f, imageSize.X - nodeSize.X / 2f),
            Math.Clamp(loc.Y, nodeSize.Y / 2f, imageSize.Y - nodeSize.Y / 2f)
        );
        node.Position = nodePos;
    }

    public void SetItemList(int locationId)
    {
        List.Visible = true;
        List.Clear();

        var node = MapLocationMap[locationId];
        LocationGroup? group = node.LocationGroup is ""
                               || !LocationGroupingMap.TryGetValue(node.LocationGroup, out var tGroup) ? null : tGroup;
        foreach (var loc in node.Locations)
        {
            if (Client is not null && Client.Locations.All(kv => kv.Key != loc)) continue;
            var i = List.AddItem(loc);
            if (group is null) continue;
            var icon = Client is not null && Client.MissingLocations.Contains(loc) ? group!.Value.AvailableIcon
                : group!.Value.CollectedIcon;
            if (icon is "" || !ItemImageLoader.TryGet(icon, out var img))
            {
                if (icon is not "") GD.PrintErr($"Missing icon for [{icon}]");
                return;
            }
            List.SetItemIcon(i, img);
        }
    }

    public void SelectMap(string mapId)
    {
        if (!AutoTab.ButtonPressed) return;
        if (!MapAutoTabbing.TryGetValue(mapId, out var map)) return;
        var container = (Control)map.GetParent();
        map.Visible = true;
        Control parent;
        while ((parent = (Control)container.GetParent()) is TabContainer)
        {
            container.Visible = true;
            container = parent;
        }
    }

    public void ResetSelectedNodes()
    {
        foreach (var id in SelectedLocation) MapLocationMap[id].EmitUnSelect();
        SelectedLocation.Clear();
        UpdateItemList = true;
    }

    public void UpdateNodes(Hint[] hints) => UpdateNodes();

    public void UpdateNodes()
    {
        foreach (var node in MapLocationMap.Values) node.QueueUpdate = true;
    }

    public void StopAndClose() => ExitEvent?.Invoke(this);

    public void ResetZoom()
    {
        var container = MapTabs[""];
        while (true)
        {
            if (container.GetChildren().Count == 0) return;
            switch (container.GetChild(container.CurrentTab))
            {
                case TabContainer newContainer: container = newContainer; break;
                case MapNavigator nav:
                    nav.Container.ResetZoom();
                    return;
            }
        }
    }

    public void SaveMapData()
    {
        File.WriteAllText($"{MapPath}/atlas.json", JsonConvert.SerializeObject(MapsList));
        File.WriteAllText($"{MapPath}/tabs.json", JsonConvert.SerializeObject(Structure));
        File.WriteAllText($"{MapPath}/locationgroups.json", JsonConvert.SerializeObject(LocationGroups));
    }

    protected override void Dispose(bool disposing)
    {
        if (IsInEditMode) SaveMapData();
        Client?.HintsTrackedEvent -= UpdateNodes;
        Client?.RemoveDataStorageListeners("Current Map", Scope.Slot);
        Page?.OnLogicUpdated -= UpdateNodes;
    }
}