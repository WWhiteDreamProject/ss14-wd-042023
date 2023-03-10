using Content.Shared.Damage;
using Content.Shared.Tools;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.Borgs;

[RegisterComponent]
public sealed class BorgRepairComponent : Component
{
    [ViewVariables(VVAccess.ReadWrite)] [DataField("qualityNeeded", customTypeSerializer:typeof(PrototypeIdSerializer<ToolQualityPrototype>))]
    public string QualityNeeded = "Welding";

    [ViewVariables(VVAccess.ReadWrite)] [DataField("selfRepairPenalty")]
    public float SelfRepairPenalty = 3f;

    [DataField("damage", required: true)]
    [ViewVariables(VVAccess.ReadWrite)]
    public DamageSpecifier Damage = default!;
}
