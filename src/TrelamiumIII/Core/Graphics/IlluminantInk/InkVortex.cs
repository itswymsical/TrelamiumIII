using Terraria.GameContent;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;
using TrelamiumIII.Core.Graphics.ScreenEffects;

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
///                 <b>A reduced layer stack.</b> The motes chase a cursor that does not exist here.
///             </item>
///         </list>
///     </para>
///     <para>
///         <b>Two batches, at two scales.</b> <see cref="Scene" /> is the disc: the hole itself, sharp
///         and local. <see cref="Overlay" /> is the same renderer again over the whole frame at a
///         fraction of the opacity, so the ink reads as having got <em>into</em> the scene rather than
///         as an object sitting in front of it. The two share a singularity, which is what keeps them
///         registered instead of looking like a backdrop with something unrelated over it. Alongside
///         them <see cref="ScreenWobbleSystem" /> pulls the actual frame around -- the one part of this
///         that touches the world rather than drawing over it.
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
///         be claiming a target; only the finished blits happen in <c>DrawDust</c>. Binding a target
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

    /// <summary>
    ///     Opacity of the screen-level batch. Deliberately very low.
    /// </summary>
    /// <remarks>
    ///     Carried in through <see cref="InkFrame.Fade" /> rather than applied to the blit colour,
    ///     which matters under additive blending: <c>BlendState.Additive</c> is
    ///     <c>(SourceAlpha, One)</c>, so a colour tinted to this figure would land at its square once
    ///     the blend multiplied it by its own alpha again. Feeding the fade instead lets the nebula's
    ///     uOpacity and every layer's own fade apply it once, linearly.
    /// </remarks>
    private const float OVERLAY_OPACITY = 0.18f;

    /// <summary>Peak strength of the whole-frame FBM warp, and how fine that warp is.</summary>
    private const float WOBBLE_STRENGTH = 0.11f;

    private const float WOBBLE_SCALE = 4.2f;

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

    /// <summary>The disc: the hole itself. See the class remarks for why it is only the one layer.</summary>
    private static readonly InkScene Scene = new(new SingularityLayer());

    /// <summary>
    ///     The screen-level batch: the same renderer again, full frame, faint.
    /// </summary>
    /// <remarks>
    ///     A second <see cref="InkScene" /> rather than a second pass over the first, because the two
    ///     want opposite masks -- this one fills the frame the way the menu does and the other is a
    ///     disc -- and the mask is chosen per render, not per scene. The cost is one more target pair,
    ///     for the length of a cutscene.
    ///     <para>
    ///         Starfield only. The motes chase a cursor that does not exist in a world, and the
    ///         singularity dressing would double up on the disc's own halo and streaks; what is wanted
    ///         here is the ink ground and a suggestion of depth behind everything else.
    ///     </para>
    /// </remarks>
    private static readonly InkScene Overlay = new(new StarfieldLayer());

    private static float intakeCarry;
    private static float time;

    /// <summary>Finished surfaces and the frames they were built for, carried between the two hooks.</summary>
    private static RenderTarget2D? composed;
    private static InkFrame frame;

    private static RenderTarget2D? composedOverlay;
    private static InkFrame overlayFrame;

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
        composedOverlay = null;

        InkLayer.Clear();
        Scene.Release();
        Overlay.Release();

        // Stopped rather than cut: the warp is something the player is feeling, and snapping it to
        // zero on the frame the cutscene ends reads as a glitch. Stop ramps it out.
        ScreenWobbleSystem.Stop();
    }

    /// <summary>
    ///     Advances both scenes, feeds the ink field, and states the screen warp, once per tick.
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

        if (Overlay.Ready)
            Overlay.Update(in overlayFrame);

        // Restated every tick rather than fired once. ScreenWobbleSystem holds a level and chases it,
        // and documents itself as safe to drive this way, so re-stating the target keeps the warp on
        // the vortex's own envelope instead of giving it a second schedule that could disagree.
        ScreenWobbleSystem.Set(WOBBLE_STRENGTH * Fade, WOBBLE_SCALE, ticks: 8);

        Lighting.AddLight(Center, RimLight * Fade);
    }

    /// <summary>
    ///     Builds both scenes into their targets. Called while it is still legal to bind one.
    /// </summary>
    private static void Compose()
    {
        composed = null;
        composedOverlay = null;

        if (!Active || Main.gameMenu)
            return;

        var device = Main.graphics.GraphicsDevice;

        if (!Scene.Ensure(device, Main.screenWidth, Main.screenHeight))
            return;

        frame = BuildFrame(Scene, MathHelper.Clamp(Fade, 0f, 1f));

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

        if (!Overlay.Ensure(device, Main.screenWidth, Main.screenHeight))
            return;

        overlayFrame = BuildFrame(Overlay, OVERLAY_OPACITY * MathHelper.Clamp(Fade, 0f, 1f));

        // MagicPixel, so the ink fills the frame exactly as it does behind the title screen. The same
        // singularity as the disc, so the two swirl about one point and read as one effect at two
        // scales rather than as a backdrop with something unrelated in front of it.
        composedOverlay = Overlay.Render(
            Main.spriteBatch,
            device,
            in overlayFrame,
            TextureAssets.MagicPixel.Value,
            new Rectangle(0, 0, Overlay.Width, Overlay.Height)
        );
    }

    /// <summary>Adds both finished surfaces over the world.</summary>
    private static void Blit()
    {
        var spriteBatch = Main.spriteBatch;

        // Screen batch first, so it reads as sitting behind the disc. Both are additive, so this is
        // about intent rather than correctness -- addition commutes and neither can occlude the other.
        if (composedOverlay is { IsDisposed: false } wide)
            Add(spriteBatch, wide, Overlay, in overlayFrame);

        if (composed is { IsDisposed: false } disc)
            Add(spriteBatch, disc, Scene, in frame);
    }

    /// <summary>Adds one finished surface, through its own bloom pass.</summary>
    /// <remarks>
    ///     No transform matrix: the surface was rendered at screen-space positions already -- the zoom
    ///     is baked into where the content was placed, not applied to the blit -- so it goes down 1:1.
    /// </remarks>
    private static void Add(SpriteBatch spriteBatch, RenderTarget2D surface, InkScene scene, in InkFrame source)
    {
        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Additive,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer
        );

        scene.PrepareBloom(in source).Apply();

        spriteBatch.Draw(surface, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);

        spriteBatch.End();
    }

    /// <summary>
    ///     Builds a frame for one scene at a given opacity.
    /// </summary>
    /// <remarks>
    ///     Both batches share a singularity, resolved through the game view matrix rather than a bare
    ///     subtraction so it tracks the world under zoom exactly like the entities around it.
    ///     <see cref="Main.GameViewMatrix" /> carries the zoom pivot as well as the scale, which a
    ///     manual multiply would get wrong away from screen centre.
    ///     <para>
    ///         Cursor is passed as the singularity: nothing in either stack reads it, and handing it a
    ///         real mouse position would only invite a layer to start using it.
    ///     </para>
    /// </remarks>
    private static InkFrame BuildFrame(InkScene scene, float fade)
    {
        Vector2 screen = Vector2.Transform(Center - Main.screenPosition, Main.GameViewMatrix.TransformationMatrix);

        return new InkFrame(scene.Width, scene.Height, time, screen, screen, fade);
    }

    /// <summary>The square the disc's ink field is rendered into, centred on the singularity.</summary>
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
    ///     <c>CheckMonoliths</c> builds the surfaces and <c>DrawDust</c> adds them, which puts the
    ///     vortex in front of tiles and entities and behind the interface -- the same band the ink
    ///     field it belongs to composites in. See the class remarks for why the two cannot be one hook.
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
