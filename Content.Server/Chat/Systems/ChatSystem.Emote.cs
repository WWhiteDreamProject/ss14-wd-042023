using Content.Shared.Chat.Prototypes;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.Chat.Systems;

// emotes using emote prototype
public partial class ChatSystem
{
    private readonly Dictionary<string, EmotePrototype> _wordEmoteDict = new();

    private void InitializeEmotes()
    {
        _prototypeManager.PrototypesReloaded += OnPrototypeReloadEmotes;
        CacheEmotes();
    }

    private void ShutdownEmotes()
    {
        _prototypeManager.PrototypesReloaded -= OnPrototypeReloadEmotes;
    }

    private void OnPrototypeReloadEmotes(PrototypesReloadedEventArgs obj)
    {
        CacheEmotes();
    }

    private void CacheEmotes()
    {
        _wordEmoteDict.Clear();
        var emotes = _prototypeManager.EnumeratePrototypes<EmotePrototype>();
        foreach (var emote in emotes)
        {
            if (emote.ChatTriggers == null)
                continue;
            foreach (var word in emote.ChatTriggers)
            {
                var lowerWord = word.ToLower();
                if (_wordEmoteDict.ContainsKey(lowerWord))
                {
                    var existingId = _wordEmoteDict[lowerWord].ID;
                    var errMsg = $"Duplicate of emote word {lowerWord} in emotes {emote.ID} and {existingId}";
                    Logger.Error(errMsg);
                    continue;
                }

                _wordEmoteDict.Add(lowerWord, emote);
            }
        }
    }

    /// <summary>
    ///     Makes selected entity to emote using <see cref="EmotePrototype"/> and sends message to chat.
    /// </summary>
    /// <param name="source">The entity that is speaking</param>
    /// <param name="emoteId">The id of emote prototype. Should has valid <see cref="EmotePrototype.ChatMessages"/></param>
    /// <param name="hideChat">Whether or not this message should appear in the chat window</param>
    /// <param name="hideGlobalGhostChat">Whether or not this message should appear in the chat window for out-of-range ghosts (which otherwise ignore range restrictions)</param>
    /// <param name="nameOverride">The name to use for the speaking entity. Usually this should just be modified via <see cref="TransformSpeakerNameEvent"/>. If this is set, the event will not get raised.</param>
    public void TryEmoteWithChat(EntityUid source, string emoteId, bool hideChat = false,
        bool hideGlobalGhostChat = false, string? nameOverride = null)
    {
        if (!_prototypeManager.TryIndex<EmotePrototype>(emoteId, out var proto))
            return;
        TryEmoteWithChat(source, proto, hideChat, hideGlobalGhostChat, nameOverride);
    }

    /// <summary>
    ///     Makes selected entity to emote using <see cref="EmotePrototype"/> and sends message to chat.
    /// </summary>
    /// <param name="source">The entity that is speaking</param>
    /// <param name="emote">The emote prototype. Should has valid <see cref="EmotePrototype.ChatMessages"/></param>
    /// <param name="hideChat">Whether or not this message should appear in the chat window</param>
    /// <param name="hideGlobalGhostChat">Whether or not this message should appear in the chat window for out-of-range ghosts (which otherwise ignore range restrictions)</param>
    /// <param name="nameOverride">The name to use for the speaking entity. Usually this should just be modified via <see cref="TransformSpeakerNameEvent"/>. If this is set, the event will not get raised.</param>
    public void TryEmoteWithChat(EntityUid source, EmotePrototype emote, bool hideChat = false,
        bool hideGlobalGhostChat = false, string? nameOverride = null)
    {
        // check if proto has valid message for chat
        if (emote.ChatMessages.Count != 0)
        {
            var action = _random.Pick(emote.ChatMessages);
            SendEntityEmote(source, action, hideChat, hideGlobalGhostChat, nameOverride, false);
        }

        // do the rest of emote event logic here
        TryEmoteWithoutChat(source, emote);
    }

    /// <summary>
    ///     Makes selected entity to emote using <see cref="EmotePrototype"/> without sending any messages to chat.
    /// </summary>
    public void TryEmoteWithoutChat(EntityUid uid, string emoteId)
    {
        if (!_prototypeManager.TryIndex<EmotePrototype>(emoteId, out var proto))
            return;
        TryEmoteWithoutChat(uid, proto);
    }

    /// <summary>
    ///     Makes selected entity to emote using <see cref="EmotePrototype"/> without sending any messages to chat.
    /// </summary>
    public void TryEmoteWithoutChat(EntityUid uid, EmotePrototype proto)
    {
        if (!_actionBlocker.CanEmote(uid))
            return;

        InvokeEmoteEvent(uid, proto);
    }

    /// <summary>
    ///     Tries to find and play relevant emote sound in emote sounds collection.
    /// </summary>
    /// <returns>True if emote sound was played.</returns>
    public bool TryPlayEmoteSound(EntityUid uid, EmoteSoundsPrototype? proto, EmotePrototype emote)
    {
        return TryPlayEmoteSound(uid, proto, emote.ID);
    }

    /// <summary>
    ///     Tries to find and play relevant emote sound in emote sounds collection.
    /// </summary>
    /// <returns>True if emote sound was played.</returns>
    public bool TryPlayEmoteSound(EntityUid uid, EmoteSoundsPrototype? proto, string emoteId)
    {
        if (proto == null)
            return false;

        // try to get specific sound for this emote
        if (!proto.Sounds.TryGetValue(emoteId, out var sound))
        {
            // no specific sound - check fallback
            sound = proto.FallbackSound;
            if (sound == null)
                return false;
        }

        // if general params for all sounds set - use them
        var param = proto.GeneralParams ?? sound.Params;
        _audio.PlayPvs(sound, uid, param);
        return true;
    }

    private void TryEmoteChatInput(EntityUid uid, string textInput)
    {
        var actionLower = textInput.ToLower();
        if (!_wordEmoteDict.TryGetValue(actionLower, out var emote))
            return;

        InvokeEmoteEvent(uid, emote);
    }

    private void InvokeEmoteEvent(EntityUid uid, EmotePrototype proto)
    {
        var ev = new EmoteEvent(proto);
        RaiseLocalEvent(uid, ref ev);
    }
}

/// <summary>
///     Raised by chat system when entity made some emote.
///     Use it to play sound, change sprite or something else.
/// </summary>
[ByRefEvent]
public struct EmoteEvent
{
    public bool Handled;
    public readonly EmotePrototype Emote;

    public EmoteEvent(EmotePrototype emote)
    {
        Emote = emote;
        Handled = false;
    }
}
