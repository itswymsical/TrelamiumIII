namespace TrelamiumIII.Core.Rarities.Text;

/// <summary>Per-glyph animation for text painters: a travelling wave, a jitter and a scale pulse. Each is off at zero.</summary>
public sealed record GlyphMotion
{
    /// <summary>Vertical wave height in pixels.</summary>
    public float WaveAmplitude { get; init; }

    /// <summary>Radians per second.</summary>
    public float WaveSpeed { get; init; } = 4f;

    /// <summary>Radians of wave phase between neighbouring characters.</summary>
    public float WavePhasePerGlyph { get; init; } = 0.4f;

    /// <summary>Jitter distance in pixels, per axis.</summary>
    public float ShakeAmplitude { get; init; }

    /// <summary>Seconds each jitter offset holds before re-rolling.</summary>
    public float ShakeInterval { get; init; } = 0.05f;

    /// <summary>Scale swing around 1; 0.1 pulses each glyph between 0.9 and 1.1.</summary>
    public float PulseAmplitude { get; init; }

    /// <summary>Radians per second.</summary>
    public float PulseSpeed { get; init; } = 3f;

    /// <summary>Radians of pulse phase between neighbouring characters.</summary>
    public float PulsePhasePerGlyph { get; init; } = 0.5f;

    /// <summary>Applies the motion for the character at <paramref name="index" /> at <paramref name="time" /> seconds.</summary>
    public void Apply(ref GlyphDraw draw, int index, float time)
    {
        if (WaveAmplitude != 0f)
            draw.Offset.Y += MathF.Sin(time * WaveSpeed + index * WavePhasePerGlyph) * WaveAmplitude;

        if (ShakeAmplitude != 0f && ShakeInterval > 0f)
        {
            int step = (int)MathF.Floor(time / ShakeInterval);

            draw.Offset += new Vector2(Noise(index, step, 0), Noise(index, step, 1)) * ShakeAmplitude;
        }

        if (PulseAmplitude != 0f)
            draw.Scale *= 1f + MathF.Sin(time * PulseSpeed + index * PulsePhasePerGlyph) * PulseAmplitude;
    }

    /// <summary>Hash of the inputs mapped to -1..1, so a jitter holds for its interval without stored state.</summary>
    private static float Noise(int index, int step, int axis)
    {
        uint hash = (uint)index * 0x9E3779B1u ^ (uint)step * 0x85EBCA77u ^ (uint)axis * 0xC2B2AE3Du;

        hash ^= hash >> 15;
        hash *= 0x2C1B3C6Du;
        hash ^= hash >> 12;

        return hash / (float)uint.MaxValue * 2f - 1f;
    }
}
