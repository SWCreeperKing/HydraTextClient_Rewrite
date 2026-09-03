using System;
using System.Collections.Concurrent;
using Archipelago.MultiClient.Net;
using Archipelago.MultiClient.Net.Enums;
using Archipelago.MultiClient.Net.Models;
using Archipelago.MultiClient.Net.Packets;
using CreepyUtil.Archipelago;
using Godot;
using Godot.Collections;
using HydraTextClient.Scripts.Clients.TextClient.MessageTypes;
using HydraTextClient.Scripts.Clients.TextClient.ParserEffects;
using HydraTextClient.Scripts.Connection.Slots;
using HydraTextClient.Scripts.Controllers;
using HydraTextClient.Scripts.Settings;
using HydraTextClient.Scripts.Utilities.ItemFilter;
using HydraTextClient.Scripts.Utility;
using HydraTextClient.Scripts.Utility.Loaders;
using HydraTextClient.Scripts.Utility.UIHelpers;

namespace HydraTextClient.Scripts.Clients.TextClient;

public partial class TextClient : Control
{
    public const string FontSizeId = "TextClient/FontSize";
    public const string ShowProgressive = "TextClient/show_progressive";
    public const string ShowUseful = "TextClient/show_useful";
    public const string ShowNormal = "TextClient/show_normal";
    public const string ShowTrap = "TextClient/show_trap";
    public const string ShowOnlyYou = "TextClient/show_only_you";
    public const string ShowFoundHints = "TextClient/show_found_hints";
    public const string ShowGamePortraits = "TextClient/show_portraits";
    public const string ShowTimestamps = "TextClient/show_timestamps";
    [Export] private Dictionary<MessageType, ChildLimiter> Containers = [];
    [Export] private Dictionary<MessageType, PackedScene> MessageScenes = [];
    [Export] private Array<ScrollFix> ScrollFixes = [];
    [Export] private Array<ChildLimiter> UniqueLimiters = [];
    [Export] private LineEdit SendMessageEdit;
    [Export] private EmotePicker EmotePicker;
    private int ScrollBackNum;
    private bool ToScroll;
    private double MessageCooldown;
    private long LastSelected;
    private bool WasLastMessageHintLocation = false;
    private string HeldText;

    private Dictionary<MessageType, string> MessageTypeSendSaveIds = new()
    {
        [MessageType.ClientMessage] = "TextClient/Sep/ClientMessage", [MessageType.ItemLog] = "TextClient/Sep/ItemLog",
        [MessageType.ItemCheatLog] = "TextClient/Sep/ItemCheatLog",
        [MessageType.ServerMessage] = "TextClient/Sep/ServerMessage",
        [MessageType.HintMessage] = "TextClient/Sep/HintMessage",
        [MessageType.CommandResult] = "TextClient/Sep/CommandResult",
        [MessageType.JoinMessage] = "TextClient/Sep/JoinMessage",
        [MessageType.LeaveMessage] = "TextClient/Sep/LeaveMessage",
        [MessageType.TagsChangedMessage] = "TextClient/Sep/TagsChangedMessage",
        [MessageType.GoalMessage] = "TextClient/Sep/GoalMessage", [MessageType.DeathLink] = "TextClient/Sep/DeathLink",
        [MessageType.TrapLink] = "TextClient/Sep/TrapLink", [MessageType.PrintJson] = "TextClient/Sep/PrintJson",
    };

    private static ConcurrentQueue<IMessagePacket> MessageQueue = [];
    private LimitedCollection<string> SentMessageHistory = new(50);

