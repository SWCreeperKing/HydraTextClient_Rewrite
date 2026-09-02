using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Godot;

namespace HydraTextClient.Scripts.Controllers;

public static class ExternalAppController
{
    private static Dictionary<int, Task> TaskProcesses = [];
    private static ConcurrentDictionary<int, Process> Processes = [];
    private static ConcurrentDictionary<int, string> ProcessCommands = [];
    private static ConcurrentDictionary<string, int> CurrentlyRunningCommands = [];

    public static int StartProcess(string console, CoreAppEntry entry, string fileHash = "")
    {
        var command = entry.CommandString;
        if (CurrentlyRunningCommands.ContainsKey(command))
        {
            MainController.ShowError($"Trying to run already running command: [{command}]");
            return -1;
        }

        int appId;
        do appId = Random.Shared.Next();
        while (TaskProcesses.ContainsKey(appId) || appId is -1 or 404);

        if (!entry.FileExists()) return 404;
        if (fileHash is not "" && !entry.MatchHash(fileHash)) return -1;
        ProcessCommands.TryAdd(appId, command);

        TaskProcesses[appId] = Task.Run(async () =>
            {
                try
                {
                    ProcessStartInfo py = new()
                    {
                        FileName = entry.Executable, Arguments = entry.Arguments, RedirectStandardInput = true,
                        RedirectStandardError = true, RedirectStandardOutput = true, UseShellExecute = false,
                        CreateNoWindow = true,
                    };

                    using Process process = new();
                    process.StartInfo = py;

                    process!.Exited += (_, _) =>
                    {
                        entry.WriteLine(console, "Process Exit");
                        EndProcess(appId);
                    };
                    process!.Disposed += (_, _) =>
                    {
                        entry.WriteLine(console, "Process Disposed");
                        EndProcess(appId);
                    };
                    process!.ErrorDataReceived += (_, args) => entry.WriteError(console, args.Data!);
                    Processes[appId] = process;

                    process.Start();
                    process.BeginErrorReadLine();
                    process.BeginOutputReadLine();
                    process!.OutputDataReceived += (_, args) => entry.Interactor(
                        args.Data!, process!.StandardInput, console
                    );
                    await process.WaitForExitAsync();
                }
                catch (Exception e) { entry.WriteError(console, e); }
            }
        );

        return appId;
    }

    public static void EndProcess(int appId)
    {
        if (ProcessCommands.ContainsKey(appId))
        {
            ProcessCommands.Remove(appId, out var command);
            CurrentlyRunningCommands.Remove(command, out _);
        }

        try
        {
            if (!Processes.ContainsKey(appId)) return;
            Processes.Remove(appId, out var process);
            process?.Kill();
            process?.Dispose();
            TaskProcesses.Remove(appId);
        }
        catch (Exception e) { GD.PrintErr(e); }
    }

    public static string GetFileSha(string file)
    {
        using var stream = File.OpenRead(file);
        var sha = Convert.ToHexString(SHA256.HashData(stream));
        stream.Close();
        return sha;
    }

    public static void CloseAll()
    {
        if (TaskProcesses.Count == 0) return;
        foreach (var id in TaskProcesses.Keys)
        {
            try { EndProcess(id); }
            catch (Exception e) { GD.PrintErr(e); }
        }
    }
}

public abstract class CoreAppEntry
{
    public virtual string Executable { get; }
    public virtual string Arguments { get; }
    public virtual string ShortName { get; }
    public virtual string Hash { get; }
    public string CommandString => $"{Executable} {Arguments}";

    protected CoreAppEntry(string exe, string args)
    {
        if (!exe.StartsWith("http") && !exe.StartsWith("www."))
        {
            var fileToRun = exe.Replace(@"\\", "/");
            if (!File.Exists(fileToRun))
            {
                if (File.Exists($"{exe}.exe")) fileToRun += ".exe";
                else if (File.Exists($"{exe}.appimage")) fileToRun += ".appimage";
                else if (File.Exists($"{exe}.x86_64")) fileToRun += ".x86_64";
                else if (File.Exists($"{exe}.app")) fileToRun += ".app";
                else if (File.Exists($"{exe}.arm64")) fileToRun += ".arm64";
                else if (File.Exists($"{exe}.bat")) fileToRun += ".bat";
                else if (File.Exists($"{exe}.sh")) fileToRun += ".sh";
                else fileToRun = "";
            }

            Executable = fileToRun;
            Arguments = args;
            ShortName = Path.GetFileNameWithoutExtension(fileToRun);
            Hash = fileToRun is "" ? "" : ExternalAppController.GetFileSha(fileToRun);
        }
        else
        {
            Executable = exe;
            Arguments = args;
            ShortName =  string.Join("//", exe.Split("//")[1..]).Split('/')[0];
            Hash = null;
        }
    }

    public abstract void Interactor(string text, StreamWriter input, string console);

    public bool FileExists() => !(Executable is "" || !File.Exists(Executable));
    public bool MatchHash(string fileHash) => Hash == fileHash;

    public void WriteError(string console, Exception error)
    {
        ConsoleController.WriteError(console, $"[{ShortName}] Threw an error:");
        ConsoleController.WriteError(console, error);
    }

    public void WriteError(string console, string message, Exception error)
    {

        ConsoleController.WriteError(console, $"[{ShortName}]: {message}");
        WriteError(console, error);
    }

    public void WriteError(string console, string error)
        => ConsoleController.WriteError(console, $"[{ShortName}] {error}");

    public void WriteLine(string console, string text) => ConsoleController.WriteLine(console, $"[{ShortName}] {text}");
}

public class ReadOnlyEntry(string exe, string args, string context) : CoreAppEntry(exe, args)
{
    public string Context = context;
    public override void Interactor(string text, StreamWriter input, string console) => WriteLine(console, text);
}