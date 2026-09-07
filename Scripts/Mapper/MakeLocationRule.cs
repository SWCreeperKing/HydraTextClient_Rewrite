using System;
using Godot;
using HydraTextClient.Scripts.Utility;

namespace HydraTextClient.Scripts.Mapper;

public partial class MakeLocationRule : VBoxContainer
{
    [Export] private OptionButton SlotDataType;
    [Export] private TabContainer CompareContainer;
    [Export] private LineEdit SlotDataVariable;
    [Export] private OptionButton ActionChosen;

    [Export, ExportGroup("Edit Groups/Bool SDV")]
    private CheckBox IsVariableTrue;

    [Export, ExportGroup("Edit Groups/Number SDV")]
    private OptionButton Operator;

    [Export] private SpinBox ValueCompare;
    [Export] private TextEdit ValueOptions;

    [Export, ExportGroup("Edit Groups/String SDV")]
    private CheckBox MatchAny;

    [Export] private TextEdit StringCompareData;

    private string[] Images;

    public void Setup(string[] images, MapItemImageLoader itemImageLoader)
    {
        Images = images;
        CompareContainer.SetCurrentTab(0);
        Operator.ItemSelected += l =>
        {
            ValueCompare.Visible = l is not 6;
            ValueOptions.Visible = l is 6;
        };

        ActionChosen.Clear();
        ActionChosen.GetPopup().AddThemeConstantOverride("icon_max_width", 14);
        foreach (var name in Images)
        {
            if (itemImageLoader.TryGet(name, out var img)) ActionChosen.AddIconItem(img, name);
            else ActionChosen.AddItem(name);
        }
    }

    public void SetData(LocationRule rule)
    {
        var varType = (int)rule.StoreType;
        SlotDataType.Selected = varType;
        CompareContainer.SetCurrentTab(varType);

        ActionChosen.Selected = Math.Max(rule.Action is "" ? 0 : Images.IndexOf(rule.Action), 0);
        IsVariableTrue.ButtonPressed = rule.BoolCompare; // bool
        Operator.Selected = rule.CompareType.ToSelected(); // number
        ValueCompare.Value = rule.NumberCompare;
        MatchAny.ButtonPressed = rule.MatchAny; // string
        SlotDataVariable.Text = rule.DataKey;
        StringCompareData.Text = string.Join('\n', rule.DataCompare ?? []);
        ValueCompare.Visible = rule.NumberCompare is not 6;
        ValueOptions.Visible = rule.NumberCompare is 6;
    }

    public LocationRule GetData() => new()
    {
        StoreType = (LocationRule.DataStorageType)SlotDataType.Selected,
        BoolCompare = IsVariableTrue.ButtonPressed, // bool
        CompareType = Operator.Selected switch
        {
            0 => LocationRule.NumberCompareType.NotEqualTo, 1 => LocationRule.NumberCompareType.EqualTo,
            2 => LocationRule.NumberCompareType.GreaterThan,
            3 => LocationRule.NumberCompareType.GreaterThan | LocationRule.NumberCompareType.EqualTo,
            4 => LocationRule.NumberCompareType.LessThan,
            5 => LocationRule.NumberCompareType.LessThan | LocationRule.NumberCompareType.EqualTo,
            6 => LocationRule.NumberCompareType.AnyOf,
        }, // number
        NumberCompare = ValueCompare.Value, MatchAny = MatchAny.ButtonPressed, // string
        DataCompare = StringCompareData.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries),
        DataKey = SlotDataVariable.Text, Action = ActionChosen.Selected is 0 ? "" : Images[ActionChosen.Selected - 1],
    };
}