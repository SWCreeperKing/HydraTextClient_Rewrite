using System;
using System.Collections.Concurrent;
using System.Linq;
using CreepyUtil.Archipelago.ApClient;
using Godot;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Settings;
using HydraTextClient.Scripts.Utility.DataTypes;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.Popups;

namespace HydraTextClient.Scripts.Connection.Slots;

public partial class SlotPortrait : TextureRect
{
    [ExportGroup("Internal"), Export] private Texture2D UnknownPortrait;
    [Export] private TextureRect Portrait;
    [Export] private RichTextLabel SlotNameLabel;
    [Export] private PackedScene RunOnCommandPopup;

    [ExportGroup("Internal - CheckCount"), Export]
    private PanelContainer CheckCountPanel;

    [Export] private RichTextLabel CheckCountLabel;
    [Export] private ProgressBar CheckProgressBar;

    [ExportGroup("Internal - Tinter"), Export]
    private ColorRect Tinter;

    [Export] private Color IdleTint;
    [Export] private Color ConnectingTint;
    [Export] private Color ConnectedTint;
    [Export] private Color ErrorTint;

    [Signal] public delegate void OnPortraitLeftClickedEventHandler(string slotName);

    [Signal] public delegate void OnPortraitRightClickedEventHandler(string slotName);

    public string SlotName;
    public string GameName;
    public ConcurrentQueue<(int, int)> UpdateCheckCounts = [];
    private Vector2 PortraitSize = new(150, 225);
    private Action<string, int, int> CheckAction;
    private Action ClearCheckCountOnDisconnect;
    private Action<string, ApClient, bool> RunOnConnectAction;
    private Action<string, ApClient, bool> OnDisconnect;
    private Tween ColorTween;
    private Tween ScaleTween;
    private ConcurrentBag<int> ProcessIds = [];
    private ConnectionStatus CurrentStatus = ConnectionStatus.NotConnected;

    public override void _Ready()
    {
        CheckAction = (slot, amount, max) =>
        {
            var mw = ConnectionController.GetCurrentMultiworld;
            if (mw is null) return;
            var player = mw.GetSlotName(slot);
            var thisPlayer = mw.GetSlotName(SlotName);
            if (thisPlayer != player)
            {
                if (mw.CheckCounts.TryGetValue(thisPlayer, out max)
                    && mw.CheckCountsChecked.TryGetValue(thisPlayer, out amount))
                    UpdateCheckCounts.Enqueue((amount, max));

                return;
            }
            UpdateCheckCounts.Enqueue((amount, max));
        };

        ClearCheckCountOnDisconnect = () => CheckCountPanel.Visible = false;
        RunOnConnectAction = (slot, _, _) =>
        {
            var mw = ConnectionController.GetCurrentMultiworld;
            if (mw is null) return;

            var player = mw.GetSlotName(slot);
            var thisPlayer = mw.GetSlotName(SlotName);
            if (thisPlayer != player) return;

            var data = SaveType<SlotGameData>.Load(SlotName, null, false);
            if (data is null) return;
            var commands =
                data.ProcessCommands
                    .Select(command => command.Trim()).Where(command => command is not "")
                    .Select(command => command.Replace("{{port}}", mw.Port).Replace("{{add}}", mw.Address)
                                              .Replace(
                                                   "{{ap}}",
                                                   SaveType<string>.Load(GlobalThemeSettings.ApDir, "", false)
                                               ).Replace("{{slot}}", slot)
                                              .Replace("{{pass}}", mw.GetPassword(slot)).Split(' ')
                     ).Where(args => args.Any(arg => arg is not ("{{mw}}" or "{{hydra}}")))
                    .Select(args =>
                         {
                             var context = args[0];
                             args = [.. args.Where(arg => arg is not ("{{mw}}" or "{{hydra}}"))];
                             if (args.Length == 0) return null;

                             if (args[0].StartsWith('"'))
                             {
                                 while (!args[0].EndsWith('"') && args.Length > 1)
                                 {
                                     args[0] = $"{args[0]} {args[1]}";

                                     if (args.Length > 2) args = [args[0], ..args[2..]];
                                     else args = [args[0]];
                                 }

                                 if (!args[0].EndsWith('"')) return null;
                             }

                             return new ReadOnlyEntry(
                                 args[0].Replace("\"", ""), string.Join(' ', args.Length > 1 ? args[1..] : []), context
                             );
                         }
                     ).Where(entry => entry is not null).ToArray();

            if (commands.Length == 0) return;

            var popup = RunOnCommandPopup.Instantiate<RunOnConnect>();
            popup.SetupEntries(
                commands, toRun =>
                {
                    foreach (var entry in toRun)
                    {
                        SaveType<string>.Save($"PROG:HASH/{entry.Executable}", entry.Hash, false);
                        var id = ExternalAppController.StartProcess(slot, entry);
                        if (id is -1 or 404) return;
                        switch (((ReadOnlyEntry)entry).Context)
                        {
                            case "{{mw}}": ConnectionController.ProcessIds.Add(id); break;
                            case "{{hydra}}": break;
                            default: ProcessIds.Add(id); break;
                        }
                    }
                }
            );
            GetParent().GetParent().CallDeferred("add_child", popup);
            popup.CallDeferred("show");
        };

        OnDisconnect = (slot, _, _) =>
        {
            var mw = ConnectionController.GetCurrentMultiworld;
            if (mw is null) return;

            var player = mw.GetSlotName(slot);
            var thisPlayer = mw.GetSlotName(SlotName);
            if (thisPlayer != player) return;

            if (ProcessIds.IsEmpty) return;
            foreach (var id in ProcessIds) ExternalAppController.EndProcess(id);
        };

        CheckCountPanel.Visible = false;
        SetScale((float)SaveType<double>.Load("Connection/SlotsMenu/PortraitScale", 1f));
        ConnectionController.OnCheckCountUpdated += CheckAction;
        ConnectionController.OnFullDisconnection += ClearCheckCountOnDisconnect;
        ConnectionController.OnClientConnection += RunOnConnectAction;
        ConnectionController.OnClientRemoved += OnDisconnect;
        Reload();
    }

