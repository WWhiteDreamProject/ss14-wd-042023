using Content.Shared.Verbs;
using Robust.Client.Console;
using Robust.Shared.Utility;

namespace Content.Client.Administration.Systems
{
    /// <summary>
    ///     Client-side admin verb system. These usually open some sort of UIs.
    /// </summary>
    sealed class AdminVerbSystem : EntitySystem
    {
        [Dependency] private readonly IClientConGroupController _clientConGroupController = default!;
        [Dependency] private readonly IClientConsoleHost _clientConsoleHost = default!;

        public override void Initialize()
        {
            SubscribeLocalEvent<GetVerbsEvent<Verb>>(AddAdminVerbs);
        }

        private void AddAdminVerbs(GetVerbsEvent<Verb> args)
        {
            // Currently this is only the ViewVariables verb, but more admin-UI related verbs can be added here.

            // View variables verbs
            if (_clientConGroupController.CanAdminMenu())
            {
                Verb verb = new();
                verb.Category = VerbCategory.Debug;
                verb.Text = "View Variables";
                verb.Icon = new SpriteSpecifier.Texture(new ResourcePath("/Textures/Interface/VerbIcons/vv.svg.192dpi.png"));
                verb.Act = () => _clientConsoleHost.ExecuteCommand($"vv {args.Target}");
                verb.ClientExclusive = true; // opening VV window is client-side. Don't ask server to run this verb.
                args.Verbs.Add(verb);
            }
        }
    }
}
