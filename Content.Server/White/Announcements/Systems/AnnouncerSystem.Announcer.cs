using System.Linq;
using Content.Server.White.Announcements.Prototypes;
using Content.Shared.White;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.White.Announcements.Systems
{
    public sealed partial class AnnouncerSystem
    {
        [Dependency] private readonly IRobustRandom _random = default!;
        [Dependency] private readonly IPrototypeManager _prototypeManager = default!;

        /// <summary>
        ///     Picks a random announcer
        /// </summary>
        public void PickAnnouncer()
        {
            Announcer = _random.Pick(_prototypeManager.EnumeratePrototypes<AnnouncerPrototype>()
                .Where(x => !_configManager.GetCVar(WhiteCCVars.AnnouncerBlacklist).Contains(x.ID))
                .ToArray());
        }

        /// <summary>
        ///     Sets the announcer
        /// </summary>
        public void PickAnnouncer(string announcerId)
        {
            if (announcerId == "random")
            {
                PickAnnouncer();
                return;
            }

            Announcer = _prototypeManager.Index<AnnouncerPrototype>(announcerId);
        }
    }
}
