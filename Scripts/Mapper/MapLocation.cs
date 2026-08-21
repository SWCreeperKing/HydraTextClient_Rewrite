using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net.Enums;
using Godot;
using HydraTextClient.Scripts.Utility;

namespace HydraTextClient.Scripts.Mapper;

public partial class MapLocation : TextureRect
{
    [Export] public Texture2D BaseCheckImage;
    public Vector2 Pos = Vector2.Zero;
    public bool QueueUpdate;
    public MapNavigator Map;
    public bool HasCustomImage;
    public MapNode RawNodeData;
    
    public List<string> Locations => RawNodeData.Locations;
    public string LocationGroup => RawNodeData.LocationGroup;

    [Signal] public delegate void OnSelectedEventHandler();

    [Signal] public delegate void OnUnSelectedEventHandler();

    [Signal] public delegate void OnEnteredEventHandler();

    [Signal] public delegate void OnExitedEventHandler();

    [Signal] public delegate void OnUnSelectHighlighterEventHandler();
    
    public void SetData(MapNavigator map) => Map = map;

    public void SetImage(string image)
    {
        QueueUpdate = true;
        Texture = image is not "" ? Map.Loader.ItemImageLoader.GetImage(image) : BaseCheckImage;
        QueueRedraw();
    }

    public void SetPos(Vector2 pos)
    {
        Pos = pos;
        var mapSize = Map.GetMapSize;
        Position = new Vector2(
            Math.Clamp(pos.X, Size.X / 2f, mapSize.X - Size.X / 2f),
            Math.Clamp(pos.Y, Size.Y / 2f, mapSize.Y - Size.Y / 2f)
        );
    }

    public void SetNodeSize(Vector2 size)
    {
        QueueUpdate = true;
        Size = size;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (Map is null) return;
        if (QueueUpdate) LocationUpdate();
    }

    // 0: in logic (hinted) <- 1: in logic <- 2: not logic (hinted) <- 3: not in logic <- 4: nothing, location checked
    private void LocationUpdate()
    {
        QueueUpdate = false;
        var client = Map.Loader.Client;
        var page = Map.Loader.Page;
        var applicableHints = client is null ? []
            : client.Hints
                    .Where(hint => hint.FindingPlayer
                         == client.PlayerSlot && !hint.Found
                                                     && hint.Status is HintStatus.Priority
                     )
                    .Select(hint => hint.LocationName)
                    .ToArray();

        var color = 4;
        foreach (var loc in Locations.ToArray())
        {
            if (client is not null && !client.MissingLocations.Contains(loc)) continue;
            var locColor = 3;
            if (page is not null && page.LocationNamesInLogic.Contains(loc)) locColor = 1;
            if (applicableHints.Contains(loc)) locColor -= 1;
            color = Math.Min(color, locColor);
            if (color is 0) break;
            if (applicableHints.Length == 0 && color is 1) break;
        }

        SelfModulate = color switch
        {
            0 => ColorIdConstants.ColorConstant.InLogicHinted.Color(),
            1 => ColorIdConstants.ColorConstant.InLogic.Color(),
            2 => ColorIdConstants.ColorConstant.NotInLogicHinted.Color(),
            3 => ColorIdConstants.ColorConstant.NotInLogic.Color(),
            4 => ColorIdConstants.ColorConstant.LocationsChecked.Color(), _ => SelfModulate,
        };
    }

    public void EmitUnSelect() => EmitSignalOnUnSelectHighlighter();
    public void EmitSelected() => EmitSignalOnSelected();
    public void EmitUnSelected() => EmitSignalOnUnSelected();
    public void EmitOnEntered() => EmitSignalOnEntered();
    public void EmitOnExited() => EmitSignalOnExited();
}