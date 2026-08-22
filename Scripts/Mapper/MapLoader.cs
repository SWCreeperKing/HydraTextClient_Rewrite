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
using HydraTextClient.Scripts.Mapper.Popups;
using HydraTextClient.Scripts.Utility.UIHelpers;
using Newtonsoft.Json;

namespace HydraTextClient.Scripts.Mapper;

public partial class MapLoader : Control
{
    [Export] public ButtonAnimation SaveMap;
    [Export] public Control Container;
    [Export] public ItemList List;
    [Export] public Control ListContainer;
    [Export] public Control ListEditControls;
    [Export] public PopupPanel LocationPanel;
    [Export] public RichTextLabel LocationPanelText;
    [Export] public PopoutWindow PopoutWindow;
    [Export] public CheckBox AutoTab;
    [Export] public PackedScene MapContainer;
    [Export] private PackedScene AddLocationsPopup;
    [Export] private PackedScene EditMapPopup;
    public MapItemImageLoader ItemImageLoader;
    public List<Maps> MapsList = [];
    public TabStructure Structure;
    public Dictionary<string, TabContainer> MapTabs = [];
    public List<MapNavigator> MapNavigators = [];
    public List<LocationGroup> LocationGroups = [];
    public Action<MapLoader> ExitEvent;
    public ApClient? Client;
    public TrackerPage? Page;
    public Control Parent;
    public bool IsInEditMode;
    public List<string> CollectedLocations = [];
    public Dictionary<string, LocationGroup> LocationGroupingMap = [];
    private string TrackerName;
    private HashSet<MapLocation> SelectedLocation = [];
    private List<MapLocation> HoveredLocation = [];
    private bool UpdateUI;
    private EmptyRichLabelInteractor LocationPopupList;
    private string MapPath;

    public void Setup(string path, string trackerName, Control parent)
    {
        ListContainer.Visible = false;
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
        if (IsInEditMode)
        {
            PopoutWindow.HideButton();
            Client?.OnItemLogPacketReceived += packet =>
            {
                if (Client is null) return;
                var player = packet.Item.Player;
                if (Client.PlayerSlot != player) return;
                CollectedLocations.Add(Client.LocationIdToLocationName(packet.Item.Location, player));
            };
        }
        SaveMap.Visible = IsInEditMode;
        ListEditControls.Visible = IsInEditMode;

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

        foreach (var map in MapsList) CreateMap(path, map);
    }

    public override void _Process(double delta)
    {
        if (!UpdateUI) return;
        ListContainer.Visible = false;
        UpdateUI = false;

        if (SelectedLocation.Count != 0) SetItemList(SelectedLocation.First());
        var possibleHovered = HoveredLocation.Where(loc => !SelectedLocation.Contains(loc)).ToArray();
        if (possibleHovered.Length != 0)
        {
            var node = possibleHovered[0];

            StringBuilder sb = new();
            foreach (var loc in node.Locations.Where(l => Client is null || Client.Locations.Any(kv => kv.Key == l)))
            {
                if (loc.Trim() is "") continue;
                if (sb.Length != 0) sb.Append('\n');
                sb.Append(loc);
            }

            if (sb.Length == 0) return;

            LocationPanelText.Text = "";
            LocationPanel.Position = Vector2I.Zero;
            LocationPanelText.Size = Vector2.Zero;
            var rect = node.GetGlobalRect();
            LocationPanel.Popup(
                new Rect2I(new Vector2I((int)rect.Position.X, (int)(rect.Position.Y + rect.Size.Y)), Vector2I.Zero)
            );

            LocationPanelText.Text = sb.ToString();
        }
        else LocationPanel.Hide();
    }

    private void CreateMap(string path, Maps map)
    {
        var container = MapTabs.GetValueOrDefault(map.Tab, MapTabs[""]);
        var mapContainer = MapContainer.Instantiate<MapNavigator>();
        mapContainer.SetupMap(this, map, $"{path}/maps/");
        container.AddChild(mapContainer);
        MapNavigators.Add(mapContainer);
    }

    public void AddHoverLocation(MapLocation node)
    {
        HoveredLocation.Add(node);
        UpdateUI = true;
    }

    public void RemoveHoverLocation(MapLocation node)
    {
        HoveredLocation.Remove(node);
        UpdateUI = true;
    }

    public void AddSelectedLocation(MapLocation loc)
    {
        SelectedLocation.Add(loc);
        UpdateUI = true;
    }

