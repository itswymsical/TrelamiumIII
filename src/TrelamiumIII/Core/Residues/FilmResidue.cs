using ReLogic.Content;

namespace TrelamiumIII.Core.Residues;

/// <summary>
///     A coat over the terrain -- slime, mud, a stain -- drawn through <c>ResidueFilmMask.fx</c>.
/// </summary>
/// <remarks>
///     <para>
///         The shortest path to a new residue. Rendering, the noise texture and every shader
///         parameter are handled here, so a subclass is a <see cref="Look" />, a
///         <see cref="Residue.Duration" />, and whatever it does to what touches it.
///     </para>
///     <para>
///         A look with <see cref="FilmLook.TintStrength" /> above zero is re-dyed per tile by
///         <see cref="ResidueInstance.Tint" />, so one residue can carry every colour of
///         slime. Untinted tiles carry the look's <see cref="FilmLook.BodyColor" /> onto the
///         canvas and so draw exactly as authored.
///     </para>
///     <para>
///         A subclass that overrides <see cref="Load" /> or <see cref="Unload" /> calls the base,
///         which holds the noise texture.
///     </para>
/// </remarks>
public abstract class FilmResidue : Residue
{
    /// <summary>The palette and shape of the coat. Applied whole, so it never inherits another residue's values.</summary>
    protected abstract FilmLook Look { get; }

    /// <summary>The FBM that erodes the coat's edge. Requested on clients only.</summary>
    protected virtual Asset<Texture2D> NoiseTexture => Assets.Textures.Noise.LiquidNoise.Asset;

    public override ResidueRenderMode RenderMode => ResidueRenderMode.Canvas;

    public override Asset<Effect>? Shader => Assets.Shaders.ResidueFilmMask.Asset;

    private Asset<Texture2D>? noise;

    public override void Load()
    {
        if (!Main.dedServ)
            noise = NoiseTexture;
    }

    public override void Unload() => noise = null;

    public override TileCanvasSample EncodeCanvas(in ResidueInstance instance)
        => Encode(in instance, instance.Strength);

    /// <summary>
    ///     A canvas sample at <paramref name="coverage" />, carrying <paramref name="color" /> -- or
    ///     the tile's own tint, or the body colour, in that order -- when the look reads one.
    /// </summary>
    /// <remarks>
    ///     The colour is scaled by coverage as the canvas requires, so the shader can divide it back
    ///     out as a local average and neighbouring tints blend where they meet.
    /// </remarks>
    protected TileCanvasSample Encode(in ResidueInstance instance, float coverage, Vector3? color = null)
    {
        if (Look.TintStrength <= 0f)
            return new TileCanvasSample(coverage);

        Vector3 tint = color ?? (instance.HasTint ? instance.Tint.ToVector3() : Look.BodyColor);

        return new TileCanvasSample(coverage, tint * coverage);
    }

    public override void ApplyShaderParameters(Effect effect)
    {
        if (noise is { Value: { } texture })
            effect.Parameters["uNoiseTex"]?.SetValue(texture);

        Look.Apply(effect);
    }

    /// <summary>The ground's noise is too broad to see on something three tiles tall, and a coat that thin needs its edge broken up more.</summary>
    public override void ApplyBodyParameters(Effect effect, float gravDir, float height)
    {
        ApplyShaderParameters(effect);

        effect.Parameters["NoiseScale"]?.SetValue(0.45f * BodyDetail(height));
        effect.Parameters["NoiseStrength"]?.SetValue(MathF.Max(Look.NoiseStrength, 0.5f));

        // Thin enough to still see what's wearing it.
        effect.Parameters["BodyAlpha"]?.SetValue(Look.BodyAlpha * 0.8f);
    }
}
