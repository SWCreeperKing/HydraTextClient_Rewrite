using System;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Utility.Popups;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class TabIndicator : WindowSetter
{
    [Export] private OptionButton InLogicHinted;
    [Export] private OptionButton InLogic;
    [Export] private OptionButton NotInLogicHinted;
    [Export] private OptionButton NotInLogic;
    [Export] private OptionButton AllChecks;

    private MapLoader Loader;
    private string[] Images;

    public void Setup(MapLoader loader)
    {
        Loader = loader;
        Loader.ItemImageLoader.ReloadImages();
        Images = ["", .. Loader.ItemImageLoader.GetImageNames().Order()];
        SetImages(InLogicHinted, Loader.TabIndicatorData.InLogicHinted);
        SetImages(InLogic, Loader.TabIndicatorData.InLogic);
        SetImages(NotInLogicHinted, Loader.TabIndicatorData.NotInLogicHinted);
        SetImages(NotInLogic, Loader.TabIndicatorData.NotInLogic);
        SetImages(AllChecks, Loader.TabIndicatorData.AllChecks);
    }

    public void SetImages(OptionButton button, string currentSelected)
    {
        button.Clear();
        button.GetPopup().AddThemeConstantOverride("icon_max_width", 14);
        foreach (var name in Images)
        {
            if (Loader.ItemImageLoader.TryGet(name, out var img)) button.AddIconItem(img, name);
            else button.AddItem(name);
        }
        button.Selected = Math.Max(Images.IndexOf(currentSelected), 0);
    }

    public void Confirm()
    {
        Loader.TabIndicatorData = new TabIndicators(
            Images[InLogicHinted.Selected], Images[InLogic.Selected], Images[NotInLogicHinted.Selected],
            Images[NotInLogic.Selected], Images[AllChecks.Selected]
        );
        Close();
    }
}