using ReLogic.Content;
using System.Collections.Generic;
using Terraria.GameContent;
using TrelamiumIII.Core.Graphics;

namespace TrelamiumIII.Core.Rarities;

/// <summary>
///     A metaball field that lives in interface space, so a tooltip or an inventory slot can have gas
///     boiling off it.
/// </summary>
/// <remarks>
///     <para>
///         The world metaball system cannot do this. It composites in <c>DrawDust</c>, which is in
///         front of tiles and entities and <em>behind</em> the interface, so a field fed from a
///         tooltip would be drawn under the very panel it was supposed to be pouring out of. The
///         pipeline is otherwise the same idea -- stamp soft blobs additively into an off-screen
///         target, then cut the summed field at a threshold so neighbours fuse -- and it reuses the
///         same shader, the same stamp convention and the same half-resolution target.
///     </para>
///     <para>
///         The split across the frame is the part worth understanding. Binding a render target is
///         only safe at <c>CheckMonoliths</c>, before anything has claimed the backbuffer, and by
///         then nothing has yet told us where this frame's tooltip is. So the field is built from the
///         nodes that were spawned during the <em>previous</em> frame's draw, and the interface pass
///         only composites the result. One frame of lag on a drifting cloud is not observable; the
///         alternative is stealing a target out from under a half-drawn interface.
///     </para>
///     <para>
///         Coordinates are interface space -- the same numbers <see cref="DrawableTooltipLine.X" />
///         is in -- not raw screen pixels. The accumulate matrix folds
///         <see cref="Main.UIScaleMatrix" /> in, so a caller never converts.
///     </para>
/// </remarks>
public sealed class UiMetaballField
{
    /// <summary>
    ///     Screen pixels per field texel. The field is blurry data rather than an image, so it loses
    ///     nothing to the downscale, and the shader quantises its decisions to this grid: one field
    ///     texel becomes one chunky output pixel.
    /// </summary>
    private const int PIXEL_SCALE = 2;

    /// <summary>Frames of an empty pool before the target is handed back.</summary>
    /// <remarks>
    ///     Not immediate. A cloud that runs dry for a few frames as the cursor crosses a gap between
    ///     slots would otherwise churn a full-screen target on and off once per flicker.
    /// </remarks>
    private const int IDLE_FRAMES_BEFORE_RELEASE = 120;

    private struct Node
    {
        public Vector2 Position;
        public Vector2 Velocity;
        public float Radius;
        public float Life;
        public float MaxLife;
        public float Growth;

        public readonly float Alpha => MaxLife <= 0f ? 0f : MathHelper.Clamp(Life / MaxLife, 0f, 1f);
    }

    /// <summary>Every field in existence, so the frame hooks can drive them all from one place.</summary>
    internal static readonly List<UiMetaballField> Fields = [];

    private readonly List<Node> nodes = [];
    private readonly Func<Asset<Effect>> shaderSource;
    private readonly Func<Asset<Texture2D>> stampSource;

    private FieldTarget? target;
    private int idleFrames;

    /// <summary>Ceiling on live nodes; past it the oldest go, so a runaway emitter costs density.</summary>
    public int MaxNodes { get; init; } = 220;

    /// <summary>Upward acceleration per tick. Positive rises, negative sinks.</summary>
    public float Buoyancy { get; init; } = 0.06f;

    /// <summary>Per-tick velocity multiplier.</summary>
    public float Drag { get; init; } = 0.94f;

    /// <summary>Fills in everything the composite shader needs beyond the shared screen uniforms.</summary>
    public required Action<Effect> ApplyParameters { get; init; }

    public int NodeCount => nodes.Count;

    public UiMetaballField(Func<Asset<Effect>> shader, Func<Asset<Texture2D>> stamp)
    {
        shaderSource = shader;
        stampSource = stamp;

        if (!Main.dedServ)
            Fields.Add(this);
    }

