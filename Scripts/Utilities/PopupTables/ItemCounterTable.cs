using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net.Models;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Utilities.PopupTables;

public partial class ItemCounterTable : TextTable
{
    public override string[] Columns => ["Count", "Item"];
    public override long DataSize => Keys.Length;
    public Dictionary<string, ItemInfo[]> Items = [];
    public string[] Keys;

    public void SetItems(ItemInfo[] items)
    {
        Items = items.OrderBy(item => item.SortNumber()).GroupBy(item => item.UID)
                     .ToDictionary(g => g.Key, g => g.ToArray());
        Keys = [.. Items.Keys];
        QueueUiRefresh(true);
    }

    public override void RefreshUi(bool recompile) { }
    public override void RunDispose(bool disposing) { }

    public override string GetData(int row, int col)
    {
        var items = Items[Keys[row]];
        return col switch { 0 => $"{items.Length}", 1 => items[0].GetEffectText(), _ => "Error" };
    }

    public override void OnMetaClicked(string key, string[] text) { }
}