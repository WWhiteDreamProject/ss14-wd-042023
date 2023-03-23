using Content.Server.Shuttles.Components;
using Robust.Shared.Audio;

namespace Content.Server.Cargo.Components;

/// <summary>
/// Lets you remotely control the cargo shuttle.
/// </summary>
[RegisterComponent]
public sealed class CargoPilotConsoleComponent : Component
{
    /// <summary>
    /// <see cref="ShuttleConsoleComponent"/> that we're proxied into.
    /// </summary>
    public EntityUid? Entity;


    [ViewVariables(VVAccess.ReadWrite), DataField("soundDeny")]
    public SoundSpecifier DenySound = new SoundPathSpecifier("/Audio/Effects/Cargo/buzz_two.ogg");
}
