using System.Linq;
using Content.Server.GameTicking.Presets;
using Content.Server.GameTicking.Rules.Configurations;
using Content.Shared.GameTicking;
using Content.Shared.Random;
using Content.Shared.Random.Helpers;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;

namespace Content.Server.GameTicking.Rules;

public sealed class SecretRuleSystem : GameRuleSystem
{
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly GameTicker _ticker = default!;

    public override string Prototype => "Secret";

    public override void Started()
    {
        PickRule();
    }

    public override void Ended()
    {
        // noop
        // Preset should already handle it.
        return;
    }

    private void PickRule()
    {
        // TODO: This doesn't consider what can't start due to minimum player count, but currently there's no way to know anyway.
        // as they use cvars.
        var preset = _prototypeManager.Index<WeightedRandomPrototype>("Secret");

        var readyPlayers = _ticker.PlayerGameStatuses.Values.ToArray().Count(status => status == PlayerGameStatus.ReadyToPlay);
        foreach (var pair in preset.Weights)
        {
            if (!_prototypeManager.TryIndex<GamePresetPrototype>(pair.Key, out var prototype))
            {
                Logger.Warning($"Couldn't find {pair.Key} game preset for secret game preset");
                preset.Weights.Remove(pair.Key);
                continue;
            }

            if (prototype.MinPlayers > readyPlayers)
                preset.Weights.Remove(pair.Key);

        }
        if (preset.Weights.Count == 0)
            preset.Weights.Add("Traitor", 1);
        var presetToPlay = preset.Pick(_random);
        Logger.InfoS("gamepreset", $"Selected {presetToPlay} for secret.");

        foreach (var rule in _prototypeManager.Index<GamePresetPrototype>(presetToPlay).Rules)
        {
            _ticker.StartGameRule(_prototypeManager.Index<GameRulePrototype>(rule));
        }
    }
}