    /// <summary>Adds a blob at <paramref name="uiPosition" />, in interface coordinates.</summary>
    public void Spawn(Vector2 uiPosition, Vector2 velocity, float radius, float life, float growth = 0f)
    {
        if (Main.dedServ || life <= 0f)
            return;

        int excess = nodes.Count - MaxNodes + 1;

        if (excess > 0)
            nodes.RemoveRange(0, Math.Min(excess, nodes.Count));

        nodes.Add(new Node
        {
            Position = uiPosition,
            Velocity = velocity,
            Radius = radius,
            Life = life,
            MaxLife = life,
            Growth = growth,
        });
    }

    public void Clear() => nodes.Clear();

    internal void Update()
    {
        for (int i = nodes.Count - 1; i >= 0; i--)
        {
            var node = nodes[i];

            node.Velocity.Y -= Buoyancy;
            node.Velocity *= Drag;
            node.Position += node.Velocity;
            node.Radius += node.Growth;
            node.Life--;

            if (node.Life <= 0f)
            {
                nodes.RemoveAt(i);
                continue;
            }

            nodes[i] = node;
        }
    }

    /// <summary>Sums this frame's blobs into the off-screen target.</summary>
    internal void Accumulate()
    {
        if (nodes.Count == 0 || Main.dedServ)
            return;

        target ??= new FieldTarget(this);
        target.Request();
        target.PrepareRenderTarget(Main.graphics.GraphicsDevice, Main.spriteBatch);
    }

    /// <summary>Stamps the blobs. Called by the target, inside its own bound pass.</summary>
    private void StampNodes(SpriteBatch spriteBatch)
    {
        if (stampSource() is not { Value: { } texture })
            return;

        Vector2 origin = texture.Size() * 0.5f;

        foreach (var node in nodes)
        {
            // Overdriven past white on purpose: the shader thresholds the summed field, and a node
            // that only just reaches 1.0 at its centre never fuses with its neighbours.
            //
            // Flat rather than Lit: an interface field is not standing anywhere in the world, so
            // there is no light to sample and the brightness channel is pinned open.
            spriteBatch.Draw(texture, node.Position, null, FieldStamp.Flat(node.Alpha * 1.25f), 0f,
                origin, node.Radius * 2f / texture.Width, SpriteEffects.None, 0f);
        }
    }

    /// <summary>
    ///     Composites the field over the interface, then hands the interface batch back untouched.
    /// </summary>
    /// <remarks>
    ///     The quad is full-screen and drawn in raw screen coordinates, so the batch is reopened
    ///     without the UI matrix; the shader discards everywhere the field is empty, which is what
    ///     keeps a screen-sized draw from being a screen-sized smear.
    /// </remarks>
    public void Composite(SpriteBatch spriteBatch)
    {
        if (nodes.Count == 0 || target is not { IsReady: true } || target.GetTarget() is not { } field)
            return;

        if (shaderSource() is not { Value: { } effect })
            return;

        ApplySharedParameters(effect);
        ApplyParameters(effect);

        spriteBatch.End();

        // PointClamp: the field is deliberately half-resolution and the shader snaps its decisions to
        // that grid. Filtering it back up would soften exactly the chunky edge we are after.
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp,
            DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        effect.CurrentTechnique.Passes[0].Apply();

        spriteBatch.Draw(field, Vector2.Zero, null, Color.White, 0f, Vector2.Zero, PIXEL_SCALE,
            SpriteEffects.None, 0f);

        RarityBatch.RestoreInterface(spriteBatch);
    }

