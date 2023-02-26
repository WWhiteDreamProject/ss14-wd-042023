using System.Net;
using Content.Shared.CCVar;
using Robust.Shared;
using Robust.Shared.Configuration;

namespace Content.Server.UtkaIntegration;

public sealed class UtkaTCPWrapper
{
    [Dependency] private readonly IConfigurationManager _cfg = default!;

    private UtkaTCPServer _server = default!;
    private string _key = string.Empty;
    private readonly ISawmill _sawmill = Logger.GetSawmill("utka.sockets");

    private bool _initialized;

    public void Initialize()
    {
        if(_initialized) return;

        _key = _cfg.GetCVar(CCVars.UtkaSocketKey);
        _key = "ass";

        if (string.IsNullOrEmpty(_key))
        {
            _sawmill.Warning($"No key provided for UtkaSocket, not initializing.");
            return;
        }

        var port = _cfg.GetCVar(CVars.NetPort) + 100;

        try
        {
             _server = new UtkaTCPServer(IPAddress.Any, port);

        }
        catch (Exception e)
        {
            _sawmill.Warning($"Failed to initialize UtkaSocket: {e}");
            return;
        }

        _server.Start();

        _initialized = true;
    }

    public void SendMessageToAll(UtkaBaseMessage message)
    {
        _server.SendMessageToAll(message);
    }

    public void SendMessageToClient(UtkaTCPSession session, UtkaBaseMessage message)
    {
        _server.SendMessageToClient(session, message);
    }

    public void Shutdown()
    {
        _server.Stop();
        _server.Multicast("Server shutting down.");
        _server.DisconnectAll();
        _server.Dispose();
    }
}
