using System;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.Popups;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class LocationGroupsManagement : WindowSetter
{
    [Export] private TabContainer TabContainer;

    [Export, ExportGroup("Add-View Groups")]
    private LineEdit AddGroupName;

    [Export] private ItemList GroupView;
    [Export, ExportGroup("Edit Groups")] private OptionButton NodeImage;
    [Export] private OptionButton ClosedImage;
    [Export] private OptionButton OpenedImage;
    [Export] private LineEdit SlotDataVariable;
    [Export] private OptionButton SlotDataType;
    [Export] private TabContainer CompareContainer;

    [Export, ExportGroup("Edit Groups/Bool SDV")]
    private CheckBox IsVariableTrue;

    [Export, ExportGroup("Edit Groups/Number SDV")]
    private OptionButton Operator;

    [Export] private SpinBox ValueCompare;

    [Export, ExportGroup("Edit Groups/String SDV")]
    private CheckBox MatchAny;

    [Export] private TextEdit StringCompareData;

    private LocationGroup[] Groups = [];
    private MapLoader Loader;
    private LocationGroup? SelectedGroup;
    private string[] Images;

    public void Setup(MapLoader loader)
    {
        TabContainer.SetCurrentTab(0);
        CompareContainer.SetCurrentTab(0);
        Loader = loader;
        Loader.ItemImageLoader.ReloadImages();
        Images = Loader.ItemImageLoader.GetImageNames().Order().ToArray();
        GroupView.ItemSelected += l => EditGroup(Groups[l]);
        SetImages(NodeImage);
        SetImages(ClosedImage);
        SetImages(OpenedImage);
        ReloadGroups();
    }

    public void SetImages(OptionButton button)
    {
        button.Clear();
        button.GetPopup().AddThemeConstantOverride("icon_max_width", 14);
        foreach (var name in Images) button.AddIconItem(Loader.ItemImageLoader[name], name);
    }

    public void AddGroup()
    {
        if (AddGroupName.Text.Trim() is "") return;
        if (Loader.LocationGroupingMap.ContainsKey(AddGroupName.Text)) return;
        var group = new LocationGroup(AddGroupName.Text, "");
        Loader.LocationGroupingMap[group.GroupName] = group;
        Loader.LocationGroups.Add(group);
        AddGroupName.Clear();
        ReloadGroups();
    }

    public void EditGroup(LocationGroup group)
    {
        SelectedGroup = group;
        NodeImage.Selected = Images.IndexOf(group.MappedIcon);
        ClosedImage.Selected = Images.IndexOf(group.AvailableIcon);
        OpenedImage.Selected = Images.IndexOf(group.CollectedIcon);
        SlotDataVariable.Text = group.SlotDataKey;

        var varType = (int)group.StoreType;
        SlotDataType.Selected = varType;
        CompareContainer.SetCurrentTab(varType);

        IsVariableTrue.ButtonPressed = group.BoolCompare; // bool
        Operator.Selected = group.CompareType.ToSelected(); // number
        ValueCompare.Value = group.NumberCompare;
        MatchAny.ButtonPressed = group.MatchAny; // string
        StringCompareData.Text = string.Join('\n', group.DataCompare ?? []);

        TabContainer.SetCurrentTab(1);
    }

    public void SaveGroup()
    {
        if (SelectedGroup is null)
        {
            TabContainer.SetCurrentTab(0);
            ReloadGroups();
        }

        SelectedGroup!.MappedIcon = NodeImage.Selected == -1 ? "" : Images[NodeImage.Selected];
        SelectedGroup!.AvailableIcon = ClosedImage.Selected == -1 ? "" : Images[ClosedImage.Selected];
        SelectedGroup!.CollectedIcon = OpenedImage.Selected == -1 ? "" : Images[OpenedImage.Selected];
        SelectedGroup!.SlotDataKey = SlotDataVariable.Text;
        SelectedGroup!.StoreType = (LocationGroup.DataStorageType)SlotDataType.Selected;

        SelectedGroup!.BoolCompare = IsVariableTrue.ButtonPressed; // bool
        SelectedGroup!.CompareType = Operator.Selected switch
        {
            0 => LocationGroup.NumberCompareType.NotEqualTo, 1 => LocationGroup.NumberCompareType.EqualTo,
            2 => LocationGroup.NumberCompareType.GreaterThan,
            3 => LocationGroup.NumberCompareType.GreaterThan | LocationGroup.NumberCompareType.EqualTo,
            4 => LocationGroup.NumberCompareType.LessThan,
            5 => LocationGroup.NumberCompareType.LessThan | LocationGroup.NumberCompareType.EqualTo,
        }; // number
        SelectedGroup!.NumberCompare = ValueCompare.Value;
        SelectedGroup!.MatchAny = MatchAny.ButtonPressed; // string
        SelectedGroup!.DataCompare = StringCompareData.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);

        TabContainer.SetCurrentTab(0);
        ReloadGroups();
    }

    public void DeleteGroup()
    {
        Loader.LocationGroupingMap.Remove(SelectedGroup!.GroupName);
        Loader.LocationGroups.Remove(SelectedGroup);
        TabContainer.SetCurrentTab(0);
        ReloadGroups();
    }

    public void ReloadGroups()
    {
        GroupView.Clear();
        Groups = Loader.LocationGroups.OrderBy(g => g.GroupName).ToArray();
        foreach (var group in Groups)
        {
            if (!Loader.ItemImageLoader.TryGet(group.MappedIcon, out var img)) img = null;
            GroupView.AddItem(group.GroupName, img);
        }
    }
}