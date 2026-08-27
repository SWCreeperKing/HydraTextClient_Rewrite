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

    [Signal] public delegate void OnRightClickEventHandler();

    [Signal] public delegate void OnLeftClickEventHandler();

    [Signal] public delegate void OnMiddleClickEventHandler();

    public void SetData(MapNavigator map) => Map = map;
    
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