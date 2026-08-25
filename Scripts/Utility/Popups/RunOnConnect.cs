using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Utility.Loaders;

namespace HydraTextClient.Scripts.Utility.Popups;

public partial class RunOnConnect : WindowSetter
{
    [Export] private VBoxContainer Container;
    [Export] private Button RunAll;
    [Export] private Button RunSelected;
    private Dictionary<CoreAppEntry, bool> ToRun = [];

    public void SetupEntries(CoreAppEntry[] entries, Action<CoreAppEntry[]> entriesToRun)
    {
        RunSelected.Pressed += () => entriesToRun([.. ToRun.Where(kv => kv.Value).Select(kv => kv.Key)]);
        RunAll.Pressed += () => entriesToRun(entries);
        
        RunSelected.Pressed += Close;
        RunAll.Pressed += Close;

        foreach (var entry in entries) AddEntry(entry);
    }
    
    public void AddEntry(CoreAppEntry entry)
    {
        var exists = entry.FileExists();
        var savedHash = SaveType<string>.Load($"PROG:HASH/{entry.Executable}", "", false);
        
        RichTextLabel label = new();
        label.BbcodeEnabled = true;
        label.FitContent = true;
        label.AutowrapMode = TextServer.AutowrapMode.Off;
        label.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        label.Text = entry.ShortName;
        if (!exists) label.Text += "\n[color=red]File not Found[/color]";
        if (entry.Hash is not "")
        {
            if (savedHash is "") label.Text += "\n[color=cyan]First Time Running[/color]";
            else if (entry.Hash != savedHash) label.Text += "\n[color=orange]File Changed[/color]";
            else label.Text += "\n[color=green]No Changes[/color]";
        }
        
        CheckBox check = new();
        check.Text = "Run";
        check.Toggled += b => ToRun[entry] = b;
        if (!exists) check.Disabled = true;
        
        HBoxContainer box = new();
        box.AddChild(label);
        box.AddChild(check);
        Container.AddChild(box);
    }
}