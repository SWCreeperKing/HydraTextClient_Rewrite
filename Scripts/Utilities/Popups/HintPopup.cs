using System;
using CreepyUtil.Archipelago.ApClient;
using Godot;
using HydraTextClient.Scripts.Utility.Popups;

namespace HydraTextClient.Scripts.Utilities.Popups;

public partial class HintPopup : WindowSetter
{
    [Export] private Label Label;
    private Action OnClick;
    private string CopySubject;

    public void Set(ApClient client, string title, string text, string command, string subject)
    {
        CopySubject = subject;
        Title = title;
        Label.Text = text;
        OnClick += () => client.Say(command);
    }

    public void Copy()
    {
        DisplayServer.ClipboardSet(CopySubject);
        Close();
    }

    public void ConfirmClicked()
    {
        OnClick?.Invoke();
        Close();
    }
}