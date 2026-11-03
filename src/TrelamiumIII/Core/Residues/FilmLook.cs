namespace TrelamiumIII.Core.Residues;

/// <summary>
///     Every look parameter of <c>ResidueFilmMask.fx</c>. Applied as a whole, so residues sharing the effect never
///     inherit each other's values.
/// </summary>
public sealed record FilmLook
{
    /// <summary>Eroded coverage at which the coat exists.</summary>
    public float Threshold { get; init; } = 0.2f;

    /// <summary>FBM cycles per tile.</summary>
    public float NoiseScale { get; init; } = 0.1f;

    public float NoiseStrength { get; init; } = 0.15f;

    /// <summary>FBM scroll in uv/sec. Positive y moves the pattern up.</summary>
    public Vector2 NoiseDrift { get; init; }

    /// <summary>Coverage past <see cref="Threshold" /> to a fully shaded core.</summary>
    public float DepthRange { get; init; } = 0.35f;

    public float Bands { get; init; } = 6f;

    public Vector3 ShadowColor { get; init; }

    public Vector3 BodyColor { get; init; }

    public Vector3 CoreColor { get; init; }

    public float BodyAlpha { get; init; } = 0.8f;

    /// <summary>Alpha of the thinnest band, as a fraction of <see cref="BodyAlpha" />.</summary>
    public float BodyAlphaFloor { get; init; } = 0.75f;

    public Vector3 OutlineColor { get; init; }

    public float OutlineAlpha { get; init; } = 0.9f;

    public Vector3 HighlightColor { get; init; }

    /// <summary>Opacity of the coat's top surface. Zero turns it off.</summary>
    public float HighlightAlpha { get; init; }

    /// <summary>How much more of the world light the rim and highlight catch, 0..1; they still go dark with the tile.</summary>
    public float EmissiveEdge { get; init; }

    /// <summary>Floor under the world light recovered from the canvas. Zero, so a coat is no more visible than its tile; a glowing coat lights itself with <see cref="Lighting.AddLight(Vector2, Vector3)"/> instead.</summary>
    public Vector3 AmbientLight { get; init; }

    /// <summary>World pixels the coat may stand proud of the terrain into open air.</summary>
    public float SurfaceLip { get; init; }

    /// <summary>
    ///     How far each tile's own colour re-dyes the palette, 0..1. Zero draws every tile as authored.
    /// </summary>
    /// <remarks>
    ///     The dye is the ratio of the tile's colour to <see cref="BodyColor" />, applied to every
    ///     colour here, so a tile carrying the body colour (which is what an untinted tile carries)
    ///     draws exactly as authored and the palette's shading survives any tint. A neutral grey body
    ///     makes the tint the whole colour; a coloured one makes the tint a shift away from it.
    /// </remarks>
    public float TintStrength { get; init; }

    /// <summary>How much of their own colour the rim and highlight keep under a tint, 0..1.</summary>
    public float TintWash { get; init; } = 0.5f;

    public void Apply(Effect effect)
    {
        effect.Parameters["Threshold"]?.SetValue(Threshold);
        effect.Parameters["NoiseScale"]?.SetValue(NoiseScale);
        effect.Parameters["NoiseStrength"]?.SetValue(NoiseStrength);
        effect.Parameters["NoiseDrift"]?.SetValue(NoiseDrift);
        effect.Parameters["DepthRange"]?.SetValue(DepthRange);
        effect.Parameters["Bands"]?.SetValue(Bands);

        effect.Parameters["ShadowColor"]?.SetValue(ShadowColor);
        effect.Parameters["BodyColor"]?.SetValue(BodyColor);
        effect.Parameters["CoreColor"]?.SetValue(CoreColor);
        effect.Parameters["BodyAlpha"]?.SetValue(BodyAlpha);
        effect.Parameters["BodyAlphaFloor"]?.SetValue(BodyAlphaFloor);

        effect.Parameters["OutlineColor"]?.SetValue(OutlineColor);
        effect.Parameters["OutlineAlpha"]?.SetValue(OutlineAlpha);
        effect.Parameters["HighlightColor"]?.SetValue(HighlightColor);
        effect.Parameters["HighlightAlpha"]?.SetValue(HighlightAlpha);

        effect.Parameters["EmissiveEdge"]?.SetValue(EmissiveEdge);
        effect.Parameters["AmbientLight"]?.SetValue(AmbientLight);
        effect.Parameters["SurfaceLip"]?.SetValue(SurfaceLip);

        effect.Parameters["TintStrength"]?.SetValue(TintStrength);
        effect.Parameters["TintWash"]?.SetValue(TintWash);
    }
}
