using System;
using System.Collections.Generic;
using Godot;
using HydraTextClient.Scripts.Consoles;

namespace HydraTextClient.Scripts.Controllers;

public partial class ConsoleController : TabContainer
{
	[Export] private PackedScene NormalConsoleLabel;
	private static ConsoleController Singleton;
	private Dictionary<string, NormalConsole> Consoles = [];
	
	public override void _Ready()
	{
		Singleton = this;
		ConnectionController.OnClientConnection += (name, _, _) =>
		{
			if (Consoles.ContainsKey(name)) return;
			var cons = NormalConsoleLabel.Instantiate<NormalConsole>();
			cons.Name = $"Slot {name}";
			cons.FitContent = true;
			Consoles[name] = cons;
			GD.Print($"Added Console: [{name}]");

			ScrollContainer container = new();
			container.Name = $"Slot {name}";
			container.CallDeferred("add_child", cons);
			CallDeferred("add_child", container);
			WriteLine(name, "Opened Console");
		};

		ConnectionController.OnClientRemoved += (name, _, _) =>
		{
			if (!Consoles.Remove(name, out var cons)) return;
			CallDeferred("remove_child", cons.GetParent());
			cons.QueueFree();
		};
	}
	
	private void WriteLineLocal(string console, string text)
	{
		if (!Consoles.TryGetValue(console, out var cons)) return;
		cons.WriteLine(text);
	}
	
	private void WriteErrorLocal(string console, string text)
	{
		if (!Consoles.TryGetValue(console, out var cons)) return;
		cons.WriteError(text);
	}
	
	private void WriteErrorLocal(string console, Exception text)
	{
		if (!Consoles.TryGetValue(console, out var cons)) return;
		cons.WriteError(text);
	}
	
	public static void WriteLine(string console, string text) => Singleton.WriteLineLocal(console, text);
	public static void WriteError(string console, string text) => Singleton.WriteErrorLocal(console, text);
	public static void WriteError(string console, Exception text) => Singleton.WriteErrorLocal(console, text);
}