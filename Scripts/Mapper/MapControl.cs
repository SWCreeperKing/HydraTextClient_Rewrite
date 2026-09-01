using System;
using Godot;

namespace HydraTextClient.Scripts.Mapper;

public partial class MapControl : Control
{
    [Export] public float ZoomSpeed = .09f;
    [Export] public ScrollContainer ScrollContainer;
    [Export] public TextureRect MapImage;
    private double ScrollXPercent = -1;
    private double ScrollYPercent = -1;

    [Signal] public delegate void OnRightClickEventHandler();

    public float Zoom
    {
        get => _Zoom;
        set
        {
            _Zoom = value;
            CallDeferred("UpdateZoom");
        }
    }

    private float _Zoom;
    private Vector2 LastMouse;
    private bool Dragging;
    private bool ToResetZoom;

    public bool IsDragging => Dragging;
    public override void _Ready() => Zoom = 1;
    public void ResetZoom() => ToResetZoom = true;

    public override void _Process(double delta)
    {
        if (!ToResetZoom) return;
        if (!IsVisibleInTree()) return;

        var rawZoom = ScrollContainer.Size / MapImage.Texture.GetSize();
        Zoom = Math.Min(rawZoom.X, rawZoom.Y) - .01f;
        ToResetZoom = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsVisibleInTree()) return;

        if (ScrollXPercent is not -1 && ScrollYPercent is not -1)
        {
            CallDeferred("SetScrollPercent", ScrollXPercent, ScrollYPercent);
            ScrollXPercent = -1;
            ScrollYPercent = -1;
        }

        var leftButton = Input.IsMouseButtonPressed(MouseButton.Left);
        switch (leftButton)
        {
            case false when Dragging: Dragging = false; break;
            case false: return;
        }

        Vector2 posDelta = new();
        if (leftButton)
        {
            var mousePos = GetGlobalMousePosition();
            if (!ScrollContainer.GetGlobalRect().HasPoint(mousePos)) Dragging = false;
            else
            {
                if (!Dragging) Dragging = true;
                else posDelta -= mousePos - LastMouse;
                LastMouse = mousePos;
            }
        }

        if (posDelta.X != 0) ScrollContainer.ScrollHorizontal += (int)posDelta.X;
        if (posDelta.Y != 0) ScrollContainer.ScrollVertical += (int)posDelta.Y;
    }

    public override void _Input(InputEvent @event)
    {
        if (!IsVisibleInTree()) return;
        if (@event is not InputEventMouseButton mouse) return;
        if (mouse.ButtonIndex is MouseButton.Right && mouse.Pressed
                                                   && ScrollContainer.GetGlobalRect()
                                                                     .HasPoint(GetGlobalMousePosition()))
            EmitSignalOnRightClick();
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsVisibleInTree()) return;
        if (@event is not InputEventMouseButton mouse) return;
        if (!ScrollContainer.GetGlobalRect().HasPoint(GetGlobalMousePosition())) return;
        switch (mouse.ButtonIndex)
        {
            case MouseButton.WheelDown:
                Zoom -= ZoomSpeed * Zoom;
                AcceptEvent();
                break;
            case MouseButton.WheelUp:
                Zoom += ZoomSpeed * Zoom;
                AcceptEvent();
                break;
        }
    }

    public void SetScrollPercent(double xPercent, double yPercent)
    {
        var scrollX = ScrollContainer.GetHScrollBar();
        var scrollY = ScrollContainer.GetVScrollBar();

        // page is the grabber size x.x
        // its /2f to grab the center of the grabber
        ScrollContainer.ScrollHorizontal = (int)(scrollX.MaxValue * xPercent - scrollX.Page / 2f);
        ScrollContainer.ScrollVertical = (int)(scrollY.MaxValue * yPercent - scrollY.Page / 2f);
    }

    private void UpdateZoom()
    {
        var scrollX = ScrollContainer.GetHScrollBar();
        var scrollY = ScrollContainer.GetVScrollBar();
        ScrollXPercent = (ScrollContainer.ScrollHorizontal + scrollX.Page / 2f) / scrollX.MaxValue;
        ScrollYPercent = (ScrollContainer.ScrollVertical + scrollY.Page / 2f) / scrollY.MaxValue;
        CustomMinimumSize = MapImage.Size * _Zoom;
        MapImage.Scale = new Vector2(_Zoom, _Zoom);
    }
}