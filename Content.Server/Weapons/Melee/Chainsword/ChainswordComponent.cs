using Content.Shared.Damage;
using Robust.Shared.Audio;

namespace Content.Server.Weapons.Melee.Chainsword
{
    [RegisterComponent, Access(typeof(ChainswordSystem))]
    public sealed class ChainswordComponent : Component
    {
        public bool Activated = false;

        [DataField("activeDamageBonus", required:true)]
        [ViewVariables(VVAccess.ReadWrite)]
        public DamageSpecifier ActiveDamageBonus = default!;

        [DataField("hitSound")]
        public SoundSpecifier HitSound { get; set; } = new SoundPathSpecifier("/Audio/Weapons/chainsword.ogg");

        [DataField("turnOnSound")]
        public SoundSpecifier TurnOnSound { get; set; } = new SoundPathSpecifier("/Audio/Weapons/chainswordon.ogg");

        [DataField("turnOffSound")]
        public SoundSpecifier TurnOffSound { get; set; } = new SoundPathSpecifier("/Audio/Weapons/chainswordoff.ogg");
    }
}
