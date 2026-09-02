using System;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class LocationIconsOverrider : SelectionEditWindow<string>
{
    [Export] private ItemList LocationView;
    [Export] private OptionButton ClosedImage;
    [Export] private OptionButton OpenedImage;
    [Export] private Label LocationName;
    private MapLoader Loader;
    private string[] Images;
    private string[] Locations;
    private string SelectedLocation;

    public void Setup(MapLoader loader)
    {
        Loader = loader;
        Loader.ItemImageLoader.ReloadImages();
        Images = ["", .. Loader.ItemImageLoader.GetImageNames().Order()];
        Locations =
        [
            .. loader.MapNavigators.SelectMany(map => map.Locations.SelectMany(loc => loc.RawNodeData.Locations))
                     .DistinctBy(s => s).Order(),
        ];
        SetImages(ClosedImage);
        SetImages(OpenedImage);
        LocationView.ItemSelected += l =>
        {
            if (l is -1 || l % 3 is not 0) return;
            SwitchToEdit(Locations[l / 3]);
        };
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

    protected override bool DataCheck(string dataIn, out string dataOut) => (dataOut = dataIn).Trim() is not "";

    protected override void EditData(string data)
    {
        SelectedLocation = data;
        LocationName.Text = data;
        ClosedImage.Selected = Loader.LocationClosedIconOverride.TryGetValue(data, out var closedKey)
            ? Images.IndexOf(closedKey) : -1;
        OpenedImage.Selected = Loader.LocationOpenedIconOverride.TryGetValue(data, out var openedKey)
            ? Images.IndexOf(openedKey) : -1;
    }

    protected override void SaveData(string data)
    {
        if (ClosedImage.Selected is not -1)
            Loader.LocationClosedIconOverride[SelectedLocation] = Images[ClosedImage.Selected];
        if (OpenedImage.Selected is not -1)
            Loader.LocationOpenedIconOverride[SelectedLocation] = Images[OpenedImage.Selected];
    }

    protected override void DeleteData(string data) { }

    public override void ReloadData()
    {
        LocationView.Clear();
        foreach (var loc in Locations)
        {
            LocationView.AddItem(loc);
            if (Loader.LocationClosedIconOverride.TryGetValue(loc, out var closedKey)
                && Loader.ItemImageLoader.TryGet(closedKey, out var closedImg))
                LocationView.AddIconItem(closedImg, false);
            else LocationView.AddItem("No Closed Image", selectable: false);
            if (Loader.LocationOpenedIconOverride.TryGetValue(loc, out var openedKey)
                && Loader.ItemImageLoader.TryGet(openedKey, out var openedImg))
                LocationView.AddIconItem(openedImg, false);
            else LocationView.AddItem("No Opened Image", selectable: false);
        }
    }
}