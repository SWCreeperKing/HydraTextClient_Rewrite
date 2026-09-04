using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using CreepyUtil.Archipelago.ApClient;
using Godot;
using HydraTextClient.Scripts.Clients.CircleTracker;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Mapper;

public partial class MapTracker : HSplitContainer
{
    [Export] private Control MapContainer;
    [Export] private Control ButtonContainer;
    [Export] private ButtonAnimation MapEditorButton;
    [Export] public PackedScene MapScene;
    [Export] private PackedScene PackImporterPopup;
    [Export] private PackedScene PackEditorPopup;
    public Dictionary<string, string> FullNameMapPaths = [];
    private Dictionary<string, string> MapPaths = [];
    private Dictionary<string, string> ClientGames = [];
    private Dictionary<string, ButtonAnimation> Buttons = [];
    private ConcurrentDictionary<string, MapLoader> Loaders = [];
    private ConcurrentDictionary<string, ApClient> Clients = []; // easy access

    public override void _Ready()
    {
        if (!Directory.Exists(Directories.MapPacks)) Directory.CreateDirectory(Directories.MapPacks);
        Reload();

        CircleTracker.CircleTrackerOpened += AddButton;
        CircleTracker.CircleTrackerClosed += RemoveButton;
    }

    public void AddButton(string name, ApClient client)
    {
        if (Buttons.ContainsKey(name)) return;
        ButtonAnimation button = new();
        button.Text = $"{name} ({client.PlayerGame})";
        button.Disabled = !MapPaths.ContainsKey(ClientGames[name] = client.PlayerGame.ToLower().Replace(":", ""));
        button.Pressed += () => button.Disabled = LoadMap(ClientGames[name], button.Text, name, client);
        Buttons.Add(name, button);
        Clients.TryAdd(name, client);
        ButtonContainer.CallDeferred("add_child", button);
    }

    public void RemoveButton(string name, ApClient __)
    {
        Clients.Remove(name, out _);
        ClientGames.Remove(name);
        if (Buttons.Remove(name, out var button)) ButtonContainer.CallDeferred("remove_child", button);
        CallDeferred("UnloadMap", name);
    }

    public bool LoadMap(string game, string tabName, string trackerName, ApClient client)
    {
        var map = MapScene.Instantiate<MapLoader>();
        map.Client = client;
        map.Name = tabName;
        map.ExitEvent = _ => CallDeferred("UnloadMap", trackerName);
        map.CallDeferred("Setup", MapPaths[game], trackerName, this);
        MapContainer.CallDeferred("add_child", map);
        Loaders[trackerName] = map;
        return true;
    }

    public void UnloadMap(string trackerName)
    {
        if (!Loaders.ContainsKey(trackerName)) return;
        Loaders.Remove(trackerName, out var loader);
        MapContainer.RemoveChild(loader);
        loader!.QueueFree();
        if (Buttons.TryGetValue(trackerName, out var button)) button.Disabled = false;
    }

    public void Reload()
    {
        MapPaths.Clear();
        FullNameMapPaths.Clear();
        foreach (var game in Directory.GetDirectories(Directories.MapPacks))
        {
            var gameName = Path.GetFileName(game).ToLower().Replace(":", "");
            MapPaths[gameName] = game;
            FullNameMapPaths[Path.GetFileName(game)] = game;
        }

        foreach (var (client, game) in ClientGames)
        {
            var button = Buttons[client];
            button.Disabled = !MapPaths.ContainsKey(game) || Loaders.ContainsKey(client);
        }
    }

    public void CallPopupImporter() => CallDeferred("PopupImporter");

    private void PopupImporter()
    {
        var popup = PackImporterPopup.Instantiate<Popups.PackImporter>();
        popup.CloseCalled += Reload;
        AddChild(popup);
        popup.Show();
    }

    public void CallOpenPackEditor() => CallDeferred("OpenPackEditor");

    public void OpenPackEditor()
    {
        Reload();
        var popup = PackEditorPopup.Instantiate<Popups.MapEditorPopup>();
        MapEditorButton.Disabled = true;
        popup.Tracker = this;
        popup.CloseCalled += () =>
        {
            Reload();
            MapEditorButton.Disabled = false;
        };
        AddChild(popup);
        popup.Show();
    }

    public void OpenFolder() => OS.ShellOpen(Directories.MapPacks);
}