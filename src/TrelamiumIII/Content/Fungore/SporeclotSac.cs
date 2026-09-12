using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Content.Rarities;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;
using TrelamiumIII.Core.Graphics.Particles;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Fungore;

[LegacyName("Sporeclot Sac")]
public class SporeclotSac : ModItem
{
    public override void SetDefaults()
    {
        Item.damage = 9;
        Item.DamageType = DamageClass.Ranged;
        Item.crit = 2;
        Item.rare = ModContent.RarityType<SporeclotRarity>();
        Item.width = Item.height = 22;
        Item.value = Item.buyPrice(silver: 2, copper: 30);

        Item.shoot = ModContent.ProjectileType<SporeclotSacProj>();
        Item.shootSpeed = 10f;
        Item.UseSound = SoundID.Item1;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.useTime = Item.useAnimation = 16;

        Item.useTurn =
            Item.noUseGraphic =
            Item.noMelee = true;
    }
}

public class SporeclotSacProj : ModProjectile
{
    public override string Texture => AssetReferences.Content.Fungore.SporeclotSac.KEY;

    /// <summary>Sacs allowed on one host at once. Past this, the oldest is dropped.</summary>
    public static int MaxStickies => 3;

    private const int ARC_TICKS = 45;
    private const int STICK_TICKS = 40;
    private const int DEATH_TICKS = 30;

    private const float ARC_GRAVITY = 0.125f;
    private const float TUMBLE_GRAVITY = 0.35f;

    private const float MAX_SPIN = 0.34f;

    protected NPC Target => Main.npc[(int)Projectile.ai[1]];

    /// <summary>Synced across clients, unlike the timers, so a remote sac still rides its host.</summary>
    protected bool Stuck
    {
        get => Projectile.ai[0] == 1f;
        set => Projectile.ai[0] = value ? 1f : 0f;
    }

    private int airTime;
    private int stickTimer;
    private Vector2 offset;
    private float stuckRotation;
    private bool stickInitialised;

    private float spinRate;
    private float spinDirection = 1f;

