using Terraria.Audio;
using TrelamiumIII.Common.Configs;
using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Core.ExperientialSystem;

/// <summary>
/// The shape of one Exp burst. Two presets rather than two effects: crossing a Threshold and completing an
/// act tier are the same event at different weights, and they should look like it.
///
/// <para>
/// Instances are shared and never mutated after construction, so a mote can hold its preset by reference
/// for the whole of its life.
/// </para>
/// </summary>
public sealed class AscendSettings
{
    /// <summary>Motes evenly spaced around the ring.</summary>
    public int RingCount { get; init; }

    /// <summary>Loose motes filling the inside of the burst.</summary>
    public int SparkCount { get; init; }

    /// <summary>
    /// How far out the ring motes start. Non-zero on purpose: spawning them all on one point stacks
    /// additive sprites on the player for the first few frames and hides the character the burst is about.
    /// </summary>
    public float Radius { get; init; }

    public float Speed { get; init; }

    public float Scale { get; init; }

    /// <summary>Draw opacity of each mote.</summary>
    public float Alpha { get; init; }

    /// <summary>
    /// Light each mote contributes. Small by design - there are dozens and Terraria's lighting sums, so a
    /// value that looks reasonable for one mote washes the screen at thirty.
    /// </summary>
    public float Light { get; init; }

    public int Lifetime { get; init; }

    /// <summary>Fraction of a mote's life spent flying out before the pull home takes over.</summary>
    public float OutwardPhase { get; init; }

    public float Pull { get; init; }

    /// <summary>Ends of the colour range each mote is picked from.</summary>
    public Color Deep { get; init; }

    public Color Pale { get; init; }

    /// <summary>
    /// Crossing a Threshold. Blue, and big enough to stop what you were doing.
    /// </summary>
    public static readonly AscendSettings Threshold = new()
    {
        RingCount = 16,
        SparkCount = 10,
        Radius = 26f,
        Speed = 4.6f,
        Scale = 0.6f,
        Alpha = 0.7f,
        Light = 0.1f,
        Lifetime = 90,
        OutwardPhase = 0.42f,
        Pull = 1.15f,
        Deep = new Color(70, 130, 255),
        Pale = new Color(180, 215, 255)
    };

    /// <summary>
    /// Completing an act tier. Green to match the Exp popup, and roughly a third the size - it happens
    /// often enough that it has to register without interrupting.
    /// </summary>
    public static readonly AscendSettings ActTier = new()
    {
        RingCount = 3,
        SparkCount = 4,
        Radius = 18f,
        Speed = 2.8f,
        Scale = 0.45f,
        Alpha = 0.6f,
        Light = 0.06f,

        // Shorter-lived and pulled home harder, so the whole gesture is over in under a second.
        Lifetime = 55,
        OutwardPhase = 0.38f,
        Pull = 1.45f,

        Deep = new Color(60, 190, 90),
        Pale = new Color(195, 255, 190)
    };
}

/// <summary>
/// What earning Exp looks like: light thrown out of the player, held for a beat, then drawn back in.
/// No panel, no banner, no text.
///
/// <para>
/// The moment is supposed to read as something happening <i>to</i> the character rather than a number
/// changing on a sheet. A player who has never been told the system exists should still notice it, and
/// should have to go and ask somebody what it meant.
/// </para>
/// </summary>
public static class ThresholdAscendEffect
{
    /// <summary>Crossing a Threshold.</summary>
    public static void PlayThreshold(Player player)
        => Play(player, AscendSettings.Threshold, SoundID.Item29 with { Pitch = -0.35f, Volume = 0.65f });

    /// <summary>Completing an act tier. Quieter and higher than the Threshold cue, so the two never read as the same event.</summary>
    public static void PlayActTier(Player player)
        => Play(player, AscendSettings.ActTier, SoundID.Item4 with { Pitch = 0.5f, Volume = 0.35f });

