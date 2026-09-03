using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using CreepyUtil.Archipelago.ApClient;
using Godot;
using HydraTextClient.Scripts.Clients.TextClient;
using HydraTextClient.Scripts.Clients.TextClient.ParserEffects;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Utilities.ItemFilter;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Clients.CircleTracker;

public partial class TrackerPage : Control
{
    public const string ShowEmptyCircles = "circle_tracker/show_empty";
    public const string ShowFutureCircles = "circle_tracker/spoil_future";
    public const string ShowExcludedLocations = "circle_tracker/exclude_locations";
    private ConcurrentQueue<bool> UpdateQueue = [];

    [Export] private PopoutWindow PopoutWindow;
    [Export] private Label RenderProgressLabel;
    [Export] private AnimatedProgressBar RenderProgressProgress;
    [Export] private ProgressionItemTable NextProgressionLabel;
    [Export] private Control CircleLabelContainer;
    [Export] private PackedScene CircleLabel;

    public ConcurrentDictionary<int, long[]> RawCircleItems = [];
    public ConcurrentDictionary<long, int> NextProgression = [];
    public ConcurrentDictionary<int, ulong[]> Circles = [];
    public ConcurrentDictionary<int, string> CircleItems = [];
    public ConcurrentDictionary<int, string[]> Entrances = [];
    public ConcurrentDictionary<string, int> EntranceEarliestCircle = [];
    public HashSet<ulong> ExcludedLocations = [];
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
    private int LastRenderedCircleProgress = -1;
    private Action<ItemInfo[], int> OnItemsReceived;
    private Action<ReadOnlyCollection<long>> OnLocationsChecked;
    private Action<Hint[], Hint[]> OnHintsUpdated;
    private Action<string, bool> OnBoolSaveDataUpdated;
    private Action<string, FilterType> OnFilterDataUpdated;
    private HydraBridgeEntry Entry;
    private string FunctionIdString;
    private HashSet<string> ListeningEntrances = [];
    private Dictionary<int, CircleTrackerLabel> CircleLabels = [];

    [Signal] public delegate void OnStopCalledEventHandler();

