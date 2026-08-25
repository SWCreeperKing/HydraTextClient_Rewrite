using System;
using System.IO;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Utility.Popups;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class EditMapWindow : WindowSetter
{
    [Export] private LineEdit MapName;
    [Export] private OptionButton MapImage;
    [Export] private LineEdit MapAutoTabId;
    private string[] MapImages;

    [Signal] public delegate void EditMapDataEventHandler(string name, string image, string mapId);

    public void Setup(MapNavigator map)
    {
        MapImages = [.. Directory.GetFiles(map.MapPath).Select(Path.GetFileName)];
        foreach (var image in MapImages) MapImage.AddItem(image);
        MapImage.Selected = MapImages.IndexOf(map.CoreMap.ImageName);
        MapName.Text = map.CoreMap.MapName;
        MapAutoTabId.Text = map.CoreMap.MapId;
    }

    public void Edit()
    {
        try
        {
            var map = MapImages[MapImage.Selected];
            EmitSignalEditMapData(MapName.Text.Trim(), map, MapAutoTabId.Text.Trim());
        }
        catch (Exception e) { GD.PrintErr(e); }
        Close();
    }
}