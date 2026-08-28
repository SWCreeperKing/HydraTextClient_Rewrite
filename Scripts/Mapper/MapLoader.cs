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
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Mapper.Popups;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.UIHelpers;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace HydraTextClient.Scripts.Mapper;

public partial class MapLoader : Control
{
    [Export] public ButtonAnimation SaveMap;
    [Export] public Control Container;
    [Export] public ItemList List;
    [Export] public Control ListContainer;
    [Export] public Control ListEditControls;
    [Export] public Control MapEditorControls;
    [Export] public PopupPanel LocationPanel;
    [Export] public RichTextLabel LocationPanelText;
    [Export] public PopoutWindow PopoutWindow;
    [Export] public CheckBox AutoTab;
    [Export] public PackedScene MapContainer;
    [Export] private CheckBox OpenConfig;
    [Export, ExportGroup("Popups")] private PackedScene AddLocationsPopup;
    [Export] private PackedScene EditMapPopup;
    [Export] private PackedScene ManageTabPopup;
    [Export] private PackedScene LocationGroupsManagerPopup;
    [Export] private PackedScene LocationIconOverridePopup;
    [Export] private PackedScene EditMapNodePopup;
    [Export] private PackedScene EditEntranceNodePopup;
    [Export] private PackedScene EntranceManagerPopup;
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
    public Dictionary<string, string> EntranceMap = [];
    public Dictionary<string, string> TrueEntranceMap = [];
    public Dictionary<string, List<EntranceLocation>> EntranceNodes = [];
    public HashSet<string> FoundEntrances = [];
    public bool UpdateUI;
    public bool IsEntranceRando;
    public bool UseEntranceRandoMaps;
    private string TrackerName;
    private string MapPath;
    private EmptyRichLabelInteractor LocationPopupList;
    private PopupMenu OptionMenu;
    private MapLocation? HoveredMapLocation;
    private MapLocation? SelectedMapLocation;
    private EntranceLocation? LinkingEntrance;
    private MapLocation? RightClickSelectedNode;
    private EntranceLocation? RightClickSelectedEntranceNode;
    private MapLocation CopyTargetNode;
    private MapLocation MoveTargetNode;
    private Vector2 PopupPos;
    private bool AutoTrackEntrances;

    public void Setup(string path, string trackerName, Control parent)
    {
        ListContainer.Visible = false;
        MapPath = path;
        IsInEditMode = parent is not MapTracker;
        Client?.HintsTrackedEvent += UpdateNodes;

        TrackerName = trackerName;
        Parent = parent;
        MapsList = JsonConvert.DeserializeObject<List<Maps>>(File.ReadAllText($"{path}/atlas.json"));
        Structure = JsonConvert.DeserializeObject<TabStructure>(File.ReadAllText($"{path}/tabs.json"));

        if (File.Exists($"{path}/entrance_rando_names.json"))
            EntranceMap = JsonConvert.DeserializeObject<Dictionary<string, string>>(
                File.ReadAllText($"{path}/entrance_rando_names.json")
            );
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
        MapEditorControls.Visible = IsInEditMode;

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

            if (tab.Name.StartsWith("__") && tab.Name.Length > 2)
            {
                var mapName = tab.Name[2..];
                var map = MapsList.FirstOrDefault(m => m.MapName == mapName, null);
                if (map is not null) CreateMap(path, map);
                continue;
            }

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

        UseEntranceRandoMaps = CheckIfEntranceRandoEnabled(out AutoTrackEntrances);
        Client?.AddDataStorageListener(
            UseEntranceRandoMaps ? "Entrance Tracker Map" : "Current Map",
            (_, newValue, _) => CallDeferred("SelectMap", (string)newValue), Scope.Slot
        );

        if (IsInEditMode || !UseEntranceRandoMaps) return;
        IsEntranceRando = true;

        if (Client!.SlotData.TryGetValue("Entrance Rando", out var value))
        {
            try
            {
                TrueEntranceMap = ((JObject)value).ToObject<Dictionary<string, string>>();
                foreach (var (entrance, dest) in TrueEntranceMap.ToArray())
                    TrueEntranceMap.TryAdd(dest, entrance); // fill in, just in case
                var saveEntranceData = false;
                foreach (var key in TrueEntranceMap.Keys.Where(key => !EntranceMap.ContainsKey(key)))
                {
                    EntranceMap.TryAdd(key, key);
                    saveEntranceData = true;
                }
                if (saveEntranceData)
                    File.WriteAllText($"{MapPath}/entrance_rando_names.json", JsonConvert.SerializeObject(EntranceMap));
            }
            catch (Exception e) { GD.PrintErr("Entrance Map not in correct format", e); }
        }

        if (!AutoTrackEntrances) return;
        foreach (var (entranceId, _) in EntranceMap)
        {
            Client.GetFromStorageAsync(
                entranceId, val =>
                {
                    if (val) CallDeferred("EntranceFound", entranceId);
                }, def: false
            );

            Client!.AddDataStorageListener(
                entranceId, (_, newValue, _) =>
                {
                    try
                    {
                        if ((bool)newValue) CallDeferred("EntranceFound", entranceId);
                    }
                    catch { Client!.RemoveDataStorageListeners(entranceId); }
                }, Scope.Slot
            );
        }
        GD.Print($"found stick? [{FoundEntrances.Contains("Overworld Redux, Sword Cave_")}]");
    }

