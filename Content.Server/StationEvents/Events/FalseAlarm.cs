using Content.Server.GameTicking.Rules.Configurations;
using Content.Server.White.Announcements.Systems;
using JetBrains.Annotations;
using Robust.Shared.Player;

namespace Content.Server.StationEvents.Events
{
    [UsedImplicitly]
    public sealed class FalseAlarm : StationEventSystem
    {
        [Dependency] private readonly AnnouncerSystem _announcerSystem = default!;

        public override string Prototype => "FalseAlarm";

        public override void Started()
        {
            base.Started();

            var ev = GetRandomEventUnweighted(PrototypeManager, RobustRandom);

            if (ev.Configuration is not StationEventRuleConfiguration cfg)
                return;

            if (cfg.StartAnnouncement != null)
            {
                _announcerSystem.SendAnnouncement(ev.ID, Filter.Broadcast(), Loc.GetString(cfg.StartAnnouncement), colorOverride: Color.Gold);
                // ChatSystem.DispatchGlobalAnnouncement(Loc.GetString(cfg.StartAnnouncement), playSound: false, colorOverride: Color.Gold);
            }

            /*if (cfg.StartAudio != null)
            {
                SoundSystem.Play(cfg.StartAudio.GetSound(), Filter.Broadcast(), cfg.StartAudio.Params);
            }*/
        }
    }
}
