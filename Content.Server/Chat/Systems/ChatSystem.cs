using System.Linq;
using System.Text;
using Content.Server.Administration.Logs;
using Content.Server.Administration.Managers;
using Content.Server.Chat.Managers;
using Content.Server.Database;
using Content.Server.GameTicking;
using Content.Server.Ghost.Components;
using Content.Server.Players;
using Content.Server.Popups;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Server.UtkaIntegration;
using Content.Shared.ActionBlocker;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Content.Shared.Chat;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.IdentityManagement;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Radio;
using Linguini.Syntax.Ast;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.Audio;
using Robust.Shared.Configuration;
using Robust.Shared.Console;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Players;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Replays;
using Robust.Shared.Utility;


namespace Content.Server.Chat.Systems;

/// <summary>
///     ChatSystem is responsible for in-simulation chat handling, such as whispering, speaking, emoting, etc.
///     ChatSystem depends on ChatManager to actually send the messages.
/// </summary>
public sealed partial class ChatSystem : SharedChatSystem
{
    [Dependency] private readonly IReplayRecordingManager _replay = default!;
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly IChatSanitizationManager _sanitizer = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IAdminLogManager _adminLogger = default!;
    [Dependency] private readonly ActionBlockerSystem _actionBlocker = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly StationSystem _stationSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly UtkaTCPWrapper _utkaSockets = default!;

    public const int VoiceRange = 10; // how far voice goes in world units
    public const int WhisperRange = 2; // how far whisper goes in world units
    public const string DefaultAnnouncementSound = "/Audio/Announcements/announce.ogg";

    private bool _loocEnabled = true;
    private bool _deadLoocEnabled = false;
    private readonly bool _adminLoocEnabled = true;

