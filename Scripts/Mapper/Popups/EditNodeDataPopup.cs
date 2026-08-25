using System;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Utility.Popups;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class EditNodeDataPopup : WindowSetter
{
    [Export] private OptionButton LocationGroup;
    [Export] private SpinBox Xposition;
    [Export] private SpinBox Yposition;
    [Export] private SpinBox Width;
    [Export] private SpinBox Height;
    private MapLoader Loader;
    private MapLocation Node;
    private string[] Groups;

    public void Setup(MapLoader loader, MapLocation selectedNode)
    {
        Loader = loader;
        Node = selectedNode;
        Groups = Loader.LocationGroupingMap.Keys.Order().ToArray();

        LocationGroup.ItemSelected += l => SetGroup(Groups[l]);
        LocationGroup.GetPopup().AddThemeConstantOverride("icon_max_width", 14);
        foreach (var groupName in Groups)
        {
            var group = Loader.LocationGroupingMap[groupName];
            LocationGroup.AddIconItem(Loader.ItemImageLoader[group.MappedIcon], groupName);
        }
        LocationGroup.Selected = Groups.IndexOf(Node.LocationGroup);

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
        Loader.SetSelectedLocation(Node);
        Node.Map.DeleteNode(Node);
        Close();
    }
    
    public void SetGroup(string groupName)
    {
        Node.RawNodeData.LocationGroup = groupName;
        Node.SetImage(Loader.LocationGroupingMap[groupName].MappedIcon);
        Loader.UpdateUI = true;
    }
    
    public void Reload()
    {
        Xposition.Value = Node.Pos.X;
        Yposition.Value = Node.Pos.Y;
        Width.Value = Node.Size.X;
        Height.Value = Node.Size.Y;
    }
}