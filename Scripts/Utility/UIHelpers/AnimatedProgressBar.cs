using Godot;

namespace HydraTextClient.Scripts.Utility.UIHelpers;

public partial class AnimatedProgressBar : ProgressBar
{
    [Export] private double Duration = 2;
    [Export] private Tween.EaseType Ease = Tween.EaseType.Out;
    [Export] private Tween.TransitionType Trans = Tween.TransitionType.Quad;

    private double Target
    {
        get => TargetPercent;
        set
        {
            TargetPercent = value;
            RefreshTween = true;
        }
    }

    private double TargetPercent;
    private bool RefreshTween;
    private Tween? ProgressTween;

    public override void _Ready()
    {
        MinValue = 0;
        MaxValue = 1000;
    }

    public override void _Process(double delta)
    {
        if (!RefreshTween) return;
        RefreshTween = false;
        ProgressTween?.Kill();
        ProgressTween = CreateTween();
        ProgressTween!.SetEase(Ease);
        ProgressTween!.SetTrans(Trans);
        ProgressTween!.TweenProperty(this, "value", TargetPercent, Duration);
    }

    public void SetTarget(double current, double max) => Target = current / max * 1000;
    public void SetTarget(double linearPercent) => Target = linearPercent * 1000;
}