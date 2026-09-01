using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Godot;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Utility.DataTypes;
using HydraTextClient.Scripts.Utility.Loaders;

namespace HydraTextClient.Scripts.Connection.Multiworld;

public partial class MwLabelContainer : VBoxContainer
{
    [Export] private MultiworldCreator Creator;
    [Export] private PackedScene DataLabel;

    public static ConcurrentDictionary<string, MultiworldLabel> Labels = [];

    public override void _Ready()
    {
        var mwDatas = SaveType<MultiworldData>.GetKeys();
        foreach (var data in mwDatas)
        {
            var mwData = SaveType<MultiworldData>.Load(data, new MultiworldData());
            if (mwData is null)
            {
                SaveType<MultiworldData>.Delete(data);
                continue;
            }

            CreateLabel(mwData);
        }
        SaveType<MultiworldData>.OnSaveEvent += (_, data) => CreateLabel(data);
    }

    public void CreateLabel(MultiworldData data)
    {
        if (Labels.ContainsKey(data.WorldName) || data.WorldName is "Temporary Multiworld") return;
        var label = DataLabel.Instantiate<MultiworldLabel>();
        label.MultiWorldName = data.WorldName;
        label.SetWorld += () => Creator.SetWorld(label);
        label.EditWorld += () => Creator.EditWorld(label);
        label.ClearWorld += () => Creator.ClearWorld(label);
        label.DeleteWorld += () =>
        {
            Labels.Remove(data.WorldName, out _);
            Creator.DeleteWorld(label);
            RemoveChild(label);
            label.QueueFree();
        };

        AddChild(label);
        Labels[data.WorldName] = label;
    }
}