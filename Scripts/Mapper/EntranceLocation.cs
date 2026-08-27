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
            Position = value;
            RawNodeData.X = Position.X;
            RawNodeData.Y = Position.Y;
        }
    }

    public MapNavigator Map;
    public EntranceNode RawNodeData;

    [Signal] public delegate void OnRightClickEventHandler();

    [Signal] public delegate void OnLeftClickEventHandler();

    [Signal] public delegate void OnMiddleClickEventHandler();

    public void SetNodeSize(Vector2 size)
    {
        Size = size;
        RawNodeData.W = size.X;
        RawNodeData.H = size.Y;
        QueueRedraw();
    }

    public void SetText(string? text = null) => Label.Text = text ?? "?";

    public void EmitOnRightClick() => EmitSignalOnRightClick();
    public void EmitOnLeftClick() => EmitSignalOnLeftClick();
    public void EmitOnMiddleClick() => EmitSignalOnMiddleClick();
}