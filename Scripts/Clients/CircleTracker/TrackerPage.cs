using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using CreepyUtil.Archipelago.ApClient;
using Godot;
using HydraTextClient.Scripts.Clients.TextClient;
using HydraTextClient.Scripts.Clients.TextClient.ParserEffects;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Settings;
using HydraTextClient.Scripts.Utilities.ItemFilter;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Clients.CircleTracker;

public partial class TrackerPage : Control
{
    private const string ShowEmptyCircles = "circle_tracker/show_empty";
    private const string ShowFutureCircles = "circle_tracker/spoil_future";
    private ConcurrentQueue<bool> UpdateQueue = [];
    [Export] private PopoutWindow PopoutWindow;
    [Export] private EmptyRichLabelInteractor Label;
    [Export] private ProgressionItemTable NextProgressionLabel;

    public ConcurrentDictionary<int, long[]> RawCircleItems = [];
    public ConcurrentDictionary<long, int> NextProgression = [];
    public ConcurrentDictionary<int, ulong[]> Circles = [];
    public ConcurrentDictionary<int, string> CircleItems = [];
    public ConcurrentDictionary<int, string[]> Entrances = [];
    public ConcurrentDictionary<string, int> EntranceEarliestCircle = [];
    public string[] InLogicEntrances = [];
    public ulong[] LocationsInLogic = [];
    public string[] LocationNamesInLogic = [];
    public event Action? OnLogicUpdated;
    public ApClient Client;

    private int ProcessId;
    private Dictionary<string, Action<RichTextLabel, string[]>>? CompileEffects;
    private IPrintableObj[] CompiledMessage;
    private int TrackedCount;
    private int CurrentCircle;
    private Action<ItemInfo[], int> OnItemsReceived;
    private Action<ReadOnlyCollection<long>> OnLocationsChecked;
    private Action<Hint[]> OnHintsUpdated;
    private Action<string, bool> OnBoolSaveDataUpdated;
    private Action<string, FilterType> OnFilterDataUpdated;
    private HydraBridgeEntry Entry;
    private string FunctionIdString;
    private string[] ListeningEntrances = [];

    [Signal] public delegate void OnStopCalledEventHandler();

    public void Setup(string name, ApClient client, HydraBridgeEntry entry)
    {
        FunctionIdString = $"Circle_Tracker_{Client?.PlayerName}";
        Client = client;
        Name = name;
        PopoutWindow.Title = name;
        Entry = entry;

        ProcessId = ExternalAppController.StartProcess(name, entry);

        CalculateCircles();
        OnItemsReceived = (_, _) => CallDeferred("CalculateCircles");
        client.ItemHandler.OnNewItemsReceived += OnItemsReceived;

        OnLocationsChecked = _ => QueueUpdate();
        client.CheckedLocationsUpdated += OnLocationsChecked;

        OnHintsUpdated = _ => QueueUpdate();
        client.HintsTrackedEvent += OnHintsUpdated;

        OnBoolSaveDataUpdated = (id, _) =>
        {
            if (id is ShowEmptyCircles or ShowFutureCircles) QueueUpdate();
        };
        SaveType<bool>.OnSaveEvent += OnBoolSaveDataUpdated;

        OnFilterDataUpdated = (_, _) => QueueUpdate();
        SaveType<FilterType>.OnSaveEvent += OnFilterDataUpdated;
        EntranceEffect.OnUpdate += CallReload;
        ItemEffect.OnUpdate += CallReload;
        LocationEffect.OnUpdate += CallReload;
        OnStopCalled += QueueFree;

        NextProgressionLabel.SetPage(this);
    }

    public override void _Process(double delta)
    {
        if (UpdateQueue.IsEmpty) return;
        var recompile = UpdateQueue.Contains(true);
        UpdateQueue.Clear();
        Label.Clear();
        NextProgressionLabel.Clear();

        if (recompile)
        {
            InLogicEntrances = [.. Entrances.SelectMany(kv => kv.Value).DistinctBy(s => s)];
            CompiledMessage = RenderCirclePage().CompileRichText(GetCompileEffects(), true);
            NextProgressionLabel.QueueUiRefresh(true);
            CircleTracker.Singleton.SendTrackerNotify();
            OnLogicUpdated?.Invoke();
        }

        Label.ApplyCompiledPrintableObjs(CompiledMessage);
    }

