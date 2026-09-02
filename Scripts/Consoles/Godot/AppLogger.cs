using System;
using System.Linq;
using CreepyUtil.Archipelago;
using Godot;
using Godot.Collections;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.UIHelpers;
using Logger = Godot.Logger;

namespace HydraTextClient.Scripts.Consoles.Godot;

public partial class AppLogger(LoggerLabel label) : Logger
{
    private LoggerLabel _Label = label;
    private LimitedCollection<string> _Messages = new((int)SaveType<double>.Load(ChildLimiter.QueueSaveId, 200));
    private const string BLOCK = "          ";

    public override void _LogError(string function, string file, int line, string code, string rationale,
        bool editorNotify, int errorType,
        Array<ScriptBacktrace> scriptBacktraces)
    {
        _LogMessage(
            $"""
             file [{file}] in func [{function}] on line [{line}], [{errorType}]
             |{code}|
             ={rationale}=
             STACK TRACE
             {string.Join("\n", scriptBacktraces.Select((s, i) => $"[{i}]: [{s.Format()}]"))}
             """, true
        );
    }

    public override void _LogMessage(string message, bool error)
    {
        lock (_Label.LoggerWriter)
        {
            if (message.Length == 0) return;
            var timeStamp = DateTime.Now.ToString("[HH:mm:ss]");
            var split = message.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            if (split.Length == 0) return;

            _Label.LoggerWriter.WriteLine($"{timeStamp} [{(error ? "ERROR" : "Info")}] {split[0]}");
            var text
                = $"[color=darkgray]{timeStamp}[/color] [color={(error ? "red" : "white")}]{split[0].Replace("[", "[lb]")}";

            if (split.Length > 1)
            {
                text += $"\n{BLOCK}{string.Join($"\n{BLOCK} ", split.Skip(1))}";
                _Label.LoggerWriter.WriteLine(
                    $"\n{BLOCK}{string.Join($"\n{BLOCK} ", split.Skip(1)).Replace("[", "[lb]")}"
                );
            }

            if (error) _Label.LoggerWriter.Flush();

            _Messages.Add($"{text}[/color]");
            _Label.RefreshUI = true;
        }
    }

    public string[] Messages => _Messages.GetCollection;
    public void SetSize(int size) => _Messages.SetLimit(size);
}