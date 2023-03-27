using System.Linq;
using Content.Server.Chat.Systems;
using Content.Server.White.Announcements.Prototypes;
using Robust.Shared.Audio;
using Robust.Shared.Player;

namespace Content.Server.White.Announcements.Systems
{
    public sealed partial class AnnouncerSystem
    {
        [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
        [Dependency] private readonly ChatSystem _chatSystem = default!;

        /// <summary>
        ///     Gets an announcement path from the announcer
        /// </summary>
        public string GetAnnouncementPath(string announcerId, string announcementId)
        {
            var announcer = _prototypeManager.EnumeratePrototypes<AnnouncerPrototype>().ToArray().First(a => a.ID == announcerId);

            var announcementType = Announcer.AnnouncementPaths.FirstOrDefault(a => a.ID == announcementId);
            if (announcementType == null)
                announcementType = Announcer.AnnouncementPaths.First(a => a.ID == "fallback");

            if (announcementType.Path != null)
                return $"{announcer.BasePath}/{announcementType.Path}";

            if (announcementType.Collection != null)
                return $"{new SoundCollectionSpecifier(announcementType.Collection).GetSound()}";

            return $"{announcer.BasePath}/{announcementType.Path}";
        }

        /// <summary>
        ///     Gets an announcement SoundSpecifier from the announcer
        /// </summary>
        public SoundSpecifier GetAnnouncementSpecifier(string announcementId)
        {
            return new SoundPathSpecifier(GetAnnouncementPath(Announcer.ID, announcementId));
        }


        /// <summary>
        ///     Sends an announcement
        /// </summary>
        public void SendAnnouncement(string announcementId, Filter filter, AudioParams? audioParams = null)
        {
            string announcement = GetAnnouncementPath(Announcer.ID, announcementId);
            _audioSystem.PlayGlobal(announcement, filter, true, audioParams);
        }

        /// <summary>
        ///     Sends an announcement with a message
        /// </summary>
        public void SendAnnouncement(string announcementId, Filter filter, string message, string? sender = null, Color? colorOverride = null, AudioParams? audioParams = null)
        {
            string announcement = GetAnnouncementPath(Announcer.ID, announcementId);
            if (sender == null)
                sender = Announcer.Name;

            _audioSystem.PlayGlobal(announcement, filter, true, audioParams);
            _chatSystem.DispatchGlobalAnnouncement(message, sender, false, colorOverride: colorOverride);
        }
    }
}
