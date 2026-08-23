using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Controllers;

namespace HydraTextClient.Scripts.Utility.Loaders;

public abstract class ImageLoader : IDisposable
{
    public abstract string ImageFolder { get; }
    public virtual bool LoadSubDirectories => true;
    public event Action? OnReloadImages;
    private ConcurrentDictionary<string, ImageTexture> Images = [];

    protected ImageLoader() => ReloadImages();

    public void ReloadImages()
    {
        if (!Directory.Exists(ImageFolder)) Directory.CreateDirectory(ImageFolder);
        LoadDirectory(ImageFolder);
        ReloadImagesResolved();
        OnReloadImages?.Invoke();
    }

    private void LoadDirectory(string dir)
    {
        foreach (var file in Directory.GetFiles(dir))
        {
            var fileName = NameModify(PathToNameModify(file));
            if (PreprocessStep(file)) continue;
            if (Images.ContainsKey(fileName)) continue;

            try
            {
                var image = ImageTexture.CreateFromImage(Image.LoadFromFile(file));
                Images[fileName] = image;
                ImageWasSet(file, fileName, image);
            }
            catch (Exception e) { MainController.ShowError($"Error Loading File [{file}]", e); }
        }

        if (!LoadSubDirectories) return;
        foreach (var subDir in Directory.GetDirectories(dir)) LoadDirectory(subDir);
    }

    public bool TryGet(string name, out ImageTexture img) => Images.TryGetValue(NameModify(name), out img);

    public Texture2D GetOrDef(string name, Texture2D def)
        => !Images.TryGetValue(NameModify(name), out var value) ? def : value;

    public ConcurrentDictionary<string, ImageTexture> GetImages() => Images;
    public string[] GetImageNames() => Images.Keys.ToArray();
    public ImageTexture GetImage(string name) => Images[NameModify(name)];
    public virtual void ReloadImagesResolved() { }
    public virtual void ImageWasSet(string path, string image, ImageTexture img) { }
    public virtual string NameModify(string name) => name;
    public virtual string PathToNameModify(string path) => Path.GetFileNameWithoutExtension(path);

    public virtual bool PreprocessStep(string path) // return true to skipp file
        => Path.GetExtension(path) switch { ".jpg" or ".png" => false, _ => true, };

    public ImageTexture this[string name] => GetImage(name);
    
    public void Dispose()
    {
        foreach (var (_, img) in Images)
        {
            img?.Free();
            img?.Dispose();
        }
        Images.Clear();
    }
}