using System;
using System.IO;
using System.Linq;
using System.Text;
using CreepyUtil.Archipelago;
using Godot;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Consoles;

public partial class NormalConsole : RichTextLabel
{
    private static StreamWriter SlotLogs = File.CreateText($"{Directories.MainDirectory}/SlotLogs.log");
    private static bool Has;
    private LimitedCollection<string> Messages = new((int)SaveType<double>.Load(ChildLimiter.QueueSaveId, 200));
    private const string BLOCK = "          ";

    public override void _Ready()
    {
        if (!Has)
        {
            MainController.OnExit += () => SlotLogs.Close();
            Has = true;
        }

        AutowrapMode = TextServer.AutowrapMode.Off;
        SelectionEnabled = true;
        SaveType<double>.AddIndividualEvent(ChildLimiter.QueueSaveId, SetLimit);
    }

    private void SetLimit(double d)
    {
        Messages.SetLimit((int)d);
        CallDeferred("Update");
    }

    private void AddLine(string text)
    {
        Messages.Add(text);
        Update();
    }

    public void WriteLine(string message, bool error = false)
    {
        lock (SlotLogs)
        {
            if (message.Length == 0) return;
            var split = message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 0) return;

            StringBuilder sb = new();
            try { SlotLogs.WriteLine($"{DateTime.Now:[HH:mm:ss]} [{(error ? "ERROR" : "Info")}] [{Name}] {split[0]}"); }
            catch (OverflowException) { }
            sb.Append(GetTimestamp()).Append("[color=").Append(error ? "red" : "white").Append(']')
              .Append(split[0].Replace("[", "[lb]"));
            if (split.Length > 1)
            {
                sb.Append('\n').Append(BLOCK).Append(string.Join($"\n{BLOCK}", split.Skip(1)).Replace("[", "[lb]"));
                SlotLogs.WriteLine($"\n{BLOCK}{string.Join($"\n{BLOCK}", split.Skip(1))}");
            }
            try
            {
                if (error) SlotLogs.Flush();
            }
            catch (ArgumentOutOfRangeException) { }
            
            CallDeferred("AddLine", sb.ToString());
        }
    }

    public void WriteError(Exception err) => WriteLine($"{err.Message}\n{err.StackTrace}", true);
    public void WriteError(string err) => WriteLine(err, true);
    public string GetTimestamp() => $"[color=darkgray]{DateTime.Now:[HH:mm:ss]}[/color] ";
    public void Update() => Text = string.Join("\n", Messages.GetCollection);

    protected override void Dispose(bool disposing)
        => SaveType<double>.RemoveIndividualEvent(ChildLimiter.QueueSaveId, SetLimit);
}