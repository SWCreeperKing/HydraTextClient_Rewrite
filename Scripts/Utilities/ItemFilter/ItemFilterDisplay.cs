using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net.Enums;
using Godot;
using HydraTextClient.Scripts.Clients.TextClient.ParserEffects;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.UIHelpers;
using HydraTextClient.Scripts.Utility.UtilityEffects;

namespace HydraTextClient.Scripts.Utilities.ItemFilter;

public partial class ItemFilterDisplay : TextTable
{
    public override string[] EffectGroups => ["default", "itemfilter"];
    public static Dictionary<ItemFlags, int> ItemToSortIdCache = new();
    public override string[] Columns => ["Game", "Item", "Item Log", "Hint Table", "Is Special", ""];
    public override long DataSize => SaveType<FilterType>.Count;
    private FilterType[] FilterTypes;

    public override void _Ready()
    {
        RefreshUi(true);
        SaveType<FilterType>.OnSaveEvent += (_, _) => QueueUiRefresh(true);
        SaveType<FilterType>.OnDeleteEvent += (_, _) => QueueUiRefresh(true);
        ItemEffect.OnUpdate += CallReload;
    }

    public override void RefreshUi(bool recompile)
    {
        FilterTypes = [.. SaveType<FilterType>.GetValues().OrderBy(f => f.GameName).ThenBy(f => f.SortNumber())];
        UpdateData(recompile);
    }

    public override void RunDispose(bool disposing) { }

    public override string GetData(int row, int col)
    {
        var filter = FilterTypes[row];
        return col switch
        {
            0 => filter.GameName, 1 => filter.GetEffectText(), 2 => $"{{{{log;{filter.ShowInItemLog};{row}}}}}",
            3 => $"{{{{table;{filter.ShowInHintsTable};{row}}}}}", 4 => $"{{{{special;{filter.IsSpecial};{row}}}}}",
            5 => $"{{{{click;Remove;{row}}}}}", _ => "Error",
        };
    }

    public override void OnMetaClicked(string key, string[] text)
    {
        switch (key)
        {
            case TextTableClickEffect.ClickedEventMsg:
                SaveType<FilterType>.Delete(FilterTypes[int.Parse(text[0])].UID); break;
        }
    }

    public override void OnVariantMetaClicked(Variant meta)
    {
        if (meta.VariantType is not Variant.Type.PackedInt32Array) return;
        var arr = (int[])meta;
        var filter = FilterTypes[arr[0]];
        switch (arr[1])
        {
            case 0: filter.ShowInItemLog = !filter.ShowInItemLog; break;
            case 1: filter.ShowInHintsTable = !filter.ShowInHintsTable; break;
            case 2: filter.IsSpecial = !filter.IsSpecial; break;
        }
        SaveType<FilterType>.Save(filter.UID, filter, true);
    }

    public void OpenEmptyFilter() => MainController.ShowItemFilter();
}