    public override void Initialize()
    {
        base.Initialize();
        InitializeEmotes();
        _configurationManager.OnValueChanged(CCVars.LoocEnabled, OnLoocEnabledChanged, true);
        _configurationManager.OnValueChanged(CCVars.DeadLoocEnabled, OnDeadLoocEnabledChanged, true);

        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnGameChange);
    }

    public override void Shutdown()
    {
        base.Shutdown();
        ShutdownEmotes();
        _configurationManager.UnsubValueChanged(CCVars.LoocEnabled, OnLoocEnabledChanged);
        _configurationManager.UnsubValueChanged(CCVars.DeadLoocEnabled, OnDeadLoocEnabledChanged);
    }

    private void OnLoocEnabledChanged(bool val)
    {
        if (_loocEnabled == val) return;

        _loocEnabled = val;
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString(val ? "chat-manager-looc-chat-enabled-message" : "chat-manager-looc-chat-disabled-message"));
    }

    private void OnDeadLoocEnabledChanged(bool val)
    {
        if (_deadLoocEnabled == val) return;

        _deadLoocEnabled = val;
        _chatManager.DispatchServerAnnouncement(
            Loc.GetString(val ? "chat-manager-dead-looc-chat-enabled-message" : "chat-manager-dead-looc-chat-disabled-message"));
    }

    private void OnGameChange(GameRunLevelChangedEvent ev)
    {
        switch(ev.New)
        {
            case GameRunLevel.InRound:
                if(!_configurationManager.GetCVar(CCVars.OocEnableDuringRound))
                    _configurationManager.SetCVar(CCVars.OocEnabled, false);
                break;
            case GameRunLevel.PostRound:
                if(!_configurationManager.GetCVar(CCVars.OocEnableDuringRound))
                    _configurationManager.SetCVar(CCVars.OocEnabled, true);
                break;
        }
    }

    /// <summary>
    ///     Sends an in-character chat message to relevant clients.
    /// </summary>
    /// <param name="source">The entity that is speaking</param>
    /// <param name="message">The message being spoken or emoted</param>
    /// <param name="desiredType">The chat type</param>
    /// <param name="hideChat">Whether or not this message should appear in the chat window</param>
    /// <param name="hideGlobalGhostChat">Whether or not this message should appear in the chat window for out-of-range ghosts (which otherwise ignore range restrictions)</param>
    /// <param name="shell"></param>
    /// <param name="player">The player doing the speaking</param>
    /// <param name="nameOverride">The name to use for the speaking entity. Usually this should just be modified via <see cref="TransformSpeakerNameEvent"/>. If this is set, the event will not get raised.</param>
    public void TrySendInGameICMessage(EntityUid source, string message, InGameICChatType desiredType, bool hideChat, bool hideGlobalGhostChat = false,
        IConsoleShell? shell = null, IPlayerSession? player = null, string? nameOverride = null, bool checkRadioPrefix = true, bool force = false)
    {
        if (HasComp<GhostComponent>(source))
        {
            // Ghosts can only send dead chat messages, so we'll forward it to InGame OOC.
            if (desiredType == InGameICChatType.Emote) return;
            TrySendInGameOOCMessage(source, message, InGameOOCChatType.Dead, hideChat, shell, player);
            return;
        }

        // Sus
        if (player?.AttachedEntity is { Valid: true } entity && source != entity)
        {
            return;
        }

        if (!force && !CanSendInGame(message, shell, player))
            return;

        if (desiredType == InGameICChatType.Speak && message.StartsWith(LocalPrefix))
        {
            // prevent radios and remove prefix.
            checkRadioPrefix = false;
            message = message[1..];
        }

        hideGlobalGhostChat |= hideChat;
        bool shouldCapitalize = (desiredType != InGameICChatType.Emote);
        bool shouldPunctuate = _configurationManager.GetCVar(CCVars.ChatPunctuation);
        bool sanitizeSlang = _configurationManager.GetCVar(CCVars.ChatSlangFilter);

        message = SanitizeInGameICMessage(source, message, out var emoteStr, shouldCapitalize, shouldPunctuate, sanitizeSlang);

        // Was there an emote in the message? If so, send it.
        if (emoteStr != message && emoteStr != null)
        {
            SendEntityEmote(source, emoteStr, hideChat, hideGlobalGhostChat, nameOverride, force);
        }

        // This can happen if the entire string is sanitized out.
        if (string.IsNullOrEmpty(message))
            return;

        // This message may have a radio prefix, and should then be whispered to the resolved radio channel
        if (checkRadioPrefix)
        {
            if (TryProccessRadioMessage(source, message, out var modMessage, out var channel))
            {
                SendEntityWhisper(source, modMessage, hideChat, hideGlobalGhostChat, channel, nameOverride);
                return;
            }
        }

        // Otherwise, send whatever type.
        switch (desiredType)
        {
            case InGameICChatType.Speak:
                SendEntitySpeak(source, message, hideChat, hideGlobalGhostChat, nameOverride);
                break;
            case InGameICChatType.Whisper:
                SendEntityWhisper(source, message, hideChat, hideGlobalGhostChat, null, nameOverride);
                break;
            case InGameICChatType.Emote:
                SendEntityEmote(source, message, hideChat, hideGlobalGhostChat, nameOverride, force);
                break;
        }
    }

    public void TrySendInGameOOCMessage(EntityUid source, string message, InGameOOCChatType type, bool hideChat,
        IConsoleShell? shell = null, IPlayerSession? player = null)
    {
        if (!CanSendInGame(message, shell, player))
            return;

        // It doesn't make any sense for a non-player to send in-game OOC messages, whereas non-players may be sending
        // in-game IC messages.
        if (player?.AttachedEntity is not { Valid: true } entity || source != entity)
            return;

        message = SanitizeInGameOOCMessage(message);

        var sendType = type;
        // If dead player LOOC is disabled, unless you are an aghost, send dead messages to dead chat
        if (!_adminManager.IsAdmin(player) && !_deadLoocEnabled &&
            (HasComp<GhostComponent>(source) || _mobStateSystem.IsDead(source)))
            sendType = InGameOOCChatType.Dead;

        switch (sendType)
        {
            case InGameOOCChatType.Dead:
                SendDeadChat(source, player, message, hideChat);
                break;
            case InGameOOCChatType.Looc:
                SendLOOC(source, player, message, hideChat);
                break;
        }
    }

    #region Announcements

    /// <summary>
    /// Dispatches an announcement to all.
    /// </summary>
    /// <param name="message">The contents of the message</param>
    /// <param name="sender">The sender (Communications Console in Communications Console Announcement)</param>
    /// <param name="playSound">Play the announcement sound</param>
    /// <param name="colorOverride">Optional color for the announcement message</param>
    public void DispatchGlobalAnnouncement(string message, string sender = "Central Command",
        bool playSound = true, SoundSpecifier? announcementSound = null, Color? colorOverride = null)
    {
        var wrappedMessage = Loc.GetString("chat-manager-sender-announcement-wrap-message", ("sender", sender), ("message", FormattedMessage.EscapeText(message)));
        _chatManager.ChatMessageToAll(ChatChannel.Radio, message, wrappedMessage, default, false, true, colorOverride);
        if (playSound)
        {
            SoundSystem.Play(announcementSound?.GetSound() ?? DefaultAnnouncementSound, Filter.Broadcast(), AudioParams.Default.WithVolume(-2f));
        }
        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Global station announcement from {sender}: {message}");
    }

    /// <summary>
    /// Dispatches an announcement on a specific station
    /// </summary>
    /// <param name="source">The entity making the announcement (used to determine the station)</param>
    /// <param name="message">The contents of the message</param>
    /// <param name="sender">The sender (Communications Console in Communications Console Announcement)</param>
    /// <param name="playDefaultSound">Play the announcement sound</param>
    /// <param name="colorOverride">Optional color for the announcement message</param>
    public void DispatchStationAnnouncement(EntityUid source, string message, string sender = "Central Command",
        bool playDefaultSound = true, SoundSpecifier? announcementSound = null, Color? colorOverride = null)
    {
        var wrappedMessage = Loc.GetString("chat-manager-sender-announcement-wrap-message", ("sender", sender), ("message", FormattedMessage.EscapeText(message)));
        var station = _stationSystem.GetOwningStation(source);

        if (station == null)
        {
            // you can't make a station announcement without a station
            return;
        }

        if (!EntityManager.TryGetComponent<StationDataComponent>(station, out var stationDataComp)) return;

        var filter = _stationSystem.GetInStation(stationDataComp);

        _chatManager.ChatMessageToManyFiltered(filter, ChatChannel.Radio, message, wrappedMessage, source, false, true, colorOverride);

        if (playDefaultSound)
        {
            SoundSystem.Play(announcementSound?.GetSound() ?? DefaultAnnouncementSound, filter, AudioParams.Default.WithVolume(-2f));
        }

        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Station Announcement on {station} from {sender}: {message}");
    }

    #endregion

    #region Private API

    private void SendEntitySpeak(EntityUid source, string originalMessage, bool hideChat, bool hideGlobalGhostChat, string? nameOverride)
    {
        if (!_actionBlocker.CanSpeak(source))
            return;

        var message = TransformSpeech(source, originalMessage);
        if (message.Length == 0)
            return;

        message = AfterSpeechTransformed(source, message);

        // get the entity's apparent name (if no override provided).
        string name;
        if (nameOverride != null)
        {
            name = nameOverride;
        }
        else
        {
            var nameEv = new TransformSpeakerNameEvent(source, Name(source));
            RaiseLocalEvent(source, nameEv);
            name = nameEv.Name;
        }

        var colorEv = new SetSpeakerColorEvent(source, name);
        RaiseLocalEvent(source, colorEv);
        name = colorEv.Name;


        var wrappedMessage = Loc.GetString("chat-manager-entity-say-wrap-message",
            ("entityName", name), ("message", message));

        SendInVoiceRange(ChatChannel.Local, message, wrappedMessage, source, hideChat, hideGlobalGhostChat);

        var ev = new EntitySpokeEvent(source, message, originalMessage, null, null);
        RaiseLocalEvent(source, ev, true);

        // To avoid logging any messages sent by entities that are not players, like vendors, cloning, etc.
        if (!HasComp<ActorComponent>(source))
            return;

        if (originalMessage == message)
        {
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Say from {ToPrettyString(source):user} as {name}: {originalMessage}.");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Say from {ToPrettyString(source):user}: {originalMessage}.");
        }
        else
        {
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Say from {ToPrettyString(source):user} as {name}, original: {originalMessage}, transformed: {message}.");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Say from {ToPrettyString(source):user}, original: {originalMessage}, transformed: {message}.");
        }
    }

    private void SendEntityWhisper(EntityUid source, string originalMessage, bool hideChat, bool hideGlobalGhostChat, RadioChannelPrototype? channel, string? nameOverride)
    {
        if (!_actionBlocker.CanSpeak(source))
            return;

        var message = TransformSpeech(source, originalMessage);
        if (message.Length == 0)
            return;

        message = AfterSpeechTransformed(source, message);

        var obfuscatedMessage = ObfuscateMessageReadability(message, 0.2f);

        // get the entity's apparent name (if no override provided).
        string name;
        if (nameOverride != null)
        {
            name = nameOverride;
        }
        else
        {
            var nameEv = new TransformSpeakerNameEvent(source, Name(source));
            RaiseLocalEvent(source, nameEv);
            name = nameEv.Name;
        }


        var wrappedMessage = Loc.GetString("chat-manager-entity-whisper-wrap-message",
            ("entityName", name), ("message", message));


        var wrappedobfuscatedMessage = Loc.GetString("chat-manager-entity-whisper-wrap-message",
            ("entityName", name), ("message", obfuscatedMessage));


        foreach (var (session, data) in GetRecipients(source, VoiceRange))
        {
            if (session.AttachedEntity is not { Valid: true } playerEntity)
                continue;

            if (hideGlobalGhostChat && data.Observer && data.Range < 0)
                continue; // Won't get logged to chat, and ghosts are too far away to see the pop-up, so we just won't send it to them.

            if (data.Range <= WhisperRange)
                _chatManager.ChatMessageToOne(ChatChannel.Whisper, message, wrappedMessage, source, data.HideChatOverride ?? hideChat, session.ConnectedClient);
            else
                _chatManager.ChatMessageToOne(ChatChannel.Whisper, obfuscatedMessage, wrappedobfuscatedMessage, source, data.HideChatOverride ?? hideChat, session.ConnectedClient);
        }

        _replay.QueueReplayMessage(new ChatMessage(ChatChannel.Whisper, message, wrappedMessage, source, hideChat));

        var ev = new EntitySpokeEvent(source, message, originalMessage, channel, obfuscatedMessage);
        RaiseLocalEvent(source, ev, true);

        if (originalMessage == message)
        {
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Whisper from {ToPrettyString(source):user} as {name}: {originalMessage}.");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Whisper from {ToPrettyString(source):user}: {originalMessage}.");
        }
        else
        {
            if (name != Name(source))
                _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Whisper from {ToPrettyString(source):user} as {name}, original: {originalMessage}, transformed: {message}.");
            else
                _adminLogger.Add(LogType.Chat, LogImpact.Low,
                    $"Whisper from {ToPrettyString(source):user}, original: {originalMessage}, transformed: {message}.");
        }
    }

    private void SendEntityEmote(EntityUid source, string action, bool hideChat, bool hideGlobalGhostChat, string? nameOverride, bool force = false, bool checkEmote = true)
    {
        if (!force && !_actionBlocker.CanEmote(source)) return;

        // get the entity's apparent name (if no override provided).
        string name = FormattedMessage.EscapeText(nameOverride ?? Identity.Name(source, EntityManager));

        // Emotes use Identity.Name, since it doesn't actually involve your voice at all.
        var wrappedMessage = Loc.GetString("chat-manager-entity-me-wrap-message",
            ("entityName", name),
            ("message", FormattedMessage.EscapeText(action)));

        if (checkEmote)
            TryEmoteChatInput(source, action);
        SendInVoiceRange(ChatChannel.Emotes, action, wrappedMessage, source, hideChat, hideGlobalGhostChat);

        if (name != Name(source))
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Emote from {ToPrettyString(source):user} as {name}: {action}");
        else
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Emote from {ToPrettyString(source):user}: {action}");

        string ckey = string.Empty;

        if (TryComp<ActorComponent>(source, out var actorComponent))
        {
            ckey = actorComponent.PlayerSession.Name;
        }

        if (string.IsNullOrEmpty(ckey)) return;

        var utkaEmoteEvent = new UtkaChatMeEvent()
        {
            Ckey = ckey,
            Message = action,
            CharacterName = MetaData(source).EntityName
        };

        _utkaSockets.SendMessageToAll(utkaEmoteEvent);
    }

    // ReSharper disable once InconsistentNaming
    private void SendLOOC(EntityUid source, IPlayerSession player, string message, bool hideChat)
    {
        var name = FormattedMessage.EscapeText(Identity.Name(source, EntityManager));

        if (_adminManager.IsAdmin(player))
        {
            if (!_adminLoocEnabled) return;
        }
        else if (!_loocEnabled) return;
        var wrappedMessage = Loc.GetString("chat-manager-entity-looc-wrap-message",
            ("entityName", name),
            ("message", FormattedMessage.EscapeText(message)));

        SendInVoiceRange(ChatChannel.LOOC, message, wrappedMessage, source, hideChat, false);
        _adminLogger.Add(LogType.Chat, LogImpact.Low, $"LOOC from {player:Player}: {message}");
    }

    private void SendDeadChat(EntityUid source, IPlayerSession player, string message, bool hideChat)
    {
        var clients = GetDeadChatClients();
        var playerName = Name(source);
        string wrappedMessage;
        if (_adminManager.HasAdminFlag(player, AdminFlags.Admin))
        {
            wrappedMessage = Loc.GetString("chat-manager-send-admin-dead-chat-wrap-message",
                ("adminChannelName", Loc.GetString("chat-manager-admin-channel-name")),
                ("userName", player.ConnectedClient.UserName),
                ("message", FormattedMessage.EscapeText(message)));
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Admin dead chat from {player:Player}: {message}");
        }
        else
        {
            wrappedMessage = Loc.GetString("chat-manager-send-dead-chat-wrap-message",
                ("deadChannelName", Loc.GetString("chat-manager-dead-channel-name")),
                ("playerName", (playerName)),
                ("message", FormattedMessage.EscapeText(message)));
            _adminLogger.Add(LogType.Chat, LogImpact.Low, $"Dead chat from {player:Player}: {message}");
        }

        _chatManager.ChatMessageToMany(ChatChannel.Dead, message, wrappedMessage, source, hideChat, false, clients.ToList());

    }
    #endregion

    #region Utility

    /// <summary>
    ///     Sends a chat message to the given players in range of the source entity.
    /// </summary>
    private void SendInVoiceRange(ChatChannel channel, string message, string wrappedMessage, EntityUid source, bool hideChat, bool hideGlobalGhostChat)
    {
        foreach (var (session, data) in GetRecipients(source, VoiceRange))
        {
            var entHideChat = data.HideChatOverride ?? (hideChat || hideGlobalGhostChat && data.Observer && data.Range < 0);
            _chatManager.ChatMessageToOne(channel, message, wrappedMessage, source, entHideChat, session.ConnectedClient);
        }

        _replay.QueueReplayMessage(new ChatMessage(channel, message, wrappedMessage, source, hideChat));
    }

    /// <summary>
    ///     Returns true if the given player is 'allowed' to send the given message, false otherwise.
    /// </summary>
    private bool CanSendInGame(string message, IConsoleShell? shell = null, IPlayerSession? player = null)
    {
        // Non-players don't have to worry about these restrictions.
        if (player == null)
            return true;

        var mindComponent = player.ContentData()?.Mind;

        if (mindComponent == null)
        {
            shell?.WriteError("You don't have a mind!");
            return false;
        }

        if (player.AttachedEntity is not { Valid: true } _)
        {
            shell?.WriteError("You don't have an entity!");
            return false;
        }

        return !_chatManager.MessageCharacterLimit(player, message);
    }

    // ReSharper disable once InconsistentNaming
    private string SanitizeInGameICMessage(EntityUid source, string message, out string? emoteStr, bool capitalize = true, bool punctuate = false, bool sanitizeSlang = true)
    {
        var newMessage = message.Trim();

        newMessage = _sanitizer.SanitizeTags(newMessage);
        
        if (sanitizeSlang)
            newMessage = _sanitizer.SanitizeOutSlang(newMessage);
        if (capitalize)
            newMessage = SanitizeMessageCapital(newMessage);
        if (punctuate)
            newMessage = SanitizeMessagePeriod(newMessage);

        _sanitizer.TrySanitizeOutSmilies(newMessage, source, out newMessage, out emoteStr);

        return newMessage;
    }

    private string SanitizeInGameOOCMessage(string message)
    {
        var newMessage = message.Trim();
        newMessage = FormattedMessage.EscapeText(newMessage);

        return newMessage;
    }

    public string TransformSpeech(EntityUid sender, string message)
    {
        var ev = new TransformSpeechEvent(sender, message);
        RaiseLocalEvent(ev);

        return ev.Message;
    }

    public string AfterSpeechTransformed(EntityUid sender, string message)
    {
        var ev = new SpeechTransformedEvent(sender, message);
        RaiseLocalEvent(ev);
        return ev.Message;
    }

    private IEnumerable<INetChannel> GetDeadChatClients()
    {
        return Filter.Empty()
            .AddWhereAttachedEntity(HasComp<GhostComponent>)
            .Recipients
            .Union(_adminManager.AdminsWithFlag)
            .Select(p => p.ConnectedClient);
    }

    private string SanitizeMessagePeriod(string message)
    {
        if (string.IsNullOrEmpty(message))
            return message;
        // Adds a period if the last character is a letter.
        if (char.IsLetter(message[^1]))
            message += ".";
        return message;
    }

    /// <summary>
    ///     Returns list of players and ranges for all players withing some range. Also returns observers with a range of -1.
    /// </summary>
    private Dictionary<ICommonSession, ICChatRecipientData> GetRecipients(EntityUid source, float voiceRange)
    {
        // TODO proper speech occlusion

        var recipients = new Dictionary<ICommonSession, ICChatRecipientData>();
        var ghosts = GetEntityQuery<GhostComponent>();
        var xforms = GetEntityQuery<TransformComponent>();

        var transformSource = xforms.GetComponent(source);
        var sourceMapId = transformSource.MapID;
        var sourceCoords = transformSource.Coordinates;

        foreach (var player in _playerManager.Sessions)
        {
            if (player.AttachedEntity is not { Valid: true } playerEntity)
                continue;

            var transformEntity = xforms.GetComponent(playerEntity);

            if (transformEntity.MapID != sourceMapId)
                continue;

            var observer = ghosts.HasComponent(playerEntity);

            // even if they are an observer, in some situations we still need the range
            if (sourceCoords.TryDistance(EntityManager, transformEntity.Coordinates, out var distance) && distance < voiceRange)
            {
                recipients.Add(player, new ICChatRecipientData(distance, observer));
                continue;
            }

            if (observer)
                recipients.Add(player, new ICChatRecipientData(-1, true));
        }

        RaiseLocalEvent(new ExpandICChatRecipientstEvent(source, voiceRange, recipients));
        return recipients;
    }

    public readonly record struct ICChatRecipientData(float Range, bool Observer, bool? HideChatOverride = null)
    {
    }

    private string ObfuscateMessageReadability(string message, float chance)
    {
        var modifiedMessage = new StringBuilder(message);

        for (var i = 0; i < message.Length; i++)
        {
            if (char.IsWhiteSpace((modifiedMessage[i])))
            {
                continue;
            }

            if (_random.Prob(1 - chance))
            {
                modifiedMessage[i] = '~';
            }
        }

        return modifiedMessage.ToString();
    }

    #endregion
}

