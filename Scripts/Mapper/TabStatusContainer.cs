using System;
using Godot;

namespace HydraTextClient.Scripts.Mapper;

public partial class TabStatusContainer(MapLoader loader) : TabContainer
{
    public MapLoader Loader = loader;
    public int TabStatus = -1;
    public bool QueueUpdateStatus;

    public override void _Process(double delta)
    {
        if (!QueueUpdateStatus) return;
        QueueUpdateStatus = false;
        TabStatus = -1;

        for (var t = 0; t < GetTabCount(); t++)
        {
            var status = GetTabControl(t) switch
            {
                TabStatusContainer tab => tab.TabStatus, MapNavigator map => map.LowestLocationStatus, _ => -1,
            };

            SetTabIcon(t, Loader.TabIndicatorData.GetImage(status, Loader.ItemImageLoader));
            SetTabIconMaxWidth(t, 14);

            if (status is -1) continue;
            TabStatus = TabStatus is -1 ? status : Math.Min(status, TabStatus);
        }

        var parent = GetParent();
        if (parent is not TabStatusContainer parentTab) return;
        parentTab.QueueUpdateStatus = true;
    }
}