using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Ghost;
using Content.Server.Ghost.Components;
using Content.Server.Mind.Components;
using Content.Shared.Examine;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Interaction.Events;
using Robust.Shared.Map;
using Robust.Shared.Timing;

namespace Content.Server.Mind;

public sealed class MindSystem : EntitySystem
{
    [Dependency] private readonly IMapManager _mapManager = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;
    [Dependency] private readonly GhostSystem _ghostSystem = default!;
    [Dependency] private readonly ISharedAdminLogManager _adminLogger = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<MindComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<MindComponent, ExaminedEvent>(OnExamined);
        SubscribeLocalEvent<MindComponent, SuicideEvent>(OnSuicide);
    }

    public void SetGhostOnShutdown(EntityUid uid, bool value, MindComponent? mind = null)
    {
        if (!Resolve(uid, ref mind))
            return;

        mind.GhostOnShutdown = value;
    }

    public void LetsALogMindChange( EntityUid target) // for code structure supporting
    {
        _adminLogger.Add(LogType.GhostRoleTaken, LogImpact.Low, $"{ToPrettyString(target):'target'} transfer mind out");

    }

    /// <summary>
    ///     Don't call this unless you know what the hell you're doing.
    ///     Use <see cref="Mind.TransferTo(System.Nullable{Robust.Shared.GameObjects.EntityUid},bool)"/> instead.
    ///     If that doesn't cover it, make something to cover it.
    /// </summary>
    public void InternalAssignMind(EntityUid uid, Mind value, MindComponent? mind = null)
    {
        if (!Resolve(uid, ref mind))
            return;

        mind.Mind = value;
        RaiseLocalEvent(uid, new MindAddedMessage(), true);
    }

    /// <summary>
    ///     Don't call this unless you know what the hell you're doing.
    ///     Use <see cref="Mind.TransferTo(System.Nullable{Robust.Shared.GameObjects.EntityUid},bool)"/> instead.
    ///     If that doesn't cover it, make something to cover it.
    /// </summary>
    public void InternalEjectMind(EntityUid uid, MindComponent? mind = null)
    {
        if (!Resolve(uid, ref mind))
            return;

        if (!Deleted(uid))
            RaiseLocalEvent(uid, new MindRemovedMessage(), true);
        mind.Mind = null;
    }

    public void TransferMindProperties(MindComponent source, MindComponent target)
    {
        var props = typeof(MindComponent).GetProperties().ToList().Where(x => x.CanWrite);
        string[] includedProps = { "ShowExamineInfo", "GhostOnShutdown", "PreventGhosting" };

        foreach (var prop in props)
        {
            if (includedProps.Contains(prop.Name))
                prop.SetValue(target, prop.GetValue(source));
        }
    }

    private void OnShutdown(EntityUid uid, MindComponent mind, ComponentShutdown args)
    {
        // Let's not create ghosts if not in the middle of the round.
        if (_gameTicker.RunLevel != GameRunLevel.InRound)
            return;

        if (mind.HasMind)
        {
            if (mind.Mind?.VisitingEntity is {Valid: true} visiting)
            {
                if (TryComp(visiting, out GhostComponent? ghost))
                {
                    _ghostSystem.SetCanReturnToBody(ghost, false);
                }

                mind.Mind!.TransferTo(visiting);
            }
            else if (mind.GhostOnShutdown)
            {
                // Changing an entities parents while deleting is VERY sus. This WILL throw exceptions.
                // TODO: just find the applicable spawn position dirctly without actually updating the transform's parent.
                Transform(uid).AttachToGridOrMap();
                var spawnPosition = Transform(uid).Coordinates;

                // Use a regular timer here because the entity has probably been deleted.
                Timer.Spawn(0, () =>
                {
                    // Make extra sure the round didn't end between spawning the timer and it being executed.
                    if (_gameTicker.RunLevel != GameRunLevel.InRound)
                        return;

                    // Async this so that we don't throw if the grid we're on is being deleted.
                    var gridId = spawnPosition.GetGridUid(EntityManager);
                    if (!spawnPosition.IsValid(EntityManager) || gridId == EntityUid.Invalid || !_mapManager.GridExists(gridId))
                    {
                        spawnPosition = _gameTicker.GetObserverSpawnPoint();
                    }

                    // TODO refactor observer spawning.
                    // please.
                    if (!spawnPosition.IsValid(EntityManager))
                    {
                        // This should be an error, if it didn't cause tests to start erroring when they delete a player.
                        Logger.WarningS("mind", $"Entity \"{ToPrettyString(uid)}\" for {mind.Mind?.CharacterName} was deleted, and no applicable spawn location is available.");
                        mind.Mind?.TransferTo(null);
                        return;
                    }

                    var ghost = Spawn("MobObserver", spawnPosition);
                    var ghostComponent = Comp<GhostComponent>(ghost);
                    _ghostSystem.SetCanReturnToBody(ghostComponent, false);

                    // Log these to make sure they're not causing the GameTicker round restart bugs...
                    Logger.DebugS("mind", $"Entity \"{ToPrettyString(uid)}\" for {mind.Mind?.CharacterName} was deleted, spawned \"{ToPrettyString(ghost)}\".");

                    if (mind.Mind == null)
                        return;

                    var val = mind.Mind.CharacterName ?? string.Empty;
                    MetaData(ghost).EntityName = val;
                    mind.Mind.TransferTo(ghost);
                });
            }
        }
    }

    private void OnExamined(EntityUid uid, MindComponent mind, ExaminedEvent args)
    {
        if (!mind.ShowExamineInfo || !args.IsInDetailsRange)
        {
            return;
        }

        var dead = TryComp<MobStateComponent?>(uid, out var state) && _mobStateSystem.IsDead(uid, state);

        if (dead)
        {
            if (mind.Mind?.Session == null) {
                // Player has no session attached and dead
                args.PushMarkup($"[color=yellow]{Loc.GetString("mind-component-no-mind-and-dead-text", ("ent", uid))}[/color]");
            } else {
                // Player is dead with session
                args.PushMarkup($"[color=red]{Loc.GetString("comp-mind-examined-dead", ("ent", uid))}[/color]");
            }
        }
        else if (!mind.HasMind)
        {
            args.PushMarkup($"[color=mediumpurple]{Loc.GetString("comp-mind-examined-catatonic", ("ent", uid))}[/color]");
        }
        else if (mind.Mind?.Session == null)
        {
            args.PushMarkup($"[color=yellow]{Loc.GetString("comp-mind-examined-ssd", ("ent", uid))}[/color]");
        }
    }

    private void OnSuicide(EntityUid uid, MindComponent component, SuicideEvent args)
    {
        if (args.Handled)
            return;

        if (component.HasMind && component.Mind!.PreventSuicide)
        {
            args.BlockSuicideAttempt(true);
        }
    }
}
