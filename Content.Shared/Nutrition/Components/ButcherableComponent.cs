using Content.Shared.Storage;
using Robust.Shared.GameStates;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.Nutrition.Components
{
    /// <summary>
    /// Indicates that the entity can be thrown on a kitchen spike for butchering.
    /// </summary>
    [RegisterComponent, NetworkedComponent]
    public sealed class ButcherableComponent : Component
    {
        [DataField("spawned", required: true)]
        public List<EntitySpawnEntry> SpawnedEntities = new();

        [ViewVariables(VVAccess.ReadWrite), DataField("butcherDelay")]
        public float ButcherDelay = 8.0f;

        [DataField("endbuthceringTime", customTypeSerializer: typeof(TimeOffsetSerializer))]
        public TimeSpan ButhcerEndTime = TimeSpan.Zero;

        [ViewVariables(VVAccess.ReadWrite), DataField("butcheringType")]
        public ButcheringType Type = ButcheringType.Knife;

        [DataField("butcheringSound")]
        public SoundSpecifier? ButcheringSound;

        [ViewVariables(VVAccess.ReadWrite)]
        [DataField("buthceringSoundDelay", customTypeSerializer: typeof(TimeOffsetSerializer))]
        public TimeSpan ButhceringSoundDelay = TimeSpan.Zero;


        /// <summary>
        /// Prevents butchering same entity on two and more spikes simultaneously and multiple doAfters on the same Spike
        /// </summary>
        [ViewVariables]
        public bool BeingButchered;
    }

    public enum ButcheringType : byte
    {
        Knife, // e.g. goliaths
        Spike, // e.g. monkeys
        Gibber // e.g. humans. TODO
    }
}
