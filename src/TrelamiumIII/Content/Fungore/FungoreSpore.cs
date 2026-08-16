using Terraria.Audio;
using Terraria.DataStructures;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;

namespace TrelamiumIII.Content.Fungore;

/// <summary>
///     A clot of spores thrown up out of a super slam.
/// </summary>
/// <remarks>
///     <para>
///         It has no sprite at all. The projectile is a hitbox and a set of physics; everything you
///         actually see is <see cref="SporeLayer" /> nodes it leaves behind, which means several
///         landing near each other fuse into one cloud rather than reading as a row of pellets.
///     </para>
///     <para>
///         Arcs rather than flies. The slam already covers the ground, so the interesting part of the
///         attack is the second beat -- the fallout coming down a moment later, somewhere else.
///     </para>
/// </remarks>
public sealed class FungoreSpore : ModProjectile
{
    /// <summary>Pre-difficulty figure. Expert and master scale it the usual way.</summary>
    public const int SPORE_DAMAGE = 20;

    /// <summary>Seconds of infection a clot passes on. Re-applied, not stacked, by a second hit.</summary>
    public const int INFECTION_TICKS = 4 * 60;

    private const int FAN_COUNT = 11;

    /// <summary>Half-angle of the fan, either side of straight up.</summary>
    private const float FAN_SPREAD = 1.02f;

    private const float MIN_SPEED = 7.5f;
    private const float MAX_SPEED = 13.5f;

    private const float GRAVITY = 0.32f;
    private const float MAX_FALL = 13f;

    private const float DRAG = 0.995f;

    private const int TRAIL_INTERVAL = 3;

    private const int LIFETIME = 240;

    public override string Texture => AssetReferences.Content.Fungore.SporeclotSac.KEY;

    /// <summary>
    ///     Throws a fan of clots out of an impact.
    /// </summary>
    /// <param name="scale">
    ///     Fraction of full strength, matching the slam that threw them. The worn-down slams of the
    ///     last phase throw fewer, slower clots rather than the same barrage.
    /// </param>
    public static void SpawnFan(NPC source, Vector2 impact, float scale = 1f)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        int count = Math.Max(3, (int)(FAN_COUNT * scale));

        var entitySource = new EntitySource_Parent(source);

        for (int i = 0; i < count; i++)
        {
            float slot = count == 1 ? 0.5f : i / (float)(count - 1);

            // Evenly spaced with a little jitter: a clean fan reads as a mechanism, a ragged one as
            // debris, and this is debris.
            float angle = -MathHelper.PiOver2
                          + MathHelper.Lerp(-FAN_SPREAD, FAN_SPREAD, slot)
                          + Main.rand.NextFloat(-0.09f, 0.09f);

            // Fastest at the edges of the fan, so the arcs land spread across the ground instead of
            // stacking into one pile at a single range.
            float reach = MathHelper.Lerp(0.55f, 1f, Math.Abs(slot - 0.5f) * 2f);
            float speed = MathHelper.Lerp(MIN_SPEED, MAX_SPEED, reach) * Main.rand.NextFloat(0.85f, 1.12f) * scale;

            Projectile.NewProjectile(
                entitySource,
                impact + new Vector2(Main.rand.NextFloat(-16f, 16f), -14f),
                angle.ToRotationVector2() * speed,
                ModContent.ProjectileType<FungoreSpore>(),
                SPORE_DAMAGE,
                0f,
                Main.myPlayer
            );
        }
    }

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 20;

        Projectile.hostile = true;
        Projectile.friendly = false;

        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;

        Projectile.penetrate = -1;
        Projectile.aiStyle = -1;
        Projectile.timeLeft = LIFETIME;
    }

    public override void AI()
    {
        Projectile.velocity.X *= DRAG;
        Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + GRAVITY, MAX_FALL);

        Projectile.rotation = Projectile.velocity.ToRotation();

        if (Main.dedServ || Projectile.timeLeft % TRAIL_INTERVAL != 0)
            return;

        // Thins out as it travels, so the head of the arc is the dense part and the tail is the
        // wisp it left behind.
        float taper = Projectile.timeLeft / (float)LIFETIME;

        SporeLayer.Trail(Projectile.Center, Projectile.velocity * -0.12f, MathHelper.Lerp(7f, 17f, taper));

        Lighting.AddLight(Projectile.Center, 0.22f, 0.12f, 0.03f);
    }

    public override void OnHitPlayer(Player target, Player.HurtInfo info)
    {
        target.AddBuff(ModContent.BuffType<FungalInfectionSpore>(), INFECTION_TICKS);

        SoundEngine.PlaySound(Assets.SFX.Sporeclot_Splash.Asset with { Volume = 0.7f, Pitch = 0.35f }, Projectile.Center);

        if (!Main.dedServ)
            SporeLayer.Cling(target, 0.9f);
    }

    /// <summary>Breaks where it lands and leaves the cloud behind rather than bouncing on.</summary>
    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        SoundEngine.PlaySound(Assets.SFX.Sporeclot_TileImpact.Asset with { Volume = 0.5f, Pitch = 0.4f }, Projectile.Center);

        if (!Main.dedServ)
            SporeLayer.TileSplat(Projectile.Center, oldVelocity);

        Projectile.Kill();

        return false;
    }

    public override bool PreDraw(ref Color lightColor) => false;
}