/// <summary>
///     This event is raised before chat messages are sent out to clients. This enables some systems to send the chat
///     messages to otherwise out-of view entities (e.g. for multiple viewports from cameras).
/// </summary>
public record ExpandICChatRecipientstEvent(EntityUid Source, float VoiceRange, Dictionary<ICommonSession, ChatSystem.ICChatRecipientData> Recipients)
{
}

public sealed class TransformSpeakerNameEvent : EntityEventArgs
{
    public EntityUid Sender;
    public string Name;

    public TransformSpeakerNameEvent(EntityUid sender, string name)
    {
        Sender = sender;
        Name = name;
    }


}

public class SetSpeakerColorEvent
{
    public EntityUid Sender { get; set; }
    public string Name { get; set; }
    public SetSpeakerColorEvent(EntityUid sender, string name)
    {
        Sender = sender;
        Name = name;
    }
}

/// <summary>
///     Raised broadcast in order to transform speech.
/// </summary>
public sealed class TransformSpeechEvent : EntityEventArgs
{
    public EntityUid Sender;
    public string Message;

    public TransformSpeechEvent(EntityUid sender, string message)
    {
        Sender = sender;
        Message = message;
    }
}

public sealed class SpeechTransformedEvent : EntityEventArgs
{
    public EntityUid Sender;
    public string Message;

