using System;
using Godot;
using HydraTextClient.Scripts.Utility;

namespace HydraTextClient.Scripts.Mapper;

public partial class EntranceLocation : PanelContainer
{
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

    [Signal] public delegate void OnRightClickEventHandler();

    [Signal] public delegate void OnLeftClickEventHandler();

    [Signal] public delegate void OnMiddleClickEventHandler();

    public override void _Process(double delta)
    {
        if (Loader.IsInEditMode || !UpdateEntrance) return;
        UpdateEntrance = false;
        if (Loader.FoundEntrances.Contains(EntranceId)
            && Loader.TrueEntranceMap.TryGetValue(EntranceId, out var foundId)
            && Loader.EntranceNodes.TryGetValue(foundId, out var nodes) && nodes.Count > 0)
        {
            SetText(nodes[0].Map.CoreMap.MapName);
            return;
        }
    }

    public void SetData(MapNavigator map, string id)
    {
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