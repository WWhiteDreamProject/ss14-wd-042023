using Content.Server.Mind.Components;
using Content.Shared.Chat;
using Robust.Server.Player;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.Chat.Managers
{
    public interface IChatManager
    {
        void Initialize();

        /// <summary>
        ///     Dispatch a server announcement to every connected player.
        /// </summary>
        /// <param name="message"></param>
        /// <param name="colorOverride">Override the color of the message being sent.</param>

        void DispatchServerAnnouncement(string message, Color? colorOverride = null);

        void DispatchServerMessage(IPlayerSession player, string message, bool suppressLog = false);

        void TrySendOOCMessage(IPlayerSession player, string message, OOCChatType type);

        void SendHookOOC(string sender, string message);
        void SendHookAdminChat(string sender, string message);
        void SendAdminAnnouncement(string message);
        void SendAdminAlert(string message);
        void SendAdminAlert(EntityUid player, string message, MindComponent? mindComponent = null);

        void ChatMessageToOne(ChatChannel channel, string message, string wrappedMessage, EntityUid source, bool hideChat,
            INetChannel client, Color? colorOverride = null, bool recordReplay = false, string? audioPath = null, float audioVolume = 0);

        void ChatMessageToMany(ChatChannel channel, string message, string wrappedMessage, EntityUid source, bool hideChat, bool recordReplay,
            IEnumerable<INetChannel> clients, Color? colorOverride = null, string? audioPath = null, float audioVolume = 0);

        void ChatMessageToManyFiltered(Filter filter, ChatChannel channel, string message, string wrappedMessage, EntityUid source, bool hideChat, bool recordReplay, Color? colorOverride, string? audioPath = null, float audioVolume = 0);

        void ChatMessageToAll(ChatChannel channel, string message, string wrappedMessage, EntityUid source, bool hideChat, bool recordReplay, Color? colorOverride = null, string? audioPath = null, float audioVolume = 0);

        bool MessageCharacterLimit(IPlayerSession player, string message);
    }
}
