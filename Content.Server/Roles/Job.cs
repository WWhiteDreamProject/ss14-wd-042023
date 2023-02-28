using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared.Roles;
using System.Globalization;
using Robust.Shared.Utility;

namespace Content.Server.Roles
{
    public sealed class Job : Role, IRoleTimer
    {
        [ViewVariables] public string Timer => Prototype.PlayTimeTracker;

        [ViewVariables]
        public JobPrototype Prototype { get; }

        public override string Name { get; }

        public override bool Antagonist => false;

        [ViewVariables]
        public string? StartingGear => Prototype.StartingGear;

        [ViewVariables]
        public string? JobEntity => Prototype.JobEntity;

        [ViewVariables]
        public bool CanBeAntag;

        public Job(Mind.Mind mind, JobPrototype jobPrototype) : base(mind)
        {
            Prototype = jobPrototype;
            Name = jobPrototype.LocalizedName;
            CanBeAntag = jobPrototype.CanBeAntag;
        }

        public override void Greet()
        {
            base.Greet();

            if (Mind.TryGetSession(out var session))
            {
                var chatMgr = IoCManager.Resolve<IChatManager>();
                string jobMessage = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(Name);

                if(!string.IsNullOrEmpty(Prototype.WikiLink))
                {
                    jobMessage = $"[cmdlink=\"[{jobMessage}]\" command=\"openlink {Prototype.WikiLink}\"][/cmdlink]";
                }

                var message = Loc.GetString("job-greet-introduce-job-name",
                    ("jobName", jobMessage));

                chatMgr.DispatchServerMessage(session, message);

                if(Prototype.RequireAdminNotify)
                    chatMgr.DispatchServerMessage(session, Loc.GetString("job-greet-important-disconnect-admin-notify"));

                chatMgr.DispatchServerMessage(session, Loc.GetString("job-greet-supervisors-warning", ("jobName", Name), ("supervisors", Loc.GetString(Prototype.Supervisors))));
            }
        }
    }
}
