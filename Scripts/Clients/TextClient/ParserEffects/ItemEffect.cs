using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net.Enums;
using Godot;
using HydraTextClient.Scripts.Utilities.ItemFilter;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.DataTypes;
using HydraTextClient.Scripts.Utility.Loaders;
using static HydraTextClient.Scripts.Utility.ColorIdConstants.ColorConstant;

// {{item;``game name``;``item name``}}
// {{item;``game name``;``item name``;item flags}}
namespace HydraTextClient.Scripts.Clients.TextClient.ParserEffects;

public class ItemEffect : MessageParserEffect
{
    public const string SaveId = "Clients/TextClient/TextEffects/ItemMessageEffect";
    public const string FallbackSaveId = "Clients/TextClient/TextEffects/ItemMessageHideFallback";
    public const string Default = "[{{img}}{{name}}]";
    public const string Hint = "{{img}} - Custom Assets/Fallback/Custom Item Image Override\n{{name}} - name of the item";
    public override string Key => "item";

    public static event Action? OnUpdate;

    public override void Effect(RichTextLabel label, string[] args, Action? reloadFunction = null)
    {
        try
        {
            var argList = args.ToList();
            while (!argList[0].EndsWith("``"))
            {
                argList[0] = $"{argList[0]};{argList[1]}";
                argList.RemoveAt(1);
            }

            while (!argList[1].EndsWith("``"))
            {
                argList[1] = $"{argList[1]};{argList[2]}";
                argList.RemoveAt(2);
            }
            args = [.. argList];
        }
        catch (IndexOutOfRangeException)
        {
            label.AddText("[Invalid Item Tag]");
            return;
        }

        if (args.Length < 2 || reloadFunction is null)
        {
            label.AddText("[Invalid Item Tag]");
            return;
        }

        args[0] = args[0][2..^2];
        args[1] = args[1][2..^2];

        label.PushContext();
        if (args.Length > 2 && int.TryParse(args[2], out var flagsRaw))
        {
            var flags = (ItemFlags)flagsRaw;
            if ((int)flags == 3) flags = ItemFlags.Advancement;
            var ft = SaveType<FilterType>.Load(FilterType.MakeUID(args[1], args[0], flags), default, false);

            label.PushMeta((string[])["itemfilter", ..args]);
            if (ft.IsSpecial)
            {
                label.PushColor(SpecialItemColor.Color());
                label.PushBgcolor(SpecialItemBackgroundColor.Color());
            }
            else
            {
                label.PushColor(flags.GetColorFromItemFlag());
                label.PushBgcolor(flags.GetBgColorFromItemFlag());
            }
        }
        label.ApplyCompiledPrintableObjs(
            SaveType<string>.Load(SaveId, Default).CompileRichText(
                new Dictionary<string, Action<RichTextLabel, string[]>>
                {
                    ["img"] = (l, _) =>
                    {
                        var img = CustomAssets.ItemImage(
                            args[0], args[1], args[0], _ => reloadFunction(), out var isFallback
                        );
                        if (isFallback && SaveType<bool>.Load(FallbackSaveId, false)) return;
                        l.AddImage(img, 0, 20);
                    },
                    ["name"] = (l, _) => l.AddText(args[1]),
                }, false
            )
        );
        label.PopContext();
    }

    public override void AddValueUpdater()
    {
        GameItemImageLoader.OnReload += () => OnUpdate?.Invoke();
        SaveType<string>.AddIndividualEvent(SaveId, _ => OnUpdate?.Invoke());
        SaveType<bool>.AddIndividualEvent(FallbackSaveId, _ => OnUpdate?.Invoke());
        SaveType<HexColor>.OnSaveEvent += (id, _) =>
        {
            if (!ColorIdConstants.IdToConstant.TryGetValue(id, out var constant)) return;
            if (constant.IsItemColor()) OnUpdate?.Invoke();
        };
    }
}