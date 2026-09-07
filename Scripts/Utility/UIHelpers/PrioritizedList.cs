using System.Collections.Concurrent;
using System.Linq;
using Godot;

namespace HydraTextClient.Scripts.Utility.UIHelpers;

public partial class PrioritizedList : ScrollContainer
{
    [Export] private VBoxContainer ListView;
    private ConcurrentQueue<Control> ControlsToAdd = [];

    public override void _Process(double delta)
    {
        if (ControlsToAdd.IsEmpty) return;
        ControlsToAdd.TryDequeue(out var control);

        HBoxContainer mainContainer = new();
        VBoxContainer leftContainer = new();
        VBoxContainer rightContainer = new();

        leftContainer.Alignment = BoxContainer.AlignmentMode.Center;
        rightContainer.Alignment = BoxContainer.AlignmentMode.Center;

        Button upButton = new();
        Button downButton = new();
        Button deleteButton = new();

        upButton.Text = "^";
        upButton.Pressed += () =>
        {
            var index = mainContainer.GetIndex();
            if (index is 0) return;
            ListView.CallDeferred("move_child", mainContainer, index - 1);
        };

        downButton.Text = "v";
        downButton.Pressed += () =>
        {
            var index = mainContainer.GetIndex();
            if (index == ListView.GetChildren().Count - 1) return;
            ListView.CallDeferred("move_child", mainContainer, index + 1);
        };

        deleteButton.Text = "X";
        deleteButton.Pressed += () => ListView.CallDeferred("remove_child", mainContainer);

        leftContainer.AddChild(upButton);
        leftContainer.AddChild(downButton);
        rightContainer.AddChild(deleteButton);

        mainContainer.AddChild(leftContainer);
        mainContainer.AddChild(control);
        mainContainer.AddChild(rightContainer);
        ListView.AddChild(mainContainer);
    }

    public void AddItems(params Control[] controls)
    {
        foreach (var control in controls) ControlsToAdd.Enqueue(control);
    }

    public T[] GetItems<T>() where T : Control
        => [.. ListView.GetChildren().Select(child => child.GetChild(1)).OfType<T>()];


    public void CallClear() => CallDeferred("Clear");
    private void Clear()
    {
        foreach (var child in ListView.GetChildren().ToArray())
        {
            ListView.RemoveChild(child);
            child.QueueFree();
        }
    }
}