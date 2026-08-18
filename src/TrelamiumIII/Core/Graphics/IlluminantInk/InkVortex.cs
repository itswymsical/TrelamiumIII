using TrelamiumIII.Core.Graphics.Metaballs.Layers;

namespace TrelamiumIII.Core.Graphics.IlluminantInk;

/// <summary>
///     Hosts <see cref="InkScene" /> in the world: an Illuminant Ink singularity opened over a point
///     in a level rather than behind the title screen.
/// </summary>
/// <remarks>
///     <para>
///         The world-side twin of <see cref="IlluminantInkScene" />, sharing the renderer and none of
///         the menu. Three things differ, and they are the three the renderer was generalised around.
///         <list type="number">
///             <item>
///                 <b>A radial mask instead of MagicPixel</b>, so the ink is a disc hanging in a place
///                 rather than a sky. <c>InkNebula.fx</c> multiplies its whole output by that mask,
///                 void included, so everything outside the disc leaves <c>RGB = 0</c>.
///             </item>
///             <item>
///                 <b>An additive composite instead of an opaque one.</b> A sky may replace the frame;
///                 a hole standing in a lit world may not. Additive also makes the masked-out region
///                 free rather than merely dark, and makes the draw order against the rest of the band
///                 irrelevant, since addition commutes.
///             </item>
///             <item>
///                 <b>Only the singularity layer.</b> The motes chase a cursor and the starfield fills
///                 the frame -- both are title-screen furniture. What survives is the infalling
///                 streaks, which are exactly what a vortex wants.
///             </item>
///         </list>
///     </para>
///     <para>
///         <b>The dark half is not drawn here.</b> Nothing additive can subtract light, so the ink
///         <em>mass</em> -- the actual black silhouette -- comes from <see cref="InkLayer" />, which is
///         alpha-blended in the same band. This host supplies the light: the horizon, the photon ring,
///         the accretion band, the sheen and the streaks. That is the same division of labour
///         <see cref="IInkLayer" /> already makes between <c>DrawInk</c> and <c>DrawLight</c>, applied
///         one level up.
///     </para>
///     <para>
///         <b>Why the frame is split across two hooks.</b> The render targets are bound in
///         <c>CheckMonoliths</c>, which is the one point in the frame where Terraria expects a mod to
///         be claiming a target; only the finished blit happens in <c>DrawDust</c>. Binding a target
///         from inside the scene draw would mean stealing one mid-scene. This is the same split
///         <c>MetaballSystem</c> uses between accumulating its field and compositing it.
///     </para>
/// </remarks>
internal static class InkVortex
{
    /// <summary>World position the vortex is centred on.</summary>
    public static Vector2 Center { get; set; }

    /// <summary>
    ///     0..1, how far the throat has wound up. Tightens the intake ring and speeds its spin.
    /// </summary>
    public static float Progress { get; set; }

    /// <summary>0..1 master opacity, so the vortex can be faded up and taken away.</summary>
    public static float Fade { get; set; }

    public static bool Active => Fade > 0f;

    /// <summary>
    ///     Diameter of the ink disc, in world pixels before zoom.
    /// </summary>
    /// <remarks>
    ///     This is the scale control for the whole composition, not just its extent. Everything the
    ///     nebula draws -- <see cref="InkStyle.Horizon" />, the photon ring, the accretion band -- is
    ///     expressed in half-height units of the quad it is rendered into, so widening the disc grows
    ///     the hole and its rings with it rather than revealing more ink around a fixed one.
    /// </remarks>
    private const float FIELD_DIAMETER = 620f;

    /// <summary>Radius the metaball intake ring starts from, in world pixels.</summary>
    private const float INTAKE_RADIUS = 165f;

    /// <summary>
    ///     Nodes fed into the ink field per tick at full fade.
    /// </summary>
    /// <remarks>
    ///     Fractional on purpose -- the remainder is carried between ticks rather than rounded, so a
    ///     faint vortex emits every few ticks instead of either every tick or never.
    /// </remarks>
    private const float INTAKE_RATE = 2.6f;

    /// <summary>
    ///     The world stack. See the class remarks for why it is only the one layer.
    /// </summary>
    private static readonly InkScene Scene = new(new SingularityLayer());

    private static float intakeCarry;
    private static float time;

    /// <summary>The finished surface and the frame it was built for, carried between the two hooks.</summary>
    private static RenderTarget2D? composed;
    private static InkFrame frame;

    public static void Open(Vector2 center)
    {
        Center = center;
        Progress = 0f;
        Fade = 0f;
        intakeCarry = 0f;
        time = 0f;
    }

    /// <summary>Shuts the vortex, drops the field it was feeding, and frees its surfaces.</summary>
    public static void Close()
    {
        Fade = 0f;
        Progress = 0f;
        intakeCarry = 0f;
        composed = null;

        InkLayer.Clear();
        Scene.Release();
    }

