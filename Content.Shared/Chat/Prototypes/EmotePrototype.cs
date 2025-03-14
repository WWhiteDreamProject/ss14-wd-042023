using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.Chat.Prototypes;

/// <summary>
///     IC emotes (scream, smile, clapping, etc).
///     Entities can activate emotes by chat input or code.
/// </summary>
[Prototype("emote")]
public sealed class EmotePrototype : IPrototype
{
    [IdDataField]
    public string ID { get; } = default!;

    /// <summary>
    ///     Different emote categories may be handled by different systems.
    ///     Also may be used for filtering.
    /// </summary>
    [DataField("category")]
    public EmoteCategory Category = EmoteCategory.General;

    /// <summary>
    ///     Текст для кнопки в эмоут меню.
    ///     Бля ну или как это описать, вы поняли короче. ¯\_(ツ)_/¯
    /// </summary>
    [DataField("buttonText")]
    public string ButtonText { get; } = "Unknown";

    /// <summary>
    ///     Collection of words that will be sent to chat if emote activates.
    ///     Will be picked randomly from list.
    /// </summary>
    [DataField("chatMessages")]
    public List<string> ChatMessages = new();

    /// <summary>
    ///     Trigger words for emote. Case independent.
    ///     When typed into players chat they will activate emote event.
    ///     All words should be unique across all emote prototypes.
    /// </summary>
    [DataField("chatTriggers")]
    public HashSet<string>? ChatTriggers = new();
}

/// <summary>
///     IC emote category. Usually physical source of emote,
///     like hands, voice, face, etc.
/// </summary>
[Flags]
[Serializable, NetSerializable]
public enum EmoteCategory : byte
{
    Invalid = 0,
    Vocal = 1 << 0,
    Gesture = 1 << 1,
    General = byte.MaxValue
}