    public override void _Ready()
    {
        EmotePicker.EmotePicked += SendMessageEdit.AppendText;
        SendMessageEdit.GuiInput += input =>
        {
            if (SentMessageHistory.Count() is 0 || input is InputEventMouseMotion) return;
            switch (input)
            {
                case InputEventMouseButton iemb when GetRect().HasPoint(iemb.Position):
                case InputEventJoypadMotion:
                case InputEventJoypadButton: return;
            }

            if (input is not InputEventKey key)
            {
                if (ScrollBackNum != -1) return;
                SendMessageEdit.Text = "";
                HeldText = "";
                ScrollBackNum = -1;
                return;
            }

            if (!key.IsPressed()) return;

            if (ScrollBackNum == -1 && SendMessageEdit.Text != "" && SendMessageEdit.Text != HeldText)
                HeldText = SendMessageEdit.Text;

            switch (key.Keycode)
            {
                case Key.Up: ScrollBackNum--; break;
                case Key.Down: ScrollBackNum++; break;
                default: return;
            }

            if (ScrollBackNum == -2) ScrollBackNum = SentMessageHistory.Count() - 1;
            else if (ScrollBackNum > SentMessageHistory.Count() - 1) ScrollBackNum = -1;

            SendMessageEdit.Text = ScrollBackNum == -1 ? HeldText : SentMessageHistory[ScrollBackNum];
        };

        SendMessageEdit.FocusExited += () =>
        {
            if (ScrollBackNum == -1) return;
            SendMessageEdit.Text = "";
            HeldText = "";
            ScrollBackNum = -1;
        };

        SendMessageEdit.FocusEntered += () => ScrollBackNum = SentMessageHistory.Count() - 1;

        ConnectionController.OnClientPrepareConnection += (_, client, _, _) =>
        {
            try
            {
                client.ExcludeBouncedPacketsFromSelf = false;
                client.OnChatPrintPacketReceived += packet => Enqueue(MessageType.ClientMessage, packet);
                client.OnItemLogPacketReceived += packet => Enqueue(MessageType.ItemLog, packet);
                client.OnItemCheatLogPacketReceived += packet => Enqueue(MessageType.ItemCheatLog, packet);
                client.OnServerMessagePacketReceived += packet => Enqueue(MessageType.ServerMessage, packet);
                client.OnHintPrintJsonPacketReceived += packet => Enqueue(MessageType.HintMessage, packet);
                client.OnCommandResult += packet => Enqueue(MessageType.CommandResult, packet);
                client.OnJoinLogPacketReceived += packet => Enqueue(MessageType.JoinMessage, packet);
                client.OnLeaveLogPacketReceived += packet => Enqueue(MessageType.LeaveMessage, packet);
                client.OnTagsChangedLogPacketReceived += packet => Enqueue(MessageType.TagsChangedMessage, packet);
                client.OnGoalPrintJsonPacketReceived += packet => Enqueue(MessageType.GoalMessage, packet);
                client.OnPrintJsonPacketReceived += packet => Enqueue(MessageType.PrintJson, packet);
                client.OnDeathLinkPacketReceived += (groups, player, message) =>
                {
                    if (ConnectionController.LeaderClient! != client) return;
                    MessageQueue.Enqueue(new DeathLinkPacket(groups, player, message));
                };
                client.OnUnregisteredTrapLinkReceived += (player, trap) =>
                {
                    if (ConnectionController.LeaderClient! != client) return;
                    MessageQueue.Enqueue(new TrapLinkPacket(player, trap));
                };
                client.HintsTrackedEvent += (_, newHints) =>
                {
                    var leader = ConnectionController.LeaderClient!;
                    foreach (var hint in newHints)
                    {
                        if (hint.ReceivingPlayer == leader.PlayerSlot) continue;
                        if (hint.FindingPlayer == leader.PlayerSlot) continue;
                        Enqueue(
                            MessageType.HintMessage, new HintPrintJsonPacket
                            {
                                Found = hint.Found, Item = new NetworkItem
                                {
                                    Flags = hint.ItemFlags, Item = hint.ItemId, Location = hint.LocationId,
                                    Player = hint.FindingPlayer,
                                },
                                MessageType = JsonMessageType.Hint, ReceivingPlayer = hint.ReceivingPlayer,
                            }
                        );
                    }
                };

                // client.OnUnhandledPacketReceived += packet => { };
            }
            catch (Exception e) { MainController.ShowError("Error with setting up client data", e); }
        };

        SettingsCreator.Tab(
            "Text Client",
            tab =>
            {
                tab
                   .AddSpinBox(
                        "Chat Message Animation Duration (sec)", AnimatedMessageScene.TextAnimationLength, 1.5f, 0, box
                            =>
                        {
                            box.Step = .01f;
                            box.AllowGreater = true;
                            box.MinValue = 0;
                        }
                    )
                   .AddSeparator()
                   .AddCheckBox("Show Timestamps", ShowTimestamps, true)
                   .AddCheckBox("Show Game Portraits", ShowGamePortraits, true)
                   .AddCheckBox("Hide Item Fallback Image", ItemEffect.FallbackSaveId)
                   .AddSeparator()
                   .AddLineEdit("Join Message", JoinMessage.SaveId, JoinMessage.Default, JoinMessage.Hint)
                   .AddSeparator()
                   .AddLineEdit("Leave Message", LeaveMessage.SaveId, LeaveMessage.Default, LeaveMessage.Hint)
                   .AddSeparator()
                   .AddLineEdit("Tags Changed", TagsChanged.SaveId, TagsChanged.Default, TagsChanged.Hint)
                   .AddSeparator()
                   .AddLineEdit("Goal Message", GoalMessage.SaveId, GoalMessage.Default, GoalMessage.Hint)
                   .AddSeparator()
                   .AddLineEdit("Hint Message", HintMessage.SaveId, HintMessage.Default, HintMessage.Hint)
                   .AddSeparator()
                   .AddLineEdit(
                        "Trap Message", TrapLinkMessage.SaveIdMessage, TrapLinkMessage.Default, TrapLinkMessage.Hint
                    )
                   .AddSeparator()
                   .AddLineEdit(
                        "Death Message", DeathLinkMessage.SaveIdMessage, DeathLinkMessage.DefaultMessage,
                        DeathLinkMessage.Hint
                    )
                   .AddSeparator()
                   .AddLineEdit(
                        "Unknown Death Cause", DeathLinkMessage.SaveIdUnknown, DeathLinkMessage.DefaultUnknown,
                        DeathLinkMessage.HintUnknown
                    )
                   .AddSeparator()
                   .AddLineEdit(
                        "Item Message (Same Person)", ItemMessage.SaveIdSamePerson, ItemMessage.DefaultSamePerson,
                        ItemMessage.HintSamePerson
                    ).AddSeparator()
                   .AddLineEdit(
                        "Item Message (Different Person)", ItemMessage.SaveIdDifferentPerson,
                        ItemMessage.DefaultDifferentPerson, ItemMessage.HintDifferentPerson
                    ).AddSeparator()
                   .AddLineEdit(
                        "Item Message (Cheated)", ItemCheatMessage.SaveId, ItemCheatMessage.Default,
                        ItemCheatMessage.Hint
                    )
                   .AddSeparator()
                   .AddLineEdit(
                        "Player Text (Without Alias)", PlayerEffect.SaveIdNoAlias, PlayerEffect.DefaultNoAlias,
                        PlayerEffect.HintNoAlias
                    )
                   .AddSeparator()
                   .AddLineEdit(
                        "Player Text (With Alias)", PlayerEffect.SaveIdWithAlias, PlayerEffect.DefaultWithAlias,
                        PlayerEffect.HintAlias
                    )
                   .AddSeparator()
                   .AddLineEdit("Item Text", ItemEffect.SaveId, ItemEffect.Default, ItemEffect.Hint)
                   .AddCheckBox(
                        "Send Chat Messages In All Tab", MessageTypeSendSaveIds[MessageType.ClientMessage], true, 1
                    )
                   .AddCheckBox(
                        "Send Item Log Messages In All Tab", MessageTypeSendSaveIds[MessageType.ItemLog], true, 1
                    )
                   .AddCheckBox(
                        "Send Item Cheat Log Messages In All Tab", MessageTypeSendSaveIds[MessageType.ItemCheatLog],
                        true, 1
                    )
                   .AddCheckBox(
                        "Send Server Messages In All Tab", MessageTypeSendSaveIds[MessageType.ServerMessage], true, 1
                    )
                   .AddCheckBox(
                        "Send Hint Messages In All Tab", MessageTypeSendSaveIds[MessageType.HintMessage], true, 1
                    )
                   .AddCheckBox(
                        "Send Command Results In All Tab", MessageTypeSendSaveIds[MessageType.CommandResult], true, 1
                    )
                   .AddCheckBox(
                        "Send Join Messages In All Tab", MessageTypeSendSaveIds[MessageType.JoinMessage], true, 1
                    )
                   .AddCheckBox(
                        "Send Leave Messages In All Tab", MessageTypeSendSaveIds[MessageType.LeaveMessage], true, 1
                    )
                   .AddCheckBox(
                        "Send Tags Changed Messages In All Tab", MessageTypeSendSaveIds[MessageType.TagsChangedMessage],
                        true, 1
                    )
                   .AddCheckBox(
                        "Send Goal Messages In All Tab", MessageTypeSendSaveIds[MessageType.GoalMessage], true, 1
                    )
                   .AddCheckBox(
                        "Send DeathLink Messages In All Tab", MessageTypeSendSaveIds[MessageType.DeathLink], true, 1
                    )
                   .AddCheckBox(
                        "Send TrapLink Messages In All Tab", MessageTypeSendSaveIds[MessageType.TrapLink], true, 1
                    )
                   .AddCheckBox(
                        "Send PrintJson Messages In All Tab", MessageTypeSendSaveIds[MessageType.PrintJson], true, 1
                    );
            }
        );


        ConnectionController.DataClearCall += () => CallDeferred("RemoveMessages");
    }

