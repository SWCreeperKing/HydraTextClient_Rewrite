using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CreepyUtil.Archipelago.ApClient;
using HydraTextClient.Scripts.Controllers;
using Newtonsoft.Json;

namespace HydraTextClient.Scripts.Clients.CircleTracker;

public class HydraBridgeEntry(string apDir, ApClient client, TrackerPage page, bool useDebug)
    : CoreAppEntry($"{apDir}/ArchipelagoLauncher{(useDebug ? "Debug" : "")}", "HydraUTBridge")
{
    public readonly ConcurrentQueue<(int, long[])> ItemsQueued = [];
    public bool CheckNextProg;

    public override void Interactor(string text, StreamWriter input, string console)
    {
        try
        {
            switch (text)
            {
                case "": break;
                case "Player YAML not installed or Generator failed": page.CallDeferred("Failure", text); break;
                case "READY": WriteLine(console, "UT line of communication ready and established"); break;
                case "slot_name":
                    WriteLine(console, $"Sending Slot Name: [{client.PlayerName}]");
                    input.WriteLine(client.PlayerName);
                    break;
                case "game": input.WriteLine(client.PlayerGame); break;
                case "slot_data": input.WriteLine(JsonConvert.SerializeObject(client.SlotData)); break;
                case "missing_locations":
                    input.WriteLine(string.Join(',', client.Locations.Select(kv => kv.Value))); break;

                default:
                    if (text.StartsWith("exit")) return;
                    if (text.StartsWith("ERROR: "))
                    {
                        WriteError(console, text);
                        MainController.ShowError(text);
                        return;
                    }

                    if (text.StartsWith("Circle "))
                    {
                        var split = text.Split('|');
                        var circle = int.Parse(split[0].Replace("Circle ", ""));
                        var remaining = split[1][1..^1];
                        if (remaining.Trim().Length is 0) return;
                        var ids = remaining.Split(',').Select(id => ulong.Parse(id.Trim())).ToArray();
                        page.Circles.TryAdd(circle, ids);
                        page.QueueUpdate();
                    }

                    if (text.StartsWith("counts "))
                    {
                        var counts = text.Replace("counts ", "").Trim().Split(
                            " ", StringSplitOptions.RemoveEmptyEntries
                        );
                        page.NextProgression.Clear();
                        foreach (var entry in counts)
                        {
                            var split = entry.Split('=');
                            page.NextProgression[long.Parse(split[0])] = int.Parse(split[1]);
                        }
                        WriteLine(console, $"Got counts: [{counts.Length}]");
                        page.QueueUpdate();
                    }

                    if (text.StartsWith("Circle ") || text is "start" || text.StartsWith("counts "))
                    {
                        if (ItemsQueued.IsEmpty && CheckNextProg)
                        {
                            CheckNextProg = false;
                            WriteLine(console, "Requesting next progression");
                            input.WriteLine(
                                $"next_items {string.Join(',', client.ItemHandler.Items.Select(item => item.ItemId))}|{string.Join(',', client.Items.Select(kv => kv.Value))}"
                            );
                            return;
                        }

                        while (ItemsQueued.IsEmpty) Task.Delay(20).Wait();
                        ItemsQueued.TryDequeue(out var next);
                        CheckNextProg = true;
                        WriteLine(
                            console, $"Requesting Data for circle [{next.Item1}] with [{next.Item2.Length}] total items"
                        );
                        input.WriteLine($"{next.Item1}|{string.Join(',', next.Item2)}");
                        return;
                    }

                    WriteLine(console, $"Command: [{text}]");
                    break;
            }
        }
        catch (Exception e)
        {
            WriteError(console, $"Error with [{text}]", e);
            Task.Delay(120).Wait();
        }
    }

    private struct CircleData
    {
        [JsonPropertyName("circle")] public int Circle;
        [JsonPropertyName("location_list")] public LocationData[] AllAvailableLocations;
        
    }
    
    private struct LocationData
    {
        [JsonPropertyName("id")] private ulong Id;
        [JsonPropertyName("is_excluded")] private bool IsExcluded;
    }
}