    /// <summary>
    ///     Advances the scene and feeds the ink field, once per tick.
    /// </summary>
    /// <remarks>
    ///     Emission belongs on the tick and not the frame. Feeding the field from a draw call would
    ///     tie how thick the vortex gets to frame rate -- the mistake the menu renderer was rewritten
    ///     to fix, where a 144 Hz monitor ran the scene at more than twice the speed a 60 Hz one did.
    ///     Here it would show as a vortex visibly denser on a fast machine.
    /// </remarks>
    private static void Tick()
    {
        if (!Active)
            return;

        time += 1f / 60f;

        // Ramps with fade as well as progress, so the intake thickens as the vortex arrives instead
        // of running at full throughput the instant it becomes visible.
        intakeCarry += INTAKE_RATE * Fade * MathHelper.Lerp(0.55f, 1f, Progress);

        while (intakeCarry >= 1f)
        {
            intakeCarry -= 1f;

            InkLayer.Vortex(Center, Progress, INTAKE_RADIUS, spin: MathHelper.Lerp(0.7f, 1.3f, Fade));
        }

        if (Scene.Ready)
            Scene.Update(in frame);

        Lighting.AddLight(Center, RimLight * Fade);
    }

    /// <summary>
    ///     Builds the scene into its target. Called while it is still legal to bind one.
    /// </summary>
    private static void Compose()
    {
        composed = null;

        if (!Active || Main.gameMenu)
            return;

        var device = Main.graphics.GraphicsDevice;

        if (!Scene.Ensure(device, Main.screenWidth, Main.screenHeight))
            return;

        frame = BuildFrame();

        composed = Scene.Render(
            Main.spriteBatch,
            device,
            in frame,
            // Glow, NOT RadialGradient. InkNebula.fx reads the mask from the ALPHA channel, and
            // RadialGradient encodes its falloff in RGB at a flat alpha of 255 -- which is why it is
            // the right stamp for a metaball field (GasFbm reads .r) and useless as a mask here. Using
            // it left alpha at 1 across the whole quad, so nothing was masked and the disc rendered as
            // a hard-edged rectangle. Any replacement mask has to carry its falloff in alpha.
            Assets.Textures.VFX.Glow.Asset.Value,
            DiscArea()
        );
    }

    /// <summary>Adds the finished surface over the world.</summary>
    private static void Blit()
    {
        if (composed is not { IsDisposed: false } result)
            return;

        var spriteBatch = Main.spriteBatch;

        // No transform matrix: the surface was rendered at screen-space positions already -- the zoom
        // is baked into where the disc was placed, not applied to the blit -- so it goes down 1:1.
        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Additive,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer
        );

        Scene.PrepareBloom(in frame).Apply();

        spriteBatch.Draw(result, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);

        spriteBatch.End();
    }

    private static InkFrame BuildFrame()
    {
        // Through the game view matrix rather than a bare subtraction, so the disc tracks the world
        // under zoom exactly like the entities around it. Main.GameViewMatrix carries the zoom pivot
        // as well as the scale, which a manual multiply would get wrong away from screen centre.
        Vector2 screen = Vector2.Transform(Center - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);

        // Cursor is passed as the singularity: nothing in the world stack reads it, and handing it a
        // real mouse position would only invite a layer to start using it.
        return new InkFrame(Scene.Width, Scene.Height, time, screen, screen, MathHelper.Clamp(Fade, 0f, 1f));
    }

    /// <summary>The square the ink field is rendered into, centred on the singularity.</summary>
    /// <remarks>
    ///     Square, so the nebula's aspect correction is the identity and its rings come out circular.
    ///     Tightens with <see cref="Progress" />, which pulls the horizon and both bands in together
    ///     and reads as the throat narrowing as it winds up.
    /// </remarks>
    private static Rectangle DiscArea()
    {
        float zoom = Main.GameViewMatrix.Zoom.X;
        float side = FIELD_DIAMETER * zoom * MathHelper.Lerp(1f, 0.78f, MathHelper.Clamp(Progress, 0f, 1f));

        return new Rectangle(
            (int)MathF.Round(frame.Singularity.X - side * 0.5f),
            (int)MathF.Round(frame.Singularity.Y - side * 0.5f),
            (int)MathF.Round(side),
            (int)MathF.Round(side)
        );
    }

    /// <summary>Cold violet, the rim colour <see cref="InkLayer" /> outlines its silhouette with.</summary>
    private static readonly Vector3 RimLight = new(0.35f, 0.34f, 0.47f);

    /// <summary>
    ///     Drives the vortex through the frame.
    /// </summary>
    /// <remarks>
    ///     <c>CheckMonoliths</c> builds the surface and <c>DrawDust</c> adds it, which puts the vortex
    ///     in front of tiles and entities and behind the interface -- the same band the ink field it
    ///     belongs to composites in. See the class remarks for why the two cannot be one hook.
    /// </remarks>
    internal sealed class Driver : ModSystem
    {
        public override void Load()
        {
            if (Main.dedServ)
                return;

            On_Main.CheckMonoliths += static orig =>
            {
                orig();

                Compose();
            };

            On_Main.DrawDust += static (orig, self) =>
            {
                orig(self);

                Blit();
            };
        }

        public override void PostUpdateEverything()
        {
            if (!Main.dedServ && !Main.gameMenu)
                Tick();
        }

        public override void OnWorldUnload()
        {
            // OnWorldUnload can run off the main thread, and FNA3D refuses to touch a graphics
            // resource from anywhere else, so the teardown is deferred a frame -- the same hazard
            // IlluminantInkScene.WorldEntryReleaser documents.
            Main.QueueMainThreadAction(static () => Close());
        }
    }
}
