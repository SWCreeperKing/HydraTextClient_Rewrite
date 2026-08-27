using Godot;

namespace HydraTextClient.Scripts.Utility;

public partial class Highlighter : ColorRect
{
    [Export] public Color Idle = Colors.Transparent;
    [Export] public Color Hover = Colors.AliceBlue;
    [Export] public Control? HigherPower;
    [Export] public bool Selectable;
    [Export] public bool DetectRightClick;
    private double Timer;
    private Tween Tween;
    private bool Selected;
    private bool IsIn;

    [Signal] public delegate void OnSelectedEventHandler();

    [Signal] public delegate void OnUnSelectedEventHandler();

    [Signal] public delegate void OnEnteredEventHandler();

    [Signal] public delegate void OnExitedEventHandler();

    [Signal] public delegate void OnRightClickEventHandler();

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

    public void Select()
    {
        Enter();
        Selected = true;
        EmitSignalOnSelected();
    }

    public void Enter()
    {
        IsIn = true;
        if (Selectable && Selected) return;
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
        try { EmitSignalOnExited(); }
        catch { }
        Tween?.Kill();
        Tween = CreateTween();
        Tween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        Tween.TweenProperty(this, "color", Idle, 1);
    }

    public void OnGuiInput(InputEvent @event)
    {
        if (!Selectable) return;
        if (!IsIn)
        {
            if (!Selected) Exit();
            return;
        }

        if (@event is not InputEventMouseButton button) return;
        if (!button.Pressed) return;
        if (button.ButtonIndex is MouseButton.Right && DetectRightClick) EmitSignalOnRightClick();
        if (button.ButtonIndex is not MouseButton.Left) return;

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