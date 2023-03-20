﻿using Content.Server.White.Worldgen.Loot.Components;
using Content.Server.Chemistry.Components.SolutionManager;
using Content.Server.Chemistry.EntitySystems;
using Robust.Shared.Random;

namespace Content.Server.White.Worldgen.Loot.System;

/// <summary>
/// This handles filling a solution container with random contents.
/// </summary>
public sealed class RandomReagentFillSystem : EntitySystem
{
    [Dependency] private readonly SolutionContainerSystem _solutionContainer = default!;
    [Dependency] private readonly IRobustRandom _random = default!;

    /// <inheritdoc/>
    public override void Initialize()
    {
        SubscribeLocalEvent<RandomReagentFillComponent, MapInitEvent>(OnStartup);
    }

    private void OnStartup(EntityUid uid, RandomReagentFillComponent component, MapInitEvent args)
    {
        if (!TryComp<SolutionContainerManagerComponent>(uid, out var container))
            return;

        if (!_solutionContainer.TryGetSolution(uid, component.TargetSolution, out var solution, container))
            return;

        component.CachedReagentSpawn.GetSpawns(_random, ref solution);
    }
}
