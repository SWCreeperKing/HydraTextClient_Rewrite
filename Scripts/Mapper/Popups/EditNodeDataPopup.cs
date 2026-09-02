using System;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Utility.Popups;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class EditNodeDataPopup : WindowSetter
{
    [Export] private OptionButton LocationGroup;
    [Export] private SpinBox Xposition;
    [Export] private SpinBox Yposition;
    [Export] private SpinBox Width;
    [Export] private SpinBox Height;
    [Export] private UISaver Saver;
    private MapLoader Loader;
    private MapLocation Node;
    private string[] Groups;

    public void Setup(MapLoader loader, MapLocation selectedNode, bool isNew)
    {
        try
        {
            Loader = loader;
            Node = selectedNode;
            Groups = ["", .. Loader.LocationGroupingMap.Keys.Order()];

            if (isNew)
            {
                Saver.BuildSavable(Width, "MapTracker/New/MapNode/W", 32d);
                Saver.BuildSavable(Height, "MapTracker/New/MapNode/H", 32d);
                Node.SetNodeSize(new Vector2((float)Width.Value, (float)Height.Value));
            }

            LocationGroup.ItemSelected += l => SetGroup(Groups[l]);
            LocationGroup.GetPopup().AddThemeConstantOverride("icon_max_width", 14);
            foreach (var groupName in Groups)
            {
                var group = Loader.LocationGroupingMap[groupName];
                if (Loader.ItemImageLoader.TryGet(group.MappedIcon, out var img)) LocationGroup.AddIconItem(img, groupName);
                else LocationGroup.AddItem(groupName);
            }
            LocationGroup.Selected = Groups.IndexOf(Node.Group);

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

        }
        catch (Exception e) { GD.PrintErr(e); }

        Reload();
    }

    public void DeleteNode()
    {
        Loader.SetSelectedLocation(null);
        Loader.RemoveSelectedLocation(Node);
        Loader.RemoveHoverLocation(Node);
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