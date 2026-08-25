using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Godot;
using HydraTextClient.Scripts.Clients.CircleTracker;
using HydraTextClient.Scripts.Clients.TextClient.ParserEffects;
using HydraTextClient.Scripts.Connection.Slots;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Settings;
using HydraTextClient.Scripts.Utilities.ItemFilter;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.DataTypes;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.UIHelpers;
using HydraTextClient.Scripts.Utility.UtilityEffects;
using static Archipelago.MultiClient.Net.Enums.HintStatus;
using static Archipelago.MultiClient.Net.Enums.ItemFlags;

namespace HydraTextClient.Scripts.Hints;

public partial class HintTable : TextTable
{
    public const string SortOrderSaveId = "hint_table_sort";
    public override string[] EffectGroups => ["default", "hinttable"];
    public const string GlobalCopyFormatProgressive = "Theme/HintTable/CopyFormat/Progressive";
    public const string GlobalCopyFormat = "Theme/HintTable/CopyFormat";

    public const string Hint = """
                               {{receiver}} - player that receives the item
                               {{item}} - item that was hinted for
                               {{loc}} - where the item is
                               {{finder}} - player who has the item
                               {{found}} - if the item was found or not
                               {{copy_receiver}} - the copy alias of the receiver
                               {{copy_finder}} - the copy alias of the finder
                               """;

    public override string[] Columns
        => ["", "", "Receiving Player", "Item", "Finding Player", "Priority", "In Logic", "Location", "Entrance"];

    public override long DataSize => SortedHints.Length;

    [Export] private PopupMenu HintChangePopup;
    private Hint CurrentlySelectedHint;
    private Hint[] SortedHints = [];

    public static List<SortObject> SortOrder => SaveType<List<SortObject>>.Load(SortOrderSaveId, []);

    public static Dictionary<HintStatus, int> HintStatusNumber = new()
    {
        [Priority] = 0, [Avoid] = 1, [NoPriority] = 2, [Unspecified] = 3,
        [Found] = 4,
    };

    public override void _Ready()
    {
        EntranceEffect.OnUpdate += CallReload;
        ItemEffect.OnUpdate += CallReload;
        LocationEffect.OnUpdate += CallReload;
        PlayerEffect.OnUpdate += CallReload;
        HintStatusEffect.OnUpdate += CallReload;
        CircleTracker.OnTrackerUpdate += () => QueueUiRefresh(true);
        SaveType<string>.AddIndividualEvents(
            CallReload, PlayerEffect.SaveIdNoAlias, PlayerEffect.SaveIdWithAlias, ItemEffect.SaveId
        );
        SaveType<HexColor>.AddIndividualEvent(ColorIdConstants.ColorConstant.InLogic.SaveId(), CallReload);
        SaveType<HexColor>.AddIndividualEvent(ColorIdConstants.ColorConstant.NotInLogic.SaveId(), CallReload);
        SaveType<bool>.AddIndividualEvent(ItemEffect.FallbackSaveId, CallReload);
        SaveType<FilterType>.OnSaveEvent += (_, _) => QueueUiRefresh(true);
        SaveType<FilterType>.OnDeleteEvent += (_, _) => QueueUiRefresh(true);

        SettingsCreator.Tab(
            "Hints", tab =>
            {
                tab
                   .AddCheckBox(
                        "Hydra Alias overrides Multiworld aliases when copying text",
                        PlayerEffect.HydraAliasOverrideInCopy, true
                    )
                   .AddCheckBox(
                        "Copy Alias overrides ALL naming when copying text", PlayerEffect.CopyAliasOverrideInCopy, true
                    )
                   .AddLineEdit(
                        "Copy Hint Text Format (Progression Items)", GlobalCopyFormatProgressive,
                        "{{receiver}}'s __`{{item}}`__ is in {{finder}}'s world at **`{{loc}}`**\n-# `{{entrance}}`",
                        Hint
                    ).AddLineEdit(
                        "Copy Hint Text Format", GlobalCopyFormat,
                        "{{receiver}}'s __`{{item}}`__ is in {{finder}}'s world at **`{{loc}}`**\n-# `{{entrance}}`",
                        Hint
                    );
            }
        );

        ConnectionController.OnClientConnection += (slot, client, _) =>
        {
            client.HintsTrackedEvent += hints =>
            {
                var mw = ConnectionController.GetCurrentMultiworld;
                if (mw is null) return;
                mw.Hints[slot] = hints;
                QueueUiRefresh(true);
            };

            var mw = ConnectionController.GetCurrentMultiworld;
            mw?.Hints[slot] = client.Hints;
            QueueUiRefresh(true);
        };

        ConnectionController.DataClearCall += () =>
        {
            SortedHints = [];
            CallDeferred("clear");
        };

        AutowrapMode = SaveType<bool>.Load("hint_table/word_wrap", false) ? TextServer.AutowrapMode.WordSmart
            : TextServer.AutowrapMode.Off;
        SaveType<bool>.OnSaveEvent += (key, b) =>
        {
            if (key is "hint_table/word_wrap")
                AutowrapMode = b ? TextServer.AutowrapMode.WordSmart : TextServer.AutowrapMode.Off;
            if (!key.StartsWith("hint_table/show_")) return;
            QueueUiRefresh(true);
        };

        SaveType<int>.AddIndividualEvent("hint_table/show_client", _ => QueueUiRefresh(true));

        HintChangePopup.IndexPressed += l =>
        {
            var hint = CurrentlySelectedHint;
            if (!ConnectionController.IsConnected(hint.ReceivingPlayer)) return;
            var client = ConnectionController.GetClient(hint.ReceivingPlayer);
            if (client is null) return;

            client.UpdateHint(
                hint.FindingPlayer, hint.LocationId, l switch { 0 => Priority, 2 => Avoid, _ => NoPriority, }
            );
        };
    }

