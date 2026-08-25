using System.Collections.Generic;
using System.Linq;
using Godot;

namespace HydraTextClient.Scripts.Utility.UIHelpers;

public partial class ListAdder : FoldableContainer
{
    [Export] private LineEdit GroupName;
    [Export] private Control GroupContainer;
    [Export] private Script Script;

    private static readonly Color ButtonColor = new("#ff434a");
    private Dictionary<string, HBoxContainer> Groups = [];

    public void AddGroups(string[] groups)
    {
        foreach (var group in groups) AddGroup(group);
    }

    public void AddGroup() => AddGroup(null);

    public void AddGroup(string name)
    {
        var group = name ?? GroupName.Text;
        if (group.Trim() is "") return;
        if (Groups.ContainsKey(group)) return;

        HBoxContainer container = new();
        Label label = new();
        ButtonAnimation button = new();

        label.Text = group;
        label.SizeFlagsHorizontal = SizeFlags.ExpandFill;

        button.Text = "Remove";
        button.Pressed += () => RemoveGroup(group);
        button.Modulate = ButtonColor;

        container.AddChild(label);
        container.AddChild(button);
        container.GetChild(1).SetScript(Script);
        GroupContainer.AddChild(container);
        Groups[group] = container;

        GroupName.Text = "";
    }

    public void RemoveGroup(string group)
    {
        GroupContainer.RemoveChild(Groups[group]);
        Groups.Remove(group);
    }

    public string[] GetItems() => [.. Groups.Keys];
    
    public void Clear()
    {
        foreach (var group in GetItems()) RemoveGroup(group);
    }
}