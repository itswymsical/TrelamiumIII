using ReLogic.Content;
using Terraria.GameContent;

namespace TrelamiumIII.Core.Rarities.Flairs;

/// <summary>
///     Binds an effect while the item sprite draws, in inventory slots and on the ground.
/// </summary>
/// <remarks>
///     <para>
///         The sprite itself is still drawn by the game (see <see cref="RarityFlair.SpriteShader" />
///         for why), so this flair only supplies the effect and its parameters.
///     </para>
///     <para>
///         The default parameters are the vocabulary the mod's existing <c>.fx</c> files already
///         speak -- <c>uTime</c>, <c>uOpacity</c>, <c>uColor</c> and the rest -- so a shader written
///         for anything else in the mod can be pointed at a rarity without being rewritten.
///         <see cref="Apply" /> replaces that set entirely for a shader that wants its own.
///     </para>
///     <para>
///         One caveat worth knowing before writing the shader: the batch binds the item's whole
///         texture, so <c>uv</c> spans the sheet rather than the frame. For animated items that means
///         a shader keying off <c>uv</c> sees the strip, not the visible cell.
///         <c>uSourceRect</c> carries the frame in pixels so a shader can correct for it.
///     </para>
/// </remarks>
public sealed class SpriteShaderFlair : RarityFlair
{
    /// <summary>Resolved lazily, so a static asset reference cannot lose a load-order race.</summary>
    public required Func<Asset<Effect>> Shader { get; init; }

    /// <summary>Replaces the default parameter set. Null keeps it.</summary>
    public Action<Effect, RaritySpriteContext>? Apply { get; init; }

    /// <summary>Fed to <c>uColor</c>. Null passes the style's own colour.</summary>
    public Color? Primary { get; init; }

    /// <summary>Fed to <c>uSecondaryColor</c>. Null passes the style's glow colour.</summary>
    public Color? Secondary { get; init; }

    /// <summary>Fed to <c>uIntensity</c>.</summary>
    public float Intensity { get; init; } = 1f;

    /// <summary>Multiplies <c>uTime</c>, for a shader whose animation runs too fast or too slow.</summary>
    public float TimeScale { get; init; } = 1f;

    public override Asset<Effect>? SpriteShader => Main.dedServ ? null : Shader();

    public override void ApplySpriteShader(Effect effect, in RaritySpriteContext ctx)
    {
        if (Apply is not null)
        {
            Apply(effect, ctx);
            return;
        }

        effect.Parameters["uTime"]?.SetValue(ctx.Time * TimeScale);
        effect.Parameters["uOpacity"]?.SetValue(ctx.DrawColor.A / 255f);
        effect.Parameters["uIntensity"]?.SetValue(Intensity);
        effect.Parameters["uColor"]?.SetValue((Primary ?? ctx.Style.Color).ToVector3());
        effect.Parameters["uSecondaryColor"]?.SetValue((Secondary ?? ctx.Style.GlowColor).ToVector3());
        effect.Parameters["uSaturation"]?.SetValue(1f);
        effect.Parameters["uZoom"]?.SetValue(ctx.InWorld ? Main.GameViewMatrix.Zoom : Vector2.One);
        effect.Parameters["uScreenResolution"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));

        ApplyFrameParameters(effect, ctx);
    }

    /// <summary>
    ///     Hands the shader the item's sheet size and the frame within it, for shaders that need to
    ///     turn a sheet-wide <c>uv</c> back into a frame-local one.
    /// </summary>
    private static void ApplyFrameParameters(Effect effect, in RaritySpriteContext ctx)
    {
        if (effect.Parameters["uSourceRect"] is null && effect.Parameters["uImageSize0"] is null)
            return;

        if (TextureAssets.Item[ctx.Item.type] is not { Value: { } sheet })
            return;

        Rectangle frame = Main.itemAnimations[ctx.Item.type] is { } animation
            ? animation.GetFrame(sheet)
            : sheet.Frame();

        effect.Parameters["uImageSize0"]?.SetValue(new Vector2(sheet.Width, sheet.Height));
        effect.Parameters["uSourceRect"]?.SetValue(new Vector4(frame.X, frame.Y, frame.Width, frame.Height));
    }
}
