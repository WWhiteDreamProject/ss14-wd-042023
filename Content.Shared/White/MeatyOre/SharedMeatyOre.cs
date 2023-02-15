using System.ComponentModel;
using Content.Shared.Actions;
using Robust.Shared.Serialization;

namespace Content.Shared.White.MeatyOre;

    [Serializable, NetSerializable]
    public sealed class MeatyOreShopRequestEvent : EntityEventArgs {}

[Serializable, NetSerializable]
public sealed class MeatyTraitorRequestActionEvent
{
    public override bool Equals(object? obj)
    {
        return true;
    }
}