    private bool ruptured;
    private bool dying;
    private int deathTimer;
    private Vector2 deathStartPosition;
    private Vector2 deathOffset;
    private float deathRotation;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 3;
        ProjectileID.Sets.TrailingMode[Type] = 1;
    }

    public override void SetDefaults()
    {
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.width = Projectile.height = 14;

        Projectile.friendly =
            Projectile.tileCollide =
            Projectile.usesLocalNPCImmunity = true;

        // One host per sac: it bursts on the first thing it touches and has nothing left after.
        Projectile.localNPCHitCooldown = -1;

        Projectile.timeLeft = 240;
        Projectile.penetrate = Projectile.aiStyle = -1;
    }

    public override void OnSpawn(IEntitySource source)
    {
        // Spin scales with and follows the throw, so a lob lolls and a hard throw whips.
        float speed = Projectile.velocity.Length();

        spinRate = MathHelper.Clamp(speed * 0.022f, 0.08f, 0.26f);
        spinDirection = Projectile.velocity.X < 0f ? -1f : 1f;
    }

    public override void AI()
    {
        if (dying)
        {
            UpdateDeath();
            return;
        }

        if (Stuck)
            UpdateStuck();
        else
            UpdateFlight();

        // A sac that never connects still gets to rot away rather than blinking out.
        if (Projectile.timeLeft <= DEATH_TICKS)
            BeginDeath();

        Projectile.spriteDirection = Projectile.direction;
    }

    private void UpdateFlight()
    {
        airTime++;

        if (airTime <= ARC_TICKS)
        {
            Projectile.velocity.Y += ARC_GRAVITY;
            Projectile.velocity.X *= 0.995f;
        }
        else
        {
            // Past its arc the sac has lost the throw and just falls, tumbling faster as it goes.
            Projectile.velocity.Y += TUMBLE_GRAVITY;
            Projectile.velocity.X *= 0.985f;

            spinRate = Math.Min(spinRate + 0.003f, MAX_SPIN);
        }

        // Continuous spin rather than velocity-aligned: a lumpy ball has no leading edge to point along.
        Projectile.rotation += spinRate * spinDirection;
    }

    private void UpdateStuck()
    {
        // A remote client learns about the stick through ai[], not through OnHitNPC, so it has to
        // derive the offset from wherever the sac happens to be when the flag arrives.
        if (!stickInitialised)
            BeginStick(Target, Projectile.velocity);

        if (Target is not { active: true } || Target.dontTakeDamage)
        {
            BeginDeath();
            return;
        }

        Projectile.tileCollide = false;
        Projectile.Center = Target.Center - offset;
        Projectile.gfxOffY = Target.gfxOffY;
        Projectile.rotation = stuckRotation;

        if (++stickTimer >= STICK_TICKS)
            BeginDeath();
    }

    private void UpdateDeath()
    {
        if (++deathTimer >= DEATH_TICKS)
            Projectile.Kill();
    }

    /// <summary>Latches onto <paramref name="host" />, capturing the pose the sac landed in.</summary>
    private void BeginStick(NPC host, Vector2 impactVelocity)
    {
        stickInitialised = true;

        // Frozen mid-spin rather than snapped to the impact angle -- it splats however it happened
        // to be turned when it hit.
        stuckRotation = Projectile.rotation;

        // Nudged along the impact direction so the sac sits buried in the host rather than
        // hovering at the exact pixel it made contact.
        offset = host.Center - Projectile.Center + impactVelocity * 0.75f;

        Projectile.velocity = Vector2.Zero;
        Projectile.friendly = false;
        Projectile.tileCollide = false;
    }

    /// <summary>Starts the fall-away; from here it's only a sprite finishing its animation, with timeLeft cut to match.</summary>
    private void BeginDeath()
    {
        if (dying)
            return;

        dying = true;
        deathTimer = 0;
        deathStartPosition = Projectile.Center;
        deathOffset = new Vector2(Main.rand.NextFloat(-20f, 20f), 0f);
        deathRotation = Projectile.rotation;

        Projectile.velocity = Vector2.Zero;
        Projectile.friendly = false;
        Projectile.tileCollide = false;
        Projectile.timeLeft = Math.Min(Projectile.timeLeft, DEATH_TICKS);
    }

    public override bool? CanHitNPC(NPC target)
    {
        if (Stuck || dying)
            return false;

        return null;
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        Vector2 impactVelocity = Projectile.velocity;

        Projectile.ai[1] = target.whoAmI;
        Stuck = true;
        Projectile.netUpdate = true;

        BeginStick(target, impactVelocity);
        RemoveStackProjectiles(target);

        SoundEngine.PlaySound(Assets.SFX.Sporeclot_Splash.Asset with { Volume = 0.9f }, Projectile.Center);
        SporeLayer.Splat(target, Projectile.Center, impactVelocity, STICK_TICKS);

        FungalInfectionNPC.AddBuffStack(target, 1, Projectile.owner);
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if (dying)
            return false;

        SoundEngine.PlaySound(Assets.SFX.Sporeclot_TileImpact.Asset with { Volume = 0.8f }, Projectile.Center);
        SporeLayer.TileSplat(Projectile.Center, oldVelocity);

        // Straight into terrain, so it comes apart on the spot rather than fading out intact.
        Rupture(Projectile.Center, -oldVelocity * 0.35f);
        BeginDeath();

        return false;
    }

    public override void OnKill(int timeLeft)
    {
        // Covers sacs that slough off a host or time out. Thrown from DrawPosition, since the fall-away has moved the sprite.
        Rupture(DrawPosition, Vector2.Zero);
    }

    /// <summary>Breaks the sac into husk, chunks and shards. Runs at most once per sac.</summary>
    private void Rupture(Vector2 position, Vector2 inheritedVelocity)
    {
        if (ruptured)
            return;

        ruptured = true;

        if (Main.dedServ)
            return;

        // One husk, so the debris still reads as a single sac coming apart.
        ParticleSystem.Spawn<SporeclotParticle>(
            position,
            inheritedVelocity * 0.5f + new Vector2(Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-3.2f, -1.4f)),
            timeLeft: TimeConversion.SetTime(secs: 2),
            variant: SporeclotParticle.HUSK,
            scale: Main.rand.NextFloat(0.85f, 1f)
        );

        int chunks = Main.rand.Next(2, 4);

        for (int i = 0; i < chunks; i++)
        {
            ParticleSystem.Spawn<SporeclotParticle>(
                position + Main.rand.NextVector2Circular(4f, 4f),
                inheritedVelocity * 0.4f + new Vector2(Main.rand.NextFloat(-3f, 3f), Main.rand.NextFloat(-4.5f, -1.5f)),
                timeLeft: TimeConversion.SetTime(secs: 3),
                variant: Main.rand.NextBool(3) ? SporeclotParticle.CHUNK : SporeclotParticle.SHARD,
                scale: Main.rand.NextFloat(0.8f, 1f)
            );
        }
    }

    /// <summary>Caps how many sacs one host can wear, dropping the one closest to expiring.</summary>
    protected void RemoveStackProjectiles(NPC target)
    {
        int count = 0;
        int oldest = -1;
        int oldestTimeLeft = int.MaxValue;

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile other = Main.projectile[i];

            if (i == Projectile.whoAmI
                || !other.active
                || other.owner != Projectile.owner
                || other.type != Projectile.type
                || other.ai[0] != 1f
                || (int)other.ai[1] != target.whoAmI)
            {
                continue;
            }

            count++;

            if (other.timeLeft < oldestTimeLeft)
            {
                oldestTimeLeft = other.timeLeft;
                oldest = i;
            }
        }

        // count tallies the *other* sacs on this host; this one makes it count + 1.
        if (count >= MaxStickies && oldest >= 0)
            Main.projectile[oldest].Kill();
    }

    /// <summary>Progress through the fall-away animation, 0 to 1.</summary>
    private float DeathProgress => dying ? Math.Min(deathTimer / (float)DEATH_TICKS, 1f) : 0f;

    /// <summary>Where the sprite is; during the fall-away it diverges from the frozen <see cref="Projectile.Center" />.</summary>
    private Vector2 DrawPosition
    {
        get
        {
            if (!dying)
                return Projectile.Center;

            float progress = DeathProgress;

            // Squared so it accelerates: the sac lets go, then drops.
            return deathStartPosition + new Vector2(
                deathOffset.X * progress,
                deathOffset.Y + progress * progress * 90f
            );
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[Type].Value;

        var spriteEffects = SpriteEffects.None;
        Vector2 origin = texture.Size() / 2f;

        if (Projectile.spriteDirection == -1)
            spriteEffects = SpriteEffects.FlipHorizontally;

        Vector2 drawPosition;
        float drawRotation;
        Color drawColor;

        if (dying)
        {
            float progress = DeathProgress;

            drawPosition = DrawPosition - Main.screenPosition;
            drawRotation = deathRotation + progress * MathHelper.TwoPi * 0.1f;
            drawColor = lightColor * (1f - progress);
        }
        else
        {
            drawPosition = Projectile.Center - Main.screenPosition;
            drawRotation = Projectile.rotation;
            drawColor = lightColor;
        }

        Main.EntitySpriteDraw(
            texture,
            drawPosition,
            null,
            drawColor,
            drawRotation,
            origin,
            Projectile.scale,
            spriteEffects,
            0
        );

        return false;
    }
}
