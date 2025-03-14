using Content.Server.Chat.Managers;
using Content.Server.Chat.Systems;
using Content.Shared.Administration;
using Robust.Server.Player;
using Robust.Shared.Console;
using Robust.Shared.Enums;

namespace Content.Server.Chat.Commands
{
    [AnyCommand]
    internal sealed class WhisperCommand : IConsoleCommand
    {
        public string Command => "whisper";
        public string Description => "Send chat messages to the local channel as a whisper";
        public string Help => "whisper <text>";

        public void Execute(IConsoleShell shell, string argStr, string[] args)
        {
            if (shell.Player is not IPlayerSession player)
            {
                shell.WriteError("This command cannot be run from the server.");
                return;
            }

            if (player.Status != SessionStatus.InGame)
                return;

            if (player.AttachedEntity is not {} playerEntity)
            {
                shell.WriteError("You don't have an entity!");
                return;
            }

            if (args.Length < 1)
                return;

            var message = string.Join(" ", args).Trim();
            if (string.IsNullOrEmpty(message))
                return;

            if (IoCManager.Resolve<IEntitySystemManager>()
                .GetEntitySystem<CoolDownChatSystem>()
                .Check(playerEntity, player, false))
                return;

            IoCManager.Resolve<IEntitySystemManager>().GetEntitySystem<ChatSystem>()
                .TrySendInGameICMessage(playerEntity, message, InGameICChatType.Whisper, false, false, shell, player);
        }
    }
}
