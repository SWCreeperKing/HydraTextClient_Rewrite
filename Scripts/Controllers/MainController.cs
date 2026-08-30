using System;
using System.IO;
using Godot;
using HydraTextClient.Scripts.Consoles.Godot;
using HydraTextClient.Scripts.Discord;
using HydraTextClient.Scripts.Settings;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.DataTypes;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.Popups;
using static HydraTextClient.Scripts.Utility.ColorIdConstants.ColorConstant;

namespace HydraTextClient.Scripts.Controllers;

public partial class MainController : Control
{
    public const string WindowSaveId = "window_nodes/MAIN_WINDOW";
    public const string WindowBackGroundImage = "Theme/BackgroundImage";
    public const string WindowBackGroundImageScale = "Theme/BackgroundImageScaleMode";
    public const string WindowBackGroundImageAlpha = "Theme/BackgroundImageAlpha";
    public const string CheckForUpdate = "Main/CheckForUpdates";
    public const string UpdateToBeta = "Main/UpdateToBetaBranches";

    [Export] private string VersionNumber;
    [Export] private PackedScene ErrorWindow;
    [Export] private PackedScene ItemFilterWindow;
    [Export] private PackedScene ItemFilterDisplay;
    [Export] private PackedScene AutoUpdater;
    [Export] private PackedScene ConfirmDialogue;
    [Export] private LoggerLabel GDLogger;
    [Export] private TextureRect BackgroundImage;
    [Export] private SettingsPorter Porter;
    [Export] private TabContainer MainContainer;
    [Export, ExportGroup("Debug")] private PackedScene VersioningHelper;

    private ErrorDialog ErrorDialog;

    private static MainController Singleton;

    public static Theme GlobalTheme;

    public static event Action? OnSave;
    public static event Action? OnExit;

    public override void _EnterTree()
    {
        if (!Directory.Exists(Directories.MainDirectory)) Directory.CreateDirectory(Directories.MainDirectory);
        Singleton = this;
        GDLogger.Init();
        OS.AddLogger(GDLogger.Logger);
        GlobalTheme = Theme;

        var window = GetWindow();
        window.Size = SaveType<Vector2I>.Load($"{WindowSaveId}_size", window.Size);
        window.Position = SaveType<Vector2I>.Load($"{WindowSaveId}_pos", window.Position);
        window.SizeChanged += () => SaveType<Vector2I>.Save($"{WindowSaveId}_size", window.Size, true);
        window.Title = $"Hydra Text Client {VersionNumber}";

        var mainBackgroundBox = (StyleBoxFlat)GetThemeStylebox("panel");
        mainBackgroundBox.BgColor = UiBackground.Load();

        var mainPopupBox = (StyleBoxFlat)GlobalTheme.GetStylebox("panel", "Panel");
        mainPopupBox.BgColor = PopupBackground.Load();

        SaveType<HexColor>.AddIndividualEvent(UiBackground.SaveId(), val => mainBackgroundBox.BgColor = val);
        SaveType<HexColor>.AddIndividualEvent(PopupBackground.SaveId(), val => mainPopupBox.BgColor = val);

        LoadBackgroundImage(SaveType<string>.Load(WindowBackGroundImage, "", false));
        LoadBackgroundImageTransparency(SaveType<double>.Load(WindowBackGroundImageAlpha, 255));

        SaveType<string>.AddIndividualEvent(WindowBackGroundImage, LoadBackgroundImage);
        SaveType<double>.AddIndividualEvent(WindowBackGroundImageAlpha, LoadBackgroundImageTransparency);

        BackgroundImage.StretchMode = (TextureRect.StretchModeEnum)SaveType<int>.Load(WindowBackGroundImageScale, 0);
        SaveType<int>.AddIndividualEvent(
            WindowBackGroundImageScale, l => BackgroundImage.StretchMode = (TextureRect.StretchModeEnum)l
        );
    }

    public override void _Ready()
    {
        DRPC.Init();
        GlobalThemeSettings.Init();
        MainContainer.CurrentTab = 0;

        if (Path.GetFileNameWithoutExtension(System.Environment.ProcessPath)! is "_OLD_HYDRA_DONT_USE_WILL_AUTODELETE")
            Quit();
        foreach (var old in Directory.GetFiles(Path.GetDirectoryName(System.Environment.ProcessPath)!))
        {
            if (Path.GetFileName(old) is "_OLD_HYDRA_DONT_USE_WILL_AUTODELETE") File.Delete(old);
        }
        if (SaveType<bool>.Load(CheckForUpdate, true) && RunAutoUpdater()) return;

        if (SaveType<bool>.Load("Main/HasPorted", !File.Exists(Directories.LegacyData))) return;
        Porter.Startup();
        Porter.Show();
    }