    public override void _Process(double delta)
    {
        while (!UpdateCheckCounts.IsEmpty)
        {
            UpdateCheckCounts.TryDequeue(out var t);
            UpdateCheckCount(t.Item1, t.Item2);
        }
    }

    public void Reload()
    {
        if (!SaveType<SlotGameData>.TryGet(SlotName, out var data))
        {
            QueueFree();
            return;
        }

        SlotNameLabel.Text = SlotName;
        Portrait.Texture = GamePortraitLoader.Singleton.GetOrDef(GameName = data.Game, UnknownPortrait);
    }

    public void SetScale(float scale)
    {
        var newSize = PortraitSize * scale;
        ScaleTween?.Kill();
        ScaleTween = CreateTween();
        ScaleTween.SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
        ScaleTween.TweenProperty(this, "custom_minimum_size", newSize, .7f);
        ScaleTween.Parallel().TweenProperty(this, "size", newSize, .7f);
        SetSize(newSize);
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton button) return;
        if (!button.Pressed) return;
        switch (button.ButtonIndex)
        {
            case MouseButton.Left: EmitSignalOnPortraitLeftClicked(SlotName); break;
            case MouseButton.Right
                when CurrentStatus is ConnectionStatus.NotConnected or ConnectionStatus.Error:
                EmitSignalOnPortraitRightClicked(SlotName); break;
        }
    }

    public void SetStatus(ConnectionStatus status) => CallDeferred("TweenStatus", (int)status);

    private void TweenStatus(int intStatus)
    {
        CurrentStatus = (ConnectionStatus)intStatus;
        ColorTween?.Kill();
        ColorTween = CreateTween();
        ColorTween.SetTrans(Tween.TransitionType.Circ).SetEase(Tween.EaseType.Out);
        switch (CurrentStatus)
        {
            case ConnectionStatus.Connecting:
                ColorTween.TweenProperty(Tinter, "color", ConnectingTint, 1);
                ColorTween.SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.In);
                ColorTween.TweenProperty(Tinter, "color", IdleTint, 1);
                ColorTween.SetLoops();
                break;
            case ConnectionStatus.NotConnected or ConnectionStatus.Connected or ConnectionStatus.Error:
                ColorTween.TweenProperty(
                    Tinter, "color",
                    CurrentStatus switch
                    {
                        ConnectionStatus.NotConnected => IdleTint, ConnectionStatus.Connected => ConnectedTint,
                        ConnectionStatus.Error => ErrorTint,
                    }, 1
                ); break;
        }
    }

    private void UpdateCheckCount(int count, int max)
    {
        CheckCountPanel.Visible = false;
        if (!ConnectionController.HasLeaderClient) return;
        var mw = ConnectionController.GetCurrentMultiworld;
        if (mw is null) return;

        var slot = mw.GetSlotName(SlotName);
        var leader = ConnectionController.LeaderClient;
        if (!leader!.PlayerNames.Contains(slot)) return;

        CheckCountPanel.Visible = true;
        CheckCountLabel.Text = $"{count:###,##0}/{max:###,##0}";
        CheckProgressBar.Value = (float)count / max;
    }

    protected override void Dispose(bool disposing)
    {
        ConnectionController.OnCheckCountUpdated -= CheckAction;
        ConnectionController.OnFullDisconnection -= ClearCheckCountOnDisconnect;
        ConnectionController.OnClientConnection -= RunOnConnectAction;
        ConnectionController.OnClientRemoved -= OnDisconnect;
    }
}

public enum ConnectionStatus
{
    NotConnected, Connecting, Connected,
    Error
}