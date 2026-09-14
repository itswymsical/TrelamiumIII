using System.Collections.Generic;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Core.Rarities.Fields;

/// <summary>
///     <typeparamref name="T" /> particles off text, slot items and world drops. On text and slots they move in interface space around
///     their emitter with the type's gravity, drag, spin and <see cref="Particle.Update" />, without collision or lighting; world drops
///     spawn ordinary world particles. Types whose <c>Update</c> touches the world suit world drops only.
/// </summary>
public sealed class ParticleFlairField<T> : FlairField where T : Particle, new()
{
    private readonly Dictionary<object, List<T>> groups = [];

    /// <summary>Ticks each particle lives.</summary>
    public int Life { get; init; } = 40;

    /// <summary>Initial velocity before <see cref="Scatter" />, in pixels per tick.</summary>
    public Vector2 Velocity { get; init; } = new(0f, -0.4f);

    /// <summary>Random spread added to <see cref="Velocity" />.</summary>
    public Vector2 Scatter { get; init; } = new(0.4f, 0.3f);

    public float MinScale { get; init; } = 0.8f;

    public float MaxScale { get; init; } = 1.2f;

    /// <summary>Largest spin speed either way, in radians per tick.</summary>
    public float Spin { get; init; }

    /// <summary>Particle colour. Null keeps white.</summary>
    public Color? Tint { get; init; }

    /// <summary>Texture variant. <see cref="ParticleSystem.RANDOM_VARIANT" /> picks one per particle.</summary>
    public int Variant { get; init; } = ParticleSystem.RANDOM_VARIANT;

    /// <summary>Live particles per emitter; past it the oldest go.</summary>
    public int MaxParticles { get; init; } = 48;

    /// <summary>Multiplies the type's <see cref="Particle.Gravity" /> in interface space.</summary>
    public float GravityScale { get; init; } = 1f;

    public override FieldSurfaces Supported => FieldSurfaces.Text | FieldSurfaces.Slot | FieldSurfaces.World;

    protected override void Spawn(in FieldEmitter emitter)
    {
        // Kept relative to the anchor, so particles follow a moving tooltip and each slot draws only its own.
        T? particle = ParticleSystem.Create<T>(emitter.RandomPoint() - emitter.Anchor, RandomVelocity() * emitter.Scale, Life, Variant,
            Main.rand.NextFloat(MinScale, MaxScale) * emitter.Scale, Main.rand.NextFloatDirection() * MathHelper.Pi,
            Main.rand.NextFloatDirection() * Spin, Tint);

        if (particle is null)
            return;

        if (!groups.TryGetValue(emitter.Key, out List<T>? group))
            groups[emitter.Key] = group = new List<T>();

        if (group.Count >= Math.Max(MaxParticles, 1))
            group.RemoveAt(0);

        group.Add(particle);
    }

    protected override void Update()
    {
        foreach ((object key, List<T> group) in groups)
        {
            for (int i = group.Count - 1; i >= 0; i--)
            {
                T particle = group[i];

                particle.Velocity.Y += particle.Gravity * GravityScale;
                particle.Velocity *= particle.Drag;
                particle.Position += particle.Velocity;
                particle.Rotation += particle.RotationVelocity;
                particle.Update();

                if (--particle.TimeLeft <= 0)
                    group.RemoveAt(i);
            }

            if (group.Count == 0)
                groups.Remove(key);
        }
    }

    protected internal override void Draw(SpriteBatch spriteBatch, in FieldEmitter emitter)
    {
        if (!groups.TryGetValue(emitter.Key, out List<T>? group))
            return;

        SpriteBatchState home = SpriteBatchState.Capture(spriteBatch);
        SpriteBatchState current = home;

        // Glow first so it sits behind the bodies, matching ParticleSystem's own passes.
        foreach (T particle in group)
        {
            if (!particle.HasGlow)
                continue;

            RarityBatch.Switch(spriteBatch, ref current, home.With(BlendState.Additive, sampler: SamplerState.PointClamp));
            particle.DrawGlowAt(spriteBatch, emitter.Anchor + particle.Position);
        }

        RarityBatch.Switch(spriteBatch, ref current, home.With(BlendState.AlphaBlend, sampler: SamplerState.PointClamp));

        foreach (T particle in group)
            particle.DrawAt(spriteBatch, emitter.Anchor + particle.Position, Color.White);

        RarityBatch.Switch(spriteBatch, ref current, home);
    }

    protected internal override void SpawnWorld(Item item)
    {
        for (int i = RollCount(WorldRate); i > 0; i--)
        {
            ParticleSystem.Spawn<T>(item.Center + Main.rand.NextVector2Circular(item.width * 0.4f, item.height * 0.4f), RandomVelocity(),
                Life, Variant, Main.rand.NextFloat(MinScale, MaxScale), Main.rand.NextFloatDirection() * MathHelper.Pi,
                Main.rand.NextFloatDirection() * Spin, Tint);
        }
    }

    protected internal override void Release() => groups.Clear();

    private Vector2 RandomVelocity() => Velocity + Main.rand.NextVector2Circular(Scatter.X, Scatter.Y);
}
