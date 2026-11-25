using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.Popups;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class MapAddLocations : WindowSetter
{
    [Export] private Control AllLocationsContainer;
    [Export] private LineEdit SearchBar;
    [Export] private ItemList AllLocations;
    
    [Export] private ItemList LastLocations;
    [Export] private TextEdit NewLocations;
    [Export] private Control ItemListContainer;
    private FuzzySearch Search = new();
    private MapLoader Loader;

    private List<string> LastLocationList => Loader.CollectedLocations;

    [Signal] public delegate void AddLocationsEventHandler(string[] locations);

    public void Setup(MapLoader loader)
    {
        Loader = loader;
        ReloadItemList();
        ReloadAllLocations();
    }

    public void SendLocations()
    {
        string[] locs = [.. NewLocations.Text.Split('\n'), ..  LastLocations.GetSelectedItemNames(), .. AllLocations.GetSelectedItemNames()];
        if (SaveType<bool>.Load("mapAddLocations/clearAllLast", false)) ClearAllLocations();
        else if (SaveType<bool>.Load("mapAddLocations/clearSelectedLast", true)) ClearSelectedLocations();
        EmitSignalAddLocations(locs);
        Close();
    }

    public void ClearSelectedLocations()
    {
        var selected = LastLocations.GetSelectedItemNames();
        LastLocationList.RemoveAll(loc => Enumerable.Contains(selected, loc));
        ReloadItemList();
    }

    public void ClearAllLocations()
    {
        LastLocationList.Clear();
        ReloadItemList();
    }

    public void ReloadItemList()
    {
        LastLocations.Clear();
        ItemListContainer.Visible = LastLocationList.Count > 0;
        if (LastLocationList.Count == 0) return;
        var list = SaveType<bool>.Load("mapAddLocations/SortAlpha", false) ? [.. LastLocationList.Order()]
            : LastLocationList;
        foreach (var loc in list) LastLocations.AddItem(loc);
    }

    public void ReloadAllLocations(string text = "")
    {
        AllLocations.Clear();
        AllLocationsContainer.Visible = Loader.Client is not null;
        if (Loader.Client is null) return;

        var locations = Loader.Client!.Locations.Select(kv => kv.Key).Order().ToArray();
        locations = text.Trim() is "" ? locations
            : [.. Search.SearchAll(text, locations).Select(res => res.Target)];
        
        foreach (var loc in locations) AllLocations.AddItem(loc);
    }
}