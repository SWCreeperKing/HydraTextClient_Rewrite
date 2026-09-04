using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Utility.Popups;
using Newtonsoft.Json;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class EditMapWindow : WindowSetter
{
    [Export] private LineEdit MapName;
    [Export] private OptionButton MapImage;
    [Export] private TextEdit MapAutoTabId;
    [Export] private FileDialog OpenPoptrackerLocation;
    private MapNavigator MapNavigator;
    private string[] MapImages;

    [Signal] public delegate void EditMapDataEventHandler(string name, string image, string[] mapIds);

    public void Setup(MapNavigator map)
    {
        OpenPoptrackerLocation.CurrentDir = map.Loader.MapPath;
        MapNavigator = map;
        try
        {
            MapImages = [.. Directory.GetFiles(map.MapPath).Select(Path.GetFileName)];
            foreach (var image in MapImages) MapImage.AddItem(image);
            MapImage.Selected = MapImages.IndexOf(map.CoreMap.ImageName);
            MapName.Text = map.CoreMap.MapName;
            MapAutoTabId.Text = string.Join('\n', map.CoreMap.MapIds);
        }
        catch (Exception e) { GD.PrintErr(e); }
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

    public void ReadLocations(string filePath)
    {
        var file = File.ReadAllText(filePath);

        Queue<PoptrackerLocation> locationsToCheck = [];
        try
        {
            var locations = JsonConvert.DeserializeObject<PoptrackerLocation[]>(file);
            foreach (var loc in locations) locationsToCheck.Enqueue(loc);
        }
        catch (Exception e)
        {
            try
            {
                var locations = JsonConvert.DeserializeObject<PoptrackerLocation>(file);
                locationsToCheck.Enqueue(locations);
            }
            catch (Exception ee)
            {
                MainController.ShowError($"Error converting [{filePath}]", ee); 
                MainController.ShowError($"Original error", e); 
                
            }
        }

        while (locationsToCheck.Count != 0)
        {
            var loc = locationsToCheck.Dequeue();
            if (loc is null) continue;

            if (loc.MapLocations is not null)
            {
                foreach (var mapLoc in loc.MapLocations)
                {
                    var size = mapLoc.Size + 2;
                    if (mapLoc.Size is 0) size += 32;
                    var x = mapLoc.X - size / 2;
                    var y = mapLoc.Y - size / 2;

                    List<string> locs = [loc.Name, .. loc.Sections.Select(section => section.Name)];
                    MapNavigator.CreateNewNode(new Vector2(x, y), new Vector2(size, size), "", locs);
                }
            }

            if (loc.Locations is null) { continue; }
            foreach (var newLoc in loc.Locations) locationsToCheck.Enqueue(newLoc);
        }
        
        Close();
    }
}