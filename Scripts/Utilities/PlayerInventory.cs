using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using CreepyUtil.Archipelago.ApClient;
using Godot;
using HydraTextClient.Scripts.Clients.TextClient.ParserEffects;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Hints;
using HydraTextClient.Scripts.Settings;
using HydraTextClient.Scripts.Utilities.ItemFilter;
using HydraTextClient.Scripts.Utilities.Popups;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.UIHelpers;
using HydraTextClient.Scripts.Utility.UtilityEffects;

namespace HydraTextClient.Scripts.Utilities;

public partial class PlayerInventory : TextTable
{
    [Export] private PackedScene SendersPopup;
    [Export] private PackedScene ItemCountPopup;
    [Export] private PackedScene ItemHistoryPopup;
    [Export] private Label CheatCounter;
    [Export] private CheckBox ShowProgression;
    [Export] private CheckBox ShowUseful;
    [Export] private CheckBox ShowNormal;
    [Export] private CheckBox ShowTraps;
    public override string[] Columns => ["Count", "Item", "Senders"];
    public override long DataSize => Keys.Length;
    private Dictionary<string, ItemInfo[]> Inventory = [];
    private string[] Keys;
    private ApClient Client;
    public string[] RawItemNames;
    public bool OpenNewWindow;
    public bool HasOpenedNewWindow = false;
    private Action<string, FilterType> OnFilterDataUpdated;
    private List<SortObject> SortOrder = [new("Item"), new("Count") { IsDescending = true }];

    public void SetupInventory(ApClient client)
    {
        ShowProgression.Pressed += () => QueueUiRefresh(true);
        ShowUseful.Pressed += () => QueueUiRefresh(true);
        ShowNormal.Pressed += () => QueueUiRefresh(true);
        ShowTraps.Pressed += () => QueueUiRefresh(true);

        Client = client;
        client.ItemHandler.OnNewItemsReceived += (_, starting) =>
        {
            if (starting is 0) OpenNewWindow = true;
            QueueUiRefresh(true);
        };
        Client?.UpdateItemHandler();
        QueueUiRefresh(true);

        OnFilterDataUpdated = (_, _) => QueueUiRefresh(true);
        SaveType<FilterType>.OnSaveEvent += OnFilterDataUpdated;
        ItemEffect.OnUpdate += CallReload;
    }

    public override void _PhysicsProcess(double delta) => Client?.UpdateItemHandler();

