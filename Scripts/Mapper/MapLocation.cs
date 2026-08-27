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
    [Export] private ColorRect ColorTarget;

    public Vector2 Pos
    {
        get => Position;
        set
        {
            var mapSize = Map.GetMapSize;
            Position = new Vector2(
                Math.Clamp(value.X, Size.X / 2f, mapSize.X - Size.X / 2f),
                Math.Clamp(value.Y, Size.Y / 2f, mapSize.Y - Size.Y / 2f)
            );
            RawNodeData.X = Position.X;
            RawNodeData.Y = Position.Y;
        }
    }

    public Color NodeColor { get => SelfModulate; set => SelfModulate = Colors.White.Lerp(value, .75f); }

    public bool HasCustomImage;
    public bool QueueUpdate;
    public MapNavigator Map;
    public MapNode RawNodeData;

    public List<string> Locations => RawNodeData.Locations;
    public string Group => RawNodeData.LocationGroup;
    public MapLoader Loader => Map.Loader;
    public ApClient Client => Loader.Client;
    private Dictionary<string, int> LocationValueDict;

    [Signal] public delegate void OnSelectedEventHandler();

    [Signal] public delegate void OnUnSelectedEventHandler();

    [Signal] public delegate void OnEnteredEventHandler();

    [Signal] public delegate void OnExitedEventHandler();

    [Signal] public delegate void OnUnSelectHighlighterEventHandler();

    [Signal] public delegate void OnRightClickEventHandler();

    public void SetData(MapNavigator map) => Map = map;

    public void SetImage(string image)
    {
        QueueUpdate = true;
        Texture = image is not "" ? Loader.ItemImageLoader[image] : BaseCheckImage;
        QueueRedraw();
    }

    public void SetNodeSize(Vector2 size)
    {
        QueueUpdate = true;
        Size = size;
        RawNodeData.W = size.X;
        RawNodeData.H = size.Y;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (Map is null) return;
        if (QueueUpdate) LocationUpdate();
    }

    // 0: in logic (hinted) <- 1: in logic <- 2: not logic (hinted) <- 3: not in logic <- 4: nothing, location checked <- 5 doesn't exist
    private void LocationUpdate()
    {
        QueueUpdate = false;
        var client = Loader.Client;
        var page = Loader.Page;
        var applicableHints = client is null ? []
            : client.Hints
                    .Where(hint => hint.FindingPlayer
                         == client.PlayerSlot && !hint.Found
                                              && hint.Status is HintStatus.Priority
                     )
                    .Select(hint => hint.LocationName)
                    .ToArray();

        LocationValueDict = Locations.ToDictionary(
            l => l, l =>
            {
                if (client is null && Loader.IsInEditMode) return 4;
                if (client is null || !Loader.IsInEditMode && client.Locations.All(kv => kv.Key != l)) return 4;
                if (!client.MissingLocations.Contains(l)) return 4;
                var locColor = 3;
                if (page is not null && page.LocationNamesInLogic.Contains(l)) locColor = 1;
                if (applicableHints.Contains(l)) locColor -= 1;
                return locColor;
            }
        );

        var min = LocationValueDict.Count == 0 ? 4 : LocationValueDict.MinBy(kv => kv.Value).Value;
        NodeColor = min switch
        {
            0 => ColorIdConstants.ColorConstant.InLogicHinted.Color(),
            1 => ColorIdConstants.ColorConstant.InLogic.Color(),
            2 => ColorIdConstants.ColorConstant.NotInLogicHinted.Color(),
            3 => ColorIdConstants.ColorConstant.NotInLogic.Color(),
            4 => ColorIdConstants.ColorConstant.LocationsChecked.Color(), _ => NodeColor,
        };
    }

    public void SetList(ItemList list)
    {
        var group = Group is "" || !Loader.LocationGroupingMap.TryGetValue(Group, out var tGroup) ? null : tGroup;
        foreach (var (loc, status) in LocationValueDict.OrderBy(kv => kv.Value))
        {
            if (status is 5 && !Loader.IsInEditMode) continue;
            if (Client is not null && Client.Locations.All(kv => kv.Key != loc)) continue;
            var i = list.AddItem(loc);

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

            if (Loader.IsInEditMode) continue;
            switch (status)
            {
                case 0: list.SetItemCustomFgColor(i, ColorIdConstants.ColorConstant.InLogicHinted.Color()); break;
                case 1: list.SetItemCustomFgColor(i, ColorIdConstants.ColorConstant.InLogic.Color()); break;
                case 2: list.SetItemCustomFgColor(i, ColorIdConstants.ColorConstant.NotInLogicHinted.Color()); break;
                case 3: list.SetItemCustomFgColor(i, ColorIdConstants.ColorConstant.NotInLogic.Color()); break;
                case 4: list.SetItemCustomFgColor(i, ColorIdConstants.ColorConstant.LocationsChecked.Color()); break;
            }
        }
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