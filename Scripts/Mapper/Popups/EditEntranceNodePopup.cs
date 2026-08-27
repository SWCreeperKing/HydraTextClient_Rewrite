using System;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Utility.Popups;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class EditEntranceNodePopup : WindowSetter
{
    [Export] private SpinBox Xposition;
    [Export] private SpinBox Yposition;
    [Export] private SpinBox Width;
    [Export] private SpinBox Height;
    [Export] private UISaver Saver;
    private MapLoader Loader;
    private EntranceLocation Node;
    private string[] Groups;
    private bool IsNewNode;

    public void Setup(MapLoader loader, EntranceLocation selectedNode, bool isNew)
    {
        Loader = loader;
        Node = selectedNode;
        Groups = [.. Loader.LocationGroupingMap.Keys.Order()];
        IsNewNode = isNew;

        if (isNew)
        {
            Saver.BuildSavable(Width, "MapTracker/New/MapNode/W", 32);
            Saver.BuildSavable(Height, "MapTracker/New/MapNode/H", 32);
            Node.SetNodeSize(new Vector2((float)Width.Value, (float)Height.Value));
        }
        
        Xposition.ValueChanged += d =>
        {
            Node.Pos = Node.Pos with { X = (int)d };
            Reload();
        };

        Yposition.ValueChanged += d =>
        {
            Node.Pos = Node.Pos with { Y = (int)d };
            Reload();
        };

        Width.ValueChanged += d =>
        {
            Node.SetNodeSize(Node.Size with { X = (int)d });
            Reload();
        };

        Height.ValueChanged += d =>
        {
            Node.SetNodeSize(Node.Size with { Y = (int)d });
            Reload();
        };
        
        Reload();
    }

    public void DeleteNode()
    {
        Node.Map.DeleteNode(Node);
        Close();
    }
    
    public void Reload()
    {
        Xposition.Value = Node.Pos.X;
        Yposition.Value = Node.Pos.Y;
        Width.Value = Node.Size.X;
        Height.Value = Node.Size.Y;
    }
}