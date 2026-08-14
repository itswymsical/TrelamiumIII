using ReLogic.Content;
using System.Collections.Generic;

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
    private const int MAX_PARTICLES = 256;

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
            assets[i] = ModContent.Request<Texture2D>(paths[i]);

        registry[prototype.GetType()] = assets;
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

        // PointClamp to keep the pixel art crisp, and the game view matrix so debris tracks the
        // world through zoom exactly like the entities it broke off.
        spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.AlphaBlend,
            SamplerState.PointClamp,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            Main.GameViewMatrix.TransformationMatrix
        );

        foreach (var particle in particles)
            particle.Draw(spriteBatch);

        spriteBatch.End();
    }
}
