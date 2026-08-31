using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using CreepyUtil.Archipelago.ApClient;
using Godot;
using HydraTextClient.Scripts.Clients.TextClient;
using HydraTextClient.Scripts.Settings;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Clients.CircleTracker;

public partial class CircleTrackerLabel : VBoxContainer
{
    private const string HeaderAlignment = "circle_tracker/header_align";
    [Export] private EmptyRichLabelInteractor Header;
    [Export] private EmptyRichLabelInteractor Entrances;
    [Export] private EmptyRichLabelInteractor Locations;
    [Export] private FoldableContainer Folder;

    private Dictionary<string, Action<RichTextLabel, string[]>>? CompileHeaderEffects;
    private Dictionary<string, Action<RichTextLabel, string[]>>? CompileEntranceEffects;
    private Dictionary<string, Action<RichTextLabel, string[]>>? CompileLocationEffects;
    private IPrintableObj[]? CachedHeaderMessage;
    private IPrintableObj[]? CachedEntranceMessage;
    private IPrintableObj[]? CachedLocationMessage;
    private int CircleNumber;
    private int FontSize;
    private string[] UniqueEntrances;
    private ulong[] UniqueLocations;
    private string ItemsText;
    private ApClient? Client;
    private Dictionary<long, string> Hints;
    private Dictionary<long, bool> HintImportance;
    private long[] Priority;
    private bool IsLater;

    public void SetData(int circle, ApClient client, string itemsText)
    {
        CircleNumber = circle;
        Client = client;
        ItemsText = itemsText;
        FontSize = (int)SaveType<double>.Load(GlobalThemeSettings.GlobalFontSize, 20d);

        SaveType<bool>.AddIndividualEvent(TrackerPage.ShowEmptyCircles, ToggleVisibility);
        SaveType<double>.AddIndividualEvent(GlobalThemeSettings.GlobalFontSize, UpdateFontSize);
        SaveType<int>.AddIndividualEvent(HeaderAlignment, UpdateAlignment);
    }

    public bool UpdateData(string[] uniqueEntrances, ulong[] uniqueLocations, Dictionary<long, string> hints,
        Dictionary<long, bool> hintImportance, long[] priority, bool isLater)
    {
        UniqueLocations = uniqueLocations;
        UniqueEntrances = uniqueEntrances;
        Hints = hints;
        HintImportance = hintImportance;
        Priority = priority;
        IsLater = isLater;
        ToggleVisibility();
        return RenderDisplay();
    }

    public bool RenderDisplay()
    {
        RenderHeader(true);
        var a = RenderEntrances(true);
        var b = RenderLocations(true);
        return a || b;
    }

    private void RenderHeader(bool recompile)
    {
        if (recompile)
        {
            StringBuilder sb = new();

            var alignment = SaveType<int>.Load(HeaderAlignment, 0) switch { 1 => "left", 2 => "right", _ => "center", };

            sb.Append('[').Append(alignment).Append("][font_size=")
              .Append(FontSize * (UniqueLocations.Length == 0 && UniqueEntrances.Length == 0 ? 1 : 2))
              .Append("]Circle #").Append($"{CircleNumber:###,###}").Append("[/font_size][/").Append(alignment)
              .Append("]\n");
            if (ItemsText.Length != 0)
                sb.Append('[').Append(alignment).Append(']').Append(ItemsText).Append("[/").Append(alignment)
                  .Append("]\n");
            CachedHeaderMessage = sb.ToString().CompileRichText(GetCompileHeaderEffects(), true);
        }

        Header.Clear();
        Header.ApplyCompiledPrintableObjs(CachedHeaderMessage);
        Folder.Visible = UniqueEntrances.Length != 0 || UniqueLocations.Length != 0;
    }

    private bool RenderEntrances(bool recompile)
    {
        Entrances.Visible = UniqueEntrances.Length != 0;
        if (!Entrances.Visible) return false;

        var important = false;
        if (CachedEntranceMessage is not null && UniqueEntrances.Length == 0) CachedEntranceMessage = null;
        if (recompile && UniqueEntrances.Length > 0)
        {
            important = true;
            StringBuilder sb = new();

            var orderedEntrances = UniqueEntrances.Order().ToArray();

            sb.Append("[table=1][cell bg=#00000069] ").Append($"{UniqueEntrances.Length:###,###}")
              .Append(" Entrances [/cell]");
            for (var i = 0; i < orderedEntrances.Length; i++)
            {
                var id = orderedEntrances[i];
                var colColor = i % 2 == 0 ? "[cell bg=#00000044]" : "[cell]";
                sb.Append(colColor).Append("[color=")
                  .Append(ColorIdConstants.ColorConstant.EntranceColor.Color().ToHtml()).Append("] ").Append(id)
                  .Append("[/color][/cell]");
            }
            sb.Append("[/table]\n");
            CachedEntranceMessage = sb.ToString().CompileRichText(GetCompileEntranceEffects(), true);
        }

        Entrances.Clear();
        Entrances.ApplyCompiledPrintableObjs(CachedEntranceMessage);
        return important;
    }

