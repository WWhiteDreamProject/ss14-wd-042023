using System.Linq;
using Content.Server.Borgs;
using Content.Server.Chat.Managers;
using Content.Server.Flash;
using Content.Server.GameTicking.Presets;
using Content.Server.GameTicking.Rules.Components;
using Content.Server.GameTicking.Rules.Configurations;
using Content.Server.Mind.Components;
using Content.Server.NPC.Systems;
using Content.Server.Players;
using Content.Server.Preferences.Managers;
using Content.Server.RoundEnd;
using Content.Server.Station.Systems;
using Content.Server.Traitor;
using Content.Server.Traits.Assorted;
using Content.Server.White.Mindshield;
using Content.Shared.CombatMode.Pacification;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Shared.Popups;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Content.Shared.White.Mindshield;
using Robust.Server.GameObjects;
using Robust.Server.Player;
using Robust.Shared.GameStates;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;


namespace Content.Server.GameTicking.Rules;

public sealed class RevolutionRuleSystem : GameRuleSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IChatManager _chatManager = default!;
    [Dependency] private readonly FactionSystem _faction = default!;
    [Dependency] private readonly RoundEndSystem _roundEndSystem = default!;
    [Dependency] private readonly SharedAudioSystem _audioSystem = default!;
    [Dependency] private readonly StationSpawningSystem _stationSpawningSystem = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;
    [Dependency] private readonly MindShieldSystem _mindShieldSystem = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;




    private enum WinType
    {
        /// <summary>
        ///     Rev major win. All Crew Heads are dead.
        /// </summary>
        RevMajor,
        /// <summary>
        ///     Neutral win. Shuttle has arrived on CentComm with some Head Revs and some Crew Heads alive.
        /// </summary>
        Neutral,
        /// <summary>
        ///     Crew major win. This means all Head Revs are dead.
        /// </summary>
        CrewMajor
    }

    private enum WinCondition
    {
        AllHeadRevsDead,
        AllCrewHeadsDead
    }

    private WinType _winType = WinType.Neutral;

    private WinType RuleWinType
    {
        get => _winType;
        set
        {
            _winType = value;

            if (value == WinType.CrewMajor || value == WinType.RevMajor)
            {
                _roundEndSystem.EndRound();
            }
        }
    }
    private List<WinCondition> _winConditions = new ();


    public override string Prototype => "Revolution";

    private AntagPrototype _antagPrototype = new();

    private RevolutionaryGameRuleConfiguration _revRuleConfig = new();

    private readonly List<IPlayerSession> _revPlayers = new();

    private readonly List<IPlayerSession> _headPlayers = new();

    private List<JobPrototype> _headJobPrototypes = new();

    public int TotalHeadRevs => _revPlayers.Count;


    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RoundStartAttemptEvent>(OnStartAttempt);
        SubscribeLocalEvent<RulePlayerJobsAssignedEvent>(OnPlayersSpawned);
        SubscribeLocalEvent<MindComponent, MobStateChangedEvent>(OnMobStateChanged);
        SubscribeLocalEvent<RoundEndTextAppendEvent>(OnRoundEndText);
        SubscribeLocalEvent<GameRunLevelChangedEvent>(OnRunLevelChanged);
        SubscribeLocalEvent<RevolutionaryComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<RevolutionaryComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(HandleLatejoin);
        SubscribeLocalEvent<FlashAttemptEvent>(OnFlashAttempt);
        SubscribeLocalEvent<RevolutionaryComponent, ComponentGetState>(OnGetState);
        SubscribeLocalEvent<MindShieldImplanted>(OnMindshieldImplanted);
    }

    private void OnMindshieldImplanted(MindShieldImplanted ev)
    {
        if (!TryComp<RevolutionaryComponent>(ev.Target, out var revolutionaryComponent)) return;

        if (!revolutionaryComponent.HeadRevolutionary)
        {
            RemComp(ev.Target, revolutionaryComponent);
            if(!TryComp<ActorComponent>(ev.Target, out var actorComponent)) return;

            _chatManager.DispatchServerMessage(actorComponent.PlayerSession, "Ваш разум очистился, вы больше не революционер");
        }
        else
        {
            _mindShieldSystem.RemoveMindShieldImplant(ev.Target, ev.MindShield, true);
        }
    }


    private void OnFlashAttempt(FlashAttemptEvent msg)
    {
        if(!RuleAdded)
            return;
        if(msg.Cancelled)
            return;
        if(!TryComp<ActorComponent>(msg.Target, out var actor))
            return;
        if(HasComp<BorgComponent>(msg.Target))
            return;
        if(TryComp<RevolutionaryComponent>(msg.User, out var comp) && !comp!.HeadRevolutionary)
            return;
        if (TryComp<MindComponent>(msg.Target, out var mindComponent) && mindComponent.HasMind
            && _headJobPrototypes.Contains(mindComponent.Mind!.CurrentJob!.Prototype))
            return;
        if(HasComp<MindShieldComponent>(msg.Target))
            return;
        if(HasComp<RevolutionaryComponent>(msg.Target))
            return;

        var targetComp = EnsureComp<RevolutionaryComponent>(msg.Target);
        targetComp.HeadRevolutionary = false;
        Dirty(targetComp);

        _chatManager.DispatchServerMessage(actor.PlayerSession, Loc.GetString("rev-welcome-rev"));
    }

    private void OnComponentInit(EntityUid uid, RevolutionaryComponent component, ComponentInit args)
    {
        if (!TryComp<MindComponent>(uid, out var mindComponent) || !RuleAdded)
            return;

        if (!mindComponent.HasMind)
            return;

        var session = mindComponent.Mind?.Session;
        if (session != null)
            _revPlayers.Add(session);

        mindComponent.Mind!.AddRole(new TraitorRole(mindComponent.Mind!, _antagPrototype));
        _faction.RemoveFaction(uid, "NanoTrasen");
        RemComp<PacifistComponent>(uid);
        RemComp<PacifiedComponent>(uid);
    }

    private void OnComponentRemove(EntityUid uid, RevolutionaryComponent component, ComponentRemove args)
    {
        if (TryComp<MindComponent>(uid, out var comp) && comp.HasMind && comp.Mind!.TryGetSession(out var playerSession))
        {
            _revPlayers.Remove(playerSession);
            if (comp.Mind!.HasRole<TraitorRole>())
            {
                foreach (var role in comp.Mind!.AllRoles )
                {
                    if (role is TraitorRole traitorRole && traitorRole.Prototype.ID == _antagPrototype.ID)
                    {
                        comp.Mind!.RemoveRole(role);
                    }
                }
            }
            _faction.AddFaction(uid, "NanoTrasen");

        }

        CheckRoundShouldEnd();
    }

    private void OnGetState(EntityUid uid, RevolutionaryComponent component, ref ComponentGetState args)
    {
        args.State = new RevolutionaryComponentState(component.HeadRevolutionary);
    }

    private void OnRunLevelChanged(GameRunLevelChangedEvent ev)
    {
        switch (ev.New)
        {
            case GameRunLevel.InRound:
                OnRoundStart();
                break;
        }
    }

    private void OnRoundStart()
    {

        var filter = Filter.Empty();
        foreach (var headrev in EntityQuery<RevolutionaryComponent>())
        {
            if (!TryComp<ActorComponent>(headrev.Owner, out var actor))
            {
                continue;
            }

            _chatManager.DispatchServerMessage(actor.PlayerSession, Loc.GetString("rev-welcome-headrev"));
            filter.AddPlayer(actor.PlayerSession);
        }

        _audioSystem.PlayGlobal(_revRuleConfig.GreetSound, filter, recordReplay: false);
    }

    private void OnRoundEndText(RoundEndTextAppendEvent ev)
    {
        if (!RuleAdded)
            return;

        var winText = Loc.GetString($"rev-{_winType.ToString().ToLower()}");

        ev.AddLine(winText);

        foreach (var cond in _winConditions)
        {
            var text = Loc.GetString($"rev-cond-{cond.ToString().ToLower()}");

            ev.AddLine(text);
        }

        string listing;
        ev.AddLine(Loc.GetString("rev-list-revs-start"));
        foreach (var session in _revPlayers)
        {
            listing = Loc.GetString("rev-list", ("name", session.ContentData()?.Mind?.CharacterName!), ("user", session.Name), ("headrev",
                CompOrNull<RevolutionaryComponent>(session.ContentData()?.Mind?.OwnedEntity) is { } revcomp && revcomp.HeadRevolutionary ? Loc.GetString("rev-list-headrevbool") : ""));
            ev.AddLine(listing);
        }
        ev.AddLine(Loc.GetString("rev-list-heads-start"));
        foreach (var session in _headPlayers)
        {
            listing = Loc.GetString("rev-list", ("name", session.ContentData()?.Mind?.CharacterName!), ("user", session.Name), ("headrev", ""));
            ev.AddLine(listing);
        }
    }

    private void CheckRoundShouldEnd()
    {
        if (!RuleAdded || RuleWinType == WinType.CrewMajor || RuleWinType == WinType.RevMajor)
            return;


        var headRevsAlive = EntityQuery<RevolutionaryComponent, MobStateComponent, MindComponent>(true)
            .Where(rev => rev.Item1.HeadRevolutionary)
            .Any(ent => ent.Item2.CurrentState == MobState.Alive && ent.Item1.Running && ent.Item3.Mind != null);

        var headCrewAlive = EntityQuery<MindComponent, MobStateComponent>(true)
            .Where(crew => _headJobPrototypes.Contains(crew.Item1?.Mind?.CurrentJob?.Prototype!))
            .Any(ent => ent.Item2.CurrentState == MobState.Alive && ent.Item1 is { Running: true, Mind: { } });

        if (headRevsAlive && headCrewAlive)
            return;

        if (!headRevsAlive)
        {
            _winConditions.Add(WinCondition.AllHeadRevsDead);
            RuleWinType = WinType.CrewMajor;
        }
        else
        {
            _winConditions.Add(WinCondition.AllCrewHeadsDead);
            RuleWinType = WinType.RevMajor;
        }
    }

    private void OnMobStateChanged(EntityUid uid, MindComponent component, MobStateChangedEvent ev)
    {
        if(ev.NewMobState == MobState.Dead)
            CheckRoundShouldEnd();
    }

    private void OnPlayersSpawned(RulePlayerJobsAssignedEvent ev)
    {
        if (!RuleAdded)
            return;

        var players = new List<IPlayerSession>(ev.Players);
        var nonCrewHeads = players.Where(player =>
        {
            var jobPrototype = player.ContentData()?.Mind?.CurrentJob!.Prototype;
            return jobPrototype != null &&
                   !_headJobPrototypes.Contains(player.ContentData()?.Mind?.CurrentJob?.Prototype!) &&
                   jobPrototype.CanBeAntag;
        }).ToList();

        _headPlayers.AddRange(players.Where(player => !nonCrewHeads.Contains(player) &&
                                                      _headJobPrototypes.Contains(player.ContentData()?.Mind?.CurrentJob?.Prototype!)));

        if (nonCrewHeads.Count == 0 || _headPlayers.Count == 0)
        {
            _gameTicker.EndGameRule(_prototypeManager.Index<GameRulePrototype>("Revolution"));
            _gameTicker.AddGameRule(_prototypeManager.Index<GameRulePrototype>("Traitor"));
            return;
        }

        var prefList = new List<IPlayerSession>();

        foreach (var player in nonCrewHeads)
        {
            var profile = ev.Profiles[player.UserId];
            if (profile.AntagPreferences.Contains(Prototype))
            {
                prefList.Add(player);
            }
        }
        if (prefList.Count == 0)
        {
            //_sawmill.Info("Insufficient preferred headrevs, picking at random.");
            prefList = nonCrewHeads;
        }

        var numHeadRevs = MathHelper.Clamp(prefList.Count / _revRuleConfig.PlayersPerHeadRev, 1, _revRuleConfig.MaxHeadRev);
        var headRevs = new List<IPlayerSession>();

        for (var i = 0; i < numHeadRevs; i++)
        {
            headRevs.Add(_random.PickAndTake(prefList));
            //_sawmill.Info("Selected a preferred Head Rev.");
        }

        foreach (var headRev in headRevs)
        {
            MakeHeadRevolution(headRev);
        }
    }

    private void MakeHeadRevolution(IPlayerSession headRev)
    {
        var mind = headRev.Data.ContentData()?.Mind;
        if (mind == null)
        {
            //_sawmill.Info("Failed getting mind for picked headrev.");
            return;
        }

        if (mind.OwnedEntity is not { } entity)
        {
            Logger.ErrorS("preset", "Mind picked for headrev did not have an attached entity.");
            return;
        }

        var profile = _prefs.GetPreferences(headRev.UserId).SelectedCharacter as HumanoidCharacterProfile;
        _stationSpawningSystem.EquipStartingGear(entity, _prototypeManager.Index<StartingGearPrototype>("HeadRevolutionaryGear"), profile);

        EntityManager.EnsureComponent<RevolutionaryComponent>(entity);
        _audioSystem.PlayGlobal(_revRuleConfig.GreetSound, Filter.Empty().AddPlayer(headRev), false);
    }

    private void OnStartAttempt(RoundStartAttemptEvent ev)
    {
        if (!RuleAdded || Configuration is not RevolutionaryGameRuleConfiguration revRuleConfig)
            return;

        _revRuleConfig = revRuleConfig;
        var minPlayers = _prototypeManager.Index<GamePresetPrototype>(Prototype).MinPlayers;
        if (!ev.Forced && ev.Players.Length < minPlayers)
        {
            _chatManager.DispatchServerAnnouncement(Loc.GetString("rev-not-enough-ready-players", ("readyPlayersCount", ev.Players.Length), ("minimumPlayers", minPlayers)));
            ev.Cancel();
            return;
        }

        if (ev.Players.Length != 0)
            return;

        _chatManager.DispatchServerAnnouncement(Loc.GetString("rev-no-one-ready"));
        ev.Cancel();
    }

    private void HandleLatejoin(PlayerSpawnCompleteEvent ev)
    {
        if (!RuleAdded)
            return;
        if (TotalHeadRevs >= _revRuleConfig.MaxHeadRev)
            return;
        if (!ev.LateJoin)
            return;
        if (!ev.Profile.AntagPreferences.Contains(Prototype))
            return;

        if (ev.JobId == null || !_prototypeManager.TryIndex<JobPrototype>(ev.JobId, out var job))
            return;

        if (_headJobPrototypes.Contains(job))
        {
            _headPlayers.Add(ev.Player);
            return;
        }

        if (!job.CanBeAntag)
            return;


        int target = ((_revRuleConfig.PlayersPerHeadRev * TotalHeadRevs) + 1);

        float chance = (1f / _revRuleConfig.PlayersPerHeadRev);

        if (ev.JoinOrder < target)
        {
            chance /= (target - ev.JoinOrder);
        } else
        {
            chance *= ((ev.JoinOrder + 1) - target);
        }
        if (chance > 1)
            chance = 1;

        if (_random.Prob(chance))
        {
            MakeHeadRevolution(ev.Player);
            _chatManager.DispatchServerMessage(ev.Player, Loc.GetString("rev-welcome-headrev"));
        }
    }

    public override void Started()
    {
        RuleWinType = WinType.Neutral;
        _winConditions.Clear();
        _revPlayers.Clear();
        _headPlayers.Clear();
        _headJobPrototypes = _prototypeManager.EnumeratePrototypes<JobPrototype>().Where(job =>
            job.Access.Contains("Command") || job.AccessGroups.Contains("AllAccess")).ToList();
        _antagPrototype = _prototypeManager.Index<AntagPrototype>(Prototype);


        var query = EntityQuery<RevolutionaryComponent, MindComponent>(true);
        foreach (var (_, mindComp) in query)
        {
            if (mindComp.Mind == null || !mindComp.Mind.TryGetSession(out var session))
                continue;
            _revPlayers.Add(session);
        }
    }

    public override void Ended() { }
}