    private static void Play(Player player, AscendSettings settings, SoundStyle sound)
    {
        if (Main.dedServ || Main.gameMenu || player.whoAmI != Main.myPlayer)
            return;

        if (!ExperientialConfig.ThresholdEffect)
            return;

        Vector2 center = player.Center;

        for (int i = 0; i < settings.RingCount; i++)
        {
            // Evenly spaced with a little jitter, so it reads as a ring rather than as a wheel.
            float angle = i / (float)settings.RingCount * MathHelper.TwoPi + Main.rand.NextFloat(-0.08f, 0.08f);
            Vector2 direction = angle.ToRotationVector2();

            Spawn(player, settings,
                center + direction * settings.Radius,
                direction * settings.Speed * Main.rand.NextFloat(0.8f, 1.2f),
                settings.Scale * Main.rand.NextFloat(0.8f, 1.2f));
        }

        for (int i = 0; i < settings.SparkCount; i++)
        {
            Spawn(player, settings,
                center + Main.rand.NextVector2Circular(settings.Radius * 0.7f, settings.Radius * 0.9f),
                Main.rand.NextVector2Circular(2.2f, 2.2f),
                settings.Scale * Main.rand.NextFloat(0.45f, 0.8f));
        }

        SoundEngine.PlaySound(sound, center);
    }

    private static void Spawn(Player player, AscendSettings settings, Vector2 position, Vector2 velocity, float scale)
    {
        var mote = ParticleSystem.Spawn<ThresholdMote>(
            position, velocity, Math.Max(settings.Lifetime, 1), scale: scale,
            rotation: Main.rand.NextFloat(MathHelper.TwoPi),
            color: Color.Lerp(settings.Deep, settings.Pale, Main.rand.NextFloat()));

        if (mote is null)
            return;

        mote.Host = player;
        mote.Tuning = settings;
    }
}

/// <summary>
/// One mote of an Exp burst. Flies outward, stalls, then falls back into whoever threw it.
///
/// <para>
/// The return is the whole point - light leaving the player is a discharge, light returning is something
/// being gained. Same particle for both presets, and the difference is entirely in the second half of its life.
/// </para>
/// </summary>
public sealed class ThresholdMote : Particle
{
    private const float MAX_SPEED = 18f;

    /// <summary>Distance at which the mote has arrived and is absorbed.</summary>
    private const float ARRIVAL = 14f;

    public override string[] Textures => [Assets.Textures.VFX.Glow.KEY];

    public override float Gravity => 0f;

    public override float Drag => 0.93f;

    public override bool TileCollide => false;

    public override bool LiquidCollide => false;

    /// <summary>Self-lit; the whole effect is a light source and should not dim in an unlit cave.</summary>
    public override bool UseLighting => false;

    public override int FadeOutTicks => 14;

    public override bool HasGlow => true;

    public Entity? Host;

    /// <summary>The preset this mote was spawned under. Never null in practice; defaulted so it cannot throw.</summary>
    public AscendSettings Tuning = AscendSettings.Threshold;

    /// <summary>
    /// Additive only. Returning false skips the normal alpha-blended body pass, leaving
    /// <see cref="DrawGlow"/> under <see cref="BlendState.Additive"/> as the only thing drawn.
    /// </summary>
    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor) => false;

    public override void Update()
    {
        if (Tuning.Light > 0f)
            Lighting.AddLight(Position, Color.ToVector3() * Tuning.Light * Opacity);

        if (Host is not { active: true } host || Progress < Tuning.OutwardPhase)
            return;

        Vector2 toHost = host.Center - Position;
        float distance = toHost.Length();

        if (distance <= ARRIVAL)
        {
            Kill();
            return;
        }

        // Ramps in over the return phase rather than snapping on, so the mote arcs back instead of
        // reversing on the spot.
        float span = Math.Max(1f - Tuning.OutwardPhase, 0.001f);
        float ramp = (Progress - Tuning.OutwardPhase) / span;

        Velocity += toHost / distance * Tuning.Pull * ramp;

        if (Velocity.LengthSquared() > MAX_SPEED * MAX_SPEED)
            Velocity = Vector2.Normalize(Velocity) * MAX_SPEED;
    }

    public override void DrawGlow(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, float opacity)
    {
        if (opacity <= 0f)
            return;

        // Flares up quickly, then tightens as it comes home - a mote arriving should look like it is
        // being drawn to a point, not like it is fading out on the way.
        float life = Progress;
        float flare = MathF.Min(life * 6f, 1f);
        float scale = Scale * MathHelper.Lerp(1.25f, 0.4f, life);

        spriteBatch.Draw(texture, drawPosition, null, Color * (flare * opacity * Tuning.Alpha),
            Rotation, texture.Size() * 0.5f, scale, SpriteEffects.None, 0f);
    }
}
