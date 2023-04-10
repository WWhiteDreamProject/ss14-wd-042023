using Content.Shared.Interaction;
using Content.Shared.Theta.ShipEvent;
using Content.Shared.Weapons.Ranged.Events;

namespace Content.Server.Theta.ShipEvent;

public sealed class CannonSystem : SharedCannonSystem
{
    [Dependency] private readonly RotateToFaceSystem _rotateToFaceSystem = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeNetworkEvent<RotateCannonsEvent>(OnRotateCannons);
        SubscribeLocalEvent<CannonComponent, AmmoShotEvent>(OnFire);
    }

    private void OnRotateCannons(RotateCannonsEvent ev)
    {
        foreach (var uid in ev.Cannons)
        {
            _rotateToFaceSystem.TryFaceCoordinates(uid, ev.Coordinates);
        }
    }

    private void OnFire(EntityUid entity, CannonComponent comp, AmmoShotEvent args)
    {
    }
}
