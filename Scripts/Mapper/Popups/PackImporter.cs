using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using Godot;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Settings;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.Popups;
using HydraTextClient.Scripts.Utility.UIHelpers;
using Newtonsoft.Json;
using Environment = System.Environment;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class PackImporter : WindowSetter
{
    private const string PreviousFolder = "Mapper/Importer/FileDialogue/LastFolderUsed";

    private static string[] AllowedFiles =
    [
        ".txt", ".json", .. GlobalThemeSettings.ImageFormats.Select(f => f.Replace("*", "")),
    ];

    [Export, ExportGroup("Hydra Pack")] public FileDialog OpenZip;
    [Export, ExportGroup("Poptracker")] public OptionButton LayoutOptions;
    [Export] public VBoxContainer LocationImports;
    [Export] public ButtonAnimation ConfirmButton;
    [Export] public ButtonAnimation MapButton;
    [Export] public FileDialog MapSelect;

    private string PackPath;
    private PoptrackerManifest Manifest;
    private PoptrackerMap[] MapsFile;
    private Dictionary<string, string> MapToFileConversion = [];
    private Dictionary<string, string> MapIdNameToMapName = [];
    private Dictionary<string, int> DefaultMapLocationSize = [];
    private Dictionary<string, int> DefaultMapLocationThickness = [];
    private Dictionary<int, TabStructure> LayoutCandidates = [];
    private Dictionary<int, Dictionary<string, Maps>> LayoutCandidateMaps = [];
    private Dictionary<int, string> LayoutNames = [];
    private Dictionary<int, int> OptionItemMap = [];
    private Dictionary<string, CheckBox> LayoutSelections = [];
    private string[] LocationJsons = [];

    public override void _Ready()
    {
        OpenZip.FileSelected += s =>
        {
            SaveType<string>.Save(PreviousFolder, Path.GetDirectoryName(s), false);
            CallDeferred("ReadPackZip", s);
        };
        OpenZip.AboutToPopup += () =>
        {
            OpenZip.CurrentDir = SaveType<string>.Load(
                PreviousFolder, Environment.GetFolderPath(Environment.SpecialFolder.Desktop)
            );
        };
    }

    public void CallReadPack(string manifest) => CallDeferred("ReadPack", manifest);
    public void CallReadMap(string map) => CallDeferred("ContinueToReadPack", map);

    private void ReadPackZip(string path)
    {
        try
        {
            var game = Path.GetFileNameWithoutExtension(path);
            var gameDirectory = $"{Directories.MapPacks}/{game}";
            if (Directory.Exists(gameDirectory)) return;
            Directory.CreateDirectory(gameDirectory);

            using var zip = ZipFile.OpenRead(path);
            var starting = $"{game}/";
            
            foreach (var entry in zip.Entries)
            {
                var subPath = entry.FullName;
                if (subPath == starting) continue;
                if (subPath.StartsWith(starting)) subPath = subPath[starting.Length..];
                
                if (entry.Name is "")
                {
                    Directory.CreateDirectory($"{gameDirectory}/{subPath}");
                    continue;
                }
                
                entry.ExtractToFile($"{gameDirectory}/{subPath}".Trim());
            }

            Queue<string> folders = [];
            folders.Enqueue(gameDirectory);

            while (folders.Count != 0)
            {
                var folder = folders.Dequeue();
                foreach (var file in Directory.GetFiles(folder))
                {
                    if (!AllowedFiles.Contains(Path.GetExtension(file))) File.Delete(file);
                }

                foreach (var subFolder in Directory.GetDirectories(folder)) folders.Enqueue(subFolder);
            }
        }
        catch (Exception e) { MainController.ShowError(e); }

        Close();
    }

    private void ReadPack(string manifestFile)
    {
        ConfirmButton.Disabled = true;
        PackPath = Path.GetDirectoryName(manifestFile);
        Manifest = JsonConvert.DeserializeObject<PoptrackerManifest>(File.ReadAllText(manifestFile));
        MapSelect.CurrentDir = PackPath;
        OptionItemMap.Clear();
        LayoutOptions.Clear();
        MapIdNameToMapName.Clear();
        foreach (var (_, selection) in LayoutSelections)
        {
            LocationImports.RemoveChild(selection);
            selection.QueueFree();
        }
        LayoutSelections.Clear();

        if (!Directory.GetDirectories(Directories.MapPacks).Select(s => s.ToLower())
                      .Contains(Manifest.GameName.Replace(":", "").ToLower().Trim()))
        {
            MapButton.Disabled = false;
            return;
        }
        MainController.ShowError($"Pack for [{Manifest.GameName}] already exists");
        CallDeferred("Close");
    }

    private void ContinueToReadPack(string selectedMap)
    {
        if (!Path.GetFullPath(selectedMap).StartsWith(Path.GetFullPath(PackPath))) return;
        LocationJsons = Directory.GetFiles($"{PackPath}/locations", "*.json", SearchOption.AllDirectories);

        MapsFile = JsonConvert.DeserializeObject<PoptrackerMap[]>(File.ReadAllText(selectedMap));
        foreach (var map in MapsFile)
        {
            var imgPath = map.Image.Replace(@"\\", "/").Split('/');
            if (!imgPath.Contains("maps") || imgPath.Length == 0)
            {
                GD.PrintErr($"map path [{map.Image}] is not a valid map path");
                return;
            }

            if (imgPath[0] is not "maps") imgPath = imgPath[imgPath.IndexOf("maps")..];

            MapToFileConversion[map.Name] = imgPath[^1];
            DefaultMapLocationSize[map.Name] = map.LocationSize == 0 ? 20 : map.LocationSize;
            DefaultMapLocationThickness[map.Name] = map.LocationBorderSize == 0 ? 1 : map.LocationBorderSize;
        }

        var optionItem = 0;
        foreach (var layoutPath in Directory.GetFiles($"{PackPath}/layouts"))
        {
            if (!layoutPath.ToLower().EndsWith(".json")) continue;

            try
            {
                var parentLayout = JsonConvert.DeserializeObject<PoptrackerLayout>(File.ReadAllText(layoutPath));
                Queue<PoptrackerLayout> searchQueue = [];
                if (parentLayout.DefaultLayout is not null) searchQueue.Enqueue(parentLayout.DefaultLayout);
                if (parentLayout.HorizontalLayout is not null) searchQueue.Enqueue(parentLayout.HorizontalLayout);
                searchQueue.Enqueue(parentLayout);

                while (searchQueue.Count != 0)
                {
                    var layout = searchQueue.Dequeue();

                    if (IsLayoutAMapTab(layout))
                    {
                        var map = GenerateLayout(layout, out var data);
                        var id = map.GetHashCode();
                        LayoutCandidates[id] = map;
                        LayoutCandidateMaps[id] = data;
                        LayoutNames[id] = Path.GetFileNameWithoutExtension(layoutPath);
                        LayoutOptions.AddItem(LayoutNames[id], optionItem);
                        OptionItemMap[optionItem] = id;
                        optionItem++;

                        searchQueue.Clear();
                        break;
                    }

                    foreach (var child in layout.Content) searchQueue.Enqueue(child);
                }
            }
            catch (Exception e)
            {
                GD.Print($"Json parse fail for: [{layoutPath}], layout not in correct format.", e.Message);
            }
        }

        if (LayoutCandidates.Count == 0)
        {
            GD.PrintErr("No valid map layouts found");
            MapButton.Disabled = true;
            return;
        }

        foreach (var json in LocationJsons)
        {
            CheckBox box = new();
            box.Name = json;
            box.Text = Path.GetFileName(json);
            box.ButtonPressed = true;
            LocationImports.AddChild(box);
            LayoutSelections[json] = box;
        }

        ConfirmButton.Disabled = false;
    }

    public void FinishConversionAndClose()
    {
        var path = $"{Directories.MapPacks}/{Manifest.GameName.Replace(":", "").Trim()}";
        var chosenLayout = OptionItemMap[LayoutOptions.Selected];
        Directory.CreateDirectory(path);
        Directory.CreateDirectory($"{path}/images");
        Directory.CreateDirectory($"{path}/maps");
        var maps = LayoutCandidateMaps[chosenLayout]; // atlas.json
        Dictionary<string, Dictionary<int, MapNode>> mapNodes = [];

        foreach (var map in MapsFile)
        {
            var imgPath = map.Image.Replace(@"\\", "/").Split('/');
            if (!imgPath.Contains("maps") || imgPath.Length == 0)
            {
                GD.PrintErr($"map path [{map.Image}] is not a valid map path");
                return;
            }

            if (imgPath[0] is not "maps") imgPath = imgPath[imgPath.IndexOf("maps")..];

            var dir = string.Join('/', imgPath[..^1]);
            if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

            if (File.Exists($"{path}/{dir}/{imgPath[^1]}")) continue;
            File.Copy($"{PackPath}/{map.Image}", $"{path}/{dir}/{imgPath[^1]}");
        }

        foreach (var (id, checkBox) in LayoutSelections)
        {
            if (!checkBox.ButtonPressed) continue;
            var file = File.ReadAllText(id);

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
                catch (Exception ee) { GD.PrintErr($"Error converting [{id}]", e, "original: ", ee); }
            }

            while (locationsToCheck.Count != 0)
            {
                var loc = locationsToCheck.Dequeue();

                if (loc.MapLocations is not null)
                {
                    foreach (var mapLoc in loc.MapLocations.Where(m => MapIdNameToMapName.ContainsKey(m.MapName)))
                    {
                        var size = mapLoc.Size + DefaultMapLocationThickness[mapLoc.MapName] * 2;
                        if (mapLoc.Size is 0) size += DefaultMapLocationSize[mapLoc.MapName];
                        var x = mapLoc.X - size / 2;
                        var y = mapLoc.Y - size / 2;

                        var mapName = MapIdNameToMapName[mapLoc.MapName];
                        if (!mapNodes.TryGetValue(mapName, out var possibleNodes))
                            mapNodes[mapName] = possibleNodes = [];
                        var posHash = HashCode.Combine(mapLoc.X, mapLoc.Y);
                        if (!possibleNodes.TryGetValue(posHash, out var node))
                        {
                            possibleNodes[posHash] = node = new MapNode(x, y, size, size);

                            if (!maps.ContainsKey(mapName))
                            {
                                GD.PrintErr(
                                    $"Map [{mapName}] (pos: [{mapLoc.X},{mapLoc.Y}]) does not exist in the layout for [{id}] at [{loc.Name}]"
                                );
                                continue;
                            }
                            maps[mapName].Nodes.Add(node);
                        }

                        node.Locations.Add(loc.Name);
                        foreach (var section in loc.Sections) node.Locations.Add(section.Name);
                    }
                }

                if (loc.Locations is null) { continue; }
                foreach (var newLoc in loc.Locations) locationsToCheck.Enqueue(newLoc);
            }
        }

        File.WriteAllText($"{path}/authors.txt", Manifest.Author);
        File.WriteAllText($"{path}/locationgroups.json", "[]");
        File.WriteAllText($"{path}/atlas.json", JsonConvert.SerializeObject(maps.Values.ToArray()));
        File.WriteAllText($"{path}/tabs.json", JsonConvert.SerializeObject(LayoutCandidates[chosenLayout]));
        Close();
    }

    private bool IsLayoutAMapTab(PoptrackerLayout parentLayout)
    {
        Queue<PoptrackerLayout> searchQueue = [];
        searchQueue.Enqueue(parentLayout);

        while (searchQueue.Count != 0)
        {
            var layout = searchQueue.Dequeue();

            if (layout.Type is "map") return true;
            if (layout.Type is not ("tabbed" or "")) return false;
            if (layout.Maps.Length > 0) return true;
            if (layout.Content.Length < 1 && layout.Tabs.Length < 1) return false;

            foreach (var child in layout.Content) searchQueue.Enqueue(child);
            foreach (var child in layout.Tabs) searchQueue.Enqueue(child);
        }
        return false;
    }

    private TabStructure GenerateLayout(PoptrackerLayout parentLayout, out Dictionary<string, Maps> mapData)
    {
        mapData = [];
        Dictionary<string, TabStructure> tabs = new() { [""] = new TabStructure("") };
        Queue<(PoptrackerLayout layout, string parent)> convertQueue = [];

        if (parentLayout.Maps.Any()) convertQueue.Enqueue((parentLayout, ""));
        if (parentLayout.Content is not null)
            foreach (var child in parentLayout.Content)
                convertQueue.Enqueue((child, ""));
        if (parentLayout.Tabs is not null)
            foreach (var child in parentLayout.Tabs)
                convertQueue.Enqueue((child, ""));

        while (convertQueue.Count > 0)
        {
            var (layout, parent) = convertQueue.Dequeue();
            switch (layout.Type)
            {
                case "tabbed":
                    var parentTab = tabs[parent];
                    if (layout.Title is "") continue;
                    tabs[layout.Title] = new TabStructure(layout.Title);
                    parentTab.SubTabs.Add(tabs[layout.Title]);
                    foreach (var content in layout.Content) convertQueue.Enqueue((content, layout.Title));
                    foreach (var content in layout.Tabs) convertQueue.Enqueue((content, layout.Title));
                    break;

                case "map":
                    if (!MapToFileConversion.TryGetValue(layout.Maps[0], out var mapImage)) continue;
                    mapData[layout.Title] = new Maps(layout.Title, mapImage, parent);
                    MapIdNameToMapName[layout.Maps[0]] = layout.Title;
                    break;

                default:
                    if (layout.Content is null || layout.Content.Length < 1) continue;
                    var map = layout.Content[0];
                    if (map.Type is not "map" || map.Maps.Length < 1) continue;
                    if (!MapToFileConversion.TryGetValue(map.Maps[0], out var mapImg)) continue;
                    mapData[layout.Title] = new Maps(layout.Title, mapImg, parent);
                    MapIdNameToMapName[map.Maps[0]] = layout.Title;
                    break;
            }
        }

        return tabs[""];
    }
}