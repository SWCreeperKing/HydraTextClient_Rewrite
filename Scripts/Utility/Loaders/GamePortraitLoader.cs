using System.Collections.Generic;
using System.IO;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Settings;
using static HydraTextClient.Scripts.Utility.Loaders.Directories;

namespace HydraTextClient.Scripts.Utility.Loaders;

public class GamePortraitLoader : ImageLoader
{
    public static GamePortraitLoader Singleton = new();
    public override string ImageFolder => GamePortraits;
    private HashSet<string> BaseList = [];
    public string[] GameList = [];

    private GamePortraitLoader() => GlobalThemeSettings.OnImageLoadersReload += ReloadImages;
    
    public string GameAt(int i) => GameList[i];

    public override void ReloadImagesResolved()
    {
        GameList = [.. BaseList.Order()];
        GD.Print("Loading Game Portraits");
    }

    public override void ImageWasSet(string path, string image, ImageTexture img)
    {
        BaseList.Add(Path.GetFileNameWithoutExtension(path));
        GD.Print($"Loaded image [{path.Replace(ImageFolder, ".")}] for game [{image}]");
    }

    public override string NameModify(string name) => name.ToLower().Replace(":", "");
    public override string PathToNameModify(string path) => Path.GetFileNameWithoutExtension(path);
}