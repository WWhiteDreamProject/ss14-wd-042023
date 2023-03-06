using System.Linq;
using System.Net;
using Content.Server.Administration.Managers;
using Content.Server.GameTicking;
using Content.Server.Maps;
using Content.Server.RoundEnd;
using Content.Shared.CCVar;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Enums;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Player;

namespace Content.Server.UtkaIntegration;

public sealed class UtkaStatusCommand : IUtkaCommand
{
    public string Name => "status";
    public Type RequestMessageType => typeof(UtkaStatusRequsets);

    [Dependency] private readonly IPlayerManager _playerManager = default!;
    [Dependency] private readonly IAdminManager _adminManager = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly UtkaTCPWrapper _utkaSocketWrapper = default!;

    public void Execute(UtkaTCPSession session, UtkaBaseMessage baseMessage)
    {
        if(baseMessage is not UtkaStatusRequsets message) return;
        IoCManager.InjectDependencies(this);

        var players = Filter.GetAllPlayers().ToList();
        var playerNames = players
            .Where(player => player.Status != SessionStatus.Disconnected)
            .Select(x => x.Name);

        var admins = _adminManager.ActiveAdmins.Select(x => x.Name).ToList();

        string shuttleData = string.Empty;

        if (_roundEndSystem.ExpectedCountdownEnd == null)
        {
            shuttleData = "shuttle is not on the way";
        }
        else
        {
            shuttleData = $"shuttle is on the way, ETA: {_roundEndSystem.ShuttleTimeLeft}";
        }

        var roundDuration = _gameTicker.RoundDuration().TotalSeconds;
        var gameMap = _cfg.GetCVar(CCVars.GameMap);

        var toUtkaMessage = new UtkaStatusResponse()
        {
            Players = playerNames.ToList(),
            Admins = admins,
            Map = gameMap,
            ShuttleStatus = shuttleData,
            RoundDuration = roundDuration
        };

        _utkaSocketWrapper.SendMessageToAll(toUtkaMessage);
    }
}