    public override void _Process(double delta)
    {
        if (MessageQueue.IsEmpty) return;

        if (!ConnectionController.HasLeaderClient)
        {
            MessageQueue.Clear();
            return;
        }

        if (!MessageQueue.TryDequeue(out var messagePacket)) return;
        if (messagePacket.GetMsgType() is MessageType.ItemLog
            && messagePacket.GetPacket() is ItemPrintJsonPacket itemPacket)
        {
            if (SaveType<FilterType>.TryGet(itemPacket.UID, out var filter) && !filter.ShowInItemLog) return;
            var flags = itemPacket.Item.Flags;
            if (flags.HasFlag(ItemFlags.Advancement))
            {
                if (!SaveType<bool>.Load(ShowProgressive, true)) return;
            }
            else if (flags.HasFlag(ItemFlags.NeverExclude))
            {
                if (!SaveType<bool>.Load(ShowUseful, true)) return;
            }
            else if (flags.HasFlag(ItemFlags.Trap))
            {
                if (!SaveType<bool>.Load(ShowTrap, true)) return;
            }
            else if (flags is ItemFlags.None && !SaveType<bool>.Load(ShowNormal, true)) return;
            var leader = ConnectionController.LeaderClient!;
            var receiver = leader.PlayerNames[itemPacket.ReceivingPlayer];
            var finder = leader.PlayerNames[itemPacket.FindingPlayer];
            if (SaveType<bool>.Load(ShowOnlyYou, false) && !SlotView.ContainsSlot(receiver)
                                                        && !SlotView.ContainsSlot(finder)) return;
        }

        if (messagePacket.GetMsgType() is MessageType.HintMessage
            && messagePacket.GetPacket() is HintPrintJsonPacket hintPacket)
        {
            if (!SaveType<bool>.Load(ShowFoundHints, true) && hintPacket.Found!.Value) return;
        }

        if (!MessageScenes.TryGetValue(messagePacket.GetMsgType(), out var scene)) return;

        if (SaveType<bool>.Load(MessageTypeSendSaveIds[messagePacket.GetMsgType()], true))
            SendMessage(scene, messagePacket, MessageType.All);
        SendMessage(scene, messagePacket, messagePacket.GetMsgType());
    }

