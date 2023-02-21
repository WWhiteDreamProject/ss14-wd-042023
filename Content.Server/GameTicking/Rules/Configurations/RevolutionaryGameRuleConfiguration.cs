using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.GameTicking.Rules.Configurations;

public sealed class RevolutionaryGameRuleConfiguration : GameRuleConfiguration
{
    public override string Id => "Revolution";

    [DataField("playersPerHeadRev")]
    public int PlayersPerHeadRev = 10; // todo transfer to proto

    [DataField("maxHeadRevs")]
    public int MaxHeadRev = 5; // todo transfer to proto

    [DataField("HeadRev")]
    public string HeadRevRolePrototype = "HeadRev";

    [DataField("greetingSound", customTypeSerializer: typeof(SoundSpecifierTypeSerializer))]
    public SoundSpecifier? GreetSound = new SoundPathSpecifier("/Audio/Misc/nukeops.ogg");
}
