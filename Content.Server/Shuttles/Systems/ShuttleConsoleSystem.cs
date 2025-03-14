using Content.Server.Cargo.Components;
using Content.Server.Cargo.Systems;
using Content.Server.Power.Components;
using Content.Server.Power.EntitySystems;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Events;
using Content.Server.UserInterface;
using Content.Shared.ActionBlocker;
using Content.Shared.Alert;
using Content.Shared.Cargo.Components;
using Content.Shared.Popups;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Events;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Tag;
using Robust.Server.GameObjects;
using Robust.Shared.Collections;
using Robust.Shared.GameStates;
using Robust.Shared.Map.Components;
using Robust.Shared.Physics.Components;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Server.Shuttles.Systems;

public sealed class ShuttleConsoleSystem : SharedShuttleConsoleSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly ActionBlockerSystem _blocker = default!;
    [Dependency] private readonly AlertsSystem _alertsSystem = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly ShuttleSystem _shuttle = default!;
    [Dependency] private readonly TagSystem _tags = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly RadarConsoleSystem _radarConsoleSystem = default!;
    [Dependency] private readonly CargoSystem _cargo = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ShuttleConsoleComponent, ComponentShutdown>(OnConsoleShutdown);
        SubscribeLocalEvent<ShuttleConsoleComponent, PowerChangedEvent>(OnConsolePowerChange);
        SubscribeLocalEvent<ShuttleConsoleComponent, AnchorStateChangedEvent>(OnConsoleAnchorChange);
        SubscribeLocalEvent<ShuttleConsoleComponent, ActivatableUIOpenAttemptEvent>(OnConsoleUIOpenAttempt);
        SubscribeLocalEvent<ShuttleConsoleComponent, ShuttleConsoleDestinationMessage>(OnDestinationMessage);
        SubscribeLocalEvent<ShuttleConsoleComponent, BoundUIClosedEvent>(OnConsoleUIClose);

        SubscribeLocalEvent<DockEvent>(OnDock);
        SubscribeLocalEvent<UndockEvent>(OnUndock);

        SubscribeLocalEvent<PilotComponent, MoveEvent>(HandlePilotMove);
        SubscribeLocalEvent<PilotComponent, ComponentGetState>(OnGetState);

        SubscribeLocalEvent<FTLDestinationComponent, ComponentStartup>(OnFtlDestStartup);
        SubscribeLocalEvent<FTLDestinationComponent, ComponentShutdown>(OnFtlDestShutdown);
    }

    private void OnFtlDestStartup(EntityUid uid, FTLDestinationComponent component, ComponentStartup args)
    {
        RefreshShuttleConsoles();
    }

    private void OnFtlDestShutdown(EntityUid uid, FTLDestinationComponent component, ComponentShutdown args)
    {
        RefreshShuttleConsoles();
    }

    private void OnDestinationMessage(EntityUid uid, ShuttleConsoleComponent component,
        ShuttleConsoleDestinationMessage args)
    {
        if (!TryComp<FTLDestinationComponent>(args.Destination, out var dest))
        {
            return;
        }

        if (!dest.Enabled)
            return;

        EntityUid? entity = uid;

        var getShuttleEv = new ConsoleShuttleEvent
        {
            Console = uid,
        };

        RaiseLocalEvent(entity.Value, ref getShuttleEv);
        entity = getShuttleEv.Console;

            if (!TryComp<TransformComponent>(entity, out var xform) ||
                !TryComp<ShuttleComponent>(xform.GridUid, out var shuttle))
            {
                return;
            }

            var gridXform = Transform(args.Destination);
            if (TryComp<CargoShuttleComponent>(xform.GridUid, out var cargoShuttle) &&
                gridXform.MapID == _cargo.CargoMap && _cargo.IsBlocked(cargoShuttle))
            {
                if (args.Session.AttachedEntity == null)
                    return;
                _popup.PopupEntity(Loc.GetString("cargo-shuttle-console-organics"), args.Session.AttachedEntity.Value, args.Session.AttachedEntity.Value);

                if (TryComp<CargoPilotConsoleComponent>(uid, out var cargoConsole))
                    _audio.PlayPvs(_audio.GetSound(cargoConsole.DenySound), args.Session.AttachedEntity.Value);
                return;
            }

        if (dest.Whitelist?.IsValid(entity.Value, EntityManager) == false &&
            dest.Whitelist?.IsValid(xform.GridUid.Value, EntityManager) == false)
        {
            return;
        }

        var shuttleUid = xform.GridUid.Value;

        if (HasComp<FTLComponent>(shuttleUid))
        {
            _popup.PopupCursor(Loc.GetString("shuttle-console-in-ftl"), args.Session);
            return;
        }

        if (!_shuttle.CanFTL(xform.GridUid, out var reason))
        {
            _popup.PopupCursor(reason, args.Session);
            return;
        }

        var dock = HasComp<MapComponent>(args.Destination) && HasComp<MapGridComponent>(args.Destination);
        var tagEv = new FTLTagEvent();
        RaiseLocalEvent(xform.GridUid.Value, ref tagEv);

        if (HasComp<CargoShuttleComponent>(shuttleUid))
        {
            _shuttle.FTLTravel(xform.GridUid.Value, shuttle, args.Destination, dock: true, priorityTag: "DockCargo");
        }
        else
        {
            _shuttle.FTLTravel(xform.GridUid.Value, shuttle, args.Destination, dock: dock, priorityTag: tagEv.Tag);
        }
    }

    private void OnDock(DockEvent ev)
    {
        RefreshShuttleConsoles();
    }

    private void OnUndock(UndockEvent ev)
    {
        RefreshShuttleConsoles();
    }

    public void RefreshShuttleConsoles(EntityUid uid)
    {
        // TODO: Should really call this per shuttle in some instances.
        RefreshShuttleConsoles();
    }

    /// <summary>
    /// Refreshes all of the data for shuttle consoles.
    /// </summary>
    public void RefreshShuttleConsoles()
    {
        var docks = GetAllDocks();
        var query = AllEntityQuery<ShuttleConsoleComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            UpdateState(uid, docks);
        }
    }

    /// <summary>
    /// Stop piloting if the window is closed.
    /// </summary>
    private void OnConsoleUIClose(EntityUid uid, ShuttleConsoleComponent component, BoundUIClosedEvent args)
    {
        if ((ShuttleConsoleUiKey) args.UiKey != ShuttleConsoleUiKey.Key ||
            args.Session.AttachedEntity is not { } user)
        {
            return;
        }

        // In case they D/C should still clean them up.
        foreach (var comp in EntityQuery<AutoDockComponent>(true))
        {
            comp.Requesters.Remove(user);
        }

        RemovePilot(user);
    }

    private void OnConsoleUIOpenAttempt(EntityUid uid, ShuttleConsoleComponent component,
        ActivatableUIOpenAttemptEvent args)
    {
        if (!TryPilot(args.User, uid))
            args.Cancel();
    }

    private void OnConsoleAnchorChange(EntityUid uid, ShuttleConsoleComponent component,
        ref AnchorStateChangedEvent args)
    {
        UpdateState(uid);
    }

    private void OnConsolePowerChange(EntityUid uid, ShuttleConsoleComponent component, ref PowerChangedEvent args)
    {
        UpdateState(uid);
    }

    private bool TryPilot(EntityUid user, EntityUid uid)
    {
        if (!_tags.HasTag(user, "CanPilot") ||
            !TryComp<ShuttleConsoleComponent>(uid, out var component) ||
            !this.IsPowered(uid, EntityManager) ||
            !Transform(uid).Anchored ||
            !_blocker.CanInteract(user, uid))
        {
            return false;
        }

        var pilotComponent = EnsureComp<PilotComponent>(user);
        var console = pilotComponent.Console;

        if (console != null)
        {
            RemovePilot(user, pilotComponent);

            if (console == component)
            {
                return false;
            }
        }

        AddPilot(user, component);
        return true;
    }

    private void OnGetState(EntityUid uid, PilotComponent component, ref ComponentGetState args)
    {
        args.State = new PilotComponentState(component.Console?.Owner);
    }

    /// <summary>
    /// Returns the position and angle of all dockingcomponents.
    /// </summary>
    private List<DockingInterfaceState> GetAllDocks()
    {
        // TODO: NEED TO MAKE SURE THIS UPDATES ON ANCHORING CHANGES!
        var result = new List<DockingInterfaceState>();
        var query = AllEntityQuery<DockingComponent, TransformComponent>();

        while (query.MoveNext(out var uid, out var comp, out var xform))
        {
            if (xform.ParentUid != xform.GridUid)
                continue;

            var state = new DockingInterfaceState()
            {
                Coordinates = xform.Coordinates,
                Angle = xform.LocalRotation,
                Entity = uid,
                Connected = comp.Docked,
                Color = comp.RadarColor,
                HighlightedColor = comp.HighlightedRadarColor,
            };
            result.Add(state);
        }

        return result;
    }

    private void UpdateState(EntityUid consoleUid, List<DockingInterfaceState>? docks = null)
    {
        EntityUid? entity = consoleUid;

        var getShuttleEv = new ConsoleShuttleEvent
        {
            Console = entity,
        };

        RaiseLocalEvent(entity.Value, ref getShuttleEv);
        entity = getShuttleEv.Console;

        TryComp<TransformComponent>(entity, out var consoleXform);
        TryComp<RadarConsoleComponent>(entity, out var radar);
        var range = radar?.MaxRange ?? SharedRadarConsoleSystem.DefaultMaxRange;

        var shuttleGridUid = consoleXform?.GridUid;

        var destinations = new List<(EntityUid, string, bool)>();
        var ftlState = FTLState.Available;
        var ftlTime = TimeSpan.Zero;

        if (TryComp<FTLComponent>(shuttleGridUid, out var shuttleFtl))
        {
            ftlState = shuttleFtl.State;
            ftlTime = _timing.CurTime + TimeSpan.FromSeconds(shuttleFtl.Accumulator);
        }

        // Mass too large
        if (entity != null && shuttleGridUid != null &&
            (!TryComp<PhysicsComponent>(shuttleGridUid, out var shuttleBody) || shuttleBody.Mass < 1000f))
        {
            var metaQuery = GetEntityQuery<MetaDataComponent>();

            // Can't go anywhere when in FTL.
            var locked = shuttleFtl != null || Paused(shuttleGridUid.Value);

            // Can't cache it because it may have a whitelist for the particular console.
            // Include paused as we still want to show CentCom.
            var destQuery = AllEntityQuery<FTLDestinationComponent>();

            while (destQuery.MoveNext(out var destUid, out var comp))
            {
                // Can't warp to itself or if it's not on the whitelist (console or shuttle).
                if (destUid == shuttleGridUid ||
                    comp.Whitelist?.IsValid(entity.Value) == false &&
                    (shuttleGridUid == null || comp.Whitelist?.IsValid(shuttleGridUid.Value, EntityManager) == false))
                {
                    continue;
                }

                var meta = metaQuery.GetComponent(destUid);
                var name = meta.EntityName;

                if (string.IsNullOrEmpty(name))
                    name = Loc.GetString("shuttle-console-unknown");

                var canTravel = !locked &&
                                comp.Enabled &&
                                (!TryComp<FTLComponent>(destUid, out var ftl) || ftl.State == FTLState.Cooldown);

                // Can't travel to same map (yet)
                if (canTravel && consoleXform?.MapUid == Transform(destUid).MapUid)
                {
                    canTravel = false;
                }

                destinations.Add((destUid, name, canTravel));
            }
        }

        docks ??= GetAllDocks();
        List<MobInterfaceState> mobs;
        List<ProjectilesInterfaceState> projectiles;
        List<CannonInformationInterfaceState> cannons;
        if (radar != null)
        {
            mobs = _radarConsoleSystem.GetMobsAround(radar);
            projectiles = _radarConsoleSystem.GetProjectilesAround(radar);
            cannons = _radarConsoleSystem.GetCannonInfosByMyGrid(radar);
        }
        else
        {
            mobs = new List<MobInterfaceState>();
            projectiles = new List<ProjectilesInterfaceState>();
            cannons = new List<CannonInformationInterfaceState>();
        }

        _ui.GetUiOrNull(consoleUid, ShuttleConsoleUiKey.Key)
            ?.SetState(new ShuttleConsoleBoundInterfaceState(
                ftlState,
                ftlTime,
                destinations,
                range,
                consoleXform?.Coordinates,
                consoleXform?.LocalRotation,
                docks,
                mobs,
                projectiles,
                cannons));
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        var toRemove = new ValueList<(EntityUid, PilotComponent)>();
        var query = EntityQueryEnumerator<PilotComponent>();

        while (query.MoveNext(out var uid, out var comp))
        {
            if (comp.Console == null)
                continue;

            if (!_blocker.CanInteract(uid, comp.Console.Owner))
            {
                toRemove.Add((uid, comp));
            }

            if(!_ui.IsUiOpen(uid, ShuttleConsoleUiKey.Key))
                continue;

            UpdateState(uid);
        }

        foreach (var (uid, comp) in toRemove)
        {
            RemovePilot(uid, comp);
        }
    }


    /// <summary>
    /// If pilot is moved then we'll stop them from piloting.
    /// </summary>
    private void HandlePilotMove(EntityUid uid, PilotComponent component, ref MoveEvent args)
    {
        if (component.Console == null || component.Position == null)
        {
            DebugTools.Assert(component.Position == null && component.Console == null);
            EntityManager.RemoveComponent<PilotComponent>(uid);
            return;
        }

        if (args.NewPosition.TryDistance(EntityManager, component.Position.Value, out var distance) &&
            distance < PilotComponent.BreakDistance)
        {
            return;
        }

        RemovePilot(uid, component);
    }

    protected override void HandlePilotShutdown(EntityUid uid, PilotComponent component, ComponentShutdown args)
    {
        base.HandlePilotShutdown(uid, component, args);
        RemovePilot(uid, component);
    }

    private void OnConsoleShutdown(EntityUid uid, ShuttleConsoleComponent component, ComponentShutdown args)
    {
        ClearPilots(component);
    }

    public void AddPilot(EntityUid entity, ShuttleConsoleComponent component)
    {
        if (!EntityManager.TryGetComponent(entity, out PilotComponent? pilotComponent) ||
            component.SubscribedPilots.Contains(pilotComponent))
        {
            return;
        }

        if (TryComp<SharedEyeComponent>(entity, out var eye))
        {
            eye.Zoom = component.Zoom;
        }

        component.SubscribedPilots.Add(pilotComponent);

        _alertsSystem.ShowAlert(entity, AlertType.PilotingShuttle);

        pilotComponent.Console = component;
        ActionBlockerSystem.UpdateCanMove(entity);
        pilotComponent.Position = EntityManager.GetComponent<TransformComponent>(entity).Coordinates;
        Dirty(pilotComponent);
    }

    public void RemovePilot(EntityUid pilotUid, PilotComponent pilotComponent)
    {
        var console = pilotComponent.Console;

        if (console is not ShuttleConsoleComponent helmsman)
            return;

        pilotComponent.Console = null;
        pilotComponent.Position = null;

        if (TryComp<SharedEyeComponent>(pilotUid, out var eye))
        {
            eye.Zoom = new(1.0f, 1.0f);
        }

        if (!helmsman.SubscribedPilots.Remove(pilotComponent))
            return;

        _alertsSystem.ClearAlert(pilotUid, AlertType.PilotingShuttle);

        _popup.PopupEntity(Loc.GetString("shuttle-pilot-end"), pilotUid, pilotUid);

        if (pilotComponent.LifeStage < ComponentLifeStage.Stopping)
            EntityManager.RemoveComponent<PilotComponent>(pilotUid);
    }

    public void RemovePilot(EntityUid entity)
    {
        if (!EntityManager.TryGetComponent(entity, out PilotComponent? pilotComponent))
            return;

        RemovePilot(entity, pilotComponent);
    }

    public void ClearPilots(ShuttleConsoleComponent component)
    {
        while (component.SubscribedPilots.TryGetValue(0, out var pilot))
        {
            RemovePilot(pilot.Owner, pilot);
        }
    }
}