    private void SendMessage(PackedScene scene, IMessagePacket messagePacket, MessageType containerType)
    {
        var msgScene = scene.Instantiate<MessageScene>();
        msgScene.SetPacket(messagePacket);
        msgScene.TimeStamp.Text = messagePacket.GetTimestamp();
        if (Containers.TryGetValue(containerType, out var allContainer)) allContainer.AddToLimiter(msgScene);
    }

    public void Enqueue(MessageType type, ArchipelagoPacketBase packet)
        => MessageQueue.Enqueue(new MessagePacket(type, packet));

    public void SubmitMsg()
    {
        SendMessage(SendMessageEdit.Text);
        Clear("", SendMessageEdit);
    }

    public void SendMessage(string message)
    {
        if (!ConnectionController.HasLeaderClient) return;
        SentMessageHistory.Enqueue(message);
        ConnectionController.LeaderClient?.Say(message);
        HeldText = "";
        ScrollBackNum = -1;
    }

    public void Clear(string _, LineEdit edit) => edit.Clear();

    public void ScrollToBottom()
    {
        foreach (var scrollFix in ScrollFixes) scrollFix.ScrollToBottom();
    }

    public void RemoveMessages()
    {
        foreach (var container in UniqueLimiters) container.EmptyLimiter();
    }
}

public enum MessageType
{
    All, ClientMessage, ItemLog,
    ItemCheatLog, ServerMessage, HintMessage,
    CommandResult, JoinMessage, LeaveMessage,
    TagsChangedMessage, GoalMessage, DeathLink,
    TrapLink, PrintJson,
}

public readonly struct DeathLinkPacket(string[] group, string player, string? cause) : IMessagePacket
{
    public readonly string[] Groups = group;
    public readonly string Player = player;
    public readonly string? Cause = cause;
    public readonly string TimeStamp = MainController.GetTimestamp();
    public MessageType GetMsgType() => MessageType.DeathLink;
    public string GetTimestamp() => TimeStamp;
    public ArchipelagoPacketBase GetPacket() => null;
}

public readonly struct TrapLinkPacket(string player, string trap) : IMessagePacket
{
    public readonly string Player = player;
    public readonly string Trap = trap;
    public readonly string TimeStamp = MainController.GetTimestamp();
    public MessageType GetMsgType() => MessageType.TrapLink;
    public string GetTimestamp() => TimeStamp;
    public ArchipelagoPacketBase GetPacket() => null;
}

public readonly struct MessagePacket(MessageType type, ArchipelagoPacketBase packet) : IMessagePacket
{
    public readonly MessageType Type = type;
    public readonly ArchipelagoPacketBase Packet = packet;
    public readonly string TimeStamp = MainController.GetTimestamp();
    public MessageType GetMsgType() => Type;
    public string GetTimestamp() => TimeStamp;
    public ArchipelagoPacketBase GetPacket() => Packet;
}

public interface IMessagePacket
{
    public MessageType GetMsgType();
    public string GetTimestamp();
    public ArchipelagoPacketBase GetPacket();
}