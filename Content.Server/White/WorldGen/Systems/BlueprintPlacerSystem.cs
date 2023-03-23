using Content.Server.White.Worldgen.Components.GrabliPlacer;
using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Shared.Serialization.Manager;

namespace Content.Server.White.Worldgen.Systems;

/// <summary>
/// This handles placing blueprints into the world via markers.
/// </summary>
public sealed class BlueprintPlacerSystem : EntitySystem
{
    [Dependency] private readonly MapLoaderSystem _mapLoader = default!;
    [Dependency] private readonly TransformSystem _transform = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly ISerializationManager _serialization = default!;
    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<BlueprintPlacerComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(EntityUid uid, BlueprintPlacerComponent component, MapInitEvent args)
    {
        var xform = Transform(uid);
        var options = new MapLoadOptions
        {
            LoadMap = false,
            Offset = _transform.GetWorldPosition(uid),
            Rotation = xform.LocalRotation
        };

        _mapLoader.TryLoad(xform.MapID, component.Blueprint.ToString(), out var root, options);

        if (root is null)
            return;

        component.Apply(root[0], _serialization, EntityManager, _componentFactory);
    }
}
