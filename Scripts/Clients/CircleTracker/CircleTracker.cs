using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
    private const string HydraUTBridgeFileHash = "A2D59F49EC29D28B80308B383EACA4BA0EE846FD25CDF18FC0AB1AFBD75FA4B9";
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
        try { entry = new HydraBridgeEntry(apDir, "ArchipelagoLauncherDebug", Clients[name], page); }
        catch (Exception e)
        {
            MainController.ShowError($"Error with [{apDir}]", e);
            page.QueueFree();
            return false;
        }

        if (!entry.FileExists())
        {
            try { entry = new HydraBridgeEntry(apDir, "ArchipelagoLauncher", Clients[name], page); }
            catch (Exception e)
            {
                MainController.ShowError($"Error with [{apDir}]", e);
                page.QueueFree();
                return false;
            }
        }

        if (!entry.FileExists())
        {
            var finalExecutableTest = (string[])
            [
                .. Directory.GetFiles(apDir).Select(Path.GetFileName).Where(f => f.StartsWith("Archipelago_"))
            ];
            if (finalExecutableTest.Length > 0)
            {
                try { entry = new HydraBridgeEntry(apDir, finalExecutableTest.First(), Clients[name], page); }
                catch (Exception e)
                {
                    MainController.ShowError($"Error with [{apDir}]", e);
                    page.QueueFree();
                    return false;
                }
            }
        }

        if (!entry.FileExists())
        {
            MainController.ShowError("The selected folder is not the Archipelago Folder (folder invalid)");
            page.QueueFree();
            return false;
        }

        if (!DoesApWorldExist(apDir, "HydraUTBridge", false, out var bridgeLoc))
        {
            MainController.ShowConfirm(
                "HydraUTBridge.apworld does not exist",
                "HydraUTBridge.apworld does not exist\nWould you like hydra to download it?\n(Might need to reopen hydra after)",
                () => DownloadUTBridge(apDir, true)
            );
            return false;
        }

        if (ExternalAppController.GetFileSha(bridgeLoc) != HydraUTBridgeFileHash)
        {
            MainController.ShowConfirm(
                "HydraUTBridge.apworld version isn't compatible",
                "HydraUTBridge.apworld version isn't compatible\nWould you like hydra to update it?\n(Might need to reopen hydra after)",
                () => DownloadUTBridge(bridgeLoc, false)
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
        var lib = Directory.Exists($"{apDir}/lib/worlds") ? $"{apDir}/lib/worlds/{world}.apworld"
            : $"{apDir}/worlds/{world}.apworld";
        var worldInWorlds = Directory.Exists($"{apDir}/custom_worlds") && File.Exists(custom);
        var worldInLibWorlds = File.Exists(lib);
        if (worldInLibWorlds ^ worldInWorlds)
        {
            path = worldInLibWorlds ? lib : custom;
            return true;
        }
        if (show404Error && !worldInWorlds || worldInLibWorlds)
        {
            MainController.ShowError(
                worldInWorlds
                    ? $"Duplicate ApWorld in ./{(Directory.Exists($"{apDir}/lib/worlds") ? "lib/" : "")}worlds and ./custom_worlds"
                    : $"ApWorld [{world}] not found"
            );
        }
        path = "";
        return false;
    }

    public void DownloadUTBridge(string apPath, bool guessPath)
    {
        var selfFile = System.Environment.ProcessPath;
        if (Path.GetFileNameWithoutExtension(selfFile)!.ToLower() is "godot") return;

        try
        {
            var path = apPath;
            if (guessPath)
            {
                var worldDest = "/custom_worlds";
                if (!Directory.Exists($"{apPath}{worldDest}")) worldDest = "/lib/worlds";
                if (!Directory.Exists($"{apPath}{worldDest}")) worldDest = "/worlds";
                if (!Directory.Exists($"{apPath}{worldDest}"))
                {
                    MainController.ShowError("Could not find an appropriate download location for the bridge");
                    return;
                };

                path = $"{worldDest}/HydraUTBridge.apworld";
            }
            
            if (File.Exists(path)) File.Delete(path);
            
            HttpClient client = new();
            var response = client.GetByteArrayAsync(
                                      $"{AutoUpdater.GithubReleasesPath}{MainController.GetVersion()}/HydraUTBridge.apworld"
                                  )
                                 .GetAwaiter().GetResult();
            
            File.WriteAllBytes(path!, response);
        }
        catch (Exception e) { MainController.ShowError(e); }
    }

    public void SendTrackerNotify() => OnTrackerUpdate?.Invoke();
}