    public bool CheckIfEntranceRandoEnabled(out bool autoTracking)
    {
        autoTracking = false;
        if (Client is null) return false;
        if (!Client!.SlotData.ContainsKey("entrance_rando")) return true;
        try { return autoTracking = (long)Client!.SlotData["entrance_rando"] == 1; }
        catch
        {
            try { return autoTracking = (bool)Client!.SlotData["entrance_rando"]; }
            catch (Exception e) { GD.PrintErr("check for entrance_rando failed", e); }
        }
        return false;
    }

    public void EntranceFound(string entranceId)
    {
        Client?.RemoveDataStorageListeners(entranceId, Scope.Slot);
        FoundEntrances.Add(entranceId);
        if (!EntranceNodes.TryGetValue(entranceId, out var entranceNode)) return;
        foreach (var node in entranceNode) node.UpdateEntrance = true;
    }

    public void SetEntrance(string entranceId, string destinationId)
    {
        if (entranceId is "") return;
        var mw = ConnectionController.GetCurrentMultiworld;
        if (mw is null) return;
        if (!mw.MapEntrances.ContainsKey(Client!.PlayerName)) mw.MapEntrances[Client!.PlayerName] = [];
        if (destinationId is not "") mw.MapEntrances[Client!.PlayerName][entranceId] = destinationId;
        else mw.MapEntrances[Client!.PlayerName].Remove(entranceId);
        if (!EntranceNodes.TryGetValue(entranceId, out var entranceNode)) return;
        foreach (var node in entranceNode) node.UpdateEntrance = true;
    }

    public override void _Process(double delta)
    {
        if (!UpdateUI) return;
        ListContainer.Visible = false;
        UpdateUI = false;

        if (SelectedMapLocation is not null) SetItemList(SelectedMapLocation);
        if (IsInEditMode) return;
        if (HoveredMapLocation is null || HoveredMapLocation == SelectedMapLocation)
        {
            LocationPanel.Hide();
            return;
        }
        StringBuilder sb = new();
        foreach (var loc in HoveredMapLocation.Locations.Where(l => Client is null
                                                                    || Client.Locations.Any(kv => kv.Key == l)
                 ))
        {
            if (loc.Trim() is "") continue;
            if (sb.Length != 0) sb.Append('\n');
            sb.Append(loc);
        }

        if (sb.Length == 0) return;

        LocationPanelText.Text = "";
        LocationPanel.Position = Vector2I.Zero;
        LocationPanelText.Size = Vector2.Zero;
        var rect = HoveredMapLocation.GetGlobalRect();
        LocationPanel.Popup(
            new Rect2I(new Vector2I((int)rect.Position.X, (int)(rect.Position.Y + rect.Size.Y)), Vector2I.Zero)
        );

        LocationPanelText.Text = sb.ToString();
    }

