using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class LocationGroupsManagement : SelectionEditWindow<LocationGrouping>
{
    [Export, ExportGroup("Add-View Groups")]
    private LineEdit AddGroupName;

    [Export] private ItemList GroupView;
    [Export, ExportGroup("Edit Groups")] private OptionButton NodeImage;
    [Export] private OptionButton ClosedImage;
    [Export] private OptionButton OpenedImage;
    [Export] private PrioritizedList Listings;
    [Export] private PackedScene LocationMakeRuleScene;

    private LocationGrouping[] Groups = [];
    private MapLoader Loader;
    private string[] Images;

    public void Setup(MapLoader loader)
    {
        Loader = loader;
        Loader.ItemImageLoader.ReloadImages();
        Images = ["", .. Loader.ItemImageLoader.GetImageNames().Order()];
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
        foreach (var name in Images)
        {
            if (Loader.ItemImageLoader.TryGet(name, out var img)) button.AddIconItem(img, name);
            else button.AddItem(name);
        }
    }

    public void AddGroup()
    {
        if (AddGroupName.Text.Trim() is "") return;
        if (Loader.LocationGroupingMap.ContainsKey(AddGroupName.Text)) return;
        var group = new LocationGrouping(AddGroupName.Text, "");
        Loader.LocationGroupingMap[group.GroupName] = group;
        Loader.LocationGroups.Add(group);
        AddGroupName.Clear();
        ReloadData();
    }

    protected override bool DataCheck(LocationGrouping dataIn, out LocationGrouping dataOut)
        => (dataOut = dataIn) is not null;

    protected override void EditData(LocationGrouping data)
    {
        Listings.CallClear();
        NodeImage.Selected = Images.IndexOf(data.MappedIcon);
        ClosedImage.Selected = Images.IndexOf(data.AvailableIcon);
        OpenedImage.Selected = Images.IndexOf(data.CollectedIcon);
        foreach (var rule in data.LocationRules) AddRule(rule);
    }

    public void AddRule() => AddRule(new LocationRule());

    private void AddRule(LocationRule rule)
    {
        var item = LocationMakeRuleScene.Instantiate<MakeLocationRule>();
        item.Setup(Images, Loader.ItemImageLoader);
        item.SetData(rule);
        Listings.AddItems(item);
    }

    protected override void SaveData(LocationGrouping data)
    {
        data.MappedIcon = NodeImage.Selected == -1 ? "" : Images[NodeImage.Selected];
        data.AvailableIcon = ClosedImage.Selected == -1 ? "" : Images[ClosedImage.Selected];
        data.CollectedIcon = OpenedImage.Selected == -1 ? "" : Images[OpenedImage.Selected];
        data.LocationRules = [.. Listings.GetItems<MakeLocationRule>().Select(rule => rule.GetData())];
    }

    protected override void DeleteData(LocationGrouping data)
    {
        Loader.LocationGroupingMap.Remove(data.GroupName);
        Loader.LocationGroups.Remove(data);
    }

    public override void ReloadData()
    {
        GroupView.Clear();
        Groups = [.. Loader.LocationGroups.OrderBy(g => g.GroupName)];
        foreach (var group in Groups)
        {
            if (!Loader.ItemImageLoader.TryGet(group.MappedIcon, out var img)) img = null;
            GroupView.AddItem(group.GroupName, img);
        }
    }
}