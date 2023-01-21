using Content.Server.GameTicking.Rules;
using JetBrains.Annotations;

namespace Content.Server.StationEvents
{
    /// <summary>
    ///     Game Rule without events.
    /// </summary>
    [UsedImplicitly]
    public sealed class NoEventsSchedulerSystem : GameRuleSystem
    {
        public override string Prototype => "NoEventsScheduler";

        public override void Started() { }

        public override void Ended() { }

        public override void Update(float frameTime) { }


    }
}
