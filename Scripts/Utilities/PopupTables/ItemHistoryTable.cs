using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Utilities.PopupTables;

public partial class ItemHistoryTable : TextTable
{
    public override string[] Columns => ["Received Order", "Item", "From", "Location"];
    public override long DataSize => ItemHistory.Length;
    private ItemEntry[] ItemHistory = [];

    public void SetItems(ReadOnlyCollection<ItemInfo> items)
    {
        var itemHistoryRaw = items
                            .Select((item, index) => new ItemEntry(
                                     index + 1, -1, [item.GetEffectText()], item.Flags,
                                     item.LocationName == "Cheat Console" ? 0 : item.Player.Slot,
                                     [item.GetLocationEffectText()]
                                 )
                             )
                            .ToArray();

        List<ItemEntry> itemHistory = [itemHistoryRaw[0]];
        for (var index = 1; index < itemHistoryRaw.Length; index++)
        {
            var current = itemHistoryRaw[index];
            var last = itemHistory[^1];

            if (last.From == current.From && last.Flags == current.Flags)
            {
                if (last.Flags.HasFlag(ItemFlags.Advancement) && last.Items.First() != current.Items.First())
                {
                    itemHistory.Add(current);
                    continue;
                }

                itemHistory[^1] = new ItemEntry(
                    last.IndexStart, current.IndexStart, [..last.Items, ..current.Items],
                    last.Flags, last.From, [..last.Locations, ..current.Locations]
                );
                continue;
            }

            itemHistory.Add(current);
        }

        ItemHistory = [.. itemHistory];
        QueueUiRefresh(true);
    }

    public override string GetData(int row, int col)
    {
        var entry = ItemHistory[row];
        return col switch
        {
            0 => entry.OrderText, 1 => entry.ItemsText, 2 => entry.FromText, 3 => entry.LocationText, _ => "Error"
        };
    }

    public override void RefreshUi(bool recompile) { }
    public override void RunDispose(bool disposing) { }

    public override void OnMetaClicked(string key, string[] text) { }

    private readonly struct ItemEntry(int indexStart,
        int indexEnd,
        HashSet<string> items,
        ItemFlags flags,
        int from,
        HashSet<string> locations)
    {
        public readonly int IndexStart = indexStart;
        public readonly int IndexEnd = indexEnd;
        public readonly HashSet<string> Items = items;
        public readonly ItemFlags Flags = flags;
        public readonly int From = from;
        public readonly HashSet<string> Locations = locations;

        public string OrderText
            => indexEnd == -1 ? $"{IndexStart:###,###}" : $"{IndexStart:###,###} - {IndexEnd:###,###}";

        public string ItemsText => string.Join(",\n ", Items);
        public string FromText => $"{{{{player;{From}}}}}";
        public string LocationText => Locations.Count > 10 ? "Various Locations" : string.Join(",\n ", Locations);
    }
}