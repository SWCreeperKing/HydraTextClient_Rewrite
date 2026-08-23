using System;
using System.Collections.Generic;
using System.Linq;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using Godot;
using HydraTextClient.Scripts.Clients.TextClient;
using HydraTextClient.Scripts.Clients.TextClient.ParserEffects;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Mapper;
using HydraTextClient.Scripts.Utilities.ItemFilter;
using HydraTextClient.Scripts.Utility.DataTypes;
using HydraTextClient.Scripts.Utility.Loaders;
using static Archipelago.MultiClient.Net.Enums.ItemFlags;
using static HydraTextClient.Scripts.Utility.ColorIdConstants;
using static HydraTextClient.Scripts.Utility.ColorIdConstants.ColorConstant;
using Color = Godot.Color;

namespace HydraTextClient.Scripts.Utility;

public static class Extensions // sorted alphabetically for the memes
{
    public static Dictionary<ItemFlags, int> ItemToSortIdCache = new();

    extension(ColorConstant constant)
    {
        public Color Color() => SaveType<HexColor>.Load(constant.SaveId(), constant.DefaultColor());
        public Color DefaultColor() => ConstantToDefaultColor[constant];
        public string SaveId() => ConstantToId[constant];

        public void Save(Color color, bool broadcast = true)
            => SaveType<HexColor>.Save(constant.SaveId(), color, broadcast);

        public HexColor Load(bool broadcast = false)
            => SaveType<HexColor>.Load(constant.SaveId(), constant.DefaultColor(), broadcast);

        public bool IsPlayerColor()
            => constant is PlayerConnected or PlayerListedNonConnected or PlayerNonConnected or ServerColor;

        public bool IsItemColor() => constant is NormalItemColor or ProgressiveItemColor or TrapItemColor
            or UsefulItemColor or SpecialItemColor or NormalItemBackgroundColor or ProgressiveItemBackgroundColor
            or TrapItemBackgroundColor or UsefulItemBackgroundColor or SpecialItemBackgroundColor;
    }

    extension(Control control)
    {
        public void SetFontSize(int fontSize, string name = "font_size")
        {
            if (control.HasThemeFontSizeOverride(name)) control.RemoveThemeFontSizeOverride(name);
            control.AddThemeFontSizeOverride(name, fontSize);
        }
    }

    extension(Hint hint)
    {
        public string? ItemGame => ConnectionController.HasLeaderClient
            ? ConnectionController.LeaderClient!.PlayerGames[hint.ReceivingPlayer] : null;

        public string? ItemName => ConnectionController.HasLeaderClient
            ? ConnectionController.LeaderClient!.ItemIdToItemName(hint.ItemId, hint.ReceivingPlayer) : null;

        public string? LocationName => ConnectionController.HasLeaderClient
            ? ConnectionController.LeaderClient!.LocationIdToLocationName(hint.LocationId, hint.FindingPlayer) : null;

        public string UID => FilterType.MakeUID(hint.ItemName, hint.ItemGame, hint.ItemFlags);
        public string EntranceName => hint.Entrance == "" ? "Vanilla" : hint.Entrance;

        public string GetItemEffectText()
            => $"{{{{item;``{hint.ItemGame}``;``{hint.ItemName}``;{(int)hint.ItemFlags}}}}}";

        public int GetHash() => HashCode.Combine(
            hint.FindingPlayer, hint.LocationId, hint.ReceivingPlayer, hint.Entrance, hint.ItemFlags
        );

        public int SortNumber()
        {
            if (SaveType<FilterType>.TryGet(hint.UID, out var filter) && filter.IsSpecial) return -1;
            return hint.ItemFlags.SortNumber();
        }
    }

    extension(HintPrintJsonPacket packet)
    {
        public int FindingPlayer => packet.Item.Player;
        public bool FinderIsReceiver => packet.FindingPlayer == packet.ReceivingPlayer;

        public string ItemGame => ConnectionController.HasLeaderClient
            ? ConnectionController.LeaderClient!.PlayerGames[packet.ReceivingPlayer] : "Unknown";

        public string ItemName => ConnectionController.HasLeaderClient
            ? ConnectionController.LeaderClient!.ItemIdToItemName(packet.Item.Item, packet.ReceivingPlayer) : "Unknown";

        public string UID
        {
            get
            {
                var leader = ConnectionController.LeaderClient!;
                var receiver = packet.ReceivingPlayer;
                return FilterType.MakeUID(
                    leader.ItemIdToItemName(packet.Item.Item, receiver), leader.PlayerGames[receiver], packet.Item.Flags
                );
            }
        }

        public string GetItemEffectText()
            => $"{{{{item;``{packet.ItemGame}``;``{packet.ItemName}``;{(int)packet.Item.Flags}}}}}";

        public string GetLocationEffectText() => $"{{{{loc;{packet.Item.Location};{packet.FindingPlayer}}}}}";
        public string GetLocationName() => LocationEffect.LocationName(packet.Item.Location, packet.FindingPlayer);
        public string GetFoundEffectText() => $"{{{{{(packet.Found ?? false ? "found" : "notfound")}}}}}";
    }

    extension(ItemCheatPrintJsonPacket packet)
    {
        public int FindingPlayer => packet.Item.Player;
        public bool FinderIsReceiver => packet.FindingPlayer == packet.ReceivingPlayer;

        public string ItemGame => ConnectionController.HasLeaderClient
            ? ConnectionController.LeaderClient!.PlayerGames[packet.ReceivingPlayer] : "Unknown";

        public string ItemName => ConnectionController.HasLeaderClient
            ? ConnectionController.LeaderClient!.ItemIdToItemName(packet.Item.Item, packet.ReceivingPlayer) : "Unknown";

        public string UID
        {
            get
            {
                var leader = ConnectionController.LeaderClient!;
                var receiver = packet.ReceivingPlayer;
                return FilterType.MakeUID(
                    leader.ItemIdToItemName(packet.Item.Item, receiver), leader.PlayerGames[receiver], packet.Item.Flags
                );
            }
        }

        public string GetItemEffectText()
            => $"{{{{item;``{packet.ItemGame}``;``{packet.ItemName}``;{(int)packet.Item.Flags}}}}}";

        public string GetLocationEffectText() => $"{{{{loc;{packet.Item.Location};{packet.FindingPlayer}}}}}";
        public string GetLocationName() => LocationEffect.LocationName(packet.Item.Location, packet.FindingPlayer);
    }

    extension(ItemFlags flags)
    {

        public Color GetColorFromItemFlag()
        {
            if (flags.HasFlag(Advancement)) return ProgressiveItemColor.Color();
            if (flags.HasFlag(NeverExclude)) return UsefulItemColor.Color();
            if (flags.HasFlag(Trap)) return TrapItemColor.Color();
            return NormalItemColor.Color();
        }

        public Color GetBgColorFromItemFlag()
        {
            if (flags.HasFlag(Advancement)) return ProgressiveItemBackgroundColor.Color();
            if (flags.HasFlag(NeverExclude)) return UsefulItemBackgroundColor.Color();
            if (flags.HasFlag(Trap)) return TrapItemBackgroundColor.Color();
            return NormalItemBackgroundColor.Color();
        }

        public int SortNumber()
        {
            if (ItemToSortIdCache.TryGetValue(flags, out var id)) return id;
            if ((flags & Advancement) == Advancement) id = 0;
            else if ((flags & NeverExclude) == NeverExclude) id = 1;
            else if ((flags & Trap) == Trap) id = 10;
            else id = 2;
            return ItemToSortIdCache[flags] = id;
        }
    }

    extension(ItemInfo item)
    {
        public string UID => FilterType.MakeUID(item.ItemName, item.ItemGame, item.Flags);
        public string GetEffectText() => $"{{{{item;``{item.ItemGame}``;``{item.ItemName}``;{(int)item.Flags}}}}}";
        public string GetLocationEffectText() => $"{{{{loc;{item.LocationId};{item.Player.Slot}}}}}";

        public int SortNumber()
        {
            if (SaveType<FilterType>.TryGet(item.UID, out var filter) && filter.IsSpecial) return -1;
            return item.Flags.SortNumber();
        }
    }

    extension(ItemPrintJsonPacket packet)
    {
        public int FindingPlayer => packet.Item.Player;
        public bool FinderIsReceiver => packet.FindingPlayer == packet.ReceivingPlayer;

        public string ItemGame => ConnectionController.HasLeaderClient
            ? ConnectionController.LeaderClient!.PlayerGames[packet.ReceivingPlayer] : "Unknown";

        public string ItemName => ConnectionController.HasLeaderClient
            ? ConnectionController.LeaderClient!.ItemIdToItemName(packet.Item.Item, packet.ReceivingPlayer) : "Unknown";

        public string UID
        {
            get
            {
                var leader = ConnectionController.LeaderClient!;
                var receiver = packet.ReceivingPlayer;
                return FilterType.MakeUID(
                    leader.ItemIdToItemName(packet.Item.Item, receiver), leader.PlayerGames[receiver], packet.Item.Flags
                );
            }
        }

        public string GetItemEffectText()
            => $"{{{{item;``{packet.ItemGame}``;``{packet.ItemName}``;{(int)packet.Item.Flags}}}}}";

        public string GetLocationEffectText() => $"{{{{loc;{packet.Item.Location};{packet.FindingPlayer}}}}}";
        public string GetLocationName() => LocationEffect.LocationName(packet.Item.Location, packet.FindingPlayer);
    }

    extension(Label label)
    {
        public void SetFontSizeOverride(double val)
        {
            if (label.HasThemeFontSizeOverride("font_size")) label.RemoveThemeFontSizeOverride("font_size");
            label.AddThemeFontSizeOverride("font_size", (int)val);
        }
    }

    extension(LineEdit edit)
    {
        public void AppendText(string text) => edit.Text += text;
    }

    extension(LocationGroup.NumberCompareType compareType)
    {
        public int ToSelected()
        {
            switch (compareType)
            {
                case LocationGroup.NumberCompareType.EqualTo: return 1;
                case LocationGroup.NumberCompareType.GreaterThan: return 2;
                case LocationGroup.NumberCompareType.GreaterThan | LocationGroup.NumberCompareType.EqualTo: return 3;
                case LocationGroup.NumberCompareType.LessThan: return 4;
                case LocationGroup.NumberCompareType.LessThan | LocationGroup.NumberCompareType.EqualTo: return 5;
                default: return 0;
            }
        }
    }
    
    extension(RichTextLabel label)
    {
        public void ApplyCompiledPrintableObjs(IPrintableObj[] objs)
        {
            foreach (var printableObj in objs) printableObj.AddText(label);
        }

        public void SetFontSizeOverride(double val)
        {
            label.SetFontSizeOverride("bold_italic_font_size", val);
            label.SetFontSizeOverride("italics_font_size", val);
            label.SetFontSizeOverride("mono_font_size", val);
            label.SetFontSizeOverride("normal_font_size", val);
            label.SetFontSizeOverride("bold_font_size", val);
        }

        public void SetFontSizeOverride(string name, double value)
        {
            if (label.HasThemeFontSizeOverride(name)) label.RemoveThemeFontSizeOverride(name);
            label.AddThemeFontSizeOverride(name, (int)value);
        }
    }

    extension(SpinBox box)
    {
        public void Increment(double step = 0) => box.SetValue(box.Value + (step is 0 ? box.Step : step));
        public void Decrement(double step = 0) => box.Increment(-(step is 0 ? box.Step : step));
    }

    extension(string str)
    {
        public Color Color() => IdToConstant.GetValueOrDefault(str, Unknown).Color();

        public IPrintableObj[] CompileRichText(Dictionary<string, Action<RichTextLabel, string[]>> effects,
            bool appendRawTextAsBBCode)
        {
            if (!str.Contains("{{")) return [new TextPrintObj(str, appendRawTextAsBBCode)];
            List<IPrintableObj> objs = [];

            var split = str.Split("{{");
            objs.Add(new TextPrintObj(split[0], appendRawTextAsBBCode));
            foreach (var section in split.Skip(1))
            {
                if (!section.Contains("}}"))
                {
                    objs.Add(new TextPrintObj($"{{{{{section}", appendRawTextAsBBCode));
                    continue;
                }

                var index = section.IndexOf("}}", StringComparison.Ordinal);
                var code = section[..index].Split(';');

                if (code.Length == 0)
                {
                    objs.Add(new TextPrintObj($"{{{{{section}", appendRawTextAsBBCode));
                    continue;
                }

                var key = code[0];

                if (!effects.ContainsKey(key.ToLower()))
                {
                    objs.Add(new TextPrintObj($"{{{{{section}", appendRawTextAsBBCode));
                    continue;
                }

                objs.Add(new CallablePrintObj(effects[key], code.Length > 1 ? code[1..] : []));
                if (section.Length <= index + 2) continue;
                objs.Add(new TextPrintObj(section[(index + 2)..], appendRawTextAsBBCode));
            }

            return objs.ToArray();
        }

        public string CompileSimpleText(Dictionary<string, string> replacers) => replacers.Aggregate(
            str, (s, kv) => s.Replace($"{{{{{kv.Key}}}}}", kv.Value)
        );

        public void SplitVersionNumber(out int majorVersion, out int minorVersion, out int patchVersion,
            out string extVersion)
        {
            var firstSplit = str.ToLower().Split('-');
            var ver = firstSplit[0].Replace("v", "").ToLower().Split('.');
            majorVersion = int.Parse(ver[0]);
            minorVersion = ver.Length == 1 ? 0 : int.Parse(ver[1]);
            patchVersion = ver.Length == 2 ? 0 : int.Parse(ver[2]);
            extVersion = str.Contains('-') ? $"-{firstSplit[1]}" : "";
        }

        public string Sanitize() => ((string[]) //bbcode blacklist
        [
            "img", "opentype_features", "bgcolor", "hint", "outline_size", "outline_color", "color", "font_size",
            "font", "code", "url",
        ]).Distinct().Aggregate(str, (s, replace) => s.Replace($"[{replace}", $"[lb]{replace}"))
        // .Replace("\r", "")
        ;
    }
}