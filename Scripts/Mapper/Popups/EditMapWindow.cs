using Godot;
using HydraTextClient.Scripts.Utility.Popups;

namespace HydraTextClient.Scripts.Mapper.Popups;

public partial class EditMapWindow : WindowSetter
{
    [Export] private TextEdit MapName;
    [Export] private OptionButton MapImage;
    [Export] private TextEdit MapAutoTabId;
    
    
}