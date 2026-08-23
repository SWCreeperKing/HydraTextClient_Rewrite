using System;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Utility.Popups;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class LocationIconsOverrider : WindowSetter
{
    [Export] private TabContainer TabContainer;
    [Export] private ItemList LocationView;
    [Export] private OptionButton ClosedImage;
    [Export] private OptionButton OpenedImage;
    [Export] private Label LocationName;
    private MapLoader Loader;
    private string[] Images;
    private string[] Locations;
    private string SelectedLocation;

    public override void _Ready() => TabContainer.SetCurrentTab(0);

    public void Setup(MapLoader loader)
    {
        Loader = loader;
        Loader.ItemImageLoader.ReloadImages();
        Images = Loader.ItemImageLoader.GetImageNames().Order().ToArray();
        Locations = loader.MapNavigators.SelectMany(map => map.Locations.SelectMany(loc => loc.Locations))
                          .DistinctBy(s => s).Order().ToArray();
        SetImages(ClosedImage);
        SetImages(OpenedImage);
        ReloadLocations();
        LocationView.ItemSelected += l =>
        {
            if (l is -1 || l % 3 is not 0) return;
            LocationSelected(Locations[l / 3]);
        };
    }

    public void SetImages(OptionButton button)
    {
        button.Clear();
        button.GetPopup().AddThemeConstantOverride("icon_max_width", 14);
        foreach (var name in Images) button.AddIconItem(Loader.ItemImageLoader[name], name);
    }

    public void LocationSelected(string loc)
    {
        SelectedLocation = loc;
        LocationName.Text = loc;
        ClosedImage.Selected = Loader.LocationClosedIconOverride.TryGetValue(loc, out var closedKey)
            ? Images.IndexOf(closedKey) : -1;
        OpenedImage.Selected = Loader.LocationOpenedIconOverride.TryGetValue(loc, out var openedKey)
            ? Images.IndexOf(openedKey) : -1;
        TabContainer.SetCurrentTab(1);
    }

    public void Save()
    {
        if (ClosedImage.Selected is not -1)
            Loader.LocationClosedIconOverride[SelectedLocation] = Images[ClosedImage.Selected];
        if (OpenedImage.Selected is not -1)
            Loader.LocationOpenedIconOverride[SelectedLocation] = Images[OpenedImage.Selected];

        ReloadLocations();
        TabContainer.SetCurrentTab(0);
    }

    public void ReloadLocations()
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