using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Settings;
using Newtonsoft.Json;
using static HydraTextClient.Scripts.Utility.Loaders.Directories;

namespace HydraTextClient.Scripts.Utility.Loaders;

public static class GameItemImageLoader
{
    private static ConcurrentDictionary<string, ItemImageLoader> GameImages = [];
    private static ConcurrentDictionary<string, ConcurrentDictionary<string, string>> GameImageAliases = [];
    public static event Action? OnReload;

    static GameItemImageLoader()
    {
        Reload();
        GlobalThemeSettings.OnImageLoadersReload += Reload;
    }

    public static void Reload()
    {
        if (!Directory.Exists(GameItemImageOverrides)) Directory.CreateDirectory(GameItemImageOverrides);
        foreach (var folder in Directory.GetDirectories(GameItemImageOverrides))
        {
            var gameName = Path.GetFileName(folder)!.ToLower();
            GD.Print($"Loading [{gameName}] assets");
            GameImages[gameName] = new ItemImageLoader(folder, gameName);
            var aliases = $"{folder}/aliases.json";
            if (!File.Exists(aliases)) continue;
            var aliasDict = GameImageAliases[gameName] = [];
            foreach (var alias in JsonConvert.DeserializeObject<AliasGroups>(File.ReadAllText(aliases)).Aliases)
            foreach (var item in alias.ItemNames)
                aliasDict.TryAdd(item.ToLower(), alias.AliasName.ToLower());
        }
        OnReload?.Invoke();
    }

    public static bool TryGet(string gameName, string itemName, out ImageTexture img)
    {
        img = null;
        gameName = gameName.ToLower().Replace(":", "");
        itemName = itemName.ToLower().Replace(":", "");
        if (GameImageAliases.TryGetValue(gameName, out var aliasGroup)
            && aliasGroup.TryGetValue(itemName, out var alias)) itemName = alias.ToLower();

        if (GameImages.TryGetValue(gameName, out var imgLoader))
            return imgLoader.TryGet(itemName, out img) || imgLoader.TryGet(gameName, out img);

        return false;
    }
}

public class ItemImageLoader(string dir, string gameName) : ImageLoader
{
    public string GameName = gameName;
    public override string ImageFolder => dir;
    public override bool LoadSubDirectories => false;
    public override string NameModify(string name) => name.ToLower().Replace(":", "").Replace($"{GameName}_", "");
}

public struct AliasGroups(Alias[] aliases)
{
    public Alias[] Aliases = aliases;
}

public struct Alias(string aliasName, string[] itemNames)
{
    public string AliasName = aliasName.ToLower().Replace(":", "");
    public string[] ItemNames = [.. itemNames.Select(img => img.ToLower().Replace(":", ""))];
}