    public override void RefreshUi(bool recompile)
    {
        if (!recompile) return;
        var cheatedCount = Client.ItemHandler.GetCheatedItems().Length;
        CheatCounter.Visible = cheatedCount > 0;
        if (cheatedCount > 0) CheatCounter.Text = $"Cheated Items: [{cheatedCount:###,##0}]";

        var items = Client.ItemHandler.Items;
        Inventory = items.GroupBy(item => item.UID).ToDictionary(g => g.Key, g => g.ToArray());
        var ordered = Inventory.Select(kv => kv.Value)
                               .Where(item =>
                                    {
                                        if (item[0].Flags.HasFlag(ItemFlags.Advancement))
                                            return ShowProgression.ButtonPressed;
                                        if (item[0].Flags.HasFlag(ItemFlags.NeverExclude))
                                            return ShowUseful.ButtonPressed;
                                        return item[0].Flags.HasFlag(ItemFlags.Trap) ? ShowTraps.ButtonPressed
                                            : ShowNormal.ButtonPressed;
                                    }
                                )
                               .OrderBy(item => item[0].SortNumber());

        if (SortOrder.Count > 0)
        {
            ordered = SortingOrder(ordered, SortOrder[0], true);
            if (SortOrder.Count > 1) ordered = SortingOrder(ordered, SortOrder[1]);
        }
        else ordered = ordered.ThenBy(item => item[0].ItemName);

        Keys = [.. ordered.Select(item => item[0].UID)];
        RawItemNames = [.. Inventory.Values.Select(arr => arr[0].ItemName).Distinct()];

        var mw = ConnectionController.GetCurrentMultiworld;

        if (mw is null) return;
        var starting = mw.ItemHistory.GetOrAdd(Client.PlayerName, 0);
        var newItems = items.Skip(starting).ToArray();

        if (newItems.Length != 0 && OpenNewWindow && !HasOpenedNewWindow)
        {
            if (SaveType<bool>.Load(GlobalThemeSettings.DisplayNewItemsPopup, true))
            {
                var popup = ItemCountPopup.Instantiate<InventoryCounter>();
                popup.SetItems(newItems);
                AddChild(popup);
                popup.Show();
            }
            HasOpenedNewWindow = true;
        }

        mw.ItemHistory[Client.PlayerName] = Client.ItemHandler.ItemIndex;
        OpenNewWindow = false;
        return;

        IOrderedEnumerable<ItemInfo[]> SortingOrder(IOrderedEnumerable<ItemInfo[]> current, SortObject option,
            bool isFirst = false)
        {
            return option.Name switch
            {
                "Item" => Order(current, item => item[0].SortNumber(), option.IsDescending, isFirst),
                "Count" => Order(current, item => item.Length, option.IsDescending, isFirst),
            };
        }

        IOrderedEnumerable<ItemInfo[]> Order(IOrderedEnumerable<ItemInfo[]> arr, Func<ItemInfo[], int> compare,
            bool descending,
            bool first)
        {
            if (first) return !descending ? arr.OrderBy(compare) : arr.OrderByDescending(compare);
            return !descending ? arr.ThenBy(compare) : arr.ThenByDescending(compare);
        }
    }

    public override string GetData(int row, int col)
    {
        var items = Inventory[Keys[row]];
        return col switch
        {
            0 => $"{items.Length}", 1 => items[0].GetEffectText(), 2 => $"{{{{click;View;{row}}}}}", _ => "Error",
        };
    }

    public override string GetColumnText(int columnNum)
    {
        var columnText = Columns[columnNum];
        if (columnNum >= 2) return columnText;

        StringBuilder sb = new();
        sb.Append("[url=\"sortorder_").Append(columnText).Append("\"]").Append(columnText);

        if (SortOrder.All(so => so.Name != columnText))
        {
            sb.Append(" -").Append("[/url]");
            return sb.ToString();
        }

        var so = SortOrder.First(so => so.Name == columnText);
        var place = SortOrder.IndexOf(so) + 1;

        sb.Append(' ').Append(place).Append(so.IsDescending ? '▼' : '▲').Append("[/url]");
        return sb.ToString();
    }

    public override void OnMetaClicked(string key, string[] text)
    {
        switch (key)
        {
            case "sortorder":
                if (SortOrder.Any(so => so.Name == text[0]))
                {
                    var index = SortOrder.FindIndex(so => so.Name == text[0]);
                    var indexed = SortOrder[index];
                    if (indexed.IsDescending) SortOrder.RemoveAt(index);
                    else
                    {
                        indexed.IsDescending = true;
                        SortOrder[index] = indexed;
                    }
                }
                else SortOrder.Add(new SortObject(text[0]));
                QueueUiRefresh(true);
                break;
            case TextTableClickEffect.ClickedEventMsg:
                var popup = SendersPopup.Instantiate<InventorySenders>();
                popup.SetItems(Inventory[Keys[int.Parse(text[0])]]);
                AddChild(popup);
                popup.Show();
                break;
        }
    }

    public void ViewItemHistory()
    {
        var items = Client.ItemHandler.Items;
        if (items.Count == 0) return;
        var popup = ItemHistoryPopup.Instantiate<InventoryHistory>();
        popup.SetItems(items);
        AddChild(popup);
        popup.Show();
    }

    public override void RunDispose(bool disposing)
    {
        SaveType<FilterType>.OnSaveEvent -= OnFilterDataUpdated;
        ItemEffect.OnUpdate -= CallReload;
    }
}