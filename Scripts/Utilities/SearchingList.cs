using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace HydraTextClient.Scripts.Utilities;

public partial class SearchingList : Control
{
    [Export] private string SearchText;
    [Export] public ItemList List;
    [Export] public LineEdit SearchBar;
    [Export] private CheckBox Box;
    public ConcurrentQueue<(int, Texture2D)> ImageQueue = [];
    private FuzzySearch Search = new();
    private List<string> Items = [];
    public Func<string[], string, bool> VisibilitySetter = (searchRes, item) => searchRes.Contains(item);

    [Signal] public delegate void OnItemPressedEventHandler(string item);

    [Signal] public delegate void OnItemCreatedEventHandler(ItemList list, int index, string item);

    public override void _Ready()
    {
        SearchBar.PlaceholderText = SearchText;
        List.ItemClicked += (index, _, mouseButton) =>
        {
            if (mouseButton is not (int)MouseButton.Left) return;
            EmitSignalOnItemPressed(List.GetItemText((int)index));
        };
    }

    public override void _Process(double delta)
    {
        while (!ImageQueue.IsEmpty)
        {
            ImageQueue.TryDequeue(out var t);
            List.SetItemIcon(t.Item1, t.Item2);
        }
    }

    public void SetItems(params string[] items)
    {
        Items.Clear();
        List.Clear();
        ImageQueue.Clear();
        Items = [.. items.Distinct().Order()];
        for (var i = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            List.AddItem(item);
            List.SetItemSelectable(i, false);
            EmitSignalOnItemCreated(List, i, item);
        }
    }

    protected void SetItemsSearch(string[] searchRes)
    {
        List.Clear();
        ImageQueue.Clear();
        for (int i = 0, j = 0; i < Items.Count; i++)
        {
            var item = Items[i];
            if (!VisibilitySetter(searchRes, item)) continue;
            List.AddItem(item);
            List.SetItemSelectable(j, false);
            EmitSignalOnItemCreated(List, j, item);
            j++;
        }
    }

    public void RemoveItems(params string[] items)
    {
        foreach (var item in items)
        {
            List.RemoveItem(Items.IndexOf(item));
            Items.Remove(item);
        }
    }

    public void UpdateSearch(string text)
    {
        var results = text.Trim() is "" ? Items.ToArray()
            : Search.SearchAll(text, Items.ToArray()).Select(res => res.Target).ToArray();

        SetItemsSearch(results);
    }

    public void SetupBox(Action<CheckBox> action) => action(Box);
    public void RefreshList() => UpdateSearch(SearchBar.Text);
}