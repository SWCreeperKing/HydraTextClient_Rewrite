using System;
using Godot;

namespace HydraTextClient.Scripts.Utility;

public partial class Highlighter : ColorRect
{
    [Export] public Color Idle = Colors.Transparent;
    [Export] public Color Hover = Colors.AliceBlue;
    [Export] public Control? HigherPower;
    [Export] public bool Selectable;
    public Func<bool>? InterruptEvents;
    private double Timer;
    private Tween Tween;
    private bool Selected;
    private bool IsIn;

    [Signal] public delegate void OnSelectedEventHandler();
    [Signal] public delegate void OnUnSelectedEventHandler();
    [Signal] public delegate void OnEnteredEventHandler();
    [Signal] public delegate void OnExitedEventHandler();

    public override void _Ready()
    {
        if (HigherPower is not null)
        {
            HigherPower.MouseEntered += Enter;
            HigherPower.MouseExited += Exit;
            HigherPower.GuiInput += OnGuiInput;
            MouseFilter = MouseFilterEnum.Pass;
            return;
        }
        MouseEntered += Enter;
        MouseExited += Exit;
        GuiInput += OnGuiInput;
    }

    public void Enter()
    {
        IsIn = true;
        if (Selectable && Selected) return;
        if (InterruptEvents is not null && InterruptEvents()) return;
        EmitSignalOnEntered();
        Tween?.Kill();
        Tween = CreateTween();
        Tween.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        Tween.TweenProperty(this, "color", Hover, 1);
    }

    public void Exit()
    {
        IsIn = false;
        if (Selectable && Selected) return;
        if (InterruptEvents is not null && InterruptEvents()) return;
        EmitSignalOnExited();
        Tween?.Kill();
        Tween = CreateTween();
        Tween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        Tween.TweenProperty(this, "color", Idle, 1);
    }

    public void OnGuiInput(InputEvent @event)
    {
        if (InterruptEvents is not null && InterruptEvents()) return;
        if (!Selectable) return;
        if (!IsIn)
        {
            ResetPressed();
            return;
        }
        if (@event is not InputEventMouseButton button) return;
        if (!button.Pressed || button.ButtonIndex is not MouseButton.Left) return;

        Selected = !Selected;
        if (Selected) EmitSignalOnSelected();
        else EmitSignalOnUnSelected();
    }

    public void ResetPressed()
    {
        Selected = false;
        Exit();
    }
}