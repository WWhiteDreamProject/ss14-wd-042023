using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;

namespace Content.Server.White.Other.EnergySword;

public sealed class EnergyDoubleSwordCraftSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DoubleSwordCraftComponent, InteractUsingEvent>(Combine);
    }

    private const string NeededEnt = "EnergySword";

    private void Combine(EntityUid uid, DoubleSwordCraftComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var usedEnt = _entityManager.GetComponent<MetaDataComponent>(args.Used).EntityPrototype!.ID;

        if (usedEnt is not NeededEnt)
            return;

        SpawnEnergyDoubleSword(uid);
        _entityManager.DeleteEntity(args.Used);
        _entityManager.DeleteEntity(uid);
    }

    private void SpawnEnergyDoubleSword(EntityUid player)
    {
        var transform = CompOrNull<TransformComponent>(player)?.Coordinates;
        if (transform == null)
        {
            return;
        }

        var weaponEntity = _entityManager.SpawnEntity("EnergyDoubleSword", transform.Value);
        _handsSystem.PickupOrDrop(player, weaponEntity);
    }
}
