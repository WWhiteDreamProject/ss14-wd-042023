﻿using Content.Server.White.Worldgen.Systems;

namespace Content.Server.White.Worldgen.Components;

/// <summary>
///     This is used for marking an entity as being a world chunk.
/// </summary>
[RegisterComponent]
[Access(typeof(WorldControllerSystem))]
public sealed class WorldChunkComponent : Component
{
    /// <summary>
    ///     The coordinates of the chunk, in chunk space.
    /// </summary>
    [DataField("coordinates")] public Vector2i Coordinates;

    /// <summary>
    ///     The map this chunk belongs to.
    /// </summary>
    [DataField("map")] public EntityUid Map;
}
