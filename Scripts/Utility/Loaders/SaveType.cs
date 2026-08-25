using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using HydraTextClient.Scripts.Controllers;
using Newtonsoft.Json;

namespace HydraTextClient.Scripts.Utility.Loaders;

public static class SaveType<T>
{
    private static string SaveDir = $"{Directories.MainDirectory}/Types";
    private static string SaveFile = $"{SaveDir}/type_{typeof(T).Name}.json";
    private static Dictionary<string, T> SaveItems = [];
    private static Dictionary<string, List<Action<T>>> IndividualOnSaveEvent = [];
    public static event Action<string, T>? OnSaveEvent;
    public static event Action<string, T>? OnDeleteEvent;
    public static int Count => SaveItems.Count;

    static SaveType()
    {
        if (!Directory.Exists(SaveDir)) Directory.CreateDirectory(SaveDir);
        LoadFromFile();
        SaveItems ??= [];
        MainController.OnSave += SaveToFile;
        OnSaveEvent += (id, value) =>
        {
            if (!IndividualOnSaveEvent.TryGetValue(id, out var list)) return;
            foreach (var item in list) item?.Invoke(value);
        };
    }

    public static event Func<(string, Action<T>)> OnIndividualSaveEvent
    {
        add
        {
            var (key, action) = value!.Invoke();
            AddIndividualEvent(key, action);
        }
        remove
        {
            var (key, action) = value!.Invoke();
            RemoveIndividualEvent(key, action);
        }
    }

    public static void Save(string id, T value, bool broadcast)
    {
        SaveItems[id] = value;
        if (broadcast) OnSaveEvent?.Invoke(id, value);
    }

    public static void Delete(string key)
    {
        if (!SaveItems.Remove(key, out var item)) return;
        OnDeleteEvent?.Invoke(key, item);
    }

    public static T Load(string id, T def, bool saveDefault = true)
    {
        if (TryGet(id, out var val)) return val;
        if (saveDefault) return SaveItems[id] = def;
        return def;
    }

    public static string[] GetKeys() => [.. SaveItems.Keys];
    public static T[] GetValues() => [.. SaveItems.Values];
    public static bool ContainsKey(string id) => SaveItems.ContainsKey(id);
    public static bool TryGet(string id, out T val) => SaveItems.TryGetValue(id, out val);

    private static void SaveToFile()
        => File.WriteAllText(SaveFile, JsonConvert.SerializeObject(SaveItems /*, Formatting.Indented*/));

    private static void LoadFromFile()
    {
        if (!File.Exists(SaveFile)) return;
        SaveItems = JsonConvert.DeserializeObject<Dictionary<string, T>>(File.ReadAllText(SaveFile));
    }

    public static void AddIndividualEvent(string key, Action<T> action)
    {
        if (!IndividualOnSaveEvent.TryGetValue(key, out var list)) IndividualOnSaveEvent[key] = list = [];
        list.Add(action);
    }

    public static void RemoveIndividualEvent(string key, Action<T> action)
    {
        if (!IndividualOnSaveEvent.TryGetValue(key, out var list)) return;
        list.Remove(action);
    }
    
    public static void AddIndividualEvents(Action<T> action, params string[] keys)
    {
        foreach (var key in keys) AddIndividualEvent(key, action);
    }

    public static void RemoveIndividualEvents(Action<T> action, params string[] keys)
    {
        foreach (var key in keys) RemoveIndividualEvent(key, action);
    }
}