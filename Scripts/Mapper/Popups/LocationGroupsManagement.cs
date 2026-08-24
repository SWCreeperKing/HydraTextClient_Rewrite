using System;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class LocationGroupsManagement : SelectionEditWindow<LocationGroup>
{
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
    private string[] Images;

    public void Setup(MapLoader loader)
    {
        CompareContainer.SetCurrentTab(0);
        Loader = loader;
        Loader.ItemImageLoader.ReloadImages();
        Images = Loader.ItemImageLoader.GetImageNames().Order().ToArray();
        GroupView.ItemSelected += l => SwitchToEdit(Groups[l]);
        SetImages(NodeImage);
        SetImages(ClosedImage);
        SetImages(OpenedImage);
        ReloadData();
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
        ReloadData();
    }

    protected override bool DataCheck(LocationGroup dataIn, out LocationGroup dataOut)
        => (dataOut = dataIn) is not null;

    protected override void EditData(LocationGroup data)
    {
        NodeImage.Selected = Images.IndexOf(data.MappedIcon);
        ClosedImage.Selected = Images.IndexOf(data.AvailableIcon);
        OpenedImage.Selected = Images.IndexOf(data.CollectedIcon);
        SlotDataVariable.Text = data.SlotDataKey;

        var varType = (int)data.StoreType;
        SlotDataType.Selected = varType;
        CompareContainer.SetCurrentTab(varType);

        IsVariableTrue.ButtonPressed = data.BoolCompare; // bool
        Operator.Selected = data.CompareType.ToSelected(); // number
        ValueCompare.Value = data.NumberCompare;
        MatchAny.ButtonPressed = data.MatchAny; // string
        StringCompareData.Text = string.Join('\n', data.DataCompare ?? []);
    }

    protected override void SaveData(LocationGroup data)
    {
        data.MappedIcon = NodeImage.Selected == -1 ? "" : Images[NodeImage.Selected];
        data.AvailableIcon = ClosedImage.Selected == -1 ? "" : Images[ClosedImage.Selected];
        data.CollectedIcon = OpenedImage.Selected == -1 ? "" : Images[OpenedImage.Selected];
        data.SlotDataKey = SlotDataVariable.Text;
        data.StoreType = (LocationGroup.DataStorageType)SlotDataType.Selected;

        data.BoolCompare = IsVariableTrue.ButtonPressed; // bool
        data.CompareType = Operator.Selected switch
        {
            0 => LocationGroup.NumberCompareType.NotEqualTo, 1 => LocationGroup.NumberCompareType.EqualTo,
            2 => LocationGroup.NumberCompareType.GreaterThan,
            3 => LocationGroup.NumberCompareType.GreaterThan | LocationGroup.NumberCompareType.EqualTo,
            4 => LocationGroup.NumberCompareType.LessThan,
            5 => LocationGroup.NumberCompareType.LessThan | LocationGroup.NumberCompareType.EqualTo,
        }; // number
        data.NumberCompare = ValueCompare.Value;
        data.MatchAny = MatchAny.ButtonPressed; // string
        data.DataCompare = StringCompareData.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
    }

    protected override void DeleteData(LocationGroup data)
    {
        Loader.LocationGroupingMap.Remove(data.GroupName);
        Loader.LocationGroups.Remove(data);
    }

    public override void ReloadData()
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