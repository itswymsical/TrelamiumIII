using ReLogic.Content;
using System.Collections.Generic;
using TrelamiumIII.Core.Graphics;

namespace TrelamiumIII.Core.Rarities.Fields;

/// <summary>
///     Custom drawing into a screen-sized target, composited at the text through an optional effect, for effects that read the whole
///     drawing as one texture: blurs, glows, masks. Contents are drawn from the previous frame's emitters. Text only.
/// </summary>
public sealed class RenderTargetFlairField : FlairField
{
    /// <summary>Draw frames without emitters before the target is freed.</summary>
    private const int IDLE_FRAMES_BEFORE_RELEASE = 120;

    private FieldRenderTarget? target;
    private Action<SpriteBatch>? drawContents;
    private int idleFrames;

    /// <summary>
    ///     Draws the contents from the previous frame's emitters. The batch blends with <see cref="ContentBlend" /> under the
    ///     emitters' own transform, so it takes their coordinates; <see cref="FieldEmitter.DrawText" /> draws the text itself.
    /// </summary>
    public required Action<SpriteBatch, IReadOnlyList<FieldEmitter>> DrawContents { get; init; }

    /// <summary>Composite effect. Null draws the target as it is.</summary>
    public Func<Asset<Effect>>? Shader { get; init; }

    /// <summary>Sets the composite effect's parameters, after uTime, uImageSize0 (target size) and uPixelScale.</summary>
    public Action<Effect>? ApplyParameters { get; init; }

    /// <summary>Screen pixels per target texel.</summary>
    public int PixelScale { get; init; } = 1;

    public BlendState ContentBlend { get; init; } = BlendState.AlphaBlend;

    public BlendState CompositeBlend { get; init; } = BlendState.AlphaBlend;

    public SamplerState CompositeSampler { get; init; } = SamplerState.PointClamp;

    public override FieldSurfaces Supported => FieldSurfaces.Text;

    protected override void Spawn(in FieldEmitter emitter) { }

    protected override void Update() { }

    protected internal override void Accumulate()
    {
        IReadOnlyList<FieldEmitter> emitters = PreviousFrameEmitters;

        if (emitters.Count == 0)
        {
            if (target is not null && ++idleFrames >= IDLE_FRAMES_BEFORE_RELEASE)
            {
                target.Free();
                target = null;
                idleFrames = 0;
            }

            return;
        }

        idleFrames = 0;
        target ??= new FieldRenderTarget(PixelScale);
        target.Build(emitters[0].Transform, ContentBlend, drawContents ??= spriteBatch => DrawContents(spriteBatch, PreviousFrameEmitters));
    }

    protected internal override void Draw(SpriteBatch spriteBatch, in FieldEmitter emitter)
    {
        if (emitter.Surface != FieldSurfaces.Text || !FirstDrawThisFrame() || target?.Current is not { } contents)
            return;

        Effect? effect = null;

        if (Shader is not null)
        {
            // Skipped while the shader loads; compositing the raw contents would flash them unprocessed.
            if (Shader() is not { Value: { } loaded })
                return;

            effect = loaded;
            effect.Parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
            effect.Parameters["uImageSize0"]?.SetValue(new Vector2(contents.Width, contents.Height));
            effect.Parameters["uPixelScale"]?.SetValue((float)target.PixelScale);
            ApplyParameters?.Invoke(effect);
        }

        SpriteBatchState home = SpriteBatchState.Capture(spriteBatch);

        RarityBatch.Restart(spriteBatch, new SpriteBatchState(SpriteSortMode.Immediate, CompositeBlend, CompositeSampler,
            DepthStencilState.None, RasterizerState.CullNone, effect, Matrix.Identity));

        spriteBatch.Draw(contents, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, target.PixelScale, SpriteEffects.None, 0f);

        RarityBatch.Restart(spriteBatch, home);
    }

    protected internal override void Release()
    {
        target?.Free();
        target = null;
    }
}
