using System.Collections.Generic;
using System.IO;
using Godot;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.Popups;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class MapEditorPopup : WindowSetter
{
    [Export] private TabContainer TabContainer;
    [Export] private VBoxContainer ButtonContainer;
    [Export] private LineEdit MapName;

    public MapTracker Tracker;
    private Dictionary<string, ButtonAnimation> MapButtons = [];
    private MapLoader Loader;

    public override void _Ready() => ButtonReload();
    public void CallButtonReload() => CallDeferred("ButtonReload");

    public void ButtonReload()
    {
        foreach (var (_, button) in MapButtons)
        {
            ButtonContainer.RemoveChild(button);
            button.QueueFree();
        }

        Tracker.Reload();
        foreach (var (game, _) in Tracker.FullNameMapPaths)
        {
            ButtonAnimation button = new();
            button.Text = game;
            button.Pressed += () => CallDeferred("OpenMap", game);
            ButtonContainer.AddChild(button);
            MapButtons.Add(game, button);
        }
    }

    public void OpenMap(string game)
    {
        Loader = Tracker.MapScene.Instantiate<MapLoader>();

        if (ConnectionController.HasLeaderClient
            && ConnectionController.LeaderClient!.PlayerGame.ToLower().Replace(":", "")
            == game.ToLower().Replace(":", "")) { Loader.Client = ConnectionController.LeaderClient; }

        Loader.Name = game;
        Loader.ExitEvent = _ =>
        {
            TabContainer.RemoveChild(Loader);
            Loader.QueueFree();
        };
        Loader.CallDeferred("Setup", Tracker.FullNameMapPaths[game], game, TabContainer);
        TabContainer.AddChild(Loader);
        TabContainer.CurrentTab = 1;
    }

    public void CreateMap()
    {
        var game = MapName.Text;
        MapName.Text = "";
        Directory.CreateDirectory($"{Directories.MapPacks}/{game}");
        Directory.CreateDirectory($"{Directories.MapPacks}/{game}/images");
        Directory.CreateDirectory($"{Directories.MapPacks}/{game}/maps");
        File.WriteAllText($"{Directories.MapPacks}/{game}/atlas.json", "[]");
        File.WriteAllText($"{Directories.MapPacks}/{game}/tabs.json", "{}");
        File.WriteAllText($"{Directories.MapPacks}/{game}/locationgroups.json", "[]");
        CallButtonReload();
        CallDeferred("OpenMap", game);
    }
}