using Robust.Client.UserInterface;
using Robust.Shared.Console;

namespace Content.Client.White.Commands;

public sealed class OpenLinkCommand : IConsoleCommand
{
    [Dependency] private readonly IUriOpener _uriOpener = default!;

    public string Command => "openlink";
    public string Description => string.Empty;
    public string Help => string.Empty;
    public void Execute(IConsoleShell shell, string argStr, string[] args)
    {
        IoCManager.InjectDependencies(this);

        if (args.Length != 1)
        {
            shell.WriteLine("Wrong number of arguments");
            return;
        }

        _uriOpener.OpenUri(args[0]);
    }
}
