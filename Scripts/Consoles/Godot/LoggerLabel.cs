using System.IO;
using Godot;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Consoles.Godot;

public partial class LoggerLabel : RichTextLabel
{
    [Export] public bool RefreshUI;
    public AppLogger Logger;
    public StreamWriter LoggerWriter;

    public void Init()
    {
        LoggerWriter = File.CreateText($"{Directories.MainDirectory}/GodotLog.log");
        MainController.OnExit += () => LoggerWriter.Close();
        Logger = new AppLogger(this);
        Logger._LogMessage("Logger Init", false);
        SaveType<double>.AddIndividualEvent(
            ChildLimiter.QueueSaveId, d =>
            {
                Logger.SetSize((int)d);
                CallDeferred("Update");
            }
        );
    }

    public override void _Process(double delta)
    {
        if (RefreshUI) Update();
    }

    public void Update()
    {
        Text = string.Join("\n", Logger.Messages);
        RefreshUI = false;
    }
}