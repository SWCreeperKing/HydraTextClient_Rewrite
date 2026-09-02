using System.Collections.Generic;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.Popups;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class MapAddLocations : WindowSetter
{
    [Export] private ItemList LastLocations;
    [Export] private TextEdit NewLocations;
    [Export] private Control ItemListContainer;
    private MapLoader Loader;

    private List<string> LastLocationList => Loader.CollectedLocations;

    [Signal] public delegate void AddLocationsEventHandler(string[] locations);

    public void Setup(MapLoader loader)
    {
        Loader = loader;
        ReloadItemList();
    }

    public void SendLocations()
    {
        var locs = NewLocations.Text.Split('\n');
        if (LastLocationList.Count > 0) locs = [.. locs, .. GetSelectedLocations()];
        if (SaveType<bool>.Load("mapAddLocations/clearAllLast", false)) ClearAllLocations();
        else if (SaveType<bool>.Load("mapAddLocations/clearSelectedLast", true)) ClearSelectedLocations();
        EmitSignalAddLocations(locs);
        Close();
    }

    public void ClearSelectedLocations()
    {
        var selected = GetSelectedLocations();
        LastLocationList.RemoveAll(loc => selected.Contains(loc));
        ReloadItemList();
    }

    public void ClearAllLocations()
    {
        LastLocationList.Clear();
        ReloadItemList();
    }

    public string[] GetSelectedLocations() => [.. LastLocations.GetSelectedItems().Select(LastLocations.GetItemText)];

    public void ReloadItemList()
    {
        LastLocations.Clear();
        ItemListContainer.Visible = LastLocationList.Count > 0;
        if (LastLocationList.Count == 0) return;
        var list = SaveType<bool>.Load("mapAddLocations/SortAlpha", false) ? LastLocationList
            : [.. LastLocationList.Order()];
        foreach (var loc in list) LastLocations.AddItem(loc);
    }
}