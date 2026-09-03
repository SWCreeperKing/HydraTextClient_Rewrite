using System.Linq;
using Godot;
using Godot.Collections;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Utility.Popups;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class TabManager : WindowSetter
{
    [Export] private LineEdit NameEdit;
    [Export] private OptionButton MapNamePicker;
    [Export] private OptionButton TabNamePicker;
    [Export] private OptionButton DestinationPicker;
    [Export] private Array<Control> VisibleTabs;
    private MapLoader Loader;
    private System.Collections.Generic.Dictionary<string, TabContainer> MapTabs => Loader.MapTabs;
    private ManageAction CurrentAction;
    private string[] TabContainers;
    private string[] MapNames;

    public void Setup(MapLoader loader)
    {
        Loader = loader;
        TabContainers = [.. MapTabs.Keys.Order()];
        MapNames = [.. Loader.MapNavigators.Select(m => m.CoreMap.MapName).Order()];

        foreach (var name in MapNames) MapNamePicker.AddItem(name);
        foreach (var name in TabContainers)
        {
            if (name is "") continue;
            TabNamePicker.AddItem(name);
        }
        foreach (var name in TabContainers) DestinationPicker.AddItem(name);
        CallDeferred("Update", 0);
    }

    public void Update(int _)
    {
        for (var i = 0; i < VisibleTabs.Count; i++)
        {
            if (!VisibleTabs[i].IsVisibleInTree())continue;
            CurrentAction = (ManageAction)i;
            break;
        }
        
        var showLineEdit = CurrentAction is ManageAction.AddMap or ManageAction.AddTab;
        var isMapTab = CurrentAction is ManageAction.AddMap or ManageAction.MoveMap or ManageAction.DeleteMap;
        NameEdit.Editable = showLineEdit;
        MapNamePicker.Visible = !showLineEdit && isMapTab;
        TabNamePicker.Visible = !showLineEdit && !isMapTab;
    }

    public string GetTarget()
    {
        var showLineEdit = CurrentAction is ManageAction.AddMap or ManageAction.AddTab;
        var isMapTab = CurrentAction is ManageAction.AddMap or ManageAction.MoveMap or ManageAction.DeleteMap;
        if (showLineEdit) return NameEdit.Text;
        return isMapTab ? MapNames[MapNamePicker.Selected] : TabContainers[TabNamePicker.Selected];
    }

    public string GetDestination() => TabContainers[DestinationPicker.Selected];

    public void CalLConfirmed() => CallDeferred("Confirm");

    private void Confirm()
    {
        var name = GetTarget();
        var destination = GetDestination();

        if (VisibleTabs[0].IsVisibleInTree() || VisibleTabs[3].IsVisibleInTree())
        {
            if (name.Trim() is "")
            {
                MainController.ShowError("The Name field must not be empty");
                return;
            }

            if (MapNames.Contains(name) || TabContainers.Contains(name))
            {
                MainController.ShowError("The Name already exists as something else, you can still mess with the .json if you want to\nBUT BE WARNED: duplicate names WILL cause strange behavior");
                return;
            }
        }


        if (name == destination && CurrentAction is ManageAction.AddTab or ManageAction.MoveTab or ManageAction.DeleteTab)
        {
            MainController.ShowError("Tab Target and Tab Destinations cannot share the same tab");
            return;
        }

        if (!MapTabs.ContainsKey(destination)) destination = "";
        var target = MapTabs[destination];
        TabContainer tab;
        MapNavigator map;
        switch (CurrentAction)
        {
            case ManageAction.AddMap:
                if (Loader.FindMapByName(name) is not null) return;
                Loader.CreateMap(Loader.MapPath, new Maps(name, "", destination));
                break;
            case ManageAction.MoveMap:
                if ((map = Loader.FindMapByName(name)) is null) return;
                map.GetParent().RemoveChild(map);
                target.AddChild(map);
                break;
            case ManageAction.DeleteMap:
                if ((map = Loader.FindMapByName(name)) is null) return;
                Loader.MapNavigators.Remove(map);
                map.GetParent().RemoveChild(map);
                map.QueueFree();
                break;
            case ManageAction.AddTab:
                if (MapTabs.ContainsKey(name)) return;
                tab = MapTabs[name] = new TabContainer();
                tab.SizeFlagsVertical = Control.SizeFlags.ExpandFill;
                tab.Name = name;
                tab.DragToRearrangeEnabled = true;
                tab.TabsRearrangeGroup = 59823532;
                target.AddChild(tab);
                break;
            case ManageAction.MoveTab:
                if (!MapTabs.TryGetValue(name, out tab)) return;
                tab.GetParent().RemoveChild(tab);
                target.AddChild(tab);
                break;
            case ManageAction.DeleteTab:
                if (!MapTabs.TryGetValue(name, out tab)) return;
                foreach (var child in tab.GetChildren())
                {
                    tab.RemoveChild(child);
                    target.AddChild(child);
                }
                tab.GetParent().RemoveChild(tab);
                tab.QueueFree();
                MapTabs.Remove(name);
                break;
        }

        Close();
    }

    public enum ManageAction
    {
        AddMap, MoveMap, DeleteMap,
        AddTab, MoveTab, DeleteTab,
    }
}