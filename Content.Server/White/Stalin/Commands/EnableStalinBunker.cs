using Content.Server.Administration;
using Content.Server.Chat.Managers;
using Content.Shared.Administration;
using Content.Shared.CCVar;
using Robust.Shared.Configuration;
using Robust.Shared.Console;

namespace Content.Server.White.Stalin.Commands;

[AdminCommand(AdminFlags.Admin)]
public sealed class EnableStalinBunker : IConsoleCommand
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    public string Command => "enableStalinBunker";
    public string Description => "Enables the stalin bunker, like PaNIk bunker, but better";
    public string Help => "enableStalinBunker <bool>";

    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        if (args.Length != 1 || !bool.TryParse(args[0], out bool value))
        {
            shell.WriteError($"{args[0]} is not a valid boolean.");
            return;
        }

        IoCManager.InjectDependencies(this);

        _cfg.SetCVar(CCVars.StalinEnabled, value);

        var announce = Loc.GetString("stalin-panic-bunker", ("enabled", $"{value}"));

        IoCManager.Resolve<IChatManager>().DispatchServerAnnouncement(announce, Color.Red);
    }
}
