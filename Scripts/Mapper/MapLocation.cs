using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net.Enums;
using CreepyUtil.Archipelago.ApClient;
using Godot;
using HydraTextClient.Scripts.Utility;

namespace HydraTextClient.Scripts.Mapper;

public partial class MapLocation : TextureRect
{
    [Export] public Texture2D BaseCheckImage;
    [Export] public Highlighter Highlighter;

    public Vector2 Pos
    {
        get => Position;
        set
        {
            var mapSize = Map.GetMapSize;
            Position = new Vector2(
                Math.Clamp(value.X, 0, mapSize.X - Size.X),
                Math.Clamp(value.Y, 0, mapSize.Y - Size.Y)
            );
            RawNodeData.X = Position.X;
            RawNodeData.Y = Position.Y;
        }
    }

    public Color NodeColor { get => SelfModulate; set => SelfModulate = Colors.White.Lerp(value, .85f); }

    public bool QueueUpdate;
    public bool QueueRender;
    public MapNavigator Map;
    public MapNode RawNodeData;

    // public List<string> Locations => RawNodeData.Locations;
    public string Group => RawNodeData.LocationGroup;
    public MapLoader Loader => Map.Loader;
    public ApClient Client => Loader.Client;
    public List<string> DisplayedLocations = [];
    public string[] OrderedLocations;
    private Dictionary<string, int> LocationValueDict = [];
    private bool NodeDead = false;

    [Signal] public delegate void OnSelectedEventHandler();

    [Signal] public delegate void OnUnSelectedEventHandler();

    [Signal] public delegate void OnEnteredEventHandler();

    [Signal] public delegate void OnExitedEventHandler();

    [Signal] public delegate void OnUnSelectHighlighterEventHandler();

    [Signal] public delegate void OnRightClickEventHandler();

    public void SetData(MapNavigator map)
    {
        Map = map;
        SetOrderedLocations();
    }

    public void AddLocations(params string[] locs)
    {
        NodeDead = false;
        RawNodeData.Locations.AddRange(locs);
        SetOrderedLocations();
    }

    public void RemoveLocations(params string[] locs)
    {
        RawNodeData.Locations.RemoveAll(l => locs.Contains(l));
        SetOrderedLocations();
    }

    public void SetOrderedLocations()
    {
        IEnumerable<string> locs = RawNodeData.Locations.DistinctBy(s => s).OrderBy(s => s);
        if (Client is not null && !Loader.IsInEditMode) locs = locs.Where(l => Client.Locations.Any(kv => kv.Key == l));
        OrderedLocations = [.. locs];
    }

    public void SetImage(string image)
    {
        var img = BaseCheckImage;
        if (image is not "" && Loader.ItemImageLoader.TryGet(image, out var rawImg)) img = rawImg;
        Texture = img;
    }

    public void SetNodeSize(Vector2 size)
    {
        QueueUpdate = true;
        Size = size;
        RawNodeData.W = size.X;
        RawNodeData.H = size.Y;
    }

    public override void _Process(double delta)
    {
        if (QueueRender)
        {
            QueueRender = false;
            UpdateVisuals();
        }

        if (Map is null) return;
        if (!QueueUpdate) return;
        QueueUpdate = false;
        UpdateVisuals();
        LocationUpdate();
    }

    public void UpdateVisuals()
    {
        SetImage("");
        if (Group is "" || !Loader.LocationGroupingMap.TryGetValue(Group, out var group)) return;
        if (group.MappedIcon is "") SetImage("");
        SetImage(group.MappedIcon);

        if (Loader.IsInEditMode) return;
        bool? visible = null;
        string? image = null;

        foreach (var rule in group.LocationRules.Where(r => r.DataKey is not ("" or null)))
        {
            if (visible is null && rule.Action is "") visible = false;
            if (image is not null && rule.Action is not "") continue;
            if (!Loader.DataDictionary.TryGetValue(rule.GetKeyHash(), out var obj) || obj is null) continue;
            if (!rule.CompareDataValue(obj)) continue;
            if (rule.Action is "") visible = true;
            else image = rule.Action;
        }

        if (visible is not null) Visible = visible.Value;
        if (image is not null) SetImage(image);
    }

