using System;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Utility.Popups;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class EditEntranceNodePopup : WindowSetter
{
    [Export] private OptionButton EntranceSelect;
    [Export] private SpinBox Xposition;
    [Export] private SpinBox Yposition;
    [Export] private SpinBox Width;
    [Export] private SpinBox Height;
    [Export] private UISaver Saver;
    private MapLoader Loader;
    private EntranceLocation Node;
    private string[] Entrances;

    public void Setup(MapLoader loader, EntranceLocation selectedNode, bool isNew)
    {
        try
        {
            Loader = loader;
            Node = selectedNode;
            Entrances = [.. Loader.EntranceMap.Keys.Order()];

            if (isNew)
            {
                Saver.BuildSavable(Width, "MapTracker/New/EntranceNode/W", 256d);
                Saver.BuildSavable(Height, "MapTracker/New/EntranceNode/H", 32d);
                Node.SetNodeSize(new Vector2((float)Width.Value, (float)Height.Value));

                foreach (var entrance in Entrances) EntranceSelect.AddItem(Loader.EntranceMap[entrance]);
                EntranceSelect.ItemSelected += l =>
                {
                    Node.RawNodeData.Entrance = Entrances[l];
                    Node.SetText(
                        Loader.IsInEditMode && Entrances[l].Trim() is not "" ? Loader.EntranceMap[Entrances[l]] : null
                    );
                };
            }
            EntranceSelect.Visible = isNew;

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
        catch (Exception e) { GD.PrintErr(e); }
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