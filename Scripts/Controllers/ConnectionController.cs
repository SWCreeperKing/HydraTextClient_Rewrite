using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net.Enums;
using CreepyUtil.Archipelago;
using CreepyUtil.Archipelago.ApClient;
using Godot;
using HydraTextClient.Scripts.Connection.Slots;
using HydraTextClient.Scripts.Settings;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.DataTypes;
using HydraTextClient.Scripts.Utility.Loaders;
using Newtonsoft.Json;

namespace HydraTextClient.Scripts.Controllers;

public partial class ConnectionController : Control
{
    private static ConnectionController Singleton;
    public static bool HasLeaderClient => LeaderClient is not null;

    public static ApClient? LeaderClient
        => Singleton.Clients.Count == 0 ? null : Singleton.NamedClients[Singleton.Clients[0]];

    public static bool LockMultiworld => Singleton?.Clients!.Count != 0 || HasLeaderClient || ClientTryConnecting;
    public static string CurrentMultiworld = null;

    public static MultiworldData? GetCurrentMultiworld => CurrentMultiworld is null ? null
        : SaveType<MultiworldData>.Load(CurrentMultiworld, null);

    public static double GetConnectionCooldown => ConnectionCooldown;
    public static bool IsConnecting => ClientTryConnecting;
    public static ConcurrentBag<int> ProcessIds = [];
    private static double ConnectionCooldown;
    private static bool ClientTryConnecting;

    public static event Action<string, ApClient, List<ArchipelagoTag>, bool>? OnClientPrepareConnection;
    public static event Action<string, ApClient, bool>? OnClientConnection;
    public static event Action<string, ApClient, bool>? OnClientRemoved;
    public static event Action? OnFullDisconnection;
    public static event Action<string, int, int>? OnCheckCountUpdated;
    public static event Action? DataClearCall;

    /// <summary>
    /// old leader, new leader
    /// </summary>
    public static event Action<ApClient, ApClient>? OnClientLeaderChanged;

    private List<string> Clients = [];
    private Dictionary<string, ApClient> NamedClients = [];
    private Dictionary<string, string> Receipts = [];

    public override void _Ready()
    {
        Singleton = this;
        OnClientConnection += (_, _, _) => ClientTryConnecting = false;
    }

    public override void _Process(double delta)
    {
        foreach (var client in Clients) NamedClients[client].UpdateConnection();
        if (ConnectionCooldown > 0) ConnectionCooldown -= delta;
    }

    private void ConnectClient(string name, string originalName)
    {
        Receipts[name] = originalName;
        var mw = SaveType<MultiworldData>.Load(CurrentMultiworld, null);

        ClientTryConnecting = true;

        try
        {
            var disconnectTimer = new TimeSpan(
                0, 0, (int)SaveType<double>.Load(GlobalThemeSettings.ServerTimeoutTime, 60)
            );
            ApClient client = new() { ServerTimeout = disconnectTimer, ExcludeBouncedPacketsFromSelf = false };
            var isLeader = !HasLeaderClient;
            List<ArchipelagoTag> tags = [ArchipelagoTag.TextOnly, ArchipelagoTag.DeathLink, ArchipelagoTag.TrapLink];
            client.DeathLinkGroups = [.. mw.DeathLinkGroups, ""];

            if (!isLeader) tags.Add(ArchipelagoTag.NoText);
            OnClientPrepareConnection?.Invoke(name, client, tags, isLeader);

            client.OnConnectionEvent += _ =>
            {
                SlotView.SetPortraitStatus(originalName, ConnectionStatus.Connected);
                mw.CheckCounts[name] = client.LocationCount;
                mw.CheckCountsChecked[name] = client.LocationsCheckedCount;

                foreach (var player in client.PlayerNames.Skip(1))
                {
                    mw.CheckCounts.TryAdd(player, 0);
                    mw.CheckCountsChecked.TryAdd(player, 0);
                }

                UpdateCheckCount(name, client.LocationsCheckedCount, client.LocationCount);
            };

            client.CheckedLocationsUpdated += _ =>
            {
                UpdateCheckCount(name, client.LocationsCheckedCount, client.LocationCount);
            };

            client.OnErrorReceived += MainController.ShowError;

            client.OnConnectionErrorReceived += (exception, message) =>
            {
                if (exception is JsonSerializationException) return;
                MainController.ShowError(message, exception);
                SlotView.SetPortraitStatus(originalName, ConnectionStatus.Error);
            };

            client.OnConnectionLost += () =>
            {
                client.TryDisconnect();
                CallDeferred("RemoveClient", name);
                SlotView.SetPortraitStatus(originalName, ConnectionStatus.Error);
            };

            client.OnDataStorageListenerError += e => GD.PrintErr(e);

            Task.Run(() =>
                {
                    try
                    {
                        var error = client.TryConnect(
                            new LoginInfo(
                                int.Parse(mw.Port), name!, mw.Address, GetMultiworldPassword(originalName, false)
                            ),
                            "", ItemsHandlingFlags.AllItems, tags: [.. tags]
                        );

                        if (error is not null && error.Length > 0)
                        {
                            client.TryDisconnect();
                            MainController.ShowError(error);
                            ClientTryConnecting = false;
                            SlotView.SetPortraitStatus(originalName, ConnectionStatus.Error);
                            return;
                        }

                        Clients.Add(name);
                        NamedClients[name] = client;
                        OnClientConnection?.Invoke(name, client, isLeader);
                        if (Clients.Count == 0) OnClientLeaderChanged?.Invoke(null, client);
                        SetConnectionCooldown();
                    }
                    catch (Exception e) { GD.PrintErr(e); }
                }
            );
        }
        catch (Exception e) { MainController.ShowError(e); }
    }

    private void DisconnectClient(string name)
    {
        if (!Clients.Contains(name)) return;
        var client = NamedClients[name];
        RemoveClient(name);
        client.TryDisconnect();
        if (Clients.Count != 0) return;
        OnFullDisconnection?.Invoke();
        if (SaveType<bool>.Load(GlobalThemeSettings.ClearDataOnFullDisconnect, true)) DataClearCall?.Invoke();
        if (ProcessIds.IsEmpty) return;
        foreach (var id in ProcessIds) ExternalAppController.EndProcess(id);
    }

    private void RemoveClient(string name)
    {
        var leader = LeaderClient;
        OnClientRemoved?.Invoke(name, NamedClients[name], leader == NamedClients[name]);

        var candidate = Clients.Skip(1).FirstOrDefault(
            c =>
            {
                NamedClients[c].UpdateConnection();
                return NamedClients[c].IsConnected;
            }, null
        );
        if (leader == NamedClients[name] && candidate is not null) ChangeLeader(candidate);

        Clients.Remove(name);
        NamedClients.Remove(name);
    }

    public void ChangeLeader(string newLeaderName)
    {
        var leader = LeaderClient;
        var newLeader = NamedClients[newLeaderName];

        _ = leader!.Tags + ArchipelagoTag.NoText;
        _ = newLeader.Tags - ArchipelagoTag.NoText;

        Clients.Remove(newLeaderName);
        Clients.Insert(0, newLeaderName);

        OnClientLeaderChanged?.Invoke(leader, newLeader);
        SetConnectionCooldown();
    }

    private void SetConnectionCooldown()
    {
        if (CurrentMultiworld is null) return;
        var mw = SaveType<MultiworldData>.Load(CurrentMultiworld, null);
        if (mw?.Address.ToLower() is "localhost" or "127.0.0.1") return;
        ConnectionCooldown = 5;
    }

    public static string GetMultiworldPassword(string slot, bool slotOnly, MultiworldData? mw = null)
    {
        mw ??= GetCurrentMultiworld;
        return mw is null ? "" : slotOnly ? mw.SlotPasswords.GetValueOrDefault(slot, "") : mw.Password;
    }

    public static void SetMultiworldPassword(string slotName, string password, MultiworldData? mw = null)
    {
        mw ??= GetCurrentMultiworld;
        if (password is not "") mw?.SlotPasswords[slotName] = password;
        else mw?.SlotPasswords.Remove(slotName, out _);
    }

    public static string GetMultiworldName(string name, MultiworldData? mw = null)
    {
        mw ??= GetCurrentMultiworld;
        return mw is null ? name : mw.GetSlotName(name);
    }

    public static void SetMultiworldName(string slotName, string alternateName, MultiworldData? mw = null)
    {
        mw ??= GetCurrentMultiworld;
        if (alternateName is not "") mw?.SlotNames[slotName] = alternateName;
        else mw?.SlotNames.Remove(slotName, out _);
    }

    public static void TryConnect(string name)
    {
        if (CurrentMultiworld is null) return;
        if (ClientTryConnecting) return;
        SlotView.SetPortraitStatus(name, ConnectionStatus.NotConnected);
        var mw = SaveType<MultiworldData>.Load(CurrentMultiworld, null);
        if (mw is null) return;

        var multiWorldName = GetMultiworldName(name, mw);

        if (IsConnected(multiWorldName))
        {
            TryDisconnect(multiWorldName, name);
            return;
        }

        if (ConnectionCooldown > 0) return;
        if (Singleton.Clients.Count >= 7 && mw.Address.ToLower() is not ("localhost" or "127.0.0.1"))
        {
            MainController.ShowError("Max Connected Slots Reached (so ap doesn't get mad at me)");
            return;
        }

        SlotView.SetPortraitStatus(name, ConnectionStatus.Connecting);
        Singleton.CallDeferred("ConnectClient", multiWorldName, name);
    }

    public static void TryDisconnect(string name, string originalName)
    {
        if (Singleton.Receipts[name] != originalName) return;
        Singleton.Receipts.Remove(name);
        SlotView.SetPortraitStatus(originalName, ConnectionStatus.NotConnected);
        Singleton.CallDeferred("DisconnectClient", name);
    }

    public static void ChangeLeaderClient(string name)
    {
        if (Singleton.Clients.Count == 0) return;
        if (Singleton.Clients[0] == name) return;
        if (CurrentMultiworld is null) return;
        if (ClientTryConnecting) return;
        if (ConnectionCooldown > 0) return;
        Singleton.CallDeferred("ChangeLeader", name);
    }

    public static bool IsConnected(int slot) => HasLeaderClient && IsConnected(LeaderClient!.PlayerNames[slot]);

    public static bool IsConnected(string name) => Singleton.Clients.Contains(name)
                                                   || HasReceipt(name) && Singleton.Clients.Contains(GetReceipt(name));

    public static string[] GetClientNames() => [.. Singleton.Clients];

    public static ApClient? GetClient(int slot) => HasLeaderClient ? GetClient(LeaderClient!.PlayerNames[slot]) : null;

    public static ApClient? GetClient(string name)
    {
        if (Singleton.NamedClients.TryGetValue(name, out var client)) return client;
        if (HasReceipt(name) && Singleton.NamedClients.TryGetValue(GetReceipt(name), out client)) return client;
        return null;
    }

    public static bool GetPlayerInfo(int playerSlot, out string name, out string alias, out string game)
    {
        var leader = LeaderClient!;
        name = leader.PlayerNames[playerSlot];
        alias = leader.GetAlias(playerSlot)!.Replace($" ({name})", "").Sanitize();
        name = name.Sanitize();
        game = leader.PlayerGames[playerSlot];
        return name != alias;
    }

    public static bool HasReceipt(string name) => Singleton.Receipts.ContainsKey(name);
    public static string GetReceipt(string name) => Singleton.Receipts.GetValueOrDefault(name, "");

    public static void UpdateCheckCount(string slot, int totalCount, int max)
    {
        var mw = GetCurrentMultiworld;
        if (mw is null) return;
        OnCheckCountUpdated?.Invoke(slot, mw.CheckCountsChecked[slot] = totalCount, mw.CheckCounts[slot] = max);
    }

    public static void IncrementCheckCount(string slot, int amount, int max)
    {
        var mw = GetCurrentMultiworld;
        if (mw is null) return;
        if (!mw.CheckCounts.TryAdd(slot, amount)) mw.CheckCounts[slot] += amount;
        OnCheckCountUpdated?.Invoke(slot, mw.CheckCountsChecked[slot], mw.CheckCounts[slot] = max);
    }

    public static bool IsLeaderClient(string client) => Singleton.Clients[0] == client;
    public static void ForceDataClear() => DataClearCall?.Invoke();
}