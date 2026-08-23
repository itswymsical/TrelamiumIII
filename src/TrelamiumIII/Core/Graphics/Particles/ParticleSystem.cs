using ReLogic.Content;
using System.Collections.Generic;
using TrelamiumIII.Common.Configs;

namespace TrelamiumIII.Core.Graphics.Particles;

/// <summary>
///     Owns every live <see cref="Particle" />, and the texture registry their variants resolve
///     through.
/// </summary>
/// <remarks>
///     Particles are client-side decoration and are never simulated on a server. They composite in
///     <c>DrawDust</c>, which puts them in front of tiles, NPCs and projectiles.
///     Uses the same hook the metaball layers use, so debris and the spore cloud share a depth band.
/// </remarks>
public sealed class ParticleSystem : ModSystem
{
    /// <summary>Pass as <c>variant</c> to <see cref="Spawn{T}" /> to pick one at random.</summary>
    public const int RANDOM_VARIANT = -1;
    /// <summary>
    ///     Past this, the oldest particles are dropped. A burst that overruns the budget costs
    ///     thinner debris rather than the frame rate.
    /// </summary>
    private static int MAX_PARTICLES => ModContent.GetInstance<VFXConfig>().ParticleCount;

    /// <summary>Marks a <see cref="Particle.Textures" /> entry as one of Terraria's own.</summary>
    private const string VANILLA_PREFIX = "Images/";

    private static readonly List<Particle> particles = [];
    private static readonly Dictionary<System.Type, Asset<Texture2D>[]> registry = [];

    private static bool loaded;

    public static int ParticleCount => particles.Count;

    /// <summary>
    ///     Called by <see cref="Particle" />'s <see cref="ILoadable" /> implementation, once per
    ///     concrete subclass, with the autoloaded prototype.
    /// </summary>
    internal static void Register(Particle prototype)
    {
        if (Main.dedServ)
            return;

        string[] paths = prototype.Textures;
        var assets = new Asset<Texture2D>[paths.Length];

        for (int i = 0; i < paths.Length; i++)
            assets[i] = RequestTexture(paths[i]);

        registry[prototype.GetType()] = assets;
    }

    /// <summary>
    ///     Resolves one texture path, from either the mod or the base game.
    /// </summary>
    /// <remarks>
    ///     A path under <c>Images/</c> is Terraria's own and has to be requested from
    ///     <see cref="Main.Assets" />; <see cref="ModContent" /> only knows about mod content and
    ///     throws on a vanilla key. This is what lets a particle wear a vanilla sprite -- the gore
    ///     sheets in particular -- rather than needing a copy of it traced into the mod.
    ///     <para>
    ///         Loaded immediately, unlike mod content. The registry hands every spawn the same
    ///         <see cref="Asset{T}" />, so a deferred vanilla request would leave the first burst
    ///         drawing nothing on the frames before it resolved.
    ///     </para>
    /// </remarks>
    private static Asset<Texture2D> RequestTexture(string path)
    {
        return path.StartsWith(VANILLA_PREFIX, StringComparison.Ordinal)
            ? Main.Assets.Request<Texture2D>(path, AssetRequestMode.ImmediateLoad)
            : ModContent.Request<Texture2D>(path);
    }

    /// <summary>
    ///     Spawns one particle. <paramref name="variant" /> selects a texture from the class's
    ///     <see cref="Particle.Textures" />, or picks at random when left as
    ///     <see cref="RANDOM_VARIANT" />.
    /// </summary>
    /// <returns>The spawned particle, or null on a server or when the class registered no textures.</returns>
    public static T? Spawn<T>(
        Vector2 position,
        Vector2 velocity,
        int timeLeft,
        int variant = RANDOM_VARIANT,
        float scale = 1f,
        float rotation = 0f,
        float rotationVelocity = 0f,
        Color? color = null) where T : Particle, new()
    {
        if (Main.dedServ || !loaded || timeLeft <= 0)
            return null;

        if (!registry.TryGetValue(typeof(T), out var textures) || textures.Length == 0)
            return null;

        if (particles.Count >= MAX_PARTICLES)
            particles.RemoveRange(0, particles.Count - MAX_PARTICLES + 1);

        var particle = new T
        {
            Position = position,
            Velocity = velocity,
            TimeLeft = timeLeft,
            MaxTimeLeft = timeLeft,
            Scale = scale,
            Rotation = rotation,
            RotationVelocity = rotationVelocity,
            Color = color ?? Color.White,
            Variant = variant < 0 ? Main.rand.Next(textures.Length) : Math.Clamp(variant, 0, textures.Length - 1),
            VariantTextures = textures,
            Active = true,
        };

        // Assigned after Variant, so a class whose FrameCount switches on the variant gets a frame
        // from the right sheet.
        particle.Frame = Main.rand.Next(Math.Max(particle.FrameCount, 1));

        particle.OnSpawn();
        particles.Add(particle);

        return particle;
    }

    public static void Clear() => particles.Clear();

    public override void OnModLoad()
    {
        loaded = true;

        if (Main.dedServ)
            return;

        On_Main.DrawDust += static (orig, self) =>
        {
            orig(self);

            if (loaded)
                DrawParticles();
        };
    }

    public override void OnModUnload()
    {
        loaded = false;
        particles.Clear();
        registry.Clear();
    }

    /// <summary>Debris does not belong to a world, so it does not survive leaving one.</summary>
    public override void OnWorldUnload() => particles.Clear();

    public override void PostUpdateEverything()
    {
        if (Main.dedServ)
            return;

        for (int i = particles.Count - 1; i >= 0; i--)
        {
            Particle particle = particles[i];

            particle.Tick();

            if (!particle.Active)
                particles.RemoveAt(i);
        }
    }

    private static void DrawParticles()
    {
        if (particles.Count == 0 || Main.gameMenu)
            return;

        var spriteBatch = Main.spriteBatch;

        // Two passes, batched by blend mode rather than by particle, for the same reason the ink
        // layers split DrawInk from DrawLight: a glow *adds* and a body *occludes*, and
        // interleaving them per particle would mean a SpriteBatch restart for every particle
        // instead of two for the whole list.
        //
        // Glow first, so it lands behind the sprite it belongs to. A particle that wants the
        // vanilla Fallen Star look -- the sprite sampled into a bloom around itself -- gets it by
        // overriding DrawGlow, and pays nothing when it does not.
        if (AnyGlow())
        {
            BeginPass(spriteBatch, BlendState.Additive);

            foreach (var particle in particles)
            {
                if (particle.HasGlow)
                    particle.DrawGlowPass(spriteBatch);
            }

            spriteBatch.End();
        }

        BeginPass(spriteBatch, BlendState.AlphaBlend);

        foreach (var particle in particles)
            particle.Draw(spriteBatch);

        spriteBatch.End();
    }

    /// <summary>
    ///     Whether any live particle wants the additive pass.
    /// </summary>
    /// <remarks>
    ///     Checked up front so a list of ordinary debris -- the common case -- does not pay for an
    ///     empty Begin/End pair every frame.
    /// </remarks>
    private static bool AnyGlow()
    {
        foreach (var particle in particles)
        {
            if (particle.HasGlow)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Opens a particle pass. PointClamp to keep the pixel art crisp, and the game view matrix
    ///     so debris tracks the world through zoom exactly like the entities it broke off.
    /// </summary>
    private static void BeginPass(SpriteBatch spriteBatch, BlendState blend)
    {
        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            blend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            Main.GameViewMatrix.TransformationMatrix
        );
    }
}