    public void RemoveSelectedLocation(MapLocation loc)
    {
        SelectedLocation.Remove(loc);
        UpdateUI = true;
    }

    public void SetItemList(MapLocation location)
    {
        ListContainer.Visible = true;
        List.Clear();

        LocationGroup? group = location.LocationGroup is ""
                               || !LocationGroupingMap.TryGetValue(location.LocationGroup, out var tGroup) ? null
            : tGroup;
        foreach (var loc in location.Locations)
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

    public void EditMap()
    {
        var map = GetCurrentMap();
        if (map is null) return;
        var popup = EditMapPopup.Instantiate<EditMapWindow>();
        popup.Setup(map);
        popup.EditMapData += map.EditMapData;
        CallDeferred("add_child", popup);
        popup.CallDeferred("show");
    }
    
    public void SelectMap(string mapId)
    {
        if (!AutoTab.ButtonPressed) return;
        if (!TryGetMapWithId(mapId, out var map)) return;
        var container = (Control)map.GetParent();
        map.Visible = true;
        Control parent;
        while ((parent = (Control)container.GetParent()) is TabContainer)
        {
            container.Visible = true;
            container = parent;
        }
    }

    public bool TryGetMapWithId(string id, out MapNavigator? foundMap)
    {
        foundMap = MapNavigators.FirstOrDefault(map => map.MapId == id, null);
        return foundMap is not null;
    }
    
    public void ResetSelectedNodes()
    {
        foreach (var loc in SelectedLocation) loc.EmitUnSelect();
        SelectedLocation.Clear();
        UpdateUI = true;
    }

    public void UpdateNodes(Hint[] hints) => UpdateNodes();

    public void UpdateNodes()
    {
        foreach (var map in MapNavigators) map.UpdateNodes();
    }

    public void CopyLocations()
    {
        var locs = SelectedLocation.First();
        if (locs.Locations.Count == 0) return;
        var locationNamesToCopy = List.GetSelectedItems().Select(i => locs.Locations[i]).ToArray();
        DisplayServer.ClipboardSet(string.Join('\n', locationNamesToCopy));
    }

    public void AddLocations()
    {
        var popup = AddLocationsPopup.Instantiate<MapAddLocations>();
        popup.Setup(this);
        popup.AddLocations += locs =>
        {
            var locMap = SelectedLocation.First();
            locs = locs.Select(l => l.Trim()).Where(l => l is not "" && !locMap.Locations.Contains(l)).ToArray();
            locMap.Locations.AddRange(locs.DistinctBy(s => s));
            UpdateUI = true;
            UpdateNodes();
        };
        AddChild(popup);
        popup.Show();
    }

    public void RemoveSelectedLocations()
    {
        var locs = SelectedLocation.First();
        if (locs.Locations.Count == 0) return;
        var locationNamesToRemove = List.GetSelectedItems().Select(i => locs.Locations[i]).ToArray();
        locs.Locations.RemoveAll(loc => locationNamesToRemove.Contains(loc));
        UpdateUI = true;
        UpdateNodes();
    }

    public void StopAndClose() => ExitEvent?.Invoke(this);
    public void ResetZoom() => GetCurrentMap()?.Container.ResetZoom();
    
    public MapNavigator GetCurrentMap()
    {
        var container = MapTabs[""];
        while (true)
        {
            if (container.GetChildren().Count == 0) return null;
            switch (container.GetChild(container.CurrentTab))
            {
                case TabContainer newContainer: container = newContainer; break;
                case MapNavigator nav: return nav;
                default: return null;
            }
        }
    }

    public void SaveMapData()
    {
        File.WriteAllText($"{MapPath}/atlas.json", JsonConvert.SerializeObject(MapsList));
        File.WriteAllText($"{MapPath}/tabs.json", JsonConvert.SerializeObject(Structure));
        File.WriteAllText($"{MapPath}/locationgroups.json", JsonConvert.SerializeObject(LocationGroups));
    }

    public void OpenFolder() => OS.ShellOpen(MapPath);

    protected override void Dispose(bool disposing)
    {
        if (IsInEditMode) SaveMapData();
        Client?.HintsTrackedEvent -= UpdateNodes;
        Client?.RemoveDataStorageListeners("Current Map", Scope.Slot);
        Page?.OnLogicUpdated -= UpdateNodes;
    }
}