    private bool RenderLocations(bool recompile)
    {
        Locations.Visible = UniqueLocations.Length != 0;
        if (!Locations.Visible) return false;

        var important = false;
        if (CachedLocationMessage is not null && UniqueLocations.Length == 0) CachedLocationMessage = null;
        if (recompile && UniqueLocations.Length > 0)
        {
            StringBuilder sb = new();

            var orderedLocations = UniqueLocations
                                  .OrderByDescending(id => Priority.Contains((long)id))
                                  .ThenBy(id => Client?.Locations[(long)id]).ToArray();

            var use2ndColumn = Hints.Keys.Any(id => orderedLocations.Contains((ulong)id));
            sb.Append("[table=").Append(use2ndColumn ? 2 : 1).Append("][cell bg=#00000069] ")
              .Append($"{UniqueLocations.Length:###,###}").Append(" Locations [/cell]");
            if (use2ndColumn) sb.Append("[cell bg=#00000069] Hinted Items [/cell]");

            for (var i = 0; i < orderedLocations.Length; i++)
            {
                var id = orderedLocations[i];
                var colColor = i % 2 == 0 ? "[cell bg=#00000044]" : "[cell]";
                sb.Append(colColor).Append(" {{loc;").Append(id).Append(';').Append(Client?.PlayerSlot)
                  .Append("}} [/cell]");
                if (!use2ndColumn)
                {
                    important = true;
                    continue;
                }

                sb.Append(colColor);
                if (Hints.TryGetValue((long)id, out var item))
                {
                    sb.Append(item);
                    important = important || HintImportance[(long)id];
                }
                else important = true;
                sb.Append(" [/cell]");
            }
            sb.Append("[/table]\n");
            CachedLocationMessage = sb.ToString().CompileRichText(GetCompileLocationEffects(), true);
        }

        Locations.Clear();
        Locations.ApplyCompiledPrintableObjs(CachedLocationMessage);
        return important;
    }

    public void UpdateFontSize(double size)
    {
        FontSize = (int)size;
        RenderHeader(true);
    }

    public void UpdateAlignment(int _) => RenderHeader(true);
    public void ToggleVisibility(bool _) => ToggleVisibility();

    public void ToggleVisibility()
    {
        Visible = SaveType<bool>.Load(TrackerPage.ShowEmptyCircles, false) || UniqueEntrances.Length != 0
                                                                           || UniqueLocations.Length != 0;
        if (!SaveType<bool>.Load(TrackerPage.ShowFutureCircles, true) && IsLater) Visible = false;
    }

    protected override void Dispose(bool disposing)
    {
        SaveType<bool>.RemoveIndividualEvent(TrackerPage.ShowEmptyCircles, ToggleVisibility);
        SaveType<double>.RemoveIndividualEvent(GlobalThemeSettings.GlobalFontSize, UpdateFontSize);
        SaveType<int>.RemoveIndividualEvent(HeaderAlignment, UpdateAlignment);
    }

    private Dictionary<string, Action<RichTextLabel, string[]>> GetCompileHeaderEffects()
    {
        if (CompileHeaderEffects is not null) return CompileHeaderEffects;
        return CompileHeaderEffects = MessageParser.CreateEffects(() => CallDeferred("RenderHeader", false));
    }

    private Dictionary<string, Action<RichTextLabel, string[]>> GetCompileEntranceEffects()
    {
        if (CompileLocationEffects is not null) return CompileLocationEffects;
        return CompileLocationEffects = MessageParser.CreateEffects(() => CallDeferred("RenderLocations", false));
    }

    private Dictionary<string, Action<RichTextLabel, string[]>> GetCompileLocationEffects()
    {
        if (CompileLocationEffects is not null) return CompileLocationEffects;
        return CompileLocationEffects = MessageParser.CreateEffects(() => CallDeferred("RenderLocations", false));
    }
}