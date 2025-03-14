﻿using Robust.Shared.GameStates;
using Robust.Shared.Serialization;
using Robust.Shared.Utility;

namespace Content.Server.GameTicking.Rules.Components;

[RegisterComponent, NetworkedComponent]
public sealed class RevolutionaryComponent : Component
{
    public static string LayerName = "RevHud";

    [DataField("HeadRevolutionary", required: true), ViewVariables(VVAccess.ReadWrite)]
    public bool HeadRevolutionary = true;
}

[Serializable, NetSerializable]
public sealed class RevolutionaryComponentState : ComponentState
{
    public bool HeadRevolutionary;

    public RevolutionaryComponentState(bool headrev)
    {
        HeadRevolutionary = headrev;
    }
}
