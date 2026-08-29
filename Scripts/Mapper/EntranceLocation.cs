using System;
using System.Collections.Generic;
using System.Linq;
using CreepyUtil.Archipelago.ApClient;
using Godot;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Utility;

namespace HydraTextClient.Scripts.Mapper;

public partial class EntranceLocation : PanelContainer
{
    [Export] public ColorRect LinkingDisplay;
    [Export] public Highlighter Highlighter;
    [Export] public RichTextLabel Label;

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

    public MapNavigator Map;
    public EntranceNode RawNodeData;
    public string EntranceId;
    public bool UpdateEntrance = true;
    public MapLoader Loader => Map.Loader;
    public ApClient Client => Loader.Client;

    [Signal] public delegate void OnRightClickEventHandler();

    [Signal] public delegate void OnLeftClickEventHandler();

    [Signal] public delegate void OnMiddleClickEventHandler();

    public override void _Process(double delta)
    {
        if (Loader.IsInEditMode || !UpdateEntrance) return;
        UpdateEntrance = false;
        SetText("?");
        if (Loader.FoundEntrances.Contains(EntranceId)
            && Loader.TrueEntranceMap.TryGetValue(EntranceId, out var foundId))
        {
            if (Loader.EntranceNodes.TryGetValue(foundId, out var nodes) && nodes.Count > 0) SetEntranceName(nodes[0]);
            else SetText("Unknown Node Destination");
            return;
        }

        var mw = ConnectionController.GetCurrentMultiworld;
        if (mw is null) return;
        if (!mw.MapEntrances.TryGetValue(Client.PlayerName, out var value)) return;
        if (!value.TryGetValue(EntranceId, out var destId)) return;
        if (!Loader.EntranceNodes.TryGetValue(destId, out var entranceNodes)) return;
        if (entranceNodes.Count == 0)
        {
            SetText("Unknown Node Destination");
            return;
        }
        SetEntranceName(entranceNodes[0]);

        return;

        void SetEntranceName(EntranceLocation node) => SetText(
            Loader.EntranceNicknames.GetValueOrDefault(node.EntranceId, node.Map.CoreMap.MapName)
        );
    }

    public void SetData(MapNavigator map, string id)
    {
        LinkingDisplay.Visible = false;
        Map = map;
        EntranceId = id;
    }

    public void SetNodeSize(Vector2 size)
    {
        Size = size;
        RawNodeData.W = size.X;
        RawNodeData.H = size.Y;
        QueueRedraw();
    }

    public void SetText(string? text = null)
    {
        Label.Text = text ?? "?";
        if (Label.Text.Trim() is "") Label.Text = "?";
    }

    public void EmitOnRightClick() => EmitSignalOnRightClick();
    public void EmitOnLeftClick() => EmitSignalOnLeftClick();
    public void EmitOnMiddleClick() => EmitSignalOnMiddleClick();
}