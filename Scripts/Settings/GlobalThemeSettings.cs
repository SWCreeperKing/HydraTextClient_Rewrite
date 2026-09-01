using System;
using System.IO;
using System.Linq;
using Godot;
using HydraTextClient.Scripts.Clients.TextClient;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.DataTypes;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.UIHelpers;
using static HydraTextClient.Scripts.Controllers.MainController;
using static HydraTextClient.Scripts.Utility.ColorIdConstants;
using Environment = System.Environment;

namespace HydraTextClient.Scripts.Settings;

public static class GlobalThemeSettings
{
    public static event Action? OnFontUpdate;

    public static readonly string[] ImageFormats =
    [
        "*.png", "*.jpg", "*.bmp", "*.exr", "*.jpeg", "*.tga", "*.svg", "*.svgz",
    ];

    private static readonly string[] FontFormats =
    [
        "*.ttf", "*.ttc", "*.otf", "*.otc", "*.woff", "*.woff2", "*.pfb", "*.pfm",
    ];

    public const string VerticalSpacing = "Theme/Global/VerticalSpacing";
    public const string HorizontalSpacing = "Theme/Global/HorizontalSpacing";
    public const string GlobalFontSize = "Theme/Global/FontSize";
    public const string ApDir = "Main/ArchipelagoFolder";
    public const string AlwaysOnTop = "Main/AlwaysOnTop";
    public const string DisplayNewItemsPopup = "Main/NewItemsPopupDisplay";
    public const string ClearDataOnFullDisconnect = "Main/NewItemsPopupDisplay";
    public const string NormalFont = "Main/NormalFont";
    public const string BoldFont = "Main/BoldFont";
    public const string ItalicFont = "Main/ItalicFont";
    public const string BoldItalicFont = "Main/BoldItalicFont";
    public const string ServerTimeoutTime = "Main/TimeoutTime";
    public const string DiscordEnabled = "Main/DiscordRPC";
    public static event Action? OnImageLoadersReload;
    public static Font DefaultFont;
    public static Font DefaultBoldFont;
    public static Font DefaultItalicFont;
    public static Font DefaultBoldItalicFont;