    public override void RefreshUi(bool resort)
    {
        var mw = ConnectionController.GetCurrentMultiworld;
        if (mw is null || !ConnectionController.HasLeaderClient)
        {
            SortedHints = [];
            Clear();
            return;
        }

        if (resort)
        {
            var orderedHints =
                mw.Hints
                  .Where(kv => SaveType<int>.Load("hint_table/show_client", 0) switch
                       {
                           1 => ConnectionController.IsConnected(kv.Key),
                           2 => ConnectionController.IsLeaderClient(kv.Key), _ => true,
                       }
                   )
                  .SelectMany(kv => kv.Value)
                  .DistinctBy(hint => hint.GetHash())
                  .Where(hint =>
                       {
                           if (!SaveType<bool>.Load("hint_table/show_hidden", false)
                               && (mw!.HiddenHints.TryGetValue(hint.GetHash(), out var isVisible)
                                   && isVisible)) return false;

                           // not obvious, remove hints where finder and receiver are not in hydra
                           var order1 = GetOrderSlot(hint.FindingPlayer);
                           return !(GetOrderSlot(hint.ReceivingPlayer) == order1 && order1 == 1);
                       }
                   )
                  .Where(hint => hint.Status switch
                       {
                           Found => SaveType<bool>.Load("hint_table/show_found", false),
                           Unspecified => SaveType<bool>.Load("hint_table/show_unspecified", true),
                           NoPriority => SaveType<bool>.Load("hint_table/show_nopriority", true),
                           Avoid => SaveType<bool>.Load("hint_table/show_avoid", true),
                           Priority => SaveType<bool>.Load("hint_table/show_priority", true), _ => false,
                       }
                   )
                  .Where(hint => !SaveType<FilterType>.TryGet(hint.UID, out var filter) || filter.ShowInHintsTable)
                  .OrderBy(hint => hint.FindingPlayer);

            orderedHints = SortOrder.Count > 0 ? SortingOrder(orderedHints, SortOrder[0], true)
                : orderedHints.ThenBy(hint => hint.ReceivingPlayer);

            if (SortOrder.Count > 1)
                orderedHints = SortOrder.Skip(1)
                                        .Aggregate(orderedHints, (current, option) => SortingOrder(current, option));

            SortedHints = [.. orderedHints];
        }

        UpdateData(resort);
        return;

        IOrderedEnumerable<Hint> SortingOrder(IOrderedEnumerable<Hint> current, SortObject option,
            bool isFirst = false)
        {
            return option.Name switch
            {
                "Receiving Player" => Order(
                    current, hint => GetOrderSlot(hint.ReceivingPlayer),
                    option.IsDescending, isFirst
                ),
                "Item" => Order(current, hint => hint.SortNumber(), option.IsDescending, isFirst),
                "Finding Player" => Order(
                    current, hint => GetOrderSlot(hint.FindingPlayer), option.IsDescending,
                    isFirst
                ),
                "Priority" => Order(current, hint => HintStatusNumber[hint.Status], option.IsDescending, isFirst),
                "In Logic" => Order(current, InLogic, option.IsDescending, isFirst),
            };
        }
    }

    public override void RunDispose(bool disposing) { }

