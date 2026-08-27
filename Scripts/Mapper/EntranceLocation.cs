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

    public void Setup()
    {
        
    }
    
    public void EmitOnRightClick() => EmitSignalOnRightClick();
}