    public void ListenForEntrances(string[] entrances)
    {
        foreach (var rawEntranceId in entrances)
        {
            var entranceId = rawEntranceId.Split(':')[^1];
            Entry.EntranceKeyMap[entranceId] = rawEntranceId;
            Client.GetFromStorageAsync(
                entranceId, val =>
                {
                    if (!val) return;
                    Entry.EntrancesQueued.Enqueue(entranceId);
                    Client!.RemoveDataStorageListeners(entranceId, FunctionIdString, Scope.Slot);
                }, def: false
            );

            Client!.AddDataStorageListener(
                entranceId, FunctionIdString, (_, newValue, _) =>
                {
                    try
                    {
                        if (!(bool)newValue) return;
                        Entry.EntrancesQueued.Enqueue(entranceId);
                        Client!.RemoveDataStorageListeners(entranceId, FunctionIdString, Scope.Slot);
                    }
                    catch { Client!.RemoveDataStorageListeners(entranceId, FunctionIdString, Scope.Slot); }
                }, Scope.Slot
            );
        }
    }

    public string RenderCirclePage()
    {
        StringBuilder sb = new();
        var font = (int)SaveType<double>.Load(GlobalThemeSettings.GlobalFontSize, 20d);
        List<ulong> recordedLocations = [];
        List<string> recordedEntrances = [];
        var localHints = Client.Hints.Where(hint => hint.FindingPlayer == Client.PlayerSlot).ToArray();
        var hints = localHints.ToDictionary(hint => hint.LocationId, hint => hint.GetItemEffectText());
        var hintImportance = localHints.ToDictionary(
            hint => hint.LocationId, hint => hint.Status is HintStatus.Priority
        );
        var priority = localHints.Where(hint => hint.Status is HintStatus.Priority).Select(hint => hint.LocationId)
                                 .ToArray();
        var firstEnd = SaveType<bool>.Load(ShowFutureCircles, false);
        LocationsInLogic = [.. Circles.Values.SelectMany(arr => arr)];
        LocationNamesInLogic = [.. LocationsInLogic.Select(loc => Client.Locations[(long)loc])];

        foreach (var circle in Circles.Keys.Order())
        {
            if (!Circles.TryGetValue(circle, out var locations))
            {
                GD.PrintErr($"Circle [{circle}] doesn't exist in the circle list??? (maybe race condition)");
                continue;
            }

            var uniqueLocations = locations.Except(recordedLocations).ToArray();
            recordedLocations.AddRange(uniqueLocations);
            uniqueLocations = [.. uniqueLocations.Where(id => Client.MissingRawLocations.Contains((long)id))];

            string[] uniqueEntrances = Entrances.TryGetValue(circle, out var entrances)
                ? [.. entrances.Except(recordedEntrances)] : [];
            recordedEntrances.AddRange(uniqueEntrances);

            if (uniqueLocations.Length == 0 && !SaveType<bool>.Load(ShowEmptyCircles, true)
                                            && uniqueEntrances.Length == 0) continue;

            sb.Append("[center][font_size=").Append(font * (uniqueLocations.Length == 0 ? 1 : 2))
              .Append("]Circle #").Append($"{circle:###,###}").Append("[/font_size]");

            if (uniqueLocations.Length != 0)
                sb.Append(" (").Append($"{uniqueLocations.Length:###,###}").Append(") locations");

            sb.Append("[/center]\n");

            if (CircleItems[circle].Length != 0)
                sb.Append("[center]").Append(CircleItems[circle]).Append("[/center]\n");

            var important = false;
            foreach (var entrance in uniqueEntrances)
            {
                sb.Append("[color=").Append(ColorIdConstants.ColorConstant.EntranceColor.Color().ToHtml()).Append(']')
                  .Append(entrance).Append("[/color]\n");
                important = true;
            }

            if (uniqueLocations.Length != 0)
            {
                var orderedLocations = uniqueLocations
                                      .OrderByDescending(id => priority.Contains((long)id))
                                      .ThenBy(id => Client.Locations[(long)id]).ToArray();

                var use2ndColumn = hints.Keys.Any(id => orderedLocations.Contains((ulong)id));
                sb.Append("[table=").Append(use2ndColumn ? 2 : 1).Append("][cell bg=#00000069] Locations [/cell]");
                if (use2ndColumn) sb.Append("[cell bg=#00000069] Hinted Items [/cell]");

                for (var i = 0; i < orderedLocations.Length; i++)
                {
                    var id = orderedLocations[i];
                    var colColor = i % 2 == 0 ? "[cell bg=#00000044]" : "[cell]";
                    sb.Append(colColor).Append(" {{loc;").Append(id).Append(';').Append(Client.PlayerSlot)
                      .Append("}} [/cell]");
                    if (!use2ndColumn)
                    {
                        important = true;
                        continue;
                    }

                    sb.Append(colColor);
                    if (hints.TryGetValue((long)id, out var item))
                    {
                        sb.Append(item);
                        important = important || hintImportance[(long)id];
                    }
                    else important = true;
                    sb.Append(" [/cell]");
                }
                sb.Append("[/table]\n");
            }
            
            if (!firstEnd && important) break;
        }

        if (sb.ToString().Trim() is "") sb.Append("Super BK :(\nEither that or there was an error from UT");
        return sb.ToString();
    }

