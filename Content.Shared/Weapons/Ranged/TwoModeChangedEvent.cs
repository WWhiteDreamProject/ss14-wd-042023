using Robust.Shared.Serialization;

namespace Content.Shared.Weapons.Ranged;

[Serializable, NetSerializable]
public sealed class TwoModeChangedEvent : EntityEventArgs
{
    public EntityUid? Weapon;

    public TwoModeChangedEvent(EntityUid? weapon)
    {
        Weapon = weapon;
    }
}
