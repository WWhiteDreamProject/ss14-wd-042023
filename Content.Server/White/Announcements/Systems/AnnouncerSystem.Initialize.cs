using Content.Server.GameTicking.Events;
using Content.Server.White.Announcements.Prototypes;
using Content.Shared.White;
using Robust.Shared.Configuration;

namespace Content.Server.White.Announcements.Systems
{
    public sealed partial class AnnouncerSystem : EntitySystem
    {
        [Dependency] private readonly IConfigurationManager _configManager = default!;

        /// <summary>
        ///     The currently selected announcer
        /// </summary>
        public AnnouncerPrototype Announcer { get; set; } = default!;

        public override void Initialize()
        {
            base.Initialize();

            PickAnnouncer();

            _configManager.OnValueChanged(WhiteCCVars.Announcer, PickAnnouncer);

            SubscribeLocalEvent<RoundStartingEvent>(OnRoundStarting);
        }


        private void OnRoundStarting(RoundStartingEvent ev)
        {
            PickAnnouncer(_configManager.GetCVar(WhiteCCVars.Announcer));
        }
    }
}
