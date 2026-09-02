using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using CreepyUtil.Archipelago.ApClient;
using Godot;
using HydraTextClient.Scripts.Clients.TextClient.ParserEffects;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Settings;
using HydraTextClient.Scripts.Utilities.Popups;
using HydraTextClient.Scripts.Utility.Loaders;

namespace HydraTextClient.Scripts.Utilities;

public partial class SlotUtility : HSplitContainer
{
    [Export] private Label HintInfo;
    [Export] private ProgressBar HintProgress;
    [Export] private PackedScene HintPopup;
    [Export] private PlayerInventory Inventory;
    [Export] private SearchingList ItemList;
    [Export] private SearchingList LocationList;
    private bool RefreshUI;
    private Action<double> ItemListIconSize;
    private bool ShowUnobtainedItems;
    private ApClient Client;

    public void SetupPlayer(ApClient client)
    {
        Client = client;
        var fontSize = (int)SaveType<double>.Load(GlobalThemeSettings.GlobalFontSize, 20d);
        ItemListIconSize = d => ItemList.List.FixedIconSize = new Vector2I((int)d, (int)d);
        SaveType<double>.AddIndividualEvent(GlobalThemeSettings.GlobalFontSize, ItemListIconSize);

        client.OnLocationsChecked += locPack =>
        {
            LocationList.RemoveItems(
                [.. locPack.Locations.Select(loc => client.LocationIdToLocationName(loc, client.PlayerSlot))]
            );
        };

        client.HintsTrackedEvent += _ => RefreshUI = true;
        client.OnRoomInfoPacketReceived += _ => RefreshUI = true;
        client.OnRoomUpdatePacketReceived += _ => RefreshUI = true;

        Inventory.SetupInventory(client);

        ItemList.SetupBox(box =>
            {
                box.Visible = true;
                box.Text = "Show Unobtained Items";
                box.Toggled += b =>
                {
                    ShowUnobtainedItems = b;
                    ItemList.UpdateSearch(ItemList.SearchBar.Text);
                };
            }
        );

        ItemList.VisibilitySetter = (results, item) => Enumerable.Contains(results, item)
                                                       && (!ShowUnobtainedItems || !Enumerable.Contains(
                                                           Inventory.RawItemNames, item
                                                       ));

        var game = client.PlayerGame;
        ItemList.OnItemCreated += (_, index, item) =>
        {
            var img = CustomAssets.ItemImage(
                game, item, game, asset => ItemList.ImageQueue.Enqueue((index, asset)), out var isFallback
            );
            if (isFallback && SaveType<bool>.Load(ItemEffect.FallbackSaveId, false)) return;
            ItemList.ImageQueue.Enqueue((index, img));
        };

        ItemList.SetItems([.. client.Items.Select(kv => kv.Key)]);
        ItemList.List.FixedIconSize = new Vector2I(fontSize, fontSize);
        LocationList.SetItems([.. client.Locations.Select(kv => kv.Key).Where(client.IsMissingLocation)]);
        ItemList.OnItemPressed += s => CallDeferred("CreateDialog", "Hint Item", $"Hint for\n{s}?", $"!hint {s}");
        LocationList.OnItemPressed += s => CallDeferred(
            "CreateDialog", "Hint Location", $"Hint for whats at\n{s}?", $"!hint_location {s}"
        );

        GameItemImageLoader.OnReload += ItemList.RefreshList;
        RefreshUI = true;
    }

    public void CreateDialog(string title, string text, string command)
    {
        var popup = HintPopup.Instantiate<HintPopup>();
        popup.Set(Client, title, text, command);
        AddChild(popup);
        popup.Show();
    }

    protected override void Dispose(bool disposing)
    {
        SaveType<double>.RemoveIndividualEvent(GlobalThemeSettings.GlobalFontSize, ItemListIconSize);
        GameItemImageLoader.OnReload -= ItemList.RefreshList;
        Inventory.QueueFree();
    }

    public override void _Process(double delta)
    {
        if (!RefreshUI) return;
        RefreshUI = false;
        RoomUpdate();
    }

    public void RoomUpdate()
    {
        if (!Client.IsConnected)
        {
            HintInfo.Text = "";
            HintProgress.Value = 0;
        }
        var costPercent = Client.HintCostPercent;

        if (costPercent is 0)
        {
            HintInfo.Text = "Hint cost percent is 0, Unlimited Hints!";
            HintProgress.Value = 1;
            return;
        }

        var cost = Client.HintCost;
        var points = Client.HintPoints;
        var hintAmount = (int)Math.Floor((double)points / cost);
        HintInfo.Text =
            $"""
             points: [{points}], cost: [{cost}]{(hintAmount > 0 ? $"\nHint(s) available: [{hintAmount}]" : "")}
             Progress to next hint ({cost - points % cost} points):
             """;
        HintProgress.Value = (double)(points % cost) / cost;
    }
}