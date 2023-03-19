using Content.Server.Administration.Systems;
using Robust.Server.Player;

namespace Content.Server.UtkaIntegration;

public sealed class UtkaPmCommand : IUtkaCommand
{
    public string Name => "discord_pm";
    public Type RequestMessageType => typeof(UtkaPmRequest);

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    //[Dependency] private readonly BwoinkSystem _bwoink = default!;

    public void Execute(UtkaTCPSession session, UtkaBaseMessage baseMessage)
    {
        if(baseMessage is not UtkaPmRequest message) return;
        var _bwoink = EntitySystem.Get<BwoinkSystem>();
        IoCManager.InjectDependencies(this);

        if(string.IsNullOrWhiteSpace(message.Message) || string.IsNullOrWhiteSpace(message.Sender) || string.IsNullOrWhiteSpace(message.Reciever)) return;

        _playerManager.TryGetUserId(message.Sender, out var sender);
        _playerManager.TryGetUserId(message.Reciever, out var reciever);
        var bwoinkText = $"[color=red]{message.Sender}[/color]: {message.Message}";

        _bwoink.BwoinkSendHookMessage(reciever, sender, bwoinkText);
    }
}
