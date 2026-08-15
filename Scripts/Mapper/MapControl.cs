using System;
using Godot;

namespace HydraTextClient.Scripts.Mapper;

public partial class MapControl : Control
{
    [Export] public float ZoomSpeed = .1f;
    [Export] public ScrollContainer ScrollContainer;
    [Export] public TextureRect MapImage;

    public float Zoom
    {
        get => _Zoom;
        set
        {
            CustomMinimumSize = MapImage.Size * value;
            MapImage.Scale = new Vector2(value, value);
            _Zoom = value;
        }
    }

    private float _Zoom;
    private Vector2 LastMouse;
    private bool Dragging;
    private bool ToResetZoom;

    public override void _Ready() => Zoom = 1;
    public void ResetZoom() => ToResetZoom = true;

    public override void _Process(double delta)
    {
        if (!ToResetZoom) return;

        var rawZoom = ScrollContainer.Size / MapImage.Texture.GetSize();
        Zoom = Math.Min(rawZoom.X, rawZoom.Y) - .01f;
        ToResetZoom = false;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!IsVisibleInTree()) return;
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
            if (!Dragging) Dragging = true;
            else posDelta -= mousePos - LastMouse;
            LastMouse = mousePos;
        }

        if (posDelta.X != 0) ScrollContainer.ScrollHorizontal += (int)posDelta.X;
        if (posDelta.Y != 0) ScrollContainer.ScrollVertical += (int)posDelta.Y;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!IsVisibleInTree()) return;
        if (@event is not InputEventMouseButton mouse) return;
        switch (mouse.ButtonIndex)
        {
            case MouseButton.WheelDown: Zoom -= ZoomSpeed * Zoom; break;
            case MouseButton.WheelUp: Zoom += ZoomSpeed * Zoom; break;
        }
    }
}