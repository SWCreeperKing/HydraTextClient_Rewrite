using System.Collections.Generic;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class EntranceManagerPopup : SelectionEditWindow<string[]> // name : id
{
    [Export] private ItemList EntranceTable;
    [Export] private LineEdit AddEntranceName;
    [Export] private LineEdit AddEntranceID;
    [Export] private LineEdit EditEntranceName;
    [Export] private LineEdit EditEntranceID;
    [Export] private LineEdit EditEntranceNickName;
    private string[][] EntranceList;
    private MapLoader Loader;

    public void Setup(MapLoader loader)
    {
        Loader = loader;
        EntranceTable.ItemActivated += l =>
        {
            if (l is -1 || l % 2 is not 0) return;
            SwitchToEdit(EntranceList[l / 2]);
        };
        ReloadData();
    }

    public void AddDataFromPage()
    {
        AddData([AddEntranceName.Text, AddEntranceID.Text, ""]);
        AddEntranceName.Clear();
        AddEntranceID.Clear();
        ReloadData();
    }

    public void AddData(string[] data)
    {
        if (data[1].Trim() is "") data[1] = data[0];
        Loader.EntranceMap[data[1]] = data[0];
        Loader.EntranceNicknames[data[1]] = data[2];
    }

    protected override void EditData(string[] data)
    {
        EditEntranceName.Text = data[0];
        EditEntranceID.Text = data[1];
        EditEntranceNickName.Text = data[2];
    }

    protected override void SaveData(string[] data)
    {
        DeleteData(data);
        AddData([EditEntranceName.Text, EditEntranceID.Text, EditEntranceNickName.Text.Trim()]);
        ReloadData();
    }

    protected override void DeleteData(string[] data)
    {
        Loader.EntranceMap.Remove(data[1]);
        Loader.EntranceNicknames.Remove(data[1]);
    }

    public override void ReloadData()
    {
        EntranceList = [.. Loader.EntranceMap.Select(kv => (string[])[kv.Value, kv.Key, Loader.EntranceNicknames.GetValueOrDefault(kv.Key, "")]).OrderBy(t => t[0])];
        EntranceTable.Clear();
        foreach (var item in EntranceList)
        {
            var entrance = item[0];
            var id = item[1];
            EntranceTable.AddItem(entrance);
            EntranceTable.AddItem(id == entrance ? "" : id);
        }
    }
}