    private void CreateMap(string path, Maps map)
    {
        if (MapNavigators.Any(m => m.CoreMap == map)) return;
        var container = MapTabs.GetValueOrDefault(map.Tab ?? "", MapTabs[""]);
        var mapContainer = MapContainer.Instantiate<MapNavigator>();
        mapContainer.SetupMap(this, map, $"{path}/maps/");
        container.AddChild(mapContainer);
        MapNavigators.Add(mapContainer);
    }

    public void SetHoverLocation(MapLocation node)
    {
        HoveredMapLocation = node;
        UpdateUI = true;
    }

    public void RemoveHoverLocation(MapLocation node)
    {
        if (HoveredMapLocation != node) return;
        SetHoverLocation(null);
    }

    public void SetSelectedLocation(MapLocation loc)
    {
        try { SelectedMapLocation?.Highlighter.ResetPressed(); }
        catch { }
        SelectedMapLocation = loc;
        UpdateUI = true;
    }

    public void RemoveSelectedLocation(MapLocation loc)
    {
        if (SelectedMapLocation != loc) return;
        SetSelectedLocation(null);
    }

    public void SetItemList(MapLocation location)
    {
        ListContainer.Visible = true;
        List.Clear();
        location.SetList(List);
    }

    public void EditMap()
    {
        var map = GetCurrentMap();
        if (map is null) return;
        EditMapPopup.OpenPopup<EditMapWindow>(
            this, p =>
            {
                p.Setup(map);
                p.EditMapData += (name, image, id) =>
                {
                    if (FindMapByName(name) is null) return;
                    map.EditMapData(name, image, id);
                };
            }
        );
    }

    public void SelectMap(string mapId)
    {
        if (!AutoTab.ButtonPressed) return;
        if (!TryGetMapWithId(mapId, out var map))
        {
            GD.Print($"Auto Tabbing map not found: [{mapId}]");
            return;
        }
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
        if (foundMap is not null) return true;
        foundMap = MapNavigators.FirstOrDefault(map => map.CoreMap.MapName == id, null);
        return foundMap is null;
    }

    public void ResetSelectedNodes() => SetSelectedLocation(null);

    public void EntranceLink(EntranceLocation loc)
    {
        if (LinkingEntrance is null)
        {
            LinkingEntrance = loc;
            loc.LinkingDisplay.Visible = true;
            return;
        }

        SetEntrance(LinkingEntrance.EntranceId, loc.EntranceId);
        if (!Input.IsKeyPressed(Key.Shift)) SetEntrance(loc.EntranceId, LinkingEntrance.EntranceId);
        LinkingEntrance.LinkingDisplay.Visible = false;
        LinkingEntrance = null;
    }

    public void BreakLink(EntranceLocation loc)
    {
        if (LinkingEntrance is not null)
        {
            LinkingEntrance.LinkingDisplay.Visible = false;
            LinkingEntrance = null;
            return;
        }
        SetEntrance(loc.EntranceId, "");
        if (!Input.IsKeyPressed(Key.Shift)) SetEntrance(FindLinkingEntrance(loc.EntranceId, false), "");
    }

