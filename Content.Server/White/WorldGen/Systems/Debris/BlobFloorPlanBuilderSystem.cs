﻿using System.Linq;
using Content.Server.White.Worldgen.Components.Debris;
using Content.Shared.Maps;
using Robust.Shared.Map;
using Robust.Shared.Map.Components;
using Robust.Shared.Random;

namespace Content.Server.White.Worldgen.Systems.Debris;

/// <summary>
///     This handles building the floor plans for "blobby" debris.
/// </summary>
public sealed class BlobFloorPlanBuilderSystem : BaseWorldSystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ITileDefinitionManager _tileDefinition = default!;

    /// <inheritdoc />
    public override void Initialize()
    {
        SubscribeLocalEvent<BlobFloorPlanBuilderComponent, ComponentStartup>(OnBlobFloorPlanBuilderStartup);
    }

    private void OnBlobFloorPlanBuilderStartup(EntityUid uid, BlobFloorPlanBuilderComponent component,
        ComponentStartup args)
    {
        PlaceFloorplanTiles(component, Comp<MapGridComponent>(uid));
    }

    private void PlaceFloorplanTiles(BlobFloorPlanBuilderComponent comp, MapGridComponent grid)
    {
        // NO MORE THAN TWO ALLOCATIONS THANK YOU VERY MUCH.
        var spawnPoints = new HashSet<Vector2i>(comp.FloorPlacements * 6);
        var taken = new Dictionary<Vector2i, Tile>(comp.FloorPlacements * 5);

        void PlaceTile(Vector2i point)
        {
            // Assume we already know that the spawn point is safe.
            spawnPoints.Remove(point);
            var north = point.Offset(Direction.North);
            var south = point.Offset(Direction.South);
            var east = point.Offset(Direction.East);
            var west = point.Offset(Direction.West);
            var radsq = Math.Pow(comp.Radius,
                2); // I'd put this outside but i'm not 100% certain caching it between calls is a gain.

            // The math done is essentially a fancy way of comparing the distance from 0,0 to the radius,
            // and skipping the sqrt normally needed for dist.
            if (!taken.ContainsKey(north) && Math.Pow(north.X, 2) + Math.Pow(north.Y, 2) <= radsq)
                spawnPoints.Add(north);
            if (!taken.ContainsKey(south) && Math.Pow(south.X, 2) + Math.Pow(south.Y, 2) <= radsq)
                spawnPoints.Add(south);
            if (!taken.ContainsKey(east) && Math.Pow(east.X, 2) + Math.Pow(east.Y, 2) <= radsq)
                spawnPoints.Add(east);
            if (!taken.ContainsKey(west) && Math.Pow(west.X, 2) + Math.Pow(west.Y, 2) <= radsq)
                spawnPoints.Add(west);

            var tileDef = _tileDefinition[_random.Pick(comp.FloorTileset)];
            taken.Add(point, new Tile(tileDef.TileId, 0, _random.Pick(((ContentTileDefinition)tileDef).PlacementVariants)));
        }

        PlaceTile(Vector2i.Zero);

        for (var i = 0; i < comp.FloorPlacements; i++)
        {
            var point = _random.Pick(spawnPoints);
            PlaceTile(point);

            if (comp.BlobDrawProb > 0.0f)
            {
                if (!taken.ContainsKey(point.Offset(Direction.North)) && _random.Prob(comp.BlobDrawProb))
                    PlaceTile(point.Offset(Direction.North));
                if (!taken.ContainsKey(point.Offset(Direction.South)) && _random.Prob(comp.BlobDrawProb))
                    PlaceTile(point.Offset(Direction.South));
                if (!taken.ContainsKey(point.Offset(Direction.East)) && _random.Prob(comp.BlobDrawProb))
                    PlaceTile(point.Offset(Direction.East));
                if (!taken.ContainsKey(point.Offset(Direction.West)) && _random.Prob(comp.BlobDrawProb))
                    PlaceTile(point.Offset(Direction.West));
            }
        }

        grid.SetTiles(taken.Select(x => (x.Key, x.Value)).ToList());
    }
}
