using Godot;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Utility.Loaders;

namespace HydraTextClient.Scripts.Utility.UIHelpers;

public abstract partial class RichLabelInteractions : RichTextLabel
{
    public bool MetaAdded;
    
    public override void _EnterTree()
    {
        if (MetaAdded) return;
        MetaAdded = true;
        MouseFilter = MouseFilterEnum.Pass;
        TextureFilter = TextureFilterEnum.Nearest;
        MetaClicked += meta =>
        {
            switch (meta.VariantType)
            {
                case Variant.Type.String:
                {
                    var metaString = (string)meta;
                    var key = metaString[..metaString.IndexOf('_')];
                    var text = metaString[(metaString.IndexOf('_') + 1)..];
                    RegisterOnMetaClicked(key, [text]);
                    break;
                }
                case Variant.Type.PackedStringArray:
                {
                    var arr = (string[])meta;
                    if (arr.Length == 0) return;
                    RegisterOnMetaClicked(arr[0], arr.Length == 1 ? [] : arr[1..]);
                    break;
                }
                default: OnVariantMetaClicked(meta); break;
            }
        };
    }

    public override GodotObject _MakeCustomTooltip(string forText)
    {
        if (forText == "") return null;
        var kind = forText[..forText.IndexOf(' ')];
        var text = forText[(forText.IndexOf(' ') + 1)..];
        if (text == "") return null;

        PanelContainer container = new();
        container.AddThemeStyleboxOverride(
            "panel", new StyleBoxFlat { BgColor = ColorIdConstants.ColorConstant.TooltipColor.Color() }
        );

        MarginContainer margin = new();
        margin.AddThemeConstantOverride("margin_left", 3);
        margin.AddThemeConstantOverride("margin_top", 3);
        margin.AddThemeConstantOverride("margin_right", 3);
        margin.AddThemeConstantOverride("margin_bottom", 3);

        switch (kind.ToLower())
        {
            case "text": margin.AddChild(CreateLabel(text)); break;
            case "emote": margin.AddChild(CreateImage(EmoteLoader.Singleton.GetOrDef(text, CustomAssets.GetFallback))); break;
            case "player":
                var hasAlias = ConnectionController.GetPlayerInfo(
                    int.Parse(text), out var name, out var alias, out var game
                );
                margin.AddChild(CreateLabel($"Slot: #{int.Parse(text):###,###}\nPlayer: {name}\n{(hasAlias ? $"Alias: {alias}\n" : "")}Game: {game}"));
                break;
        }

        container.AddChild(margin);
        return container;
    }

    public TextureRect CreateImage(Texture2D texture2D)
    {
        TextureRect rect = new();
        rect.Texture = texture2D;
        return rect;
    }

    public Label CreateLabel(string text)
    {
        Label tooltip = new();
        tooltip.Theme = MainController.GlobalTheme;
        // tooltip.AddThemeFontSizeOverride("font_size", 18);
        tooltip.Text = text;
        return tooltip;
    }

    public void RegisterOnMetaClicked(string key, string[] text)
    {
        switch (key)
        {
            case "itemfilter": MainController.ShowItemFilter(text); break;
            default: OnMetaClicked(key, text); break;
        }
    }

    public abstract void OnMetaClicked(string key, string[] text);
    public virtual void OnVariantMetaClicked(Variant meta) { }
}