    public static void Init()
    {
        DefaultFont = GlobalTheme.DefaultFont;
        DefaultBoldFont = GlobalTheme.GetFont("bold_font", "RichTextLabel");
        DefaultItalicFont = GlobalTheme.GetFont("italics_font", "RichTextLabel");
        DefaultBoldItalicFont = GlobalTheme.GetFont("bold_italics_font", "RichTextLabel");

        SetNormalFont(SaveType<string>.Load(NormalFont, ""));
        SetBoldFont(SaveType<string>.Load(BoldFont, ""));
        SetItalicFont(SaveType<string>.Load(ItalicFont, ""));
        SetBoldItalicFont(SaveType<string>.Load(BoldItalicFont, ""));
        SetVerticalContainerSpacing(SaveType<double>.Load(VerticalSpacing, 7));
        SetHorizontalContainerSpacing(SaveType<double>.Load(HorizontalSpacing, 7));

        SaveType<string>.AddIndividualEvent(NormalFont, SetNormalFont);
        SaveType<string>.AddIndividualEvent(BoldFont, SetBoldFont);
        SaveType<string>.AddIndividualEvent(ItalicFont, SetItalicFont);
        SaveType<string>.AddIndividualEvent(BoldItalicFont, SetBoldItalicFont);
        SaveType<double>.AddIndividualEvent(VerticalSpacing, SetVerticalContainerSpacing);
        SaveType<double>.AddIndividualEvent(HorizontalSpacing, SetHorizontalContainerSpacing);

        SetAlwaysOnTop(SaveType<bool>.Load(AlwaysOnTop, false));
        SaveType<double>.AddIndividualEvent(GlobalFontSize, LoadGlobalFont);
        CreateSettings();

        var emptyFontDir = Environment.GetFolderPath(Environment.SpecialFolder.Fonts);
        if (emptyFontDir.EndsWith("WINDOWS\\Fonts"))
            emptyFontDir
                = $"{Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)}/Microsoft/Windows/Fonts/";

        SettingsCreator.Tab(
            "Main Settings", tab =>
            {
                tab.AddBrowseFile("Set Archipelago Folder", ApDir, FileDialog.FileModeEnum.OpenDir, [])
                   .AddSeparator()
                   .AddSpinBox("Connection Timeout Timer (sec)", ServerTimeoutTime, 60)
                   .AddSeparator()
                   .AddCheckBox("Clear UI on Full Disconnection", ClearDataOnFullDisconnect, true)
                   .AddSpinBox("Message History Limit", ChildLimiter.QueueSaveId, 200d)
                   .AddSeparator()
                   .AddCheckBox("Always on top", AlwaysOnTop, false, 0, b => b.Toggled += SetAlwaysOnTop)
                   .AddCheckBox("Show new Items on Connect", DisplayNewItemsPopup, true)
                   .AddCheckBox("Discord Rich Presence (requires restart)", DiscordEnabled, true)
                   .AddButton("Force (Safety) Save", Save, 1)
                   .AddButton("Open Save Directory", () => OS.ShellOpen(Directories.MainDirectory), 1)
                   .AddSeparator(1)
                   .AddCheckBox("Check For Updates on Start", CheckForUpdate, true, 1)
                   .AddCheckBox("Update to Beta Branches", UpdateToBeta, false, 1)
                   .AddText("Will still update to beta\nif you are on a beta branch", 1)
                   .AddButton("Check For Updates", CheckForUpdates, 1)
                   .AddButton("Open Emotes Directory", () => OS.ShellOpen(Directories.Emotes), 2)
                   .AddButton("Open Portrait Directory", () => OS.ShellOpen(Directories.GamePortraits), 2)
                   .AddButton(
                        "Open Game Item Override Directory", () => OS.ShellOpen(Directories.GameItemImageOverrides), 2
                    )
                   .AddButton("Reload Image Loaders", () => OnImageLoadersReload?.Invoke(), 2)
                   .AddSeparator(2)
                   .AddText("Sites to download portraits", 2)
                   .AddButton("SteamGridDB.com", () => OS.ShellOpen("https://www.steamgriddb.com/"), 2)
                   .AddButton("IGDB.com", () => OS.ShellOpen("https://www.igdb.com/"), 2)
                   .AddButton(
                        "mk-404's Archipelago Library",
                        () => OS.ShellOpen("https://mk-404.github.io/Archipelago-Games-Library/"), 2
                    );
            }, int.MinValue
        );

        SettingsCreator.Tab(
            "Theme",
            tab => tab
                  .AddButton(
                       "Export Theme Colors to Clipboard",
                       () => DisplayServer.ClipboardSet(
                           string.Join('|', ConstantToId.Select(kv => $"{kv.Value}={kv.Key.Color().ToHtml()}"))
                       ), 1
                   ).AddButton(
                       "Import Theme Colors from Clipboard"
                       , () =>
                       {
                           var colors = DisplayServer.ClipboardGet().Split('|');
                           if (colors.Length < 1) return;
                           foreach (var color in colors)
                           {
                               var split = color.Split('=');
                               var id = split[0];
                               if (split.Length is not 2 || !SaveType<HexColor>.ContainsKey(id)) continue;
                               SaveType<HexColor>.Save(id, new HexColor(new Color(split[1]).ToRgba64()), true);
                           }
                       }, 1
                   )
                  .AddSeparator(1)
                  .AddBrowseFile(
                       "Set Background Image", WindowBackGroundImage, FileDialog.FileModeEnum.OpenFile, ImageFormats,
                       col: 1
                   ).AddSpinBox(
                       "Background Image Alpha", WindowBackGroundImageAlpha, 255d, 1, box =>
                       {
                           box.MaxValue = 255;
                           box.MinValue = 0;
                       }
                   ).AddDropdown("Background Image Stretch Mode", WindowBackGroundImageScale, ["Scale", "Tile"], col: 1)
                  .AddSeparator(1)
                  .AddSpinBox("Vertical Container Spacing", VerticalSpacing, 7, 1)
                  .AddSpinBox("Horizontal Container Spacing", HorizontalSpacing, 7, 1)
                  .AddSeparator(1)
                  .AddSpinBox("Global Font Size", GlobalFontSize, 20d, 1, c => c.MinValue = 1)
                  .AddSpinBox("Text Client Font Size", TextClient.FontSizeId, 20d, 1)
                  .AddBrowseFile(
                       "Set Normal Font Override", NormalFont, FileDialog.FileModeEnum.OpenFile, FontFormats, col: 2,
                       defaultOpen: emptyFontDir
                   )
                  .AddSeparator(2)
                  .AddBrowseFile(
                       "Set Bold Font Override", BoldFont, FileDialog.FileModeEnum.OpenFile, FontFormats, col: 2,
                       defaultOpen: emptyFontDir
                   )
                  .AddSeparator(2)
                  .AddBrowseFile(
                       "Set Italics Font Override", ItalicFont, FileDialog.FileModeEnum.OpenFile, FontFormats, col: 2,
                       defaultOpen: emptyFontDir
                   )
                  .AddSeparator(2)
                  .AddBrowseFile(
                       "Set Bold Italics Font Override", BoldItalicFont, FileDialog.FileModeEnum.OpenFile, FontFormats,
                       col: 2, defaultOpen: emptyFontDir
                   )
        );

        LoadTheme();
    }

    public static void LoadTheme() { LoadGlobalFont(SaveType<double>.Load(GlobalFontSize, 20)); }

    public static void LoadGlobalFont(double d) => GlobalTheme.DefaultFontSize = (int)Math.Round(d);

    public static void SetNormalFont(string path)
    {
        if (path is "" || !File.Exists(path))
        {
            GlobalTheme.DefaultFont = DefaultFont;
            OnFontUpdate?.Invoke();
            return;
        }
        try
        {
            FontFile font = new();
            font.LoadDynamicFont(path);
            OnFontUpdate?.Invoke();
            GlobalTheme.DefaultFont = font;
        }
        catch (Exception e) { ShowError(e); }
    }

    public static void SetBoldFont(string path) => SetRichTextFont("bold_font", DefaultBoldFont, path);
    public static void SetItalicFont(string path) => SetRichTextFont("italics_font", DefaultItalicFont, path);

    public static void SetBoldItalicFont(string path)
        => SetRichTextFont("bold_italics_font", DefaultBoldItalicFont, path);

    private static void SetRichTextFont(string name, Font def, string path)
    {
        if (path is "" || !File.Exists(path))
        {
            GlobalTheme.SetFont(name, "RichTextLabel", def);
            OnFontUpdate?.Invoke();
            return;
        }
        try
        {
            FontFile font = new();
            font.LoadDynamicFont(path);
            OnFontUpdate?.Invoke();
            GlobalTheme.SetFont(name, "RichTextLabel", font);
        }
        catch (Exception e) { ShowError(e); }
    }

    public static void SetVerticalContainerSpacing(double amt)
        => GlobalTheme.SetConstant("separation", "VBoxContainer", (int)amt);

    public static void SetHorizontalContainerSpacing(double amt) =>
        GlobalTheme.SetConstant("separation", "HBoxContainer", (int)amt);
}