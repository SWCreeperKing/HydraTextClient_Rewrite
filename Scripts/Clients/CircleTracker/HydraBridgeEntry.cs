using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using CreepyUtil.Archipelago.ApClient;
using Godot;
using HydraTextClient.Scripts.Controllers;
using Newtonsoft.Json;

namespace HydraTextClient.Scripts.Clients.CircleTracker;

public class HydraBridgeEntry(string apDir, ApClient client, TrackerPage page, bool useDebug)
    : CoreAppEntry($"{apDir}/ArchipelagoLauncher{(useDebug ? "Debug" : "")}", "HydraUTBridge")
{
    public readonly ConcurrentDictionary<string, string> EntranceKeyMap = [];
    public readonly ConcurrentQueue<(int, long[])> ItemsQueued = [];
    public readonly ConcurrentQueue<string> EntrancesQueued = [];
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
                    if (text.StartsWith("sending_data_store_keys "))
                    {
                        page.ListenForEntrances(JsonConvert.DeserializeObject<string[]>(text[24..]));
                        return;
                    }

                    if (text.StartsWith("exit")) return;
                    if (text.StartsWith("ERROR: "))
                    {
                        WriteError(console, text);
                        MainController.ShowError(text);
                        return;
                    }

                    if (text.StartsWith("Circle "))
                    {
                        var circleData = JsonConvert.DeserializeObject<CircleData>(text[7..]);
                        page.Circles[circleData.Circle] = [.. circleData.AllAvailableLocations.Select(loc => loc.Id)];

                        if (page.Entrances.TryGetValue(circleData.Circle, out var entrances) && entrances.Length > 0)
                        {
                            foreach (var entrance in entrances)
                            {
                                if (!page.EntranceEarliestCircle.TryGetValue(entrance, out var value)
                                    || value <= circleData.Circle) continue;
                                page.EntranceEarliestCircle.Remove(entrance, out _);
                            }
                        }

                        page.Entrances[circleData.Circle] = circleData.EntranceNames;

                        foreach (var entrance in circleData.EntranceNames)
                        {
                            if (page.EntranceEarliestCircle.ContainsKey(entrance)
                                && page.EntranceEarliestCircle[entrance] <= circleData.Circle) continue;
                            page.EntranceEarliestCircle[entrance] = circleData.Circle;
                        }
                        page.QueueUpdate();
                    }

                    if (text.StartsWith("counts "))
                    {
                        page.NextProgression = new ConcurrentDictionary<long, int>(
                            JsonConvert.DeserializeObject<Dictionary<long, int>>(text[7..])
                        );
                        WriteLine(console, $"Got next progression counts: [{page.NextProgression.Count}]");
                        page.QueueUpdate();
                    }

                    if (text.StartsWith("Circle ") || text is "start" || text.StartsWith("counts ")) // respond
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

                        while (ItemsQueued.IsEmpty && EntrancesQueued.IsEmpty) Task.Delay(50).Wait();

                        if (!EntrancesQueued.IsEmpty)
                        {
                            List<string> entranceList = [];
                            while (!EntrancesQueued.IsEmpty)
                            {
                                EntrancesQueued.TryDequeue(out var entrance);
                                entranceList.Add(entrance);
                            }

                            var earliestCircle = page.EntranceEarliestCircle.Values.Min();
                            ItemsQueued.Clear(); // only re-calc circles if entrance was in logic
                            for (var i = earliestCircle; i <= page.RawCircleItems.Keys.Max(); i++)
                            {
                                if (!page.RawCircleItems.TryGetValue(i, out var item)) continue;
                                ItemsQueued.Enqueue((i, item));
                            }

                            input.WriteLine($"entrance {JsonConvert.SerializeObject(entranceList)}");
                        }

                        if (!ItemsQueued.IsEmpty)
                        {
                            ItemsQueued.TryDequeue(out var next);
                            CheckNextProg = true;
                            WriteLine(
                                console,
                                $"Requesting Data for circle [{next.Item1}] with [{next.Item2.Length}] total items"
                            );
                            input.WriteLine($"{next.Item1}|{string.Join(',', next.Item2)}");
                        }
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
        [JsonProperty("circle")] public int Circle;
        [JsonProperty("location_list")] public LocationData[] AllAvailableLocations;
        [JsonProperty("glitched_list")] public ulong[] GlitchedLocations;
        [JsonProperty("entrances")] public string[] EntranceNames;
    }

    private struct LocationData
    {
        [JsonProperty("id")] public ulong Id;
        [JsonProperty("is_excluded")] public bool IsExcluded;
    }
}