    public void CalculateCircles()
    {
        var items = Client.ItemHandler.Items
                          .Where(item => item.Player.Name is "Server"
                                         || (item.Flags.HasFlag(ItemFlags.Advancement)
                                             || item.Flags.HasFlag(ItemFlags.NeverExclude))
                                         && !item.Flags.HasFlag(ItemFlags.Trap)
                           ).ToArray();

        if (CurrentCircle is 0)
        {
            CurrentCircle = 1;
            var start = items.TakeWhile(item => item.Player.Name is "Server" && item.LocationName is "Server")
                             .ToArray();
            QueueCircle(CurrentCircle++, start);
        }

        while (items.Length > TrackedCount) { QueueCircle(CurrentCircle++, [.. items.Take(TrackedCount + 1)]); }
    }

    public void QueueCircle(int circle, params ItemInfo[] items)
    {
        if (!RawCircleItems.ContainsKey(circle))
            RawCircleItems[circle] = [.. items.Skip(TrackedCount).Select(i => i.ItemId)];

        CircleItems[circle] = $"{string.Join(", ", items.Skip(TrackedCount).Select(item => item.GetEffectText()))}";
        Entry.ItemsQueued.Enqueue((circle, [.. items.Select(item => item.ItemId)]));
        TrackedCount = items.Length;
    }

    public void Stop() => EmitSignalOnStopCalled();
    public void QueueUpdate(bool recompile = true) => UpdateQueue.Enqueue(recompile);
    public void CallReload() => QueueUpdate(false);

    private Dictionary<string, Action<RichTextLabel, string[]>> GetCompileEffects()
    {
        if (CompileEffects is not null) return CompileEffects;
        return CompileEffects = MessageParser.CreateEffects(() => CallDeferred("UpdateData", false));
    }

    public void Failure(string text) => Label.Text = $"[color=red]{text}[/color]";

    protected override void Dispose(bool disposing)
    {
        foreach (var entranceId in ListeningEntrances)
            Client!.RemoveDataStorageListeners(entranceId, FunctionIdString, Scope.Slot);

        EntranceEffect.OnUpdate -= CallReload;
        ItemEffect.OnUpdate -= CallReload;
        LocationEffect.OnUpdate -= CallReload;
        ExternalAppController.EndProcess(ProcessId);
        Client?.CheckedLocationsUpdated -= OnLocationsChecked;
        Client?.HintsTrackedEvent -= OnHintsUpdated;
        Client?.ItemHandler.OnNewItemsReceived -= OnItemsReceived;
        SaveType<bool>.OnSaveEvent -= OnBoolSaveDataUpdated;
        SaveType<FilterType>.OnSaveEvent -= OnFilterDataUpdated;
    }
}