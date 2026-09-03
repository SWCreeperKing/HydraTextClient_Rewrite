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
    [Export] private TextEdit MapAutoTabId;
    private string[] MapImages;

    [Signal] public delegate void EditMapDataEventHandler(string name, string image, string[] mapIds);

    public void Setup(MapNavigator map)
    {
        try
        {
            MapImages = [.. Directory.GetFiles(map.MapPath).Select(Path.GetFileName)];
            foreach (var image in MapImages) MapImage.AddItem(image);
            MapImage.Selected = MapImages.IndexOf(map.CoreMap.ImageName);
            MapName.Text = map.CoreMap.MapName;
            MapAutoTabId.Text = string.Join('\n', map.CoreMap.MapIds);
        }
        catch (Exception e)
        {
            GD.PrintErr(e);
        }
    }

    public void Edit()
    {
        try
        {
            var map = MapImages[MapImage.Selected];
            EmitSignalEditMapData(
                MapName.Text.Trim(), map,
                [.. MapAutoTabId.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries).Where(s => s.Trim() is not "")]
            );
        }
        catch (Exception e) { GD.PrintErr(e); }
        Close();
    }
}