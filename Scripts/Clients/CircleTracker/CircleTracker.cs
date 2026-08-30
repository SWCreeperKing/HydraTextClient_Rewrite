using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using CreepyUtil.Archipelago.ApClient;
using Godot;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Settings;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.Popups;
using HydraTextClient.Scripts.Utility.UIHelpers;
using HttpClient = System.Net.Http.HttpClient;

namespace HydraTextClient.Scripts.Clients.CircleTracker;

public partial class CircleTracker : Control
{
    private const string HydraUTBridgeFileHash = "93FEFB7801E36ECDB2601851E5E44F204F917EAAC2540F4EDF04DA3609B48633";
    public static CircleTracker Singleton;
    public static event Action? OnTrackerUpdate;
    [Export] private PackedScene TrackerScene;
    [Export] private VBoxContainer ButtonContainer;
    [Export] private TabContainer PageContainer;

    public static event Action<string, ApClient>? CircleTrackerOpened; 
    public static event Action<string, ApClient>? CircleTrackerClosed; 
    
    private ConcurrentDictionary<string, ApClient> Clients = []; // easy access
    private Dictionary<string, ButtonAnimation> Buttons = [];
    public ConcurrentDictionary<string, TrackerPage> Pages = [];

    public override void _Ready()
    {
        Singleton = this;
        ConnectionController.OnClientConnection += (name, client, _) => AddButton(name, client);
        ConnectionController.OnClientRemoved += (name, _, _) => RemoveButton(name);
    }

    public void AddButton(string name, ApClient client)
    {
        ButtonAnimation button = new();
        button.Pressed += () => button.Disabled = OpenTracker(name);
        button.Text = name;
        Buttons.Add(name, button);
        Clients.TryAdd(name, client);
        ButtonContainer.CallDeferred("add_child", button);
    }

    public void RemoveButton(string name)
    {
        Clients.Remove(name, out _);
        if (Buttons.Remove(name, out var button)) ButtonContainer.CallDeferred("remove_child", button);
        if (!Pages.Remove(name, out var page)) return;
        page.Stop();
        PageContainer.CallDeferred("remove_child", page);
    }

    public bool OpenTracker(string name)
    {
        var apDir = SaveType<string>.Load(GlobalThemeSettings.ApDir, "");
        if (apDir is "" || !Directory.Exists(apDir))
        {
            MainController.ShowError(
                apDir is "" ? "Archipelago Directory not set, set it in the Settings/Main Settings"
                    : "Invalid Archipelago Directory"
            );
            return false;
        }

        if (!DoesApWorldExist(apDir, "tracker", true, out _)) return false;

        var page = TrackerScene.Instantiate<TrackerPage>();
        HydraBridgeEntry entry;
        try { entry = new HydraBridgeEntry(apDir, Clients[name], page, true); }
        catch (Exception e)
        {
            MainController.ShowError($"Error with [{apDir}]", e);
            page.QueueFree();
            return false;
        }

        if (!entry.FileExists())
        {
            try { entry = new HydraBridgeEntry(apDir, Clients[name], page, false); }
            catch (Exception e)
            {
                MainController.ShowError($"Error with [{apDir}]", e);
                page.QueueFree();
                return false;
            }
        }

        if (!entry.FileExists())
        {
            MainController.ShowError("The selected folder is not the Archipelago Folder (folder invalid)");
            page.QueueFree();
            return false;
        }

        var downloadBridge = () => DownloadUTBridge($"{apDir}/custom_worlds/HydraUTBridge.apworld");
        if (!DoesApWorldExist(apDir, "HydraUTBridge", false, out var bridgeLoc))
        {
            MainController.ShowConfirm(
                "HydraUTBridge.apworld does not exist", "HydraUTBridge.apworld does not exist\nWould you like hydra to download it?", downloadBridge
            );
            return false;
        }

        if (ExternalAppController.GetFileSha(bridgeLoc) != HydraUTBridgeFileHash)
        {
            MainController.ShowConfirm(
                "HydraUTBridge.apworld version isn't compatible", "HydraUTBridge.apworld version isn't compatible\nWould you like hydra to update it?", downloadBridge
            );
            return false;
        }

        var client = Clients[name];
        page.OnStopCalled += () =>
        {
            if (Pages.Remove(name, out var node)) PageContainer.CallDeferred("remove_child", node);
            if (Buttons.TryGetValue(name, out var button)) button.Disabled = false;
            CircleTrackerClosed?.Invoke(name, client);
        };
        Pages.TryAdd(name, page);
        PageContainer.CallDeferred("add_child", page);
        CircleTrackerOpened?.Invoke(name, client);
        page.Setup(name, client, entry);
        return true;
    }

    public bool DoesApWorldExist(string apDir, string world, bool show404Error, out string path)
    {
        var custom = $"{apDir}/custom_worlds/{world}.apworld";
        var lib = $"{apDir}/lib/worlds/{world}.apworld";
        var worldInWorlds = File.Exists(custom);
        var worldInLibWorlds = File.Exists(lib);
        if (worldInLibWorlds ^ worldInWorlds)
        {
            path = worldInLibWorlds ? lib : custom;
            return true;
        }
        if (show404Error && !worldInWorlds || worldInLibWorlds)
        {
            MainController.ShowError(
                worldInWorlds ? "Duplicate ApWorld in ./custom_worlds and ./lib/worlds" : $"ApWorld [{world}] not found"
            );
        }
        path = "";
        return false;
    }

    public void DownloadUTBridge(string path)
    {
        var selfFile = System.Environment.ProcessPath;
        if (Path.GetFileNameWithoutExtension(selfFile)!.ToLower() is "godot") return;

        try
        {
            HttpClient client = new();
            var response = client.GetByteArrayAsync(
                                      $"{AutoUpdater.GithubReleasesPath}{MainController.GetVersion()}/HydraUTBridge.apworld"
                                  )
                                 .GetAwaiter().GetResult();

            if (File.Exists(path)) File.Delete(path);
            File.WriteAllBytes(path!, response);
        }
        catch (Exception e) { MainController.ShowError(e); }
    }

    public void SendTrackerNotify() => OnTrackerUpdate?.Invoke();
}