    public SpeechTransformedEvent(EntityUid sender, string message)
    {
        Sender = sender;
        Message = message;
    }
}

/// <summary>
///     Raised on an entity when it speaks, either through 'say' or 'whisper'.
/// </summary>
public sealed class EntitySpokeEvent : EntityEventArgs
{
    public readonly EntityUid Source;
    public readonly string Message;
    public readonly string OriginalMessage;
    public readonly string? ObfuscatedMessage; // not null if this was a whisper

    /// <summary>
    ///     If the entity was trying to speak into a radio, this was the channel they were trying to access. If a radio
    ///     message gets sent on this channel, this should be set to null to prevent duplicate messages.
    /// </summary>
    public RadioChannelPrototype? Channel;

    public EntitySpokeEvent(EntityUid source, string message, string originalMessage, RadioChannelPrototype? channel, string? obfuscatedMessage)
    {
        Source = source;
        Message = message;
        OriginalMessage = originalMessage;
        Channel = channel;
        ObfuscatedMessage = obfuscatedMessage;
    }
}

/// <summary>
///     InGame IC chat is for chat that is specifically ingame (not lobby) but is also in character, i.e. speaking.
/// </summary>
// ReSharper disable once InconsistentNaming
public enum InGameICChatType : byte
{
    Speak,
    Emote,
    Whisper
}

/// <summary>
///     InGame OOC chat is for chat that is specifically ingame (not lobby) but is OOC, like deadchat or LOOC.
/// </summary>
public enum InGameOOCChatType : byte
{
    Looc,
    Dead
}
