using System.Collections.Generic;
using CreepyUtil.DiscordRpc;
using Godot;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Settings;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.Loaders;
using static HydraTextClient.Scripts.Discord.DRPC_GTI;
using static HydraTextClient.Scripts.Discord.DRPC_GTT;

namespace HydraTextClient.Scripts.Discord;

public static class DRPC
{
    private const string AppId = "1339447230909644851"; 
    public static string? LastLocationChecked = null;
    public static bool RunDiscordRPC = SaveType<bool>.Load(GlobalThemeSettings.DiscordEnabled, true);

    public static void Init()
    {
        if (!RunDiscordRPC) return;
        ConnectionController.OnClientLeaderChanged += (_, _) => DiscordIntegration.UpdateActivity();
        ConnectionController.OnClientConnection += (_, client, _) =>
        {
            client.OnItemLogPacketReceived += packet =>
            {
                var player = packet.FindingPlayer;
                if (client.PlayerSlot != player) return;
                LastLocationChecked = client.LocationIdToLocationName(packet.Item.Location, player);
                DiscordIntegration.UpdateActivity();
            };
        };
        ConnectionController.OnFullDisconnection += () => LastLocationChecked = null;

        DiscordIntegration.LogOut = GD.Print;
        DiscordIntegration.Details = () =>
        {
            if (!ConnectionController.HasLeaderClient) return "Not connected";
            var leader = ConnectionController.LeaderClient!;
            var game = leader.PlayerGame;
            return GameToTitle.GetValueOrDefault(game, game);
        };

        DiscordIntegration.State = () =>
        {
            if (!ConnectionController.HasLeaderClient) return "";
            var leader = ConnectionController.LeaderClient!;
            return $"In the Multiworld ({leader.LocationsCheckedCount} / {leader.LocationCount})";
        };

        DiscordIntegration.LargeImage = () =>
        {
            if (!ConnectionController.HasLeaderClient) return "archipelago";
            var leader = ConnectionController.LeaderClient!;
            var game = leader.PlayerGame;
            return GameToImage.GetValueOrDefault(game, "archipelago");
        };

        DiscordIntegration.LargeText = () =>
        {
            if (!ConnectionController.HasLeaderClient) return "Not Connected";
            return LastLocationChecked is null ? "Nothing Yet" : $"Last Location Checked: {LastLocationChecked}";
        };

        DiscordIntegration.SmallImage = () => "archipelago";

        DiscordIntegration.SmallText = () => !ConnectionController.HasLeaderClient ? "Not connected"
            : $"{ConnectionController.LeaderClient!.PlayerNames.Length - 1} Player Multiworld";
        
        DiscordIntegration.InitDiscord(AppId);
        CheckDiscord();
    }

    public static void CheckDiscord()
    {
        if (!RunDiscordRPC) return;
        DiscordIntegration.UpdateActivity();
    }
}