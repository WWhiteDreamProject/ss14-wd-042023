using Content.Shared.CCVar;
using Robust.Shared.Configuration;

namespace Content.Server.UtkaIntegration;

public sealed class UtkaAuthenticationCommand : IUtkaCommand
{
    [Dependency] private readonly IConfigurationManager _configurationManager = default!;
    [Dependency] private readonly UtkaTCPWrapper _utkaTcpWrapper = default!;

    public string Name => "handshake";
    public Type RequestMessageType => typeof(UtkaHandshakeMessage);

    public void Execute(UtkaTCPSession session, UtkaBaseMessage baseMessage)
    {
        if (baseMessage is not UtkaHandshakeMessage message) return;

        IoCManager.InjectDependencies(this);
        
        if (string.IsNullOrWhiteSpace(message.Key))
        {
            SendMessage(session, "key_missmatch");
            return;
        }

        var key = _configurationManager.GetCVar(CCVars.UtkaSocketKey);

        if (key != message.Key)
        {
            SendMessage(session, "key_missmatch");
            return;
        }

        if (session.Authenticated)
        {
            SendMessage(session, "already_authentificated");
            return;
        }

        session.Authenticated = true;
        SendMessage(session, "handshake_accepted");
    }

    private void SendMessage(UtkaTCPSession session, string message)
    {
        var response = new UtkaHandshakeMessage()
        {
            Key = _configurationManager.GetCVar(CCVars.UtkaSocketKey),
            Message = message
        };

        _utkaTcpWrapper.SendMessageToClient(session, response);
    }
}
