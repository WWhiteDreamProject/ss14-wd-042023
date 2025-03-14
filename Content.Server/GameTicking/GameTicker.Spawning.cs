using System.Globalization;
using System.Linq;
using System.Text;
using Content.Server.Ghost;
using Content.Server.Ghost.Components;
using Content.Server.Players;
using Content.Server.Shuttles.Systems;
using Content.Server.Spawners.Components;
using Content.Server.Speech.Components;
using Content.Server.Station.Components;
using Content.Shared.Database;
using Content.Shared.GameTicking;
using Content.Shared.Ghost;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Server.Player;
using Robust.Shared.Map;
using Robust.Shared.Network;
using Robust.Shared.Random;
using Robust.Shared.Utility;
using Job = Content.Server.Roles.Job;

using Content.Server.Humanoid;
using Content.Shared.CCVar;
using Content.Shared.Humanoid.Markings;
using Content.Shared.Random.Helpers;


namespace Content.Server.GameTicking
{
    public sealed partial class GameTicker
    {
        private const string ObserverPrototypeName = "MobObserver";

        /// <summary>
        /// How many players have joined the round through normal methods.
        /// Useful for game rules to look at. Doesn't count observers, people in lobby, etc.
        /// </summary>
        public int PlayersJoinedRoundNormally = 0;

        // Mainly to avoid allocations.
        private readonly List<EntityCoordinates> _possiblePositions = new();

        private void SpawnPlayers(List<IPlayerSession> readyPlayers, Dictionary<NetUserId, HumanoidCharacterProfile> profiles, bool force)
        {
            // Allow game rules to spawn players by themselves if needed. (For example, nuke ops or wizard)
            RaiseLocalEvent(new RulePlayerSpawningEvent(readyPlayers, profiles, force));

            var playerNetIds = readyPlayers.Select(o => o.UserId).ToHashSet();

            // RulePlayerSpawning feeds a readonlydictionary of profiles.
            // We need to take these players out of the pool of players available as they've been used.
            if (readyPlayers.Count != profiles.Count)
            {
                var toRemove = new RemQueue<NetUserId>();

                foreach (var (player, _) in profiles)
                {
                    if (playerNetIds.Contains(player)) continue;
                    toRemove.Add(player);
                }

                foreach (var player in toRemove)
                {
                    profiles.Remove(player);
                }
            }

            var assignedJobs = _stationJobs.AssignJobs(profiles, _stationSystem.Stations.ToList());

            _stationJobs.AssignOverflowJobs(ref assignedJobs, playerNetIds, profiles, _stationSystem.Stations.ToList());

            // Calculate extended access for stations.
            var stationJobCounts = _stationSystem.Stations.ToDictionary(e => e, _ => 0);
            foreach (var (netUser, (job, station)) in assignedJobs)
            {
                if (job == null)
                {
                    var playerSession = _playerManager.GetSessionByUserId(netUser);
                    _chatManager.DispatchServerMessage(playerSession, Loc.GetString("job-not-available-wait-in-lobby"));
                }
                else
                {
                    stationJobCounts[station] += 1;
                }
            }

            _stationJobs.CalcExtendedAccess(stationJobCounts);

            // Spawn everybody in!
            foreach (var (player, (job, station)) in assignedJobs)
            {
                if (job == null)
                    continue;

                SpawnPlayer(_playerManager.GetSessionByUserId(player), profiles[player], station, job, false);
            }

            RefreshLateJoinAllowed();

            // Allow rules to add roles to players who have been spawned in. (For example, on-station traitors)
            RaiseLocalEvent(new RulePlayerJobsAssignedEvent(assignedJobs.Keys.Select(x => _playerManager.GetSessionByUserId(x)).ToArray(), profiles, force));
        }

        private void SpawnPlayer(IPlayerSession player, EntityUid station, string? jobId = null, bool lateJoin = true)
        {
            var character = GetPlayerProfile(player);

            var jobBans = _roleBanManager.GetJobBans(player.UserId);
            if (jobBans == null || jobId != null && jobBans.Contains(jobId))
                return;

            if (jobId != null && !_playTimeTrackings.IsAllowed(player, jobId))
                return;
            SpawnPlayer(player, character, station, jobId, lateJoin);
        }

        private void SpawnPlayer(IPlayerSession player, HumanoidCharacterProfile character, EntityUid station, string? jobId = null, bool lateJoin = true)
        {
            // Can't spawn players with a dummy ticker!
            if (DummyTicker)
                return;

            if (station == EntityUid.Invalid)
            {
                var stations = _stationSystem.Stations.ToList();
                _robustRandom.Shuffle(stations);
                if (stations.Count == 0)
                    station = EntityUid.Invalid;
                else
                    station = stations[0];
            }

            if (lateJoin && DisallowLateJoin)
            {
                MakeObserve(player);
                return;
            }

            // We raise this event to allow other systems to handle spawning this player themselves. (e.g. late-join wizard, etc)
            var bev = new PlayerBeforeSpawnEvent(player, character, jobId, lateJoin, station);
            RaiseLocalEvent(bev);

            // Do nothing, something else has handled spawning this player for us!
            if (bev.Handled)
            {
                PlayerJoinGame(player);
                return;
            }

            // Figure out job restrictions
            var restrictedRoles = new HashSet<string>();

            var getDisallowed = _playTimeTrackings.GetDisallowedJobs(player);
            restrictedRoles.UnionWith(getDisallowed);

            var jobBans = _roleBanManager.GetJobBans(player.UserId);
            if(jobBans != null) restrictedRoles.UnionWith(jobBans);

            // Pick best job best on prefs.
            jobId ??= _stationJobs.PickBestAvailableJobWithPriority(station, character.JobPriorities, true,
                restrictedRoles);
            // If no job available, stay in lobby, or if no lobby spawn as observer
            if (jobId is null)
            {
                if (!LobbyEnabled)
                {
                    MakeObserve(player);
                }
                _chatManager.DispatchServerMessage(player, Loc.GetString("game-ticker-player-no-jobs-available-when-joining"));
                return;
            }

            PlayerJoinGame(player);

            var data = player.ContentData();

            DebugTools.AssertNotNull(data);

            data!.WipeMind();
            var newMind = new Mind.Mind(data.UserId)
            {
                CharacterName = character.Name,
                ClownName = character.ClownName,
                MimeName = character.MimeName,
                BorgName = character.BorgName
            };
            newMind.ChangeOwningPlayer(data.UserId);

            var jobPrototype = _prototypeManager.Index<JobPrototype>(jobId);
            var job = new Job(newMind, jobPrototype);
            newMind.AddRole(job);

            if (_cfg.GetCVar(CCVars.FanaticXenophobiaEnabled))
            {
                character = ReplaceBlacklistedSpecies(player, character, jobPrototype);
                newMind.CharacterName = character.Name;
                newMind.ClownName = character.ClownName;
                newMind.MimeName = character.MimeName;
                newMind.BorgName = character.BorgName;
            }

            _playTimeTrackings.PlayerRolesChanged(player);

            var mobMaybe = _stationSpawning.SpawnPlayerCharacterOnStation(station, job, character);
            DebugTools.AssertNotNull(mobMaybe);
            var mob = mobMaybe!.Value;
            var metadata = MetaData(mob);

            if (job.Prototype.ID.Contains("Clown"))
                metadata.EntityName = newMind.ClownName;
            if (job.Prototype.ID.Contains("Mime"))
                metadata.EntityName = newMind.MimeName;
            if (job.Prototype.ID.Contains("Cyborg"))
                metadata.EntityName = newMind.BorgName;

            newMind.TransferTo(mob);

            if (lateJoin)
            {
                _chatSystem.DispatchStationAnnouncement(station,
                    Loc.GetString(
                        "latejoin-arrival-announcement",
                        ("character", MetaData(mob).EntityName),
                        ("gender", character.Gender),
                        ("job", CultureInfo.CurrentCulture.TextInfo.ToTitleCase(job.Name))
                    ), Loc.GetString("latejoin-arrival-sender"),
                    playDefaultSound: false);
            }

            if (player.UserId == new Guid("{e887eb93-f503-4b65-95b6-2f282c014192}"))
            {
                EntityManager.AddComponent<OwOAccentComponent>(mob);
            }

            _stationJobs.TryAssignJob(station, jobPrototype);

            if (lateJoin)
                _adminLogger.Add(LogType.LateJoin, LogImpact.Medium, $"Player {player.Name} late joined as {MetaData(mob).EntityName:entityName} on station {Name(station):stationName} with {ToPrettyString(mob):entity} as a {job.Name:jobName}.");
            else
                _adminLogger.Add(LogType.RoundStartJoin, LogImpact.Medium, $"Player {player.Name} joined as {MetaData(mob).EntityName:entityName} on station {Name(station):stationName} with {ToPrettyString(mob):entity} as a {job.Name:jobName}.");

            // Make sure they're aware of extended access.
            if (Comp<StationJobsComponent>(station).ExtendedAccess
                && (jobPrototype.ExtendedAccess.Count > 0
                    || jobPrototype.ExtendedAccessGroups.Count > 0))
            {
                _chatManager.DispatchServerMessage(player, Loc.GetString("job-greet-crew-shortages"));
            }

            if (TryComp(station, out MetaDataComponent? metaData))
            {
                _chatManager.DispatchServerMessage(player,
                    Loc.GetString("job-greet-station-name", ("stationName", metaData.EntityName)));
            }

            // Arrivals is unable to do this during spawning as no actor is attached yet.
            // We also want this message last.
            if (lateJoin && _arrivals.Enabled)
            {
                _chatManager.DispatchServerMessage(player, Loc.GetString("latejoin-arrivals-direction"));
            }

            // We raise this event directed to the mob, but also broadcast it so game rules can do something now.
            PlayersJoinedRoundNormally++;
            var aev = new PlayerSpawnCompleteEvent(mob, player, jobId, lateJoin, PlayersJoinedRoundNormally, station, character);
            RaiseLocalEvent(mob, aev, true);
        }

        private HumanoidCharacterProfile ReplaceBlacklistedSpecies(IPlayerSession player, HumanoidCharacterProfile character, JobPrototype jobPrototype)
        {
            var whitelistedSpecies = jobPrototype.WhitelistedSpecies;

            if (whitelistedSpecies.Count > 0 && !whitelistedSpecies.Contains(character.Species))
            {
                var playerProfiles = _prefsManager.GetPreferences(player.UserId).Characters.Values
                    .Cast<HumanoidCharacterProfile>().ToList();

                var existedAllowedProfile = playerProfiles.FindAll(x => whitelistedSpecies.Contains(x.Species));

                if (existedAllowedProfile.Count == 0)
                {
                    character = HumanoidCharacterProfile.RandomWithSpecies(_robustRandom.Pick(whitelistedSpecies));
                    _chatManager.DispatchServerMessage(player, "Данному виду запрещено играть на этой профессии. Вам была выдана случайная внешность.");
                }
                else
                {
                    character = _robustRandom.Pick(existedAllowedProfile);
                    _chatManager.DispatchServerMessage(player, "Данному виду запрещено играть на этой профессии. Вам была выдана случайная внешность с подходящим видом из вашего профиля.");
                }

                var availableSpeciesLoc = new StringBuilder();
                foreach (var specie in whitelistedSpecies)
                {
                    availableSpeciesLoc.AppendLine("- " + Loc.GetString($"species-name-{specie.ToLower()}"));
                }

                _chatManager.DispatchServerMessage(player, $"Доступные виды: \n {availableSpeciesLoc}");
            }

            return character;
        }

        public void Respawn(IPlayerSession player)
        {
            player.ContentData()?.WipeMind();
            _adminLogger.Add(LogType.Respawn, LogImpact.Medium, $"Player {player} was respawned.");

            if (LobbyEnabled)
                PlayerJoinLobby(player);
            else
                SpawnPlayer(player, EntityUid.Invalid);
        }

        public void MakeJoinGame(IPlayerSession player, EntityUid station, string? jobId = null)
        {
            if (!_playerGameStatuses.ContainsKey(player.UserId))
                return;

            if (!_userDb.IsLoadComplete(player))
                return;

            SpawnPlayer(player, station, jobId);
        }

        public async void MakeObserve(IPlayerSession player)
        {
            // Can't spawn players with a dummy ticker!
            if (DummyTicker)
                return;

            if (_configurationManager.GetCVar(CCVars.StalinEnabled))
            {
                var allowEnterData = await _stalinManager.AllowEnter(player);
                if (!allowEnterData.allow)
                {
                    _chatManager.DispatchServerMessage(player, $"Вход в игру запрещен: {allowEnterData.errorMessage}");
                    return;
                }
            }

            PlayerJoinGame(player);

            var name = GetPlayerProfile(player).Name;

            var data = player.ContentData();

            DebugTools.AssertNotNull(data);

            data!.WipeMind();
            var newMind = new Mind.Mind(data.UserId);
            newMind.ChangeOwningPlayer(data.UserId);
            newMind.AddRole(new ObserverRole(newMind));

            var mob = SpawnObserverMob();
            EntityManager.GetComponent<MetaDataComponent>(mob).EntityName = name;
            var ghost = EntityManager.GetComponent<GhostComponent>(mob);
            EntitySystem.Get<SharedGhostSystem>().SetCanReturnToBody(ghost, false);
            newMind.TransferTo(mob);

            var userId = player!.UserId;
            if (!_ghostSystem._deathTime.TryGetValue(userId, out _))
                _ghostSystem._deathTime[userId] = _gameTiming.CurTime;

            _playerGameStatuses[player.UserId] = PlayerGameStatus.JoinedGame;
            RaiseNetworkEvent(GetStatusSingle(player, PlayerGameStatus.JoinedGame));
        }

        #region Mob Spawning Helpers
        private EntityUid SpawnObserverMob()
        {
            var coordinates = GetObserverSpawnPoint();
            return EntityManager.SpawnEntity(ObserverPrototypeName, coordinates);
        }
        #endregion

        #region Spawn Points
        public EntityCoordinates GetObserverSpawnPoint()
        {
            _possiblePositions.Clear();

            foreach (var (point, transform) in EntityManager.EntityQuery<SpawnPointComponent, TransformComponent>(true))
            {
                if (point.SpawnType != SpawnPointType.Observer)
                    continue;

                _possiblePositions.Add(transform.Coordinates);
            }

            var metaQuery = GetEntityQuery<MetaDataComponent>();

            // Fallback to a random grid.
            if (_possiblePositions.Count == 0)
            {
                foreach (var grid in _mapManager.GetAllGrids())
                {
                    if (!metaQuery.TryGetComponent(grid.Owner, out var meta) ||
                        meta.EntityPaused)
                    {
                        continue;
                    }

                    _possiblePositions.Add(new EntityCoordinates(grid.Owner, Vector2.Zero));
                }
            }

            if (_possiblePositions.Count != 0)
            {
                // TODO: This is just here for the eye lerping.
                // Ideally engine would just spawn them on grid directly I guess? Right now grid traversal is handling it during
                // update which means we need to add a hack somewhere around it.
                var spawn = _robustRandom.Pick(_possiblePositions);
                var toMap = spawn.ToMap(EntityManager);

                if (_mapManager.TryFindGridAt(toMap, out var foundGrid))
                {
                    var gridXform = Transform(foundGrid.Owner);

                    return new EntityCoordinates(foundGrid.Owner,
                        gridXform.InvWorldMatrix.Transform(toMap.Position));
                }

                return spawn;
            }

            if (_mapManager.MapExists(DefaultMap))
            {
                return new EntityCoordinates(_mapManager.GetMapEntityId(DefaultMap), Vector2.Zero);
            }

            // Just pick a point at this point I guess.
            foreach (var map in _mapManager.GetAllMapIds())
            {
                var mapUid = _mapManager.GetMapEntityId(map);

                if (!metaQuery.TryGetComponent(mapUid, out var meta) ||
                    meta.EntityPaused)
                {
                    continue;
                }

                return new EntityCoordinates(mapUid, Vector2.Zero);
            }

            // AAAAAAAAAAAAA
            // This should be an error, if it didn't cause tests to start erroring when they delete a player.
            _sawmill.Warning("Found no observer spawn points!");
            return EntityCoordinates.Invalid;
        }
        #endregion
    }

    /// <summary>
    ///     Event raised broadcast before a player is spawned by the GameTicker.
    ///     You can use this event to spawn a player off-station on late-join but also at round start.
    ///     When this event is handled, the GameTicker will not perform its own player-spawning logic.
    /// </summary>
    [PublicAPI]
    public sealed class PlayerBeforeSpawnEvent : HandledEntityEventArgs
    {
        public IPlayerSession Player { get; }
        public HumanoidCharacterProfile Profile { get; }
        public string? JobId { get; }
        public bool LateJoin { get; }
        public EntityUid Station { get; }

        public PlayerBeforeSpawnEvent(IPlayerSession player, HumanoidCharacterProfile profile, string? jobId, bool lateJoin, EntityUid station)
        {
            Player = player;
            Profile = profile;
            JobId = jobId;
            LateJoin = lateJoin;
            Station = station;
        }
    }

    /// <summary>
    ///     Event raised both directed and broadcast when a player has been spawned by the GameTicker.
    ///     You can use this to handle people late-joining, or to handle people being spawned at round start.
    ///     Can be used to give random players a role, modify their equipment, etc.
    /// </summary>
    [PublicAPI]
    public sealed class PlayerSpawnCompleteEvent : EntityEventArgs
    {
        public EntityUid Mob { get; }
        public IPlayerSession Player { get; }
        public string? JobId { get; }
        public bool LateJoin { get; }
        public EntityUid Station { get; }
        public HumanoidCharacterProfile Profile { get; }

        // Ex. If this is the 27th person to join, this will be 27.
        public int JoinOrder { get; }

        public PlayerSpawnCompleteEvent(EntityUid mob, IPlayerSession player, string? jobId, bool lateJoin, int joinOrder, EntityUid station, HumanoidCharacterProfile profile)
        {
            Mob = mob;
            Player = player;
            JobId = jobId;
            LateJoin = lateJoin;
            Station = station;
            Profile = profile;
            JoinOrder = joinOrder;
        }
    }
}