    public void Setup(string name, ApClient client, HydraBridgeEntry entry)
    {
        Client = client;
        Name = name;
        PopoutWindow.Title = name;
        Entry = entry;
        FunctionIdString = $"Circle_Tracker_{Client?.PlayerName}";

        ProcessId = ExternalAppController.StartProcess(name, entry);

        CalculateCircles();
        OnItemsReceived = (_, _) => CallDeferred("CalculateCircles");
        client.ItemHandler.OnNewItemsReceived += OnItemsReceived;

        OnLocationsChecked = _ => QueueUpdate();
        client.CheckedLocationsUpdated += OnLocationsChecked;

        OnHintsUpdated = (_, _) => QueueUpdate();
        client.HintsTrackedEvent += OnHintsUpdated;

        OnBoolSaveDataUpdated = (id, _) =>
        {
            if (id is ShowFutureCircles or ShowExcludedLocations) QueueUpdate();
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
        try
        {
            if (Entry.LastRanCircle != LastRenderedCircleProgress)
            {
                var cur = LastRenderedCircleProgress = Entry.LastRanCircle;
                var total = Math.Max(CircleLabels.Count == 0 ? 0 : CircleLabels.Keys.Max(), cur);
                RenderProgressLabel.Text = $"Circle Calculations [{cur:###,##0}/{total:###,##0}]";
                RenderProgressProgress.SetTarget(cur, total);
            }

            if (UpdateQueue.IsEmpty) return;
            var recompile = UpdateQueue.Contains(true);
            UpdateQueue.Clear();
            NextProgressionLabel.Clear();

            if (!recompile) return;
            InLogicEntrances = [.. Entrances.SelectMany(kv => kv.Value).DistinctBy(s => s)];
            RenderCirclePage();
            NextProgressionLabel.QueueUiRefresh(true);
            CircleTracker.Singleton.SendTrackerNotify();
            OnLogicUpdated?.Invoke();
        }
        catch (Exception e) { GD.PrintErr(e); }
    }

    public void ListenForEntrances(string[] rawEntrances)
    {
        foreach (var rawEntranceId in rawEntrances)
        {
            var entranceId = rawEntranceId.Split(':')[^1];
            Entry.EntranceKeyMap[entranceId] = rawEntranceId;
            ListeningEntrances.Add(entranceId);
            Client!.AddDataStorageListener(
                entranceId, FunctionIdString, (_, newValue, _) =>
                {
                    try
                    {
                        if (!(bool)newValue) return;
                        if (!Entry.EntranceList.Contains(entranceId)) Entry.EntrancesQueued.Enqueue(entranceId);
                        else Client!.RemoveDataStorageListeners(entranceId, FunctionIdString, Scope.Slot);
                    }
                    catch { Client!.RemoveDataStorageListeners(entranceId, FunctionIdString, Scope.Slot); }
                }, Scope.Slot
            );

            Client.GetFromStorageAsync(
                entranceId, val =>
                {
                    if (!val) return;
                    if (!Entry.EntranceList.Contains(entranceId)) Entry.EntrancesQueued.Enqueue(entranceId);
                    else Client!.RemoveDataStorageListeners(entranceId, FunctionIdString, Scope.Slot);
                }, def: false
            );
        }
    }

    public void RenderCirclePage()
    {
        List<ulong> recordedLocations = [];
        List<string> recordedEntrances = [];

        if (!SaveType<bool>.Load(ShowExcludedLocations, true))
        {
            lock (ExcludedLocations) recordedLocations.AddRange(ExcludedLocations);
        }

        var localHints = Client.Hints.Where(hint => hint.FindingPlayer == Client.PlayerSlot).ToArray();
        var hints = localHints.ToDictionary(hint => hint.LocationId, hint => hint.GetItemEffectText());
        var hintImportance = localHints.ToDictionary(
            hint => hint.LocationId, hint => hint.Status is HintStatus.Priority
        );
        var priority = localHints.Where(hint => hint.Status is HintStatus.Priority).Select(hint => hint.LocationId)
                                 .ToArray();
        LocationsInLogic = [.. Circles.Values.SelectMany(arr => arr)];
        LocationNamesInLogic = [.. LocationsInLogic.Select(loc => Client.Locations[(long)loc])];
        var wasImportant = false;

        foreach (var circle in Circles.Keys.Concat(Entrances.Keys).DistinctBy(i => i).Order())
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

            if (!CircleLabels.TryGetValue(circle, out var circleLabel))
            {
                CircleLabels[circle] = circleLabel = CircleLabel.Instantiate<CircleTrackerLabel>();
                CircleLabelContainer.AddChild(circleLabel);
                circleLabel.SetData(circle, Client, CircleItems[circle]);
            }

            wasImportant = circleLabel.UpdateData(
                uniqueEntrances, uniqueLocations, hints, hintImportance, priority, wasImportant
            ) || wasImportant;
        }
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
        if (!RawCircleItems.ContainsKey(circle)) RawCircleItems[circle] = [.. items.Select(i => i.ItemId)];

        CircleItems[circle] = $"{string.Join(", ", items.Skip(TrackedCount).Select(item => item.GetEffectText()))}";
        Entry.ItemsQueued.Enqueue((circle, [.. items.Select(item => item.ItemId)]));
        TrackedCount = items.Length;
    }

    public void Stop() => EmitSignalOnStopCalled();
    public void QueueUpdate(bool recompile = true) => UpdateQueue.Enqueue(recompile);
    public void CallReload() => QueueUpdate(false);

    protected override void Dispose(bool disposing)
    {
        if (Client is not null)
        {
            Client.UpdateConnection();
            foreach (var entranceId in ListeningEntrances)
            {
                Client?.RemoveDataStorageListeners(entranceId, FunctionIdString, Scope.Slot);
            }
            Client?.CheckedLocationsUpdated -= OnLocationsChecked;
            Client?.HintsTrackedEvent -= OnHintsUpdated;
            Client?.ItemHandler.OnNewItemsReceived -= OnItemsReceived;
        }

        EntranceEffect.OnUpdate -= CallReload;
        ItemEffect.OnUpdate -= CallReload;
        LocationEffect.OnUpdate -= CallReload;
        ExternalAppController.EndProcess(ProcessId);
        SaveType<bool>.OnSaveEvent -= OnBoolSaveDataUpdated;
        SaveType<FilterType>.OnSaveEvent -= OnFilterDataUpdated;
    }
}