using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.DataTypes;
using HydraTextClient.Scripts.Utility.Loaders;

namespace HydraTextClient.Scripts.Connection.Slots;

public partial class SlotView : MarginContainer
{
    private static SlotView Singleton;
    private const string UseStrictSearch = "Connection/SlotsMenu/useStrictSearch"; // bool
    private const string SearchType = "Connection/SlotsMenu/searchType"; // int

    [ExportGroup("Internal")] [Export] private HFlowContainer MainSlotContainer;
    [Export] private HFlowContainer SubSlotContainer;
    [Export] private PackedScene SlotPortraitScene;
    [Export] private OptionButton LeaderChanger;
    [Export] private SpinBox ScaleBox;
    [Export] private Texture2D UnknownGame;
    [Export] private LineEdit SlotSearch;

    [Signal] public delegate void EditPortraitEventHandler(string slotName);

    [Signal] public delegate void AddNewPortraitEventHandler();

    private FuzzySearch SearchAlg = new();
    private Dictionary<string, SlotPortrait> Portraits = [];
    private string[] OrderedSlots = [];

    public override void _Ready()
    {
        Singleton = this;

        SaveType<bool>.AddIndividualEvent(UseStrictSearch, _ => ReOrganizeSlots());
        SaveType<int>.AddIndividualEvent(SearchType, _ => ReOrganizeSlots());
        SlotSearch.TextChanged += _ => ReOrganizeSlots();

        var portraitData = SaveType<SlotGameData>.GetKeys();
        foreach (var key in portraitData) CreatePortrait(SaveType<SlotGameData>.Load(key, new SlotGameData()), false);
        SaveType<SlotGameData>.OnSaveEvent += (_, data) => CreatePortrait(data, true);
        SaveType<SlotGameData>.OnDeleteEvent += (key, _) =>
        {
            Portraits[key].QueueFree();
            Portraits.Remove(key);
        };
        ConnectionController.OnClientConnection += (_, _, _) => CallDeferred("ReOrganizeSlots");
        ConnectionController.OnClientConnection += (_, _, _) => CallDeferred("UpdateLeaderBox");
        ConnectionController.OnClientRemoved += (_, _, _) => CallDeferred("ReOrganizeSlots");
        ConnectionController.OnClientRemoved += (_, _, _) => CallDeferred("UpdateLeaderBox");
        ConnectionController.OnClientLeaderChanged += (_, _) => CallDeferred("UpdateLeaderBox");

        ReOrganizeSlots();
        LeaderChanger.GetPopup().AddThemeConstantOverride("icon_max_width", 14);
    }

    public override void _Process(double delta)
        => LeaderChanger.Disabled = ConnectionController.IsConnecting || ConnectionController.GetConnectionCooldown > 0;

    public override void _GuiInput(InputEvent @event)
    {
        if (@event is not InputEventMouseButton button || !button.IsPressed()) return;
        if (!Input.IsKeyPressed(Key.Ctrl)) return;

        if (button.ButtonIndex is MouseButton.WheelUp) ScaleBox.Increment();
        if (button.ButtonIndex is MouseButton.WheelDown) ScaleBox.Decrement();
    }

    private void UpdateLeaderBox()
    {
        LeaderChanger.Clear();
        var names = ConnectionController.GetClientNames();
        if (names.Length == 0) return;

        OrderedSlots = [.. names.Order()];
        var leader = names[0];
        var index = Array.IndexOf(OrderedSlots, leader);

        var games = Portraits.ToDictionary(kv => kv.Key, kv => kv.Value.GameName);

        foreach (var player in OrderedSlots)
        {
            LeaderChanger.AddIconItem(
                GamePortraitLoader.Singleton.GetOrDef(
                    games[ConnectionController.HasReceipt(player) ? ConnectionController.GetReceipt(player) : player],
                    UnknownGame
                ), player
            );
        }

        LeaderChanger.Selected = index;
    }

    public void ChangeLeader(int index) => ConnectionController.ChangeLeaderClient(OrderedSlots[index]);

    public void CreatePortrait(SlotGameData data, bool reorg)
    {
        if (Portraits.TryGetValue(data.Name, out var value)) value.Reload();
        else
        {
            var portrait = SlotPortraitScene.Instantiate<SlotPortrait>();
            portrait.SlotName = data.Name;
            portrait.OnPortraitRightClicked += EmitSignalEditPortrait;
            portrait.OnPortraitLeftClicked += ConnectionController.TryConnect;
            Portraits[portrait.SlotName] = portrait;
        }
        ReOrganizeSlots();
    }

    public void ChangeFontSize(float size)
    {
        var sizeInt = (int)size;
        SaveType<double>.Save("Connection/SlotsMenu/PortraitFontSize", sizeInt, false);
        foreach (var slot in Portraits.Values) slot.SetFontSize(sizeInt);
    }

    public void ChangePortraitScale(float scale)
    {
        SaveType<double>.Save("Connection/SlotsMenu/PortraitScale", scale, false);
        foreach (var slot in Portraits.Values) slot.SetScale(scale);
    }

    public void ReOrganizeSlots()
    {
        foreach (var child in MainSlotContainer.GetChildren()) MainSlotContainer.RemoveChild(child);
        foreach (var child in SubSlotContainer.GetChildren()) SubSlotContainer.RemoveChild(child);

        var mw = ConnectionController.GetCurrentMultiworld;
        var leader = ConnectionController.LeaderClient;

        var searchText = SlotSearch.Text.Trim();
        var searchType = SaveType<int>.Load(SearchType, 0);
        foreach (var rawSlot in Portraits.Keys.Order())
        {
            var slot = rawSlot;
            var isSub = false;

            if (mw is not null && leader is not null)
            {
                var names = leader!.PlayerNames;
                if (!(names.Contains(slot) || names.Contains(mw!.GetSlotName(slot)))) isSub = true;
                slot = mw!.GetSlotName(slot);
            }

            if (!isSub && searchText is not "")
            {
                var slotNameMatches = IsMatch(searchText, slot);
                var gameNameMatches = IsMatch(searchText, Portraits[rawSlot].GameName);

                switch (searchType)
                {
                    case 0:
                        if (!slotNameMatches) isSub = true;
                        break;
                    case 1:
                        if (!gameNameMatches) isSub = true;
                        break;
                    case 2:
                        if (!slotNameMatches || !gameNameMatches) isSub = true;
                        break;
                    case 3:
                        if (!slotNameMatches && !gameNameMatches) isSub = true;
                        break;
                }
            }

            if (!isSub) MainSlotContainer.AddChild(Portraits[rawSlot]);
            else SubSlotContainer.AddChild(Portraits[rawSlot]);
        }
    }

    public bool IsMatch(string searchText, string candidate)
    {
        if (SaveType<bool>.Load(UseStrictSearch, false)) return candidate.Contains(searchText, StringComparison.CurrentCultureIgnoreCase);
        return SearchAlg.SearchAll(searchText, [candidate]).Count > 0;
    }

    public static bool ContainsSlot(string name)
    {
        var mw = ConnectionController.GetCurrentMultiworld;
        if (Singleton.Portraits.ContainsKey(name)) return true;
        return mw is not null && mw!.SlotNames.Values.Contains(name);
    }

    public void CallAddNew() => EmitSignalAddNewPortrait();
    public static SlotPortrait Portrait(string name) => Singleton.Portraits[name];
    public static void SetPortraitStatus(string name, ConnectionStatus status) => Portrait(name).SetStatus(status);
}