    public override void _Notification(int what)
    {
        if (what != NotificationWMCloseRequest) return;
        try { ExternalAppController.CloseAll(); }
        catch (Exception e) { GD.PrintErr(e); }
        try
        {
            SaveType<Vector2I>.Save($"{WindowSaveId}_pos", GetWindow().Position, true);
            Save();
        }
        catch (Exception e) { GD.PrintErr(e); }
        try { OnExit?.Invoke(); }
        catch (Exception e) { GD.PrintErr(e); }
    }

    public bool RunAutoUpdater()
    {
        if (ConnectionController.HasLeaderClient)
        {
            ShowError("Cannot check for updates with connected slots");
            return false;
        }

        var updater = AutoUpdater.Instantiate<AutoUpdater>();
        if (!updater.CanRunUpdater()) return false;
        CallDeferred("add_child", updater);
        updater.CallDeferred("show");
        return true;
    }

    public void LoadBackgroundImage(string path)
    {
        if (path.Trim() is "" || !File.Exists(path))
        {
            BackgroundImage.Visible = false;
            return;
        }
        var image = ImageTexture.CreateFromImage(Image.LoadFromFile(path));
        if (image is null)
        {
            BackgroundImage.Visible = false;
            return;
        }
        BackgroundImage.Texture = image;
        BackgroundImage.Visible = true;
    }

    public void LoadBackgroundImageTransparency(double val)
    {
        var color = BackgroundImage.Modulate;
        color.A = (int)Math.Clamp(val, 0, 255) / 255f;
        BackgroundImage.Modulate = color;
    }

#if DEBUG
    public override void _UnhandledInput(InputEvent @event)
    {
        if (Input.IsActionJustPressed("debug_HasherHelper"))
        {
            var helper = VersioningHelper.Instantiate<VersioningHelperPopup>();
            AddChild(helper);
            helper.Show();
        }
    }
#endif

    public static void ShowError(string message, Exception e) => ShowError($"{message}\n{e.Message}\n{e.StackTrace}");
    public static void ShowError(Exception e) => ShowError($"{e.Message}\n{e.StackTrace}");
    public static void ShowError(string[] error) => ShowError(string.Join('\n', error));
    public static void ShowError(string error) => Singleton.CallDeferred("CreateErrorDialogue", error);

    public void CreateErrorDialogue(string error)
    {
        if (ErrorDialog is null || !IsInstanceValid(ErrorDialog) || ErrorDialog.IsQueuedForDeletion())
        {
            ErrorDialog = ErrorWindow.Instantiate<ErrorDialog>();
            AddChild(ErrorDialog);
            ErrorDialog.Show();
            ErrorDialog.CloseRequested += () => ErrorDialog = null;
        }
        else ErrorDialog.AddText("\n\nExtra Error:\n");
        ErrorDialog.AddText(error);
        GD.PrintErr(error);
    }

    public static void ShowConfirm(string title, string msg, Action yes)
        => Singleton.CreateConfirmWindow(title, msg, yes);

    public void CreateConfirmWindow(string title, string msg, Action yes, Action? no = null)
    {
        var window = ConfirmDialogue.Instantiate<ConfirmWindow>();
        CallDeferred("add_child", window);
        window.OnTop = true;
        window.Setup(title, msg, yes, no);
    }

    public static void ShowItemFilter() => Singleton.CallDeferred("CreateItemFilterDialogue", (string[])["", "", "0"]);
    public static void ShowItemFilter(string[] args) => Singleton.CallDeferred("CreateItemFilterDialogue", args);

    public void CreateItemFilterDialogue(string[] args)
    {
        var filter = ItemFilterWindow.Instantiate<ItemFilter>();
        AddChild(filter);
        filter.SetFilter(args[0], args[1], args[2]);
        filter.Show();
    }

    public static void Save() => OnSave?.Invoke();
    public static string GetTimestamp() => DateTime.Now.ToString("[HH:mm:ss]");
    public static void SetAlwaysOnTop(bool val) => Singleton.GetWindow().AlwaysOnTop = val;
    public static string GetVersion() => Singleton.VersionNumber;
    public static void CheckForUpdates() => Singleton.RunAutoUpdater();
    public void UpdateDiscord() => DRPC.CheckDiscord();
    public void Quit() => GetTree().CallDeferred("quit");

    public static void QuitHydra()
    {
        Save();
        Singleton.GetTree().CallDeferred("quit");
    }
}