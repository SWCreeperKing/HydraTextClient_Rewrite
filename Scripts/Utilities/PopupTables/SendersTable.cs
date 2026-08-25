using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net.Models;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Utilities.PopupTables;

public partial class SendersTable : TextTable
{
    public override string[] Columns => ["Count", "Player", "Locations"];
    public override long DataSize => Players.Length;
    public Dictionary<int, ItemInfo[]> ItemCount = [];
    public int[] Players = [];

    public void SetItems(ItemInfo[] items)
    {
        ItemCount = items.GroupBy(item => item.Player.Slot).ToDictionary(g => g.Key, g => g.ToArray());
        Players = [.. ItemCount.Keys];
        QueueUiRefresh(true);
    }

    public override string GetData(int row, int col)
    {
        var player = Players[row];
        var items = ItemCount[player];
        return col switch
        {
            0 => $"{items.Length:###,##0}", 1 => $"{{{{player;{player}}}}}",
            2 => items.Length > 10 ? "Various Locations" : string.Join(
                "\n ", items.Select(item => item.GetLocationEffectText())
            ),
            _ => "Error",
        };
    }

    public override void RefreshUi(bool recompile) { }
    public override void RunDispose(bool disposing) { }
    public override void OnMetaClicked(string key, string[] text) { }
}