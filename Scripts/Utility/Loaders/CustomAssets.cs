using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;
using Archipelago.MultiClient.Net.Models;
using Godot;
using KaitoKid.ArchipelagoUtilities.AssetDownloader.ItemSprites;
using KaitoKid.Utilities.Interfaces;
using Newtonsoft.Json;
using ILogger = KaitoKid.Utilities.Interfaces.ILogger;

namespace HydraTextClient.Scripts.Utility.Loaders;

public partial class CustomAssets : Control
{
    [Export] private Texture2D Fallback;
    private static ConcurrentDictionary<string, Task> CurrentDownloadingTasks = [];
    private static CustomAssets Singleton;

    public static ConcurrentDictionary<string, ImageTexture> ItemSprites = [];
    public static ArchipelagoItemSprites ItemSpritesManager;
    private static Logger Logger;

    public static Texture2D GetFallback => Singleton.Fallback;

    public override void _Ready()
    {
        Singleton = this;
        if (Directories.IsPortable) return;
        Logger = new Logger();
        ItemSpritesManager = new ArchipelagoItemSprites(
            Logger, JsonConvert.DeserializeObject<ItemSpriteAliases>, new TimeSpan(30, 0, 0, 0)
        );
    }

    public static ImageTexture CreateSprite(string file) => ImageTexture.CreateFromImage(Image.LoadFromFile(file));

    public static Texture2D ItemImage(string itemGameName, string itemName, Action<Texture2D> callback,
        out bool isFallback)
        => ItemImage(new AssetItem(itemGameName, itemName), itemGameName, callback, out isFallback);

    private static Texture2D ItemImage(AssetItem location, string selfGame, Action<Texture2D> callback,
        out bool isFallback)
    {
        try
        {
            isFallback = false;
            if (GameItemImageLoader.TryGet(location.GameName, location.ItemName, out var spriteOverride))
                return spriteOverride;

            if (Directories.IsPortable)
            {
                isFallback = true;
                return GetFallback;
            }
            
            if (ItemSprites.TryGetValue(location.Uid, out var sprite)) return sprite;
            isFallback = true;
            if (CurrentDownloadingTasks.ContainsKey(selfGame)) return Singleton.Fallback;
            var task = Task.Run(() =>
            {
                try
                {
                    bool res;
                    ItemSprite spriteData;
                    lock (ItemSpritesManager)
                        res = ItemSpritesManager.TryGetCustomAsset(
                            location, selfGame, false, true,
                            out spriteData, false
                        );

                    if (!res || spriteData is null) return Task.FromResult(Singleton.Fallback);
                    var file = spriteData.FilePath;
                    ItemSprites[location.Uid] = sprite = CreateSprite(file);
                    CurrentDownloadingTasks.TryRemove(selfGame, out _);
                    callback(sprite);
                }
                catch (Exception e) { GD.PrintErr(e); }
                return Task.CompletedTask;
            });
            CurrentDownloadingTasks.TryAdd(selfGame, task);

            return Singleton.Fallback;
        }
        catch (Exception e)
        {
            GD.Print("Custom Assets: Race Condition? Defaulting on Fallback");
            isFallback = true;
            return GetFallback;
        }
    }
}

public class Logger : ILogger
{
    public void LogError(string message) => GD.PrintErr(message);
    public void LogError(string message, Exception e) => GD.PrintErr(message, e);
    public void LogWarning(string message) => GD.Print(message);
    public void LogInfo(string message) => GD.Print(message);
    public void LogMessage(string message) => GD.Print(message);
    public void LogDebug(string message) => GD.Print(message);

    public void LogDebugPatchIsRunning(string patchedType, string patchedMethod, string patchType, string patchMethod,
        params object[] arguments)
        => GD.Print($"Debug Patch: [{patchedMethod}] -> [{patchMethod}]");

    public void LogDebug(string message, params object[] arguments) => GD.Print(message);
    public void LogErrorException(string prefixMessage, Exception ex, params object[] arguments) => GD.PrintErr(ex);
    public void LogWarningException(string prefixMessage, Exception ex, params object[] arguments) => GD.PrintErr(ex);
    public void LogErrorException(Exception ex, params object[] arguments) => GD.PrintErr(ex);
    public void LogWarningException(Exception ex, params object[] arguments) => GD.PrintErr(ex);
    public void LogErrorMessage(string message, params object[] arguments) => GD.PrintErr(message);

    public void LogErrorException(string patchType, string patchMethod, Exception ex, params object[] arguments)
        => GD.PrintErr(ex);
}

public class AssetItem(string game, string item) : IAssetLocation
{
    public int GetSeed() => 0;
    public string GameName { get; } = game;
    public string ItemName { get; } = item;
    public string Uid = $"{game};{item}";

    public static implicit operator AssetItem(ScoutedItemInfo item) => new(item.ItemGame, item.ItemName);
    public static implicit operator AssetItem(ItemInfo item) => new(item.ItemGame, item.ItemName);
}