    /// <summary>
    ///     The screen uniforms every interface field sets the same way.
    /// </summary>
    /// <remarks>
    ///     <c>Zoom</c> at one and <c>WorldOffset</c> at zero are what turn the shader's world-locked
    ///     noise into screen-locked noise. That is the correct answer here rather than a shortcut:
    ///     the camera has nothing to do with a tooltip, and noise that tracked the world would slide
    ///     across the panel every time the player walked.
    /// </remarks>
    private static void ApplySharedParameters(Effect effect)
    {
        effect.Parameters["Time"]?.SetValue((float)Main.timeForVisualEffects / 60f);
        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(PIXEL_SCALE / (float)Main.screenWidth, PIXEL_SCALE / (float)Main.screenHeight));
        effect.Parameters["Aspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);
        effect.Parameters["Zoom"]?.SetValue(Vector2.One);
        effect.Parameters["WorldOffset"]?.SetValue(Vector2.Zero);

        // Interface elements are not lit by the world, so the brightness the field carries is left
        // fully open rather than floored against a room the item is not standing in.
        effect.Parameters["WorldLight"]?.SetValue(Vector3.One);
    }

    internal void ReleaseIfIdle()
    {
        if (nodes.Count > 0)
        {
            idleFrames = 0;
            return;
        }

        if (target is null || ++idleFrames < IDLE_FRAMES_BEFORE_RELEASE)
            return;

        target.Reset();
        target = null;
        idleFrames = 0;
    }

    internal void Release()
    {
        target?.Reset();
        target = null;
        nodes.Clear();
    }

    /// <summary>The off-screen surface one field sums its blobs into.</summary>
    private sealed class FieldTarget(UiMetaballField field) : ARenderTargetContentByRequest
    {
        protected override void HandleUseReqest(GraphicsDevice device, SpriteBatch spriteBatch)
        {
            // Ceiling: an odd screen width would otherwise drop the last column and leave a seam
            // down the edge of the composite.
            int width = (int)Math.Ceiling(Main.screenWidth / (float)PIXEL_SCALE);
            int height = (int)Math.Ceiling(Main.screenHeight / (float)PIXEL_SCALE);

            PrepareARenderTarget_AndListenToEvents(ref _target, device, width, height, RenderTargetUsage.PreserveContents);

            device.SetRenderTarget(_target);
            device.Clear(Color.Transparent);

            // Nodes are in interface coordinates, so the UI matrix comes along, scaled down into the
            // half-resolution target. Skipping it would leave the cloud correct at 100% UI scale and
            // increasingly displaced at every other setting.
            Matrix transform = Main.UIScaleMatrix * Matrix.CreateScale(1f / PIXEL_SCALE, 1f / PIXEL_SCALE, 1f);

            // Additive is the point: overlapping blobs have to sum for neighbours to fuse.
            spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp,
                DepthStencilState.None, RasterizerState.CullNone, null, transform);

            field.StampNodes(spriteBatch);

            spriteBatch.End();

            device.SetRenderTarget(null);
            _wasPrepared = true;
        }
    }

}

/// <summary>
///     Ticks every <see cref="UiMetaballField" /> and builds its target at the one point in the frame
///     where binding one is safe.
/// </summary>
/// <remarks>
///     Deliberately a separate hook from the world metaball system's rather than a shared one: the
///     two have no ordering relationship, and a field that exists only while a tooltip is open should
///     not be able to take the cloud on a boss down with it.
/// </remarks>
public sealed class UiMetaballDriver : ModSystem
{
    private static bool loaded;

    public override void OnModLoad()
    {
        if (Main.dedServ)
            return;

        loaded = true;

        On_Main.CheckMonoliths += static orig =>
        {
            orig();

            if (!loaded || Main.gameMenu)
                return;

            foreach (var field in UiMetaballField.Fields)
                field.Accumulate();
        };
    }

    public override void OnModUnload()
    {
        loaded = false;

        foreach (var field in UiMetaballField.Fields)
            field.Release();

        UiMetaballField.Fields.Clear();
    }

    public override void PostUpdateEverything()
    {
        if (Main.dedServ)
            return;

        foreach (var field in UiMetaballField.Fields)
        {
            field.Update();
            field.ReleaseIfIdle();
        }
    }
}
