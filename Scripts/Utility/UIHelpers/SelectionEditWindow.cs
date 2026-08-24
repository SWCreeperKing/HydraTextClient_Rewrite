using Godot;
using HydraTextClient.Scripts.Utility.Popups;

namespace HydraTextClient.Scripts.Utility.UIHelpers;

public abstract partial class SelectionEditWindow<T> : WindowSetter
{
    [Export] private TabContainer MainContainer;
    private T SelectedData;

    public override void _Ready()
    {
        MainContainer.SetCurrentTab(0);
        MainContainer.TabsVisible = false;
    }

    protected void SwitchToEdit(T data)
    {
        if (!DataCheck(data, out var newData)) return;
        SelectedData = newData;
        EditData(newData);
        MainContainer.SetCurrentTab(1);
    }

    public void SaveAndSwitchBack()
    {
        SaveData(SelectedData);
        SwitchBack();
    }

    public void DeleteAndSwitchBack()
    {
        DeleteData(SelectedData);
        SwitchBack();
    }

    public void SwitchBack()
    {
        SelectedData = default;
        ReloadData();
        MainContainer.SetCurrentTab(0);
    }

    protected abstract bool DataCheck(T dataIn, out T dataOut);
    protected abstract void EditData(T data);
    protected abstract void SaveData(T data);
    protected abstract void DeleteData(T data);
    public abstract void ReloadData();
}