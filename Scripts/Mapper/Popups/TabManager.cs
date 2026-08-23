using Godot;
using HydraTextClient.Scripts.Utility.Popups;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class TabManager : WindowSetter
{
    [Export] private OptionButton Action;
    [Export] private LineEdit NameEdit;
    [Export] private LineEdit Destination;

    [Signal] public delegate void ConfirmActionEventHandler(ManageAction action, string name, string destination);
    
    public void Confirm()
    {
        if (NameEdit.Text.Trim() == "") return;
        EmitSignalConfirmAction((ManageAction)Action.Selected, NameEdit.Text, Destination.Text);
        Close();
    }
    
    public enum ManageAction
    {
        AddMap,
        MoveMap,
        DeleteMap,
        AddTab,
        MoveTab,
        DeleteTab,
    }
}