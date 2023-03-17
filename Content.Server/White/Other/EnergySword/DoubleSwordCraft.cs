using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Robust.Shared.Audio;

namespace Content.Server.White.Other.EnergySword;

public sealed class EnergyDoubleSwordCraftSystem : EntitySystem
{
    [Dependency] private readonly SharedHandsSystem _handsSystem = default!;
    [Dependency] private readonly IEntityManager _entityManager = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<DoubleSwordCraftComponent, InteractUsingEvent>(Combine);
    }

    private const string NeededEnt = "EnergySword";
    private const string EnergyDoubleSword = "EnergyDoubleSword";

    private void Combine(EntityUid uid, DoubleSwordCraftComponent component, InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var usedEnt = _entityManager.GetComponent<MetaDataComponent>(args.Used).EntityPrototype!.ID;
        var usedTo = _entityManager.GetComponent<MetaDataComponent>(uid).EntityPrototype!.ID;

        if (usedTo is EnergyDoubleSword or null)
        {
            _audio.PlayPvs("/Audio/White/Other/fail.ogg", uid, AudioParams.Default.WithVolume(-6f));
            return;
        }

        if (usedEnt is not NeededEnt or null)
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

        var weaponEntity = _entityManager.SpawnEntity(EnergyDoubleSword, transform.Value);
        _handsSystem.PickupOrDrop(player, weaponEntity);
    }
}