    public void NavigateEntrance(EntranceLocation loc)
    {
        var switchTo = "";

        if (FoundEntrances.Contains(loc.EntranceId))
        {
            if (!Input.IsKeyPressed(Key.Shift))
            {
                if (TrueEntranceMap.TryGetValue(loc.EntranceId, out var foundId)
                    && EntranceNodes.TryGetValue(foundId, out var nodes)
                    && nodes.Count > 0) CallDeferred("SelectMap", nodes[0].Map.MapId);
            }
            else
            {
                var backwards = TrueEntranceMap.FirstOrDefault(
                    kv => kv.Value == loc.EntranceId, new KeyValuePair<string, string>(null, null)
                ).Key;
                if (backwards is not null && EntranceNodes.TryGetValue(backwards, out var nodes) && nodes.Count > 0)
                    CallDeferred("SelectMap", nodes[0].Map.MapId);
            }
        }

        if (switchTo is "") switchTo = FindLinkingEntrance(loc.EntranceId, !Input.IsKeyPressed(Key.Shift));
        if (switchTo is "" || !EntranceNodes.TryGetValue(switchTo, out var entranceNodes)
                           || entranceNodes.Count == 0) return;
        CallDeferred("SelectMap", entranceNodes[0].Map.MapId);
    }

    public string FindLinkingEntrance(string entranceId, bool forward)
    {
        var mw = ConnectionController.GetCurrentMultiworld;
        if (mw is null || !mw.MapEntrances.TryGetValue(Client!.PlayerName, out var erMap)) return "";
        return !forward
            ? erMap.FirstOrDefault(kv => kv.Value == entranceId, new KeyValuePair<string, string>(null, null)).Key ?? ""
            : erMap.GetValueOrDefault(entranceId, "");
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

    public void RightClickedNode(EntranceLocation location)
    {
        if (!IsInEditMode) return;
        RightClickSelectedEntranceNode = location;
        CreatePopup(menu => menu.AddItem("Edit Entrance", 7));
    }

    public void RightClickedMap()
    {
        if (!IsInEditMode) return;
        CreatePopup(menu =>
            {
                if (RightClickSelectedNode is not null) ResetRightClickSelectedNode();
                menu.AddItem("Create Node", 3);
                menu.AddItem("Create Entrance", 6);
                if (MoveTargetNode is not null) menu.AddItem("Move Node Here", 4);
                if (CopyTargetNode is not null) menu.AddItem("Paste Node Here", 5);
            }
        );
    }

    public void OptionSelected(long option)
    {
        switch (option)
        {
            case 0:
                RightClickSelectedNode?.Highlighter.Select();
                UpdateUI = true;
                EditNode();
                break;
            case 1: MoveTargetNode = RightClickSelectedNode; break;
            case 2: CopyTargetNode = RightClickSelectedNode; break;
            case 3: CreateNewNodAtMouse(true); break;
            case 4:
                try
                {
                    CreateNewNodAtMouse(
                        false, MoveTargetNode.Size, MoveTargetNode.Group, [.. MoveTargetNode.Locations]
                    );
                    RemoveSelectedLocation(MoveTargetNode);
                    MoveTargetNode.Map.DeleteNode(MoveTargetNode);
                }
                catch { }
                MoveTargetNode = null;
                break;
            case 5:
                try
                {
                    CreateNewNodAtMouse(
                        false, CopyTargetNode.Size, CopyTargetNode.Group, [.. CopyTargetNode.Locations]
                    );
                    RemoveSelectedLocation(CopyTargetNode);
                }
                catch { }
                CopyTargetNode = null;
                break;
            case 6:
                var map = GetCurrentMap();
                AddEntranceNode(map.CreateNewEntranceNode(map.ToLocalPos(PopupPos), new Vector2(256, 32), ""));
                break;
            case 7: EditEntranceNode(RightClickSelectedEntranceNode); break;
        }

        ResetRightClickSelectedNode();
        return;

        void CreateNewNodAtMouse(bool isNew, Vector2? size = null, string group = "", params List<string> locs)
        {
            var map = GetCurrentMap();
            var node = map.CreateNewNode(map.ToLocalPos(PopupPos), size ?? new Vector2(32, 32), group, locs);
            if (OpenConfig.ButtonPressed && isNew) AddNode(node);
        }
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

    public void ResetRightClickSelectedNode()
    {
        RightClickSelectedNode = null;
        RightClickSelectedEntranceNode = null;
    }

    public void UpdateNodes(Hint[] hints) => UpdateNodes();

    public void UpdateNodes()
    {
        foreach (var map in MapNavigators) map.UpdateNodes();
    }

    public void CopyLocations()
    {
        if (SelectedMapLocation is null) return;
        var locationNamesToCopy = List.GetSelectedItems().Select(i => SelectedMapLocation.Locations[i]).ToArray();
        DisplayServer.ClipboardSet(string.Join('\n', locationNamesToCopy));
    }

    public void AddNode(MapLocation loc)
        => EditMapNodePopup.OpenPopup<EditNodeDataPopup>(this, p => p.Setup(this, loc, true));

    public void AddEntranceNode(EntranceLocation loc)
        => EditEntranceNodePopup.OpenPopup<EditEntranceNodePopup>(this, p => p.Setup(this, loc, true));

    public void EditNode()
    {
        if (SelectedMapLocation is null) return;
        EditMapNodePopup.OpenPopup<EditNodeDataPopup>(this, p => p.Setup(this, SelectedMapLocation, false));
    }

    public void EditEntranceNode(EntranceLocation loc)
        => EditEntranceNodePopup.OpenPopup<EditEntranceNodePopup>(this, p => p.Setup(this, loc, false));

    public void AddLocations()
    {
        if (SelectedMapLocation is null) return;
        AddLocationsPopup.OpenPopup<MapAddLocations>(
            this, p =>
            {
                p.Setup(this);
                p.AddLocations += locs =>
                {
                    if (SelectedMapLocation is null) return;
                    locs =
                    [
                        .. locs.Select(l => l.Trim())
                               .Where(l => l is not "" && !SelectedMapLocation.Locations.Contains(l)),
                    ];
                    SelectedMapLocation.Locations.AddRange(locs.DistinctBy(s => s));
                    UpdateUI = true;
                    UpdateNodes();
                };
            }
        );
    }

    public void RemoveSelectedLocations()
    {
        if (SelectedMapLocation is null) return;
        var locationNamesToRemove = List.GetSelectedItems().Where(i => i >= 0)
                                        .Select(i => SelectedMapLocation.DisplayedLocations[i]).ToArray();
        SelectedMapLocation.Locations.RemoveAll(loc => locationNamesToRemove.Contains(loc));
        SetDeferred("UpdateUI", true);
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

    public void ManageTabs() => ManageTabPopup.OpenPopup<TabManager>(
        this,
        p => p.ConfirmAction += (action, name, dest) => CallDeferred("OnPopupOnConfirmAction", (int)action, name, dest)
    );

    public void EditLocationGroup()
        => LocationGroupsManagerPopup.OpenPopup<LocationGroupsManagement>(this, p => p.Setup(this));

    public void EditLocationIconOverrides()
        => LocationIconOverridePopup.OpenPopup<LocationIconsOverrider>(this, p => p.Setup(this));

    public void ManageEntrances()
        => EntranceManagerPopup.OpenPopup<EntranceManagerPopup>(this, p => p.Setup(this));

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
                        associatedStructure.SubTabs.Add(new TabStructure($"__{map.MapName}"));
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
        File.WriteAllText($"{MapPath}/entrance_rando_names.json", JsonConvert.SerializeObject(EntranceMap));
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
        Client?.RemoveDataStorageListeners(UseEntranceRandoMaps ? "Entrance Tracker Map" : "Current Map", Scope.Slot);
        Page?.OnLogicUpdated -= UpdateNodes;

        if (!IsEntranceRando || !AutoTrackEntrances || IsInEditMode || Client is null) return;
        foreach (var (id, _) in EntranceMap) Client!.RemoveDataStorageListeners(id, Scope.Slot);
    }
}