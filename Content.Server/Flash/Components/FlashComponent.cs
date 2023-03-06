using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Server.Flash.Components
{
    [RegisterComponent, Access(typeof(FlashSystem))]
    public sealed class FlashComponent : Component
    {
        [DataField("duration")]
        [ViewVariables(VVAccess.ReadWrite)]
        public int FlashDuration { get; set; } = 5000;

        [DataField("uses")]
        [ViewVariables(VVAccess.ReadWrite)]
        public int Uses = 5;

        [DataField("range")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float Range { get; set; } = 7f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("aoeFlashDuration")]
        public int AoeFlashDuration { get; set; } = 2000;

        [DataField("slowTo")]
        [ViewVariables(VVAccess.ReadWrite)]
        public float SlowTo { get; set; } = 0.5f;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("sound")]
        public SoundSpecifier Sound { get; set; } = new SoundPathSpecifier("/Audio/Weapons/flash.ogg");


        /// <summary>
        /// Whether or not the flash automatically recharges over time.
        /// </summary>
        [DataField("autoRecharge"), ViewVariables(VVAccess.ReadWrite)]
        public bool AutoRecharge = false;

        /// <summary>
        /// The time it takes to regain a single charge
        /// </summary>
        [DataField("rechargeDuration"), ViewVariables(VVAccess.ReadWrite)]
        public TimeSpan RechargeDuration = TimeSpan.FromSeconds(120);

        /// <summary>
        /// The time when the next charge will be added
        /// </summary>
        [DataField("nextChargeTime", customTypeSerializer: typeof(TimeOffsetSerializer)), ViewVariables(VVAccess.ReadWrite)]
        public TimeSpan NextChargeTime = TimeSpan.FromSeconds(120);

        /// <summary>
        /// The maximum number of charges flash can have
        /// </summary>
        [DataField("maxCharges"), ViewVariables(VVAccess.ReadWrite)]
        public int MaxCharges = 5;

        public bool Flashing;

        public bool HasUses => Uses > 0;
    }
}
