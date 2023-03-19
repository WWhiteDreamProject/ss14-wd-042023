using Content.Server.Administration.Systems;
using Robust.Server.Player;

namespace Content.Server.UtkaIntegration;

public sealed class UtkaPmCommand : IUtkaCommand
{
    public string Name => "discord_pm";
    public Type RequestMessageType => typeof(UtkaPmRequest);

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private UtkaTCPWrapper _utkaSocketWrapper = default!;

    public void Execute(UtkaTCPSession session, UtkaBaseMessage baseMessage)
    {
        if(baseMessage is not UtkaPmRequest message) return;
        var _bwoink = EntitySystem.Get<BwoinkSystem>();
        IoCManager.InjectDependencies(this);

        if(string.IsNullOrWhiteSpace(message.Message) || string.IsNullOrWhiteSpace(message.Sender) || string.IsNullOrWhiteSpace(message.Reciever)) return;

        _playerManager.TryGetUserId(message.Sender, out var sender);

        var toUtkaMessage = new UtkaPmResponse();
        if (!_playerManager.TryGetUserId(message.Reciever, out var reciever))
        {
            toUtkaMessage.Message = false;
            _utkaSocketWrapper.SendMessageToAll(toUtkaMessage);
            return;
        }
        var bwoinkText = $"[color=red]{message.Sender}[/color]: {message.Message}";

        _bwoink.BwoinkSendHookMessage(reciever, sender, bwoinkText);

        toUtkaMessage.Message = true;
        _utkaSocketWrapper.SendMessageToAll(toUtkaMessage);
    }
}
