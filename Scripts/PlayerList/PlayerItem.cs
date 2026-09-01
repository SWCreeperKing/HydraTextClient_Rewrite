using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Godot;
using HydraTextClient.Scripts.Clients.TextClient;
using HydraTextClient.Scripts.Clients.TextClient.ParserEffects;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.PlayerList;

public partial class PlayerItem : PanelContainer
{
    [Export] private Gradient CheckGradient;
    [Export] private Color GoalColor;
    [Export] private RichTextLabel Player;
    [Export] private EmptyRichLabelInteractor CheckCounter;
    [Export] private Label CheckIndicator;
    [Export] private ProgressBar CheckProgress;
    [Export] private TextureRect ConnectedIndicator;
    [Export] private TextureRect DisconnectedIndicator;
    [Export] private TextureRect GoalIndicator;
    [Export] private LineEdit Alias;
    [Export] private LineEdit CopyAlias;

    public ConcurrentQueue<(int, int)> ReloadCheckCounts = [];
    private Dictionary<string, Action<RichTextLabel, string[]>> Effects;
    private string PlayerText;
    private bool Goaled;
    private Tween ProgressTween;
    private Tween CheckGainTween;
    private int LastCount = -1;
    private int PlayerSlot;

    public override void _Ready()
    {
        CheckProgress.Modulate = CheckGradient.Sample(0);
        Effects = MessageParser.CreateEffects(() => CallDeferred("UpdatePlayerText"));
    }

    public override void _Process(double delta)
    {
        while (!ReloadCheckCounts.IsEmpty)
        {
            ReloadCheckCounts.TryDequeue(out var t);
            SetCheckCount(t.Item1, t.Item2);
        }
    }

    public void SetPlayer(int player)
    {
        PlayerSlot = player;
        PlayerText = $" {{{{player;{player}}}}}";

        Alias.TextChanged += s =>
        {
            var mw = ConnectionController.GetCurrentMultiworld;
            if (mw is null) return;
            if (s.Trim() is "") mw.PlayerAliases.TryRemove(player, out _);
            else mw.PlayerAliases[player] = s;
            PlayerEffect.UpdatePlayerEffect();
        };

        CopyAlias.TextChanged += s =>
        {
            var mw = ConnectionController.GetCurrentMultiworld;
            if (mw is null) return;
            mw.PlayerCopyAliases[player] = s;
        };

        UpdateCopyText();
        UpdatePlayerText();
        SetCheckCount();
    }

    public void UpdateCopyText()
    {
        var mw = ConnectionController.GetCurrentMultiworld;
        if (mw is not null && mw.PlayerAliases.TryGetValue(PlayerSlot, out var alias)) Alias.Text = alias;
        if (mw is not null && mw.PlayerCopyAliases.TryGetValue(PlayerSlot, out var copyAlias))
            CopyAlias.Text = copyAlias;
    }

    public void UpdatePlayerText()
    {
        Player.Clear();
        Player.ApplyCompiledPrintableObjs(PlayerText.CompileRichText(Effects, false));
    }

    private void SetCheckCount(int count = 0, int max = 0)
    {
        if (max <= 0 && !Goaled)
        {
            CheckProgress.Visible = false;
            CheckCounter.Text = "";
            return;
        }

        CheckProgress.Visible = true;

        var normalized = (double)count / max;
        if (Goaled)
        {
            CheckCounter.Text = max == 0 ? "Goaled "
                : $"[hint=text {count:###,##0}/{max:###,##0} ({normalized * 100d:#00.00}%)]Goaled[/hint] ";
            if (LastCount == -2) return;
            ProgressTween?.Kill();
            ProgressTween = CreateTween();
            ProgressTween.SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
            SetConnected(null);
            GoalIndicator.Visible = true;

            ProgressTween.TweenProperty(CheckProgress, "value", 100, 1);
            ProgressTween.Parallel().TweenProperty(CheckProgress, "modulate", GoalColor, 1);
            LastCount = -2;
            return;
        }

        if (LastCount == count) return;
        LastCount = count;
        ProgressTween?.Kill();
        ProgressTween = CreateTween();
        ProgressTween.SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.Out);
        CheckCounter.Text = $"{count:###,##0}/{max:###,##0} ({normalized * 100d:#00.00}%)";

        ProgressTween.TweenProperty(CheckProgress, "value", normalized * 100d, 1);
        ProgressTween.Parallel()
                     .TweenProperty(CheckProgress, "modulate", CheckGradient.Sample((float)normalized), 1);

        CheckGainTween?.Kill();
        CheckGainTween = CreateTween();
        CheckGainTween.SetTrans(Tween.TransitionType.Expo).SetEase(Tween.EaseType.InOut);
        CheckGainTween.TweenProperty(CheckIndicator, "modulate:a", 0, 3).From(1);
    }

    public void HasGoaled()
    {
        if (Goaled) return;
        Goaled = true;
        SetCheckCount();
    }

    public void SetConnected(bool? isConnected)
    {
        if (isConnected is null) DisconnectedIndicator.Visible = ConnectedIndicator.Visible = false;
        else DisconnectedIndicator.Visible = !(ConnectedIndicator.Visible = isConnected!.Value);
    }
}