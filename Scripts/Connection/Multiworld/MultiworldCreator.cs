using Godot;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Utility.DataTypes;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.Popups;
using HydraTextClient.Scripts.Utility.UIHelpers;
using static HydraTextClient.Scripts.Controllers.ConnectionController;

namespace HydraTextClient.Scripts.Connection.Multiworld;

public partial class MultiworldCreator : WindowSetter
{
    [Export] private MultiworldLabel TemporaryLabel;
    [Export] private Control LabelContainer;
    [Export] private Label CurrentMultiWorldLabel;

    [ExportGroup("Internal")] [Export] private LineEdit Address;
    [Export] private LineEdit Port;
    [Export] private LineEdit Password;
    [Export] private LineEdit MultiworldName;
    [Export] private ListAdder ListAdder;

    private MultiworldData? EditData;

    public override void _Ready()
    {
        CurrentMultiworld = SaveType<string>.Load("CurrentMultiworld", null);
        if (CurrentMultiworld is null || !SaveType<MultiworldData>.ContainsKey(CurrentMultiworld))
            SetWorld(TemporaryLabel);
        else SetWorld(CurrentMultiworld);

        if (SaveType<MultiworldData>.ContainsKey(TemporaryLabel.MultiWorldName)) return;
        var def = new MultiworldData { WorldName = TemporaryLabel.MultiWorldName };
        SaveType<MultiworldData>.Save(def.WorldName, def, false);
        CloseCalled += ClearData;
    }

    public MultiworldData GenDataFromFields()
    {
        var mw = new MultiworldData
        {
            WorldName = MultiworldName.Text is not "" ? MultiworldName.Text : TemporaryLabel.MultiWorldName,
            Address = Address.Text is not "" ? Address.Text : "archipelago.gg",
            Port = Port.Text is not "" ? Port.Text : "12345", Password = Password.Text,
            DeathLinkGroups = ListAdder.GetItems(),
        };

        if (EditData is not null)
        {
            mw.SlotNames = EditData.SlotNames;
            mw.CheckCounts = EditData.CheckCounts;
            mw.CheckCountsChecked = EditData.CheckCountsChecked;
        }

        return mw;
    }

    public void SetWorld(MultiworldLabel label) => SetWorld(label.MultiWorldName);

    private void SetWorld(string world)
    {
        if (LockMultiworld)
        {
            MainController.ShowConfirm(
                "Log out of all slots?",
                "You are currently logged into Multiple slots\nDisconnect all of them and switch worlds?",
                () =>
                {
                    foreach (var client in GetClientNames()) TryConnect(client);
                    SetWorld(world);
                }
            );
            return;
        }

        ForceDataClear();
        CurrentMultiworld = world;
        CurrentMultiWorldLabel.Text = $"Current Multiworld: {world}";
        SaveType<string>.Save("CurrentMultiworld", CurrentMultiworld, false);
        MainController.Save();
    }

    public void EditWorld(MultiworldLabel label)
    {
        var data = SaveType<MultiworldData>.Load(label.MultiWorldName, new MultiworldData());
        MultiworldName.Text = data.WorldName;
        Address.Text = data.Address;
        Port.Text = data.Port;
        Password.Text = data.Password;
        ListAdder.Clear();
        ListAdder.AddGroups(data.DeathLinkGroups);
        EditData = data;
        Show();
    }

    public void ClearWorld(MultiworldLabel label)
    {
        var data = SaveType<MultiworldData>.Load(label.MultiWorldName, new MultiworldData());
        data.ClearCache();
    }

    public void DeleteWorld(MultiworldLabel label)
    {
        if (CurrentMultiworld == label.MultiWorldName) SetWorld(TemporaryLabel);
        SaveType<MultiworldData>.Delete(label.MultiWorldName);
        Close();
    }

    private void SetAsTemp()
    {
        var data = GenDataFromFields();
        data.WorldName = TemporaryLabel.MultiWorldName;
        SaveType<MultiworldData>.Save(TemporaryLabel.MultiWorldName, data, false);
        Close();
    }

    public void AddMultiworld(string _) => AddMultiworld();

    public void AddMultiworld()
    {
        var data = GenDataFromFields();
        if (data.WorldName == TemporaryLabel.MultiWorldName)
        {
            SetAsTemp();
            return;
        }

        if (SaveType<MultiworldData>.TryGet(data.WorldName, out var oldWorld))
        {
            var newWorld = GenDataFromFields();
            oldWorld.Address = newWorld.Address;
            oldWorld.Port = newWorld.Port;
            oldWorld.Password = newWorld.Password;
            oldWorld.DeathLinkGroups = newWorld.DeathLinkGroups;
            SaveType<MultiworldData>.Save(data.WorldName, oldWorld, true);
        }
        else SaveType<MultiworldData>.Save(data.WorldName, GenDataFromFields(), true);
        Close();
    }

    public void ClearData()
    {
        MultiworldName.Text = "";
        Address.Text = "";
        Port.Text = "";
        Password.Text = "";
        ListAdder.Clear();
        EditData = null;
    }
}