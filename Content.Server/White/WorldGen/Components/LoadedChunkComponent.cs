﻿using Content.Server.White.Worldgen.Systems;

namespace Content.Server.White.Worldgen.Components;

/// <summary>
///     This is used for marking a chunk as loaded.
/// </summary>
[RegisterComponent]
[Access(typeof(WorldControllerSystem))]
public sealed class LoadedChunkComponent : Component
{
    /// <summary>
    ///     The current list of entities loading this chunk.
    /// </summary>
    [ViewVariables] public List<EntityUid>? Loaders = null;
}