    // 0: in logic (hinted) <- 1: in logic <- 2: not logic (hinted) <- 3: not in logic <- 4: nothing, location checked <- 5 doesn't exist
    private void LocationUpdate()
    {
        if (!Loader.IsInEditMode && NodeDead || !Visible) return;
        try
        {
            var page = Loader.Page;
            var applicableHints = Client is null ? []
                : Client.Hints
                        .Where(hint => hint.FindingPlayer
                             == Client.PlayerSlot && !hint.Found
                                                  && hint.Status is HintStatus.Priority
                         )
                        .Select(hint => hint.LocationName)
                        .ToArray();

            LocationValueDict = OrderedLocations.ToDictionary(
                l => l, l =>
                {
                    if (Client is null && Loader.IsInEditMode) return 4;
                    if (Client is null || Client.Locations.All(kv => kv.Key != l)) return 5;
                    if (!Client.IsMissingLocation(l)) return 4;
                    var locColor = 3;
                    if (page is not null && page.LocationNamesInLogic.Contains(l)) locColor = 1;
                    if (applicableHints.Contains(l)) locColor -= 1;
                    return locColor;
                }
            );
        }
        catch (Exception e) { LocationValueDict = []; }

        var min = Math.Clamp(LocationValueDict.Count == 0 ? 4 : LocationValueDict.MinBy(kv => kv.Value).Value, 0, 4);
        NodeColor = min switch
        {
            0 => ColorIdConstants.ColorConstant.InLogicHinted.Color(),
            1 => ColorIdConstants.ColorConstant.InLogic.Color(),
            2 => ColorIdConstants.ColorConstant.NotInLogicHinted.Color(),
            3 => ColorIdConstants.ColorConstant.NotInLogic.Color(),
            4 => ColorIdConstants.ColorConstant.LocationsChecked.Color(),
        };
        NodeDead = min is 4;
    }

    public void SetList(ItemList list)
    {
        DisplayedLocations.Clear();
        var group = Group is "" || !Loader.LocationGroupingMap.TryGetValue(Group, out var tGroup) ? null : tGroup;
        foreach (var (loc, status) in LocationValueDict.OrderBy(kv => kv.Value))
        {
            var i = list.AddItem(loc);
            DisplayedLocations.Add(loc);
            if (status is 5 && !Loader.IsInEditMode || Client is not null && Client.Locations.All(kv => kv.Key != loc))
            {
                list.SetItemCustomBgColor(i, Colors.DarkRed);
                continue;
            }

            if (group is not null && status < 5)
            {
                var icon = status < 4 ? group!.AvailableIcon : group!.CollectedIcon;
                if (icon is "" || !Loader.ItemImageLoader.TryGet(icon, out var img))
                {
                    if (icon is not "") GD.PrintErr($"Missing icon for [{icon}]");
                }
                else list.SetItemIcon(i, img);
            }

            switch (status)
            {
                case < 4: SetListIcon(Loader.LocationClosedIconOverride, loc, i); break;
                case 4: SetListIcon(Loader.LocationOpenedIconOverride, loc, i); break;
            }

            var colorStatus = status;
            if (Loader.IsInEditMode) { colorStatus = status is 5 ? 5 : 3; }
            switch (colorStatus)
            {
                case 0: list.SetItemCustomFgColor(i, ColorIdConstants.ColorConstant.InLogicHinted.Color()); break;
                case 1: list.SetItemCustomFgColor(i, ColorIdConstants.ColorConstant.InLogic.Color()); break;
                case 2: list.SetItemCustomFgColor(i, ColorIdConstants.ColorConstant.NotInLogicHinted.Color()); break;
                case 3: list.SetItemCustomFgColor(i, ColorIdConstants.ColorConstant.NotInLogic.Color()); break;
                case 4: list.SetItemCustomFgColor(i, ColorIdConstants.ColorConstant.LocationsChecked.Color()); break;
            }
        }
        QueueUpdate = true;
        return;

        void SetListIcon(Dictionary<string, string> iconOverride, string loc, int index)
        {
            if (!iconOverride.TryGetValue(loc, out var value)) return;
            if (!Loader.ItemImageLoader.TryGet(value, out var closedImg)) return;
            list.SetItemIcon(index, closedImg);
        }
    }

    public void EmitUnSelect() => EmitSignalOnUnSelectHighlighter();
    public void EmitSelected() => EmitSignalOnSelected();
    public void EmitUnSelected() => EmitSignalOnUnSelected();
    public void EmitOnEntered() => EmitSignalOnEntered();
    public void EmitOnExited() => EmitSignalOnExited();
    public void EmitOnRightClick() => EmitSignalOnRightClick();
}