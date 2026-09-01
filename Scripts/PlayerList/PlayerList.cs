using System;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Clients.TextClient.ParserEffects;
using HydraTextClient.Scripts.Controllers;
using static Archipelago.MultiClient.Net.Enums.ArchipelagoClientState;

namespace HydraTextClient.Scripts.PlayerList;

public partial class PlayerList : MarginContainer
{
    [Export] private PackedScene PlayerItem;
    [Export] private VBoxContainer ItemContainer;

    private PlayerItem[]? Items;
    private Action<string, int, int>[]? CheckFunctions = null;

    public override void _Ready()
    {
        PlayerEffect.OnUpdate += RefreshPlayerText;
        ConnectionController.OnClientRemoved += (_, _, _) => RefreshPlayerText();
        ConnectionController.OnFullDisconnection += Reset;

        ConnectionController.OnClientConnection += (_, client, _) =>
        {
            client.OnPlayerStateChanged += _ => CallDeferred("Refresh");
            client.SetupPlayerList();
            client.OnCommandResult += result =>
            {
                var mw = ConnectionController.GetCurrentMultiworld;
                if (mw is null) return;

                var msg = result.Data[0].Text.Replace("\r", "").Split('\n').Skip(1).Select(line =>
                    {
                        var start = line.LastIndexOf('(') + 1;
                        var end = line.LastIndexOf(')');
                        var counter = line[start..end].Split('/');
                        return (count: int.Parse(counter[0]), max: int.Parse(counter[1]));
                    }
                ).ToArray();

                for (var i = 0; i < msg.Length; i++)
                {
                    var player = client.PlayerNames[i + 1];
                    var (count, max) = msg[i];
                    ConnectionController.UpdateCheckCount(player, count, max);
                }
            };

            client.OnItemLogPacketReceived += item =>
            {
                var mw = ConnectionController.GetCurrentMultiworld;
                if (mw is null) return;
                var finder = item.Item.Player;
                var player = client.PlayerNames[finder];
                if (ConnectionController.IsConnected(player)) return;
                if (!mw.CheckCounts.TryGetValue(player, out var max)) return;
                if (!mw.CheckCountsChecked.TryGetValue(player, out var value)) return;
                ConnectionController.UpdateCheckCount(player, value + 1, max);
            };

            if (!ConnectionController.HasLeaderClient) return;
            RefreshPlayerText();
        };
    }

    public void Refresh() => CallDeferred("CallRefresh");

    private void CallRefresh()
    {
        var mw = ConnectionController.GetCurrentMultiworld;
        if (!ConnectionController.HasLeaderClient || mw is null)
        {
            Reset();
            return;
        }

        var leader = ConnectionController.LeaderClient!;
        var statuses = leader.PlayerStates[1..];
        var newList = Items is null;
        Items ??= new PlayerItem[statuses.Length];
        CheckFunctions ??= new Action<string, int, int>[statuses.Length];

        for (var i = 0; i < statuses.Length; i++)
        {
            var name = leader.PlayerNames[i + 1];
            if (newList)
            {
                var item = Items[i] = PlayerItem.Instantiate<PlayerItem>();
                ItemContainer.AddChild(item);
                var i1 = i;
                CheckFunctions[i] = (slot, amount, count) =>
                {
                    if (slot != name) return;
                    Items[i1].ReloadCheckCounts.Enqueue((amount, count));
                };
                ConnectionController.OnCheckCountUpdated += CheckFunctions[i];
                item.SetPlayer(i + 1);
            }


            if (statuses[i] == ClientGoal) { Items[i].HasGoaled(); }
            else
                Items[i].SetConnected(
                    statuses[i] switch
                    {
                        ClientUnknown => false, ClientConnected or ClientReady or ClientPlaying => true, _ => null,
                    }
                );

            if (!mw.CheckCountsChecked.ContainsKey(name)
                || !mw.CheckCounts.TryGetValue(name, out var maxCount)) continue;
            Items[i].ReloadCheckCounts.Enqueue((mw.CheckCountsChecked[name], maxCount));
        }
    }

    public void RefreshPlayerText()
    {
        if (Items is null) return;
        foreach (var item in Items) item.CallDeferred("UpdatePlayerText");
    }

    public void Reset()
    {
        if (Items is null || Items.Length == 0) return;
        foreach (var item in Items) ItemContainer.RemoveChild(item);
        if (CheckFunctions is not null)
        {
            foreach (var action in CheckFunctions) ConnectionController.OnCheckCountUpdated -= action;
        }

        Items = null;
        CheckFunctions = null;
    }

    public void SayStatus()
    {
        if (!ConnectionController.HasLeaderClient) return;
        ConnectionController.LeaderClient!.Say("!status");
    }

    public void ImportCopyList()
    {
        var mw = ConnectionController.GetCurrentMultiworld;
        if (mw is null) return;
        if (!ConnectionController.HasLeaderClient) return;
        var text = DisplayServer.ClipboardGet();
        if (text.Trim() is "") return;
        try
        {
            var split = text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            foreach (var playerCopy in split)
            {
                var copySplit = playerCopy.Split('\t', StringSplitOptions.RemoveEmptyEntries);
                if (copySplit.Length < 2) continue;
                if (!int.TryParse(copySplit[0], out var slotNumber)) continue;
                mw.PlayerCopyAliases[slotNumber] = string.Join('\t', copySplit.Skip(1));
            }
        }
        catch (Exception e) { MainController.ShowError("Failed to import copy list from clipboard", e); }

        if (Items is null) return;
        foreach (var item in Items) item.UpdateCopyText();
    }

    public void ExportCopyList()
    {
        var mw = ConnectionController.GetCurrentMultiworld;
        if (mw is null) return;
        if (!ConnectionController.HasLeaderClient) return;
        DisplayServer.ClipboardSet(string.Join('\n', mw.PlayerCopyAliases.Select(kv => $"{kv.Key}\t{kv.Value}")));
    }
}