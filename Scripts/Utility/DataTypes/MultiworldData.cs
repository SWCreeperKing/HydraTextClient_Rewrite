using System.Collections.Concurrent;
using System.Collections.Generic;
using Archipelago.MultiClient.Net.Models;

namespace HydraTextClient.Scripts.Utility.DataTypes;

public class MultiworldData
{
    public string WorldName = "Untitled Multiworld";
    public string Address = "archipelago.gg";
    public string Port = "12345";
    public string Password = "";
    public string[] DeathLinkGroups = [];
    public ConcurrentDictionary<string, string> SlotNames = [];
    public ConcurrentDictionary<string, string> SlotPasswords = [];
    public ConcurrentDictionary<string, int> CheckCountsChecked = [];
    public ConcurrentDictionary<string, int> CheckCounts = [];
    public ConcurrentDictionary<string, Hint[]> Hints = [];
    public ConcurrentDictionary<int, bool> HiddenHints = [];
    public ConcurrentDictionary<string, int> ItemHistory = [];
    public ConcurrentDictionary<int, string> PlayerAliases = [];
    public ConcurrentDictionary<int, string> PlayerCopyAliases = [];
    public ConcurrentDictionary<string, Dictionary<string, string>> MapEntrances = [];

    public void ClearCache()
    {
        SlotNames.Clear();
        SlotPasswords.Clear();
        CheckCountsChecked.Clear();
        CheckCounts.Clear();
        Hints.Clear();
        HiddenHints.Clear();
        ItemHistory.Clear();
        PlayerAliases.Clear();
        PlayerCopyAliases.Clear();
        MapEntrances.Clear();
    }

    public string GetSlotName(string slot) => SlotNames.GetValueOrDefault(slot, slot);

    public string GetPassword(string slot)
    {
        var pass = SlotPasswords.GetValueOrDefault(slot, Password);
        return pass.Trim() is "" ? "None" : pass;
    }
}