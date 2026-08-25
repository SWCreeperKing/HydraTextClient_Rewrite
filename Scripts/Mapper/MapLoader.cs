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
    [Export] private PackedScene ManageTabPopup;
    [Export] private PackedScene LocationGroupsManagerPopup;
    [Export] private PackedScene LocationIconOverridePopup;
    [Export] private PackedScene EditMapNodePopup;
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
    public Dictionary<string, string> LocationClosedIconOverride = [];
    public Dictionary<string, string> LocationOpenedIconOverride = [];
    public bool UpdateUI;
    private string TrackerName;
    private List<MapLocation> SelectedLocation = [];
    private List<MapLocation> HoveredLocation = [];
    private EmptyRichLabelInteractor LocationPopupList;
    private string MapPath;
    private PopupMenu OptionMenu;
    private MapLocation RightClickSelectedNode;
    private MapLocation CopyTargetNode;
    private MapLocation MoveTargetNode;
    private Vector2 PopupPos;

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

        if (File.Exists($"{path}/locationiconopen.json"))
            LocationOpenedIconOverride = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                File.ReadAllText($"{path}/locationiconopen.json")
            );
        if (File.Exists($"{path}/locationiconclose.json"))
            LocationClosedIconOverride = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                File.ReadAllText($"{path}/locationiconclose.json")
            );

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
        Structure.Name = "";
        if (Structure.SubTabs is null) Structure.SubTabs = [];
        structures.Enqueue(Structure);

        while (structures.Count != 0)
        {
            var tab = structures.Dequeue();
            foreach (var child in tab.SubTabs) structures.Enqueue(child with { Parent = tab.Name });
            if (MapTabs.ContainsKey(tab.Name)) continue;

            var container = MapTabs[tab.Name] = new TabContainer();
            container.SizeFlagsVertical = SizeFlags.ExpandFill;

            if (IsInEditMode)
            {
                container.DragToRearrangeEnabled = true;
                container.TabsRearrangeGroup = 59823532;
            }

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
        if (IsInEditMode) return;
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
        var container = MapTabs.GetValueOrDefault(map.Tab ?? "", MapTabs[""]);
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
        HoveredLocation.RemoveAll(n => n == node);
        UpdateUI = true;
    }

    public void AddSelectedLocation(MapLocation loc)
    {
        SelectedLocation.Add(loc);
        UpdateUI = true;
    }

    public void RemoveSelectedLocation(MapLocation loc)
    {
        SelectedLocation.RemoveAll(l => l == loc);
        UpdateUI = true;
    }

    public void SetItemList(MapLocation location)
    {
        ListContainer.Visible = true;
        List.Clear();

        var group = location.LocationGroup is ""
                    || !LocationGroupingMap.TryGetValue(location.LocationGroup, out var tGroup) ? null : tGroup;
        foreach (var loc in location.Locations)
        {
            if (Client is not null && Client.Locations.All(kv => kv.Key != loc)) continue;
            var i = List.AddItem(loc);
            switch (Client?.MissingLocations.Contains(loc))
            {
                case true when LocationClosedIconOverride.ContainsKey(loc):
                    if (ItemImageLoader.TryGet(LocationClosedIconOverride[loc], out var closedImg))
                        List.SetItemIcon(i, closedImg);
                    break;
                case false when LocationOpenedIconOverride.ContainsKey(loc):
                    if (ItemImageLoader.TryGet(LocationOpenedIconOverride[loc], out var openedImg))
                        List.SetItemIcon(i, openedImg);
                    break;
                default:
                    if (group is null) continue;
                    var icon = Client is not null && Client.MissingLocations.Contains(loc) ? group!.AvailableIcon
                        : group!.CollectedIcon;
                    if (icon is "" || !ItemImageLoader.TryGet(icon, out var img))
                    {
                        if (icon is not "") GD.PrintErr($"Missing icon for [{icon}]");
                        return;
                    }
                    List.SetItemIcon(i, img);
                    break;
            }
        }
    }

    public void EditMap()
    {
        var map = GetCurrentMap();
        if (map is null) return;
        var popup = EditMapPopup.Instantiate<EditMapWindow>();
        popup.Setup(map);
        popup.EditMapData += (name, image, id) =>
        {
            if (FindMapByName(name) is null) return;
            map.EditMapData(name, image, id);
        };
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

    public void RightClickedNode(MapLocation location)
    {
        if (!IsInEditMode) return;
        RightClickSelectedNode = location;
        CreatePopup(menu =>
            {
                menu.AddItem("Edit Node", 0);
                menu.AddItem("Move Node", 1);
                menu.AddItem("Copy Node", 2);
            }
        );
    }

    public void RightClickedMap()
    {
        if (!IsInEditMode) return;
        CreatePopup(menu =>
            {
                if (RightClickSelectedNode is not null) ResetRightClickSelectedNode();
                menu.AddItem("Create Node", 3);
                if (MoveTargetNode is not null) menu.AddItem("Move Node Here", 4);
                if (CopyTargetNode is not null) menu.AddItem("Paste Node Here", 5);
            }
        );
    }

    public void OptionSelected(long option)
    {
        MapNavigator map;
        Vector2 pos;
        switch (option)
        {
            case 0:
                SelectedLocation.Insert(0, RightClickSelectedNode);
                RightClickSelectedNode.Highlighter.Enter();
                RightClickSelectedNode.Highlighter.Select();
                UpdateUI = true;
                EditNode();
                break;
            case 1: MoveTargetNode = RightClickSelectedNode; break;
            case 2: CopyTargetNode = RightClickSelectedNode; break;
            case 3:
                map = GetCurrentMap();
                pos = (PopupPos - map.Container.MapImage.GlobalPosition) / map.Container.MapImage.Scale;
                map.CreateNewNode(pos);
                break;
            case 4:
                map = GetCurrentMap();
                pos = (PopupPos - map.Container.MapImage.GlobalPosition) / map.Container.MapImage.Scale;
                map.CreateNewNode(MoveTargetNode.RawNodeData, pos - MoveTargetNode.Size / 2);
                RemoveSelectedLocation(MoveTargetNode);
                MoveTargetNode.Map.DeleteNode(MoveTargetNode);
                MoveTargetNode = null;
                break;
            case 5:
                map = GetCurrentMap();
                pos = (PopupPos - map.Container.MapImage.GlobalPosition) / map.Container.MapImage.Scale;
                map.CreateNewNode(CopyTargetNode.RawNodeData.Copy(), pos - CopyTargetNode.Size / 2);
                RemoveSelectedLocation(CopyTargetNode);
                CopyTargetNode = null;
                break;
        }

        ResetRightClickSelectedNode();
    }

    public void CreatePopup(Action<PopupMenu> propagateItems)
    {
        if (OptionMenu is not null)
        {
            OptionMenu?.Hide();
            CallDeferred("remove_child", OptionMenu);
            OptionMenu?.QueueFree();
            OptionMenu = null;
        }

        PopupMenu menu = new();
        CallDeferred("add_child", menu);
        menu.PopupHide += () =>
        {
            OptionMenu = null;
            CallDeferred("remove_child", menu);
            menu.QueueFree();
        };
        menu.IdPressed += OptionSelected;

        propagateItems(menu);

        menu.Position = Vector2I.Zero;
        menu.CallDeferred("popup", new Rect2I((Vector2I)(PopupPos = GetGlobalMousePosition()), menu.Size));
        OptionMenu = menu;
    }

    public void ResetRightClickSelectedNode() => RightClickSelectedNode = null;
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

    public void EditNode()
    {
        var popup = EditMapNodePopup.Instantiate<EditNodeDataPopup>();
        popup.Setup(this, SelectedLocation.First());
        AddChild(popup);
        popup.Show();
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

    public void StopAndClose()
    {
        if (IsInEditMode) SaveMapData();
        ExitEvent?.Invoke(this);
    }

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

    public void ManageTabs()
    {
        var popup = ManageTabPopup.Instantiate<TabManager>();
        popup.ConfirmAction += (action, name, dest) => CallDeferred("OnPopupOnConfirmAction", (int)action, name, dest);
        AddChild(popup);
        popup.Show();
    }

    private void OnPopupOnConfirmAction(int action, string name, string destination)
    {
        if (!MapTabs.ContainsKey(destination)) destination = "";
        var target = MapTabs[destination];
        TabContainer tab;
        MapNavigator map;
        switch ((TabManager.ManageAction)action)
        {
            case TabManager.ManageAction.AddMap:
                if (FindMapByName(name) is not null) return;
                CreateMap(MapPath, new Maps(name, "", destination));
                break;
            case TabManager.ManageAction.MoveMap:
                if ((map = FindMapByName(name)) is null) return;
                map.GetParent().RemoveChild(map);
                target.AddChild(map);
                break;
            case TabManager.ManageAction.DeleteMap:
                if ((map = FindMapByName(name)) is null) return;
                MapNavigators.Remove(map);
                map.GetParent().RemoveChild(map);
                map.QueueFree();
                break;
            case TabManager.ManageAction.AddTab:
                if (MapTabs.ContainsKey(name)) return;
                tab = MapTabs[name] = new TabContainer();
                tab.SizeFlagsVertical = SizeFlags.ExpandFill;
                tab.Name = name;
                tab.DragToRearrangeEnabled = true;
                tab.TabsRearrangeGroup = 59823532;
                target.AddChild(tab);
                break;
            case TabManager.ManageAction.MoveTab:
                if (!MapTabs.TryGetValue(name, out tab)) return;
                tab.GetParent().RemoveChild(tab);
                target.AddChild(tab);
                break;
            case TabManager.ManageAction.DeleteTab:
                if (!MapTabs.TryGetValue(name, out tab)) return;
                foreach (var child in tab.GetChildren())
                {
                    tab.RemoveChild(child);
                    target.AddChild(child);
                }
                tab.GetParent().RemoveChild(tab);
                tab.QueueFree();
                MapTabs.Remove(name);
                break;
        }
    }

    public MapNavigator FindMapByName(string name)
        => MapNavigators.FirstOrDefault(map => map.CoreMap.MapName == name, null);

    public void EditLocationGroup()
    {
        var popup = LocationGroupsManagerPopup.Instantiate<LocationGroupsManagement>();
        popup.Setup(this);
        AddChild(popup);
        popup.Show();
    }

    public void EditLocationIconOverrides()
    {
        var popup = LocationIconOverridePopup.Instantiate<LocationIconsOverrider>();
        popup.Setup(this);
        AddChild(popup);
        popup.Show();
    }

    public void SaveMapData()
    {
        if (!IsInEditMode) return;
        List<Maps> newMapList = [];
        TabStructure newStructure = new("");

        Queue<(TabContainer, TabStructure)> containers = [];
        containers.Enqueue((MapTabs[""], newStructure));
        while (containers.Count != 0)
        {
            var (container, associatedStructure) = containers.Dequeue();

            foreach (var child in container.GetChildren())
            {
                switch (child)
                {
                    case TabContainer tab:
                        var childStructure = new TabStructure(tab.Name);
                        associatedStructure.SubTabs.Add(childStructure);
                        containers.Enqueue((tab, childStructure));
                        break;
                    case MapNavigator nav:
                        var map = nav.CoreMap;
                        map.Tab = associatedStructure.Name;
                        newMapList.Add(map);
                        break;
                }
            }
        }

        File.WriteAllText($"{MapPath}/atlas.json", JsonConvert.SerializeObject(newMapList));
        File.WriteAllText($"{MapPath}/tabs.json", JsonConvert.SerializeObject(newStructure));
        File.WriteAllText($"{MapPath}/locationgroups.json", JsonConvert.SerializeObject(LocationGroups));
        File.WriteAllText($"{MapPath}/locationiconopen.json", JsonConvert.SerializeObject(LocationOpenedIconOverride));
        File.WriteAllText($"{MapPath}/locationiconclose.json", JsonConvert.SerializeObject(LocationClosedIconOverride));
    }

    public void OpenFolder() => OS.ShellOpen(MapPath);

    public override void _Notification(int what)
    {
        if (what != NotificationWMCloseRequest) return;
        if (IsInEditMode) SaveMapData();
    }

    protected override void Dispose(bool disposing)
    {
        Client?.HintsTrackedEvent -= UpdateNodes;
        Client?.RemoveDataStorageListeners("Current Map", Scope.Slot);
        Page?.OnLogicUpdated -= UpdateNodes;
    }
}