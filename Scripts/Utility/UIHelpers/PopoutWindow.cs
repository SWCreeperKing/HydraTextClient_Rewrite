using Godot;
using HydraTextClient.Scripts.Utility.Popups;

namespace HydraTextClient.Scripts.Utility.UIHelpers;

public partial class PopoutWindow : Control
{
	[Export] public string Title;
	[Export] private Control Child;
	[Export] private bool RestoreSize = true;
	[Export] private ButtonAnimation PopoutButton;
	
	[Signal] public delegate void PoppedOutEventHandler();
	[Signal] public delegate void PoppedInEventHandler();
	
	private WindowSetter Window;
	private Control WindowContainer;
	private LayoutPreset Preset;
	private int LayoutMode;

	public override void _Ready()
	{
		Window = new WindowSetter();
		Window.Title = Title;
		Window.WindowPosition = Godot.Window.WindowInitialPosition.Absolute;
		Window.ToQueueFree = false;
		Window.BlockParent = false;
		Window.CloseCalled += () =>
		{
			WindowContainer.CallDeferred("remove_child", Child);
			CallDeferred("add_child", Child);
			CallDeferred("move_child", Child, 0);

			if (RestoreSize) Child.Size = Size;
			Child.LayoutMode = LayoutMode;
			Child.SetAnchorsPreset(Preset);
			Child.Position = Vector2.Zero;
			EmitSignalPoppedIn();
		};

		Panel backdrop = new();
		backdrop.SetAnchorsPreset(LayoutPreset.FullRect);
		
		WindowContainer = new Control();
		WindowContainer.SetAnchorsPreset(LayoutPreset.FullRect);
		WindowContainer.SizeFlagsHorizontal = SizeFlags.ExpandFill;
		WindowContainer.SizeFlagsVertical = SizeFlags.ExpandFill;
			
		backdrop.AddChild(WindowContainer);
		Window.AddChild(backdrop);
		AddChild(Window);

		Preset = (LayoutPreset)Child.AnchorsPreset;
		LayoutMode = Child.LayoutMode;
	}

	public void Popout()
	{
		Window.Size = (Vector2I)Size;
		Window.Position = GetViewport().GetWindow().Position;

		RemoveChild(Child);
		WindowContainer.AddChild(Child);
		
		Window.Show();
		EmitSignalPoppedOut();
	}
	
	public void ToggleWindow()
	{
		if (Window.Visible) Window.Close();
		else Popout();
	}

	public void HideButton() => PopoutButton.Visible = false;
	public void ShowButton() => PopoutButton.Visible = false;
}