﻿using Robust.Shared.Noise;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype.Array;

namespace Content.Server.White.Worldgen.Prototypes;

/// <summary>
///     This is a config for noise channels, used by worldgen.
/// </summary>
[Virtual]
public class NoiseChannelConfig
{
    /// <summary>
    ///     The noise type used by the noise generator.
    /// </summary>
    [DataField("noiseType")]
    public FastNoise.NoiseType NoiseType { get; } = FastNoise.NoiseType.Cellular;

    /// <summary>
    ///     The fractal type used by the noise generator.
    /// </summary>
    [DataField("fractalType")]
    public FastNoise.FractalType FractalType { get; } = FastNoise.FractalType.Billow;

    /// <summary>
    ///     Multiplied by pi in code when used.
    /// </summary>
    [DataField("fractalLacunarityByPi")]
    public float FractalLacunarityByPi { get; } = 2.0f / 3.0f;

    /// <summary>
    ///     Ranges of values that get clamped down to the "clipped" value.
    /// </summary>
    [DataField("clippingRanges")]
    public List<Vector2> ClippingRanges { get; } = new();

    /// <summary>
    ///     The value clipped chunks are set to.
    /// </summary>
    [DataField("clippedValue")]
    public float ClippedValue { get; }

    /// <summary>
    ///     A value the output is multiplied by.
    /// </summary>
    [DataField("outputMultiplier")]
    public float OutputMultiplier { get; } = 1.0f;

    /// <summary>
    ///     A value the input is multiplied by.
    /// </summary>
    [DataField("inputMultiplier")]
    public float InputMultiplier { get; } = 1.0f;

    /// <summary>
    ///     Remaps the output of the noise function from the range (-1, 1) to (0, 1). This is done before all other output
    ///     transformations.
    /// </summary>
    [DataField("remapTo0Through1")]
    public bool RemapTo0Through1 { get; }

    /// <summary>
    ///     For when the transformation you need is too complex to describe in YAML.
    /// </summary>
    [DataField("noisePostProcess")]
    public NoisePostProcess? NoisePostProcess { get; }

    /// <summary>
    ///     For when you need a complex transformation of the input coordinates.
    /// </summary>
    [DataField("noiseCoordinateProcess")]
    public NoiseCoordinateProcess? NoiseCoordinateProcess { get; }

    /// <summary>
    ///     The "center" of the range of values. Or the minimum if mapped 0 through 1.
    /// </summary>
    [DataField("minimum")]
    public float Minimum { get; }
}

[Prototype("noiseChannel")]
public sealed class NoiseChannelPrototype : NoiseChannelConfig, IPrototype, IInheritingPrototype
{
    /// <inheritdoc />
    [ParentDataField(typeof(AbstractPrototypeIdArraySerializer<EntityPrototype>))]
    public string[]? Parents { get; }

    /// <inheritdoc />
    [NeverPushInheritance]
    [AbstractDataField]
    public bool Abstract { get; }

    /// <inheritdoc />
    [IdDataField]
    public string ID { get; } = default!;
}

/// <summary>
///     A wrapper around FastNoise's noise generation, using noise channel configs.
/// </summary>
public struct NoiseGenerator
{
    private readonly NoiseChannelConfig _config;
    private readonly FastNoise _noise;

    public NoiseGenerator(NoiseChannelConfig config, int seed)
    {
        _config = config;
        _noise = new FastNoise();
        _noise.SetSeed(seed);
        _noise.SetNoiseType(_config.NoiseType);
        _noise.SetFractalType(_config.FractalType);
        _noise.SetFractalLacunarity(_config.FractalLacunarityByPi * MathF.PI);
    }

    /// <summary>
    ///     Evaluates the noise generator at the provided coordinates.
    /// </summary>
    /// <param name="coords">Coordinates to use as input</param>
    /// <returns>Computed noise value</returns>
    public float Evaluate(Vector2 coords)
    {
        var finCoords = coords * _config.InputMultiplier;

        if (_config.NoiseCoordinateProcess is not null)
            finCoords = _config.NoiseCoordinateProcess.Process(finCoords);

        var value = _noise.GetNoise(finCoords.X, finCoords.Y);

        if (_config.RemapTo0Through1)
            value = (value + 1.0f) / 2.0f;

        foreach (var range in _config.ClippingRanges)
        {
            if (range.X < value && value < range.Y)
            {
                value = _config.ClippedValue;
                break;
            }
        }

        if (_config.NoisePostProcess is not null)
            value = _config.NoisePostProcess.Process(value);
        value *= _config.OutputMultiplier;
        return value + _config.Minimum;
    }
}

/// <summary>
///     A processing class that adjusts the input coordinate space to a noise channel.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract class NoiseCoordinateProcess
{
    public abstract Vector2 Process(Vector2 inp);
}

/// <summary>
///     A processing class that adjusts the final result of the noise channel.
/// </summary>
[ImplicitDataDefinitionForInheritors]
public abstract class NoisePostProcess
{
    public abstract float Process(float inp);
}