    public override string GetColumnText(int columnNum)
    {
        var columnText = Columns[columnNum];
        if (columnNum is < 2 or > 6) return columnText;

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

    public override string GetData(int row, int col)
    {
        var hint = SortedHints[row];
        var mw = ConnectionController.GetCurrentMultiworld;
        if (col is 0 && mw is null) return "???";
        return col switch
        {
            0 =>
                $"{{{{vis;{!(mw!.HiddenHints.TryGetValue(hint.GetHash(), out var isVisible) && isVisible)};{row}}}}}",
            1 => $"{{{{click;Copy;{row}}}}}",
            2 or 4 => $"{{{{player;{(col is 2 ? hint.ReceivingPlayer : hint.FindingPlayer)}}}}}",
            3 => hint.GetItemEffectText(), 5 =>
                $"{{{{hintstatus;{hint.Status switch { Found => '4', NoPriority => '1', Avoid => '2', Priority => '3', _ => '0' }};{row};{ConnectionController.IsConnected(hint.ReceivingPlayer)}}}}}",
            6 => $"{{{{log;{InLogic(hint)}}}}}", 7 => $"{{{{loc;{hint.LocationId};{hint.FindingPlayer}}}}}",
            8 => $"{{{{entrance;{hint.Entrance}}}}}", _ => "Error",
        };
    }

    public static IOrderedEnumerable<Hint> Order(IOrderedEnumerable<Hint> arr, Func<Hint, int> compare, bool descending,
        bool first)
    {
        if (first) return !descending ? arr.OrderBy(compare) : arr.OrderByDescending(compare);
        return !descending ? arr.ThenBy(compare) : arr.ThenByDescending(compare);
    }

    public int GetOrderSlot(int slot)
    {
        var player = ConnectionController.LeaderClient!.PlayerNames[slot];
        if (ConnectionController.IsConnected(player)) return 3;
        return SlotView.ContainsSlot(player) ? 2 : 1;
    }

    public int InLogic(Hint hint)
    {
        PlayerEffect.PlayerName(hint.FindingPlayer, false, out var name);
        if (!SlotView.ContainsSlot(name)) return 3;
        if (!CircleTracker.Singleton.Pages.TryGetValue(name, out var page)) return 2;
        return page.LocationsInLogic.Contains((ulong)hint.LocationId) ? 0 : 1;
    }

    public override void OnMetaClicked(string key, string[] text)
    {
        switch (key)
        {
            case "sortorder":
                var order = SortOrder.ToList();
                if (order.Any(so => so.Name == text[0]))
                {
                    var index = order.FindIndex(so => so.Name == text[0]);
                    var indexed = order[index];
                    if (indexed.IsDescending) order.RemoveAt(index);
                    else
                    {
                        indexed.IsDescending = true;
                        order[index] = indexed;
                    }
                }
                else order.Add(new SortObject(text[0]));
                SaveType<List<SortObject>>.Save(SortOrderSaveId, order, true);
                QueueUiRefresh(true);
                break;
            case "change":
                CurrentlySelectedHint = SortedHints[int.Parse(text[0])];
                HintChangePopup.Position = Vector2I.Zero;
                HintChangePopup.Popup(new Rect2I((Vector2I)HintChangePopup.GetMousePosition(), HintChangePopup.Size));
                break;
            case TextTableClickEffect.ClickedEventMsg:
                var hint = SortedHints[int.Parse(text[0])];
                var rawCopy = SaveType<string>.Load(
                    hint.ItemFlags.HasFlag(Advancement) ? GlobalCopyFormatProgressive : GlobalCopyFormat,
                    "{{receiver}}'s __`{{item}}`__ is in {{finder}}'s world at **`{{loc}}`**\n-# `{{entrance}}`"
                );

                var mw = ConnectionController.GetCurrentMultiworld;
                if (mw is null) return;

                DisplayServer.ClipboardSet(
                    rawCopy.CompileSimpleText(
                        new Dictionary<string, string>
                        {
                            ["finder"] = PlayerEffect.PlayerName(hint.FindingPlayer, true, out _),
                            ["receiver"] = PlayerEffect.PlayerName(hint.ReceivingPlayer, true, out _),
                            ["loc"] = hint.LocationName, ["entrance"] = hint.EntranceName, ["item"] = hint.ItemName,
                            ["copy_finder"] = mw.PlayerCopyAliases.GetValueOrDefault(hint.FindingPlayer, ""),
                            ["copy_receiver"] = mw.PlayerCopyAliases.GetValueOrDefault(hint.ReceivingPlayer, ""),
                        }
                    ).Replace("\\n", "\n")
                );
                break;
        }
    }

    public override void OnVariantMetaClicked(Variant meta)
    {
        if (meta.VariantType is not Variant.Type.PackedInt32Array) return;
        var mw = ConnectionController.GetCurrentMultiworld;
        if (mw is null) return;
        var arr = (int[])meta;
        var hint = SortedHints[arr[0]];
        switch (arr[1])
        {
            case 0:
                var current = mw!.HiddenHints.TryGetValue(hint.GetHash(), out var isVisible) && isVisible;
                mw!.HiddenHints[hint.GetHash()] = !current;
                GD.Print($"toggle: [{arr[0]}]:[{mw!.HiddenHints[hint.GetHash()]}]");
                QueueUiRefresh(true);
                break;
        }
    }
}