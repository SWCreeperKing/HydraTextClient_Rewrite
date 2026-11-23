using System.Collections.Generic;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Packets;
using HydraTextClient.Scripts.Clients.TextClient.ParserEffects;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Hints;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.Loaders;

namespace HydraTextClient.Scripts.Clients.TextClient.MessageTypes;

public partial class HintMessage : MessageScene
{
    public const string SaveId = "Clients/TextClient/HintMessage";
    public const string Default = "{{receiver}}'s {{item}} is at {{loc}} in {{finder}}'s world ({{found}})";

    public const string Hint
        = "{{receiver}} - player that receives the item\n{{item}} - item that was hinted for\n{{loc}} - where the item is\n{{finder}} - player who has the item\n{{found}} - if the item was found or not";

    private bool HasBeenFound;
    public ItemFlags Flags;
    private int FinderSlot;
    private int ReceiverSlot;
    public string ItemName;
    public string LocationName;
    public bool IsFound;

    public override void SetInternalPacket(IMessagePacket packetBase)
    {
        if (packetBase.GetPacket() is not HintPrintJsonPacket hint) return;
        if (!ConnectionController.HasLeaderClient) return;
        Flags = hint.Item.Flags;
        ItemName = hint.ItemName;
        LocationName = hint.GetLocationName();

        HasBeenFound = hint.Found!.Value;
        CachedReplacement = new Dictionary<string, string>
        {
            ["finder"] = $"{{{{player;{FinderSlot = hint.FindingPlayer}}}}}", ["item"] = hint.GetItemEffectText(),
            ["receiver"] = $"{{{{player;{ReceiverSlot = hint.ReceivingPlayer}}}}}",
            ["loc"] = hint.GetLocationEffectText(), ["found"] = hint.GetFoundEffectText(),
        };

        SaveType<string>.AddIndividualEvent(SaveId, CallReload);
        SaveType<bool>.AddIndividualEvent(TextClient.ShowFoundHints, CallReload);
        Reload();
    }

    public override void Reload()
    {
        if (!SaveType<bool>.Load(SaveId, true) && HasBeenFound)
        {
            Visible = false;
            return;
        }
        Visible = true;

        var final = SaveType<string>.Load(SaveId, Default).CompileSimpleText(CachedReplacement);

        UpdateFontSize(Message);
        UpdateFontSize(TimeStamp);

        Message.Clear();
        Message.ApplyCompiledPrintableObjs(final.CompileRichText(GetCompileEffects(), false));
    }

    public override string CopyText()
    {
        var mw = ConnectionController.GetCurrentMultiworld;
        if (mw is null) return "Unknown Multiworld";
        return SaveType<string>
              .Load(
                   Flags.HasFlag(ItemFlags.Advancement) ? HintTable.GlobalCopyFormatProgressive
                       : HintTable.GlobalCopyFormat, HintTable.DefaultCopyFormat
               ).CompileSimpleText(
                   new Dictionary<string, string>
                   {
                       ["finder"] = PlayerEffect.PlayerName(FinderSlot, true, out _),
                       ["receiver"] = PlayerEffect.PlayerName(ReceiverSlot, true, out _), ["loc"] = LocationName,
                       ["entrance"] = HasBeenFound ? "(Found)" : "(Not Found)", ["item"] = ItemName,
                       ["copy_finder"] = mw.PlayerCopyAliases.GetValueOrDefault(FinderSlot, ""),
                       ["copy_receiver"] = mw.PlayerCopyAliases.GetValueOrDefault(ReceiverSlot, ""),
                   }
               );
    }

    public override void RemoveEvents()
    {
        SaveType<string>.RemoveIndividualEvent(SaveId, CallReload);
        SaveType<bool>.RemoveIndividualEvent(TextClient.ShowFoundHints, CallReload);
    }
}