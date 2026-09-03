using System.IO;
using Godot;
using HydraTextClient.Scripts.Utility.Popups;
using Newtonsoft.Json;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class AutoTrackingInputPopup : WindowSetter
{
    [Export] private LineEdit MapKey;
    [Export] private OptionButton ScopeOption;
    [Export] private LineEdit OnOffKey;
    [Export] private LineEdit TruthMapKey;
    private string MapPath;

    public void Setup(string mapPath)
    {
        MapPath = mapPath;
        AutoTrackingData data = new();
        if (File.Exists($"{mapPath}/autotracking.json"))
        {
            data = JsonConvert.DeserializeObject<AutoTrackingData>(File.ReadAllText($"{mapPath}/autotracking.json"));
        }

        MapKey.Text = data.RawMapKey;
        ScopeOption.Selected = data.KeyScope;
        OnOffKey.Text = data.EntranceRandoIndicatorKey;
        TruthMapKey.Text = data.EntranceRandoTrueMapKey;
    }

    public void Save()
    {
        AutoTrackingData data = new(MapKey.Text, OnOffKey.Text, TruthMapKey.Text);
        data.KeyScope = ScopeOption.Selected;
        File.WriteAllText($"{MapPath}/autotracking.json", JsonConvert.SerializeObject(data));
        Close();
    }
}