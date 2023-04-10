using System.Linq;
using Content.Server.MachineLinking.Components;
using Content.Server.Mind.Components;
using Content.Server.Theta.ShipEvent.Console;
using Content.Server.UserInterface;
using Content.Shared.Mobs.Components;
using Content.Shared.Mobs.Systems;
using Content.Shared.Projectiles;
using Content.Shared.Shuttles.BUIStates;
using Content.Shared.Shuttles.Components;
using Content.Shared.Shuttles.Systems;
using Content.Shared.Theta.ShipEvent;
using Content.Shared.Weapons.Ranged.Events;
using Robust.Server.GameObjects;
using Robust.Shared.Map;

namespace Content.Server.Shuttles.Systems;

public sealed class RadarConsoleSystem : SharedRadarConsoleSystem
{
    [Dependency] private readonly UserInterfaceSystem _uiSystem = default!;
    [Dependency] private readonly TransformSystem _transformSystem = default!;
    [Dependency] private readonly MobStateSystem _mobStateSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<RadarConsoleComponent, ComponentStartup>(OnRadarStartup);
    }

    private void OnRadarStartup(EntityUid uid, RadarConsoleComponent component, ComponentStartup args)
    {
        UpdateState(component);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        foreach (var radar in EntityManager.EntityQuery<RadarConsoleComponent>())
        {
            if (!_uiSystem.IsUiOpen(radar.Owner, RadarConsoleUiKey.Key))
                continue;
            UpdateState(radar);
        }
    }

    public List<MobInterfaceState> GetMobsAround(RadarConsoleComponent component)
    {
        var list = new List<MobInterfaceState>();

        if (!TryComp<TransformComponent>(component.Owner, out var xform))
            return list;

        foreach (var (_, mobState, transform) in EntityManager
                     .EntityQuery<MindComponent, MobStateComponent, TransformComponent>())
        {
            if (_mobStateSystem.IsIncapacitated(mobState.Owner, mobState))
                continue;
            if (!xform.MapPosition.InRange(transform.MapPosition, component.MaxRange))
                continue;

            list.Add(new MobInterfaceState
            {
                Coordinates = transform.Coordinates,
            });
        }

        return list;
    }

    public List<ProjectilesInterfaceState> GetProjectilesAround(RadarConsoleComponent component)
    {
        var list = new List<ProjectilesInterfaceState>();

        if (!TryComp<TransformComponent>(component.Owner, out var xform))
            return list;

        foreach (var (_, transform) in EntityManager.EntityQuery<ProjectileComponent, TransformComponent>())
        {
            if (!xform.MapPosition.InRange(transform.MapPosition, component.MaxRange))
                continue;

            list.Add(new ProjectilesInterfaceState()
            {
                Coordinates = transform.Coordinates,
                Angle = _transformSystem.GetWorldRotation(xform),
            });
        }

        return list;
    }

    public List<CannonInformationInterfaceState> GetCannonInfosByMyGrid(RadarConsoleComponent component)
    {
        var list = new List<CannonInformationInterfaceState>();

        var myGrid = Transform(component.Owner).GridUid;
        var isCannonConsole = HasComp<CannonConsoleComponent>(component.Owner);

        var controlledCannons = GetControlledCannons(component.Owner);

        foreach (var (cannon, transform) in EntityQuery<CannonComponent, TransformComponent>())
        {
            if (transform.GridUid != myGrid)
                continue;
            if (!transform.Anchored)
                continue;

            var controlled = false;
            if (controlledCannons != null)
            {
                controlled = controlledCannons.Contains(cannon.Owner);
            }

            var color = controlled ? Color.Lime : (isCannonConsole ? Color.LightGreen : Color.YellowGreen);

            var ammoCountEv = new GetAmmoCountEvent();
            RaiseLocalEvent(cannon.Owner, ref ammoCountEv);

            var maxCapacity = cannon.BoundLoader?.MaxContainerCapacity != null ? cannon.BoundLoader.MaxContainerCapacity : 0;
            var usedCapacity = cannon.BoundLoader?.CurrentContainerCapacity != null ? cannon.BoundLoader.CurrentContainerCapacity : 0;

            list.Add(new CannonInformationInterfaceState
            {
                Uid = cannon.Owner,
                Coordinates = transform.Coordinates,
                Color = color,
                Angle = _transformSystem.GetWorldRotation(transform),
                IsControlling = controlled,
                Ammo = ammoCountEv.Count,
                UsedCapacity = usedCapacity,
                MaxCapacity = maxCapacity
            });
        }

        return list;
    }

    private List<EntityUid>? GetControlledCannons(EntityUid uid)
    {
        List<EntityUid>? controlledCannons = null;
        var hasSignalTransmitter = TryComp<SignalTransmitterComponent>(uid, out var signalTransmitter);
        if (!hasSignalTransmitter || signalTransmitter == null)
            return controlledCannons;

        controlledCannons = new List<EntityUid>();
        foreach (var (_, cannons) in signalTransmitter.Outputs)
        {
            controlledCannons.AddRange(cannons.Select(i => i.Uid));
        }

        return controlledCannons;
    }

    protected override void UpdateState(RadarConsoleComponent component)
    {
        var xform = Transform(component.Owner);
        var onGrid = xform.ParentUid == xform.GridUid;
        Angle? angle = onGrid ? xform.LocalRotation : Angle.Zero;
        // find correct grid
        while (!onGrid && !xform.ParentUid.IsValid())
        {
            xform = Transform(xform.ParentUid);
            angle = Angle.Zero;
            onGrid = xform.ParentUid == xform.GridUid;
        }

        EntityCoordinates? coordinates = onGrid ? xform.Coordinates : null;

        // Use ourself I guess.
        if (TryComp<IntrinsicUIComponent>(component.Owner, out var intrinsic))
        {
            foreach (var uiKey in intrinsic.UIs)
            {
                if (uiKey.Key?.Equals(RadarConsoleUiKey.Key) == true)
                {
                    coordinates = new EntityCoordinates(component.Owner, Vector2.Zero);
                    angle = Angle.Zero;
                    break;
                }
            }
        }

        var mobs = GetMobsAround(component);
        var projectiles = GetProjectilesAround(component);
        var cannons = GetCannonInfosByMyGrid(component);

        var radarState = new RadarConsoleBoundInterfaceState(
            component.MaxRange,
            coordinates,
            angle,
            new List<DockingInterfaceState>(),
            mobs,
            projectiles,
            cannons
        );

        _uiSystem.TrySetUiState(component.Owner, RadarConsoleUiKey.Key, radarState);
    }
}
