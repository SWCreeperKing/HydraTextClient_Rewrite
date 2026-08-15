using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net.Enums;
using Godot;
using HydraTextClient.Scripts.Utility;

namespace HydraTextClient.Scripts.Mapper;

public partial class MapLocation : TextureRect
{
    public static Dictionary<string, Texture2D> TextureCache = [];

    [Export] public Texture2D BaseCheckImage;
    public Vector2? SetSize;
    public List<string> Locations;
    public bool QueueUpdate;
    public MapLoader Loader;
    public int MapId;
    public int NodeId;
    public bool HasCustomImage;
    public string LocationGroup;

    [Signal] public delegate void OnSelectedEventHandler();

    [Signal] public delegate void OnUnSelectedEventHandler();

    [Signal] public delegate void OnEnteredEventHandler();

    [Signal] public delegate void OnExitedEventHandler();

    [Signal] public delegate void OnUnSelectHighlighterEventHandler();

    public void SetImage(int mapId, int nodeId, string path, string image, Vector2 size, MapLoader loader)
    {
        MapId = mapId;
        NodeId = nodeId;
        Loader = loader;
        SetSize = size;
        QueueUpdate = true;
        if (image is not "") return;
        Texture = BaseCheckImage;
        QueueRedraw();
    }

    public override void _Process(double delta)
    {
        if (Loader is null) return;
        if (QueueUpdate) LocationUpdate();
        if (SetSize is null) return;
        Size = SetSize!.Value;
        SetSize = null;
    }

    // 0: in logic (hinted) <- 1: in logic <- 2: not logic (hinted) <- 3: not in logic <- 4: nothing, location checked
    private void LocationUpdate()
    {
        QueueUpdate = false;
        var applicableHints = Loader.Client is null ? []
            : Loader.Client.Hints
                    .Where(hint => hint.FindingPlayer
                         == Loader.Client.PlayerSlot && !hint.Found
                                                     && hint.Status is HintStatus.Priority
                     )
                    .Select(hint => hint.LocationName)
                    .ToArray();

        var color = 4;
        foreach (var loc in Locations.ToArray())
        {
            if (Loader.Client is not null && !Loader.Client.MissingLocations.Contains(loc)) continue;
            var locColor = 3;
            if (Loader.Page is not null && Loader.Page.LocationNamesInLogic.Contains(loc)) locColor = 1;
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