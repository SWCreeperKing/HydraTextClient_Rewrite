using Godot;
using HydraTextClient.Scripts.Utility;

namespace HydraTextClient.Scripts.Mapper;

public partial class EntranceLocation : PanelContainer
{
    [Export] public Highlighter Highlighter;
    [Export] public OptionButton EntranceSelection;
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

    public override void _Ready()
    {
        EntranceSelection.Visible = false;
        Label.Visible = false;
    }

    public void SetNodeFontSize(int size)
    {
        EntranceSelection.SetFontSize(size);
        Label.SetFontSizeOverride(size);
        QueueRedraw();
    }
    
    public void EmitOnRightClick() => EmitSignalOnRightClick();
}