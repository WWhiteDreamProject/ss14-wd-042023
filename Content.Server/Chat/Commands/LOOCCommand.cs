using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared.Administration;
using Content.Shared.Chat;
using Content.Shared.Mobs.Systems;
using Linguini.Syntax.Ast;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Enums;
using YamlDotNet.Serialization;

namespace Content.Server.Chat.Commands
{
    [AnyCommand]
    internal sealed class LOOCCommand : IConsoleCommand
    {
        public string Command => "looc";
        public string Description => "Send Local Out Of Character chat messages.";
        public string Help => "looc <text>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (shell.Player is not IPlayerSession player)
            {
                shell.WriteError("This command cannot be run from the server.");
                return;
            }

            if (player.AttachedEntity is not { Valid: true } entity)
                return;

            if (player.Status != SessionStatus.InGame)
                return;

            if (args.Length < 1)
                return;

            var message = string.Join(" ", args).Trim();
            if (string.IsNullOrEmpty(message))
                return;

            if (IoCManager.Resolve<IEntitySystemManager>()
                .GetEntitySystem<CoolDownChatSystem>()
                .CheckDeadOrLooc(entity, player, false))
                return;

            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ChatSystem>()
                .TrySendInGameOOCMessage(entity, message, InGameOOCChatType.Looc, false, shell, player);
        }
    }
}
