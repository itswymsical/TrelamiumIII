using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Core;

namespace TrelamiumIII.Content.WoodenWarden;

/// <summary>
///     A boulder the Warden conjures. Spawned with a hang it manifests in the air and is then cast
///     down; spawned without one it is a thrown rock and keeps the velocity it was given.
/// </summary>
/// <remarks>
///     Placeholder body for the tile-sampled cluster attack: that one samples real terrain and
///     shatters back into it, where this wears one sprite and breaks into dust.
/// </remarks>
public class WardenBoulder : ModProjectile
{
    /// <summary>Read by <see cref="WardenArm" /> to lead a throw. Must match what the AI applies.</summary>
    public const float GRAVITY = 0.42f;
    private const float MAX_FALL = 17f;
    private const float AIR_DRAG = 0.995f;

    /// <summary>Vertical drift across the hang, in pixels per tick. Negative rises.</summary>
    private const float HANG_DRIFT = -0.35f;

    /// <summary>Speed the cast-down boulder starts with once the hang ends.</summary>
    private const float CAST_SPEED = 9f;

    public override string Texture => AssetReferences.Content.WoodenWarden.AlluvialBoulderProj.KEY;

    /// <summary>Ticks spent manifesting before the boulder is cast down. Zero for a thrown one.</summary>
    private ref float HangTicks => ref Projectile.ai[0];

    private ref float Age => ref Projectile.ai[1];

    private bool Hanging => Age < HangTicks;

    public override void SetDefaults()
    {
        Projectile.width = 32;
        Projectile.height = 30;

        Projectile.aiStyle = -1;

        Projectile.hostile = true;
        Projectile.friendly = false;

        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;

        Projectile.penetrate = -1;
        Projectile.timeLeft = 480;
    }

    public override void OnSpawn(IEntitySource source)
    {
        Projectile.rotation = Main.rand.NextFloat(MathHelper.TwoPi);

        if (HangTicks > 0f)
            Projectile.velocity = Vector2.Zero;
    }

    public override void AI()
    {
        Age++;

        // Off while forming, or a boulder conjured under a cave roof shatters on the frame it
        // appears. Back on the moment it is cast down.
        Projectile.tileCollide = !Hanging;

        if (Hanging)
        {
            Manifest();
            return;
        }

        // The tick the hang ends: the conjured boulder is released downward. A thrown one never
        // hangs, so it is already moving and this never runs for it.
        if (HangTicks > 0f && Age == HangTicks)
        {
            Projectile.velocity = new Vector2(Projectile.velocity.X, CAST_SPEED);

            SoundEngine.PlaySound(SoundID.Item70 with { Pitch = -0.6f }, Projectile.Center);
        }

        Projectile.scale = 1f;

        Projectile.velocity.X *= AIR_DRAG;
        Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + GRAVITY, MAX_FALL);

        Projectile.rotation += Projectile.velocity.X * 0.03f + 0.02f;

        if (Main.rand.NextBool(6))
            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.WoodFurniture, Scale: 0.9f);
    }

    /// <summary>Scales up in place with a slow rise, so the boulder reads as assembling out of the air.</summary>
    private void Manifest()
    {
        float progress = HangTicks <= 0f ? 1f : Math.Clamp(Age / HangTicks, 0f, 1f);

        Projectile.scale = MathHelper.SmoothStep(0.1f, 1f, progress);
        Projectile.velocity = new Vector2(0f, HANG_DRIFT);
        Projectile.rotation += 0.04f;

        // Pulled inward rather than thrown outward: the motes converge on the boulder as it forms.
        if (!Main.rand.NextBool(2))
        {
            Vector2 offset = Main.rand.NextVector2CircularEdge(46f, 46f);

            Dust dust = Dust.NewDustPerfect(Projectile.Center + offset, DustID.WoodFurniture, -offset * 0.05f, Scale: 1.1f);

            dust.noGravity = true;
        }
    }

    /// <summary>True kills the projectile, so the break itself is left to <see cref="OnKill" />.</summary>
    public override bool OnTileCollide(Vector2 oldVelocity) => true;

    public override void OnKill(int timeLeft) => Shatter();

    private void Shatter()
    {
        SoundEngine.PlaySound(Assets.SFX.NPC.WoodHit2.Asset with { Volume = 0.9f, Pitch = -0.3f }, Projectile.Center);

        if (Main.dedServ)
            return;

        for (int i = 0; i < 16; i++)
        {
            Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(14f, 14f),
                DustID.WoodFurniture,
                new Vector2(Main.rand.NextFloat(-4f, 4f), Main.rand.NextFloat(-5f, 1f)),
                90,
                Color.White,
                Main.rand.NextFloat(0.9f, 1.6f)
            );
        }
    }

    /// <summary>Harmless while it is still forming, so the tell is a real one.</summary>
    public override bool CanHitPlayer(Player target) => !Hanging;

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[Type].Value;

        Main.EntitySpriteDraw(
            texture,
            Projectile.Center - Main.screenPosition,
            null,
            lightColor,
            Projectile.rotation,
            texture.Size() * 0.5f,
            Projectile.scale,
            SpriteEffects.None
        );

        return false;
    }

    /// <summary>Conjured above <paramref name="over" />, held, then dropped. Server-side.</summary>
    public static void Cast(NPC source, Vector2 over, int damage, int hangTicks)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        Projectile.NewProjectile(
            source.GetSource_FromAI(),
            over,
            Vector2.Zero,
            ModContent.ProjectileType<WardenBoulder>(),
            damage,
            4f,
            Main.myPlayer,
            ai0: hangTicks
        );
    }

    /// <summary>Thrown along <paramref name="velocity" />. Server-side.</summary>
    public static void Throw(NPC source, Vector2 from, Vector2 velocity, int damage)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        Projectile.NewProjectile(
            source.GetSource_FromAI(),
            from,
            velocity,
            ModContent.ProjectileType<WardenBoulder>(),
            damage,
            4f,
            Main.myPlayer
        );
    }
}
