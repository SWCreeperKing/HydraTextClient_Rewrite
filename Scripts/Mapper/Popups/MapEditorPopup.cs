using System.Collections.Generic;
using Godot;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Utility.Popups;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class MapEditorPopup : WindowSetter
{
    [Export] private TabContainer TabContainer;
    [Export] private VBoxContainer ButtonContainer;

    public MapTracker Tracker;
    private Dictionary<string, ButtonAnimation> MapButtons = [];
    private MapLoader Loader;

    public override void _Ready() => ButtonReload();

    public void CallButtonReload() => CallDeferred("ButtonReload");

    public void ButtonReload()
    {
        foreach (var (_, button) in MapButtons) ButtonContainer.RemoveChild(button);
        Tracker.Reload();
        foreach (var (game, _) in Tracker.FullNameMapPaths)
        {
            ButtonAnimation button = new();
            button.Text = game;
            button.Pressed += () => CallDeferred("OpenMap", game);
            ButtonContainer.AddChild(button);
        }
    }

    public void OpenMap(string game)
    {
        Loader = Tracker.MapScene.Instantiate<MapLoader>();

        if (ConnectionController.HasLeaderClient && ConnectionController.LeaderClient!.PlayerGame.ToLower().Replace(":", "") == game.ToLower().Replace(":", ""))
        {
            Loader.Client = ConnectionController.LeaderClient;
        }
        
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
}