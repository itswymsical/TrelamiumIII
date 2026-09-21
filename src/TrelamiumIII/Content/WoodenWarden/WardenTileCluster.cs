using System.IO;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Graphics.Shockwaves;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Content.WoodenWarden;

/// <summary>
///     A chunk of terrain the Warden lifts out of the ground: rises from its dig site, hangs, is launched at the nearest
///     player and shatters on impact. The terrain is sampled in <see cref="OnSpawn" />, which runs before the first sync.
/// </summary>
public sealed class WardenTileCluster : ModProjectile
{
    /// <summary>Radius of the sampled disc, in tiles: a scoop of ground, and a boulder.</summary>
    public const float CHUNK_RADIUS = 2.4f;

    public const float BOULDER_RADIUS = 6f;

    /// <summary>Hitbox side as a fraction of the disc's diameter, so the corners of the rim do not hurt.</summary>
    private const float HITBOX_FRACTION = 0.68f;

    public const int RISE_TICKS = 32;

    private const float RISE_HEIGHT = 150f;

    /// <summary>Extra rise per seed step, so a volley does not hang in one flat row.</summary>
    private const float RISE_STEP = 26f;

    private const float BOB = 4f;

    private const float LAUNCH_SPEED = 13f;

    private const float LAUNCH_SPEED_BOULDER = 10.5f;

    /// <summary>Longest flight the launch lead will plan for, so a far target gets a flat shot rather than a lob.</summary>
    private const float MAX_FLIGHT_TICKS = 55f;

    private const float GRAVITY = 0.14f;
    private const float MAX_FALL = 15f;
    private const float SPIN = 0.035f;

    /// <summary>Ticks before the launch the chunk shakes, so the release has a tell.</summary>
    private const int TELL_TICKS = 14;

    private const float TELL_SHAKE = 2.5f;

    /// <summary>Ticks the beam from the Warden's eye stays up after the chunk appears.</summary>
    private const int BEAM_TICKS = 14;

    private static readonly SoundStyle RiseSound = SoundID.Dig with { Volume = 1.1f, Pitch = -0.7f };
    private static readonly SoundStyle LaunchSound = SoundID.Item1 with { Volume = 0.9f, Pitch = -0.8f };
    private static readonly SoundStyle ShatterSound = SoundID.Item70 with { Volume = 0.9f, Pitch = -0.5f };

    public override string Texture => Assets.Textures.NullTex.KEY;

    /// <summary>Ticks from spawn to launch.</summary>
    private ref float HangTicks => ref Projectile.ai[0];

    private ref float Age => ref Projectile.ai[1];

    private int Seed => (int)Projectile.ai[2];

    private bool Launched => Age >= HangTicks;

    private TileCluster? cluster;

    /// <summary>Radius of the sampled disc, in tiles. Set from the <see cref="ClusterSource" /> and synced.</summary>
    private float radius = CHUNK_RADIUS;

    private bool Boulder => radius > CHUNK_RADIUS;

    /// <summary>World centre of the dig site. The rise and the hang are measured from it.</summary>
    private Vector2 origin;

    /// <summary>The Warden that conjured this, for the eye beam; -1 if none.</summary>
    private int caster = -1;

    /// <summary>Velocity at the moment of impact; tile collision has already zeroed the real one by then.</summary>
    private Vector2 impactVelocity;

    /// <summary>Client-side ripple through the chunk as it tears free. Evaluated, never registered.</summary>
    private BlockWave? wobble;

    /// <summary>Where it hangs: higher for a bigger disc, so its underside still clears the ground it came out of.</summary>
    private Vector2 Hover => origin - Vector2.UnitY * (RISE_HEIGHT + Seed % 4 * RISE_STEP + (radius - CHUNK_RADIUS) * TileCluster.TILE * 2f);

    /// <summary>Hitbox side for <paramref name="tiles" /> of radius, in world pixels.</summary>
    private static int HitboxFor(float tiles) => (int)(tiles * 2f * TileCluster.TILE * HITBOX_FRACTION);

    public override void SetDefaults()
    {
        Projectile.width = HitboxFor(CHUNK_RADIUS);
        Projectile.height = HitboxFor(CHUNK_RADIUS);

        Projectile.aiStyle = -1;

        Projectile.hostile = true;
        Projectile.friendly = false;

        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;

        Projectile.penetrate = -1;
        Projectile.timeLeft = 600;
    }

    /// <summary>Server-side. The chunk spawns centred on the dig site and samples a disc of <paramref name="tiles" /> there.</summary>
    public static void Conjure(NPC source, Point dig, float tiles, int hangTicks, int damage, int seed)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        Projectile.NewProjectile(
            new ClusterSource(source, tiles),
            dig.ToWorldCoordinates(),
            Vector2.Zero,
            ModContent.ProjectileType<WardenTileCluster>(),
            damage,
            4f,
            Main.myPlayer,
            ai0: hangTicks,
            ai2: seed
        );
    }

    public override void OnSpawn(IEntitySource source)
    {
        if (source is ClusterSource { Radius: > 0f } sized)
            radius = sized.Radius;

        if (source is EntitySource_Parent { Entity: NPC npc })
            caster = npc.whoAmI;

        // Spawned on the surface tile; a boulder is sampled from below it so the whole disc is ground.
        Projectile.Center += Vector2.UnitY * (Depth * TileCluster.TILE);

        origin = Projectile.Center;
        cluster = TileCluster.Sample(origin.ToTileCoordinates(), radius, Seed);

        Projectile.Resize(HitboxFor(radius), HitboxFor(radius));
    }

    /// <summary>Tiles below the surface the disc is centred at: none for a scoop, so its top is the ground line; all but the top row for a boulder.</summary>
    private float Depth => Boulder ? MathF.Ceiling(radius) - 1f : 0f;

    /// <summary>The ground line the chunk came out of.</summary>
    private Vector2 Surface => origin - Vector2.UnitY * (Depth * TileCluster.TILE);

    /// <summary>Radius relative to a scoop, for scaling the effects around it.</summary>
    private float Size => radius / CHUNK_RADIUS;

    public override void AI()
    {
        if (Age == 0f)
            TearFree();

        wobble?.Update();

        if (wobble is { Finished: true })
            wobble = null;

        if (!Launched)
            Hang();
        else if (Age == HangTicks)
            Launch();
        else
            Fly();

        Age++;

        if (!Main.dedServ)
            Lighting.AddLight(Projectile.Center, TileClusterShards.EdgeColor.ToVector3() * 0.25f);
    }

    /// <summary>The ground heaves at the dig site and the chunk starts to come away from it.</summary>
    private void TearFree()
    {
        if (Main.dedServ)
            return;

        float size = Size;
        Vector2 surface = Surface;

        SoundEngine.PlaySound(RiseSound with { Pitch = RiseSound.Pitch - 0.1f * (size - 1f) }, surface);

        TileShockwaveSystem.Spawn(new BlockWave(surface, 120f * size, lifetime: (int)(26 * MathF.Sqrt(size)))
        {
            Amplitude = 10f * MathF.Sqrt(size),
            Thickness = 48f * size,
            Reach = 80f * size,
            Blend = 0.6f,
        });

        wobble = new BlockWave(TileCluster.WaveOrigin, 64f * size, lifetime: (int)(30 * MathF.Sqrt(size)))
        {
            Amplitude = 6f * MathF.Sqrt(size),
            Thickness = 26f * size,
            Reach = 64f * size,
            Cycles = 3f,
            Blend = 0.25f,
        };

        if (size > 1f && Main.LocalPlayer.DistanceSQ(surface) < 1400f * 1400f)
            CameraSystem.shake = Math.Max(CameraSystem.shake, (int)(4f * size));

        for (int i = 0; i < (int)(14 * size); i++)
        {
            Dust.NewDustPerfect(
                surface + Main.rand.NextVector2Circular(34f * size, 8f),
                DustID.Dirt,
                new Vector2(Main.rand.NextFloat(-2.5f, 2.5f), Main.rand.NextFloat(-4f, -1f)),
                Scale: Main.rand.NextFloat(1f, 1.5f)
            );
        }
    }

    /// <summary>Position servo up to the hover point, then a bob until the launch.</summary>
    private void Hang()
    {
        float rise = EaseCubicOut.Ease(Math.Clamp((Age + 1f) / RISE_TICKS, 0f, 1f));

        Vector2 wanted = Vector2.Lerp(origin, Hover, rise)
                         + Vector2.UnitY * (MathF.Sin(Age * 0.08f + Seed) * BOB * rise);

        Projectile.velocity = wanted - Projectile.Center;
        Projectile.rotation = MathF.Sin(Age * 0.05f + Seed) * 0.06f * rise;

        if (Main.dedServ || Age < RISE_TICKS || !Main.rand.NextBool(3))
            return;

        Dust mote = Dust.NewDustPerfect(Projectile.Center + Main.rand.NextVector2Circular(40f * Size, 40f * Size), DustID.GreenTorch, Vector2.Zero, Scale: 1.1f);

        mote.noGravity = true;
        mote.velocity = (Projectile.Center - mote.position) * 0.04f;
    }

    /// <summary>Aimed so the drop over the flight lands it on the target rather than short of it.</summary>
    private void Launch()
    {
        Projectile.tileCollide = true;

        int closest = Player.FindClosest(Projectile.position, Projectile.width, Projectile.height);
        Vector2 toTarget = Main.player[closest].Center - Projectile.Center;

        float flight = Math.Clamp(toTarget.Length() / (Boulder ? LAUNCH_SPEED_BOULDER : LAUNCH_SPEED), 1f, MAX_FLIGHT_TICKS);

        Vector2 velocity = toTarget / flight;

        velocity.Y -= 0.5f * GRAVITY * flight;

        Projectile.velocity = velocity;

        if (Main.netMode != NetmodeID.MultiplayerClient)
            Projectile.netUpdate = true;

        SoundEngine.PlaySound(LaunchSound, Projectile.Center);
    }

    private void Fly()
    {
        // Server-authoritative: the kill syncs, so every client shatters it where the server saw it land.
        if (Main.netMode != NetmodeID.MultiplayerClient && TouchingPlayer())
        {
            impactVelocity = Projectile.velocity;
            Projectile.Kill();

            return;
        }

        Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + GRAVITY, MAX_FALL);
        // A heavier disc turns slower.
        Projectile.rotation += SPIN / Size * Math.Sign(Projectile.velocity.X);
    }

    /// <summary>
    ///     Checked a tick after contact: <c>Damage</c> runs after <c>AI</c>, so the hit has already landed by the time
    ///     this sees the overlap.
    /// </summary>
    private bool TouchingPlayer()
    {
        foreach (Player player in Main.ActivePlayers)
        {
            if (!player.dead && Projectile.Hitbox.Intersects(player.Hitbox))
                return true;
        }

        return false;
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        impactVelocity = oldVelocity;

        return true;
    }

    public override bool CanHitPlayer(Player target) => Launched;

    public override void OnKill(int timeLeft)
    {
        if (Main.dedServ)
            return;

        Vector2 velocity = impactVelocity == Vector2.Zero ? Projectile.velocity : impactVelocity;

        SoundEngine.PlaySound(ShatterSound, Projectile.Center);
        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.8f, Pitch = -0.2f }, Projectile.Center);

        if (cluster is not null)
            TileClusterShards.Shatter(cluster, Projectile.Center, Projectile.rotation, velocity);

        if (Boulder && Main.LocalPlayer.DistanceSQ(Projectile.Center) < 1400f * 1400f)
            CameraSystem.shake = Math.Max(CameraSystem.shake, 10);

        for (int i = 0; i < (int)(12 * Size); i++)
        {
            Dust.NewDustPerfect(
                Projectile.Center + Main.rand.NextVector2Circular(30f * Size, 30f * Size),
                DustID.Smoke,
                Main.rand.NextVector2Circular(2.5f, 2.5f),
                120,
                Color.White,
                Main.rand.NextFloat(1.1f, 1.7f)
            );
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (cluster is null)
            return false;

        Vector2 centre = Projectile.Center;

        if (!Launched && HangTicks - Age <= TELL_TICKS)
            centre += Main.rand.NextVector2Circular(TELL_SHAKE, TELL_SHAKE);

        DrawBeam(Main.spriteBatch);

        cluster.Draw(Main.spriteBatch, centre, Projectile.rotation, 1f, wobble);

        return false;
    }

    /// <summary>A line from the Warden's eye to the chunk over its first few ticks.</summary>
    private void DrawBeam(SpriteBatch spriteBatch)
    {
        if (Age >= BEAM_TICKS || caster < 0 || Main.npc[caster] is not { active: true, ModNPC: WoodenWardenBoss warden })
            return;

        float fade = 1f - Age / BEAM_TICKS;

        Vector2 from = warden.EyePosition;
        Vector2 to = Projectile.Center;

        Texture2D pixel = TextureAssets.MagicPixel.Value;
        var source = new Rectangle(0, 0, 1, 1);
        float angle = (to - from).ToRotation();
        float length = Vector2.Distance(from, to);

        spriteBatch.Draw(pixel, from - Main.screenPosition, source, Additive(0.3f * fade), angle, new Vector2(0f, 0.5f), new Vector2(length, 8f * fade), SpriteEffects.None, 0f);
        spriteBatch.Draw(pixel, from - Main.screenPosition, source, Additive(0.9f * fade), angle, new Vector2(0f, 0.5f), new Vector2(length, 2f), SpriteEffects.None, 0f);
    }

    private static Color Additive(float opacity) => (TileClusterShards.EdgeColor * opacity) with { A = 0 };

    public override void SendExtraAI(BinaryWriter writer)
    {
        writer.Write(radius);
        writer.WriteVector2(origin);
        writer.Write((short)caster);

        writer.Write(cluster is not null);
        cluster?.Write(writer);
    }

    public override void ReceiveExtraAI(BinaryReader reader)
    {
        radius = reader.ReadSingle();

        // Set directly rather than resized: the synced position is already the corner of the server's resized hitbox.
        Projectile.width = Projectile.height = HitboxFor(radius);

        origin = reader.ReadVector2();
        caster = reader.ReadInt16();

        cluster = reader.ReadBoolean() ? TileCluster.Read(reader) : null;
    }
}

/// <summary>Carries the disc radius from <see cref="WardenTileCluster.Conjure" /> into <c>OnSpawn</c>, which runs before the first sync.</summary>
internal sealed class ClusterSource(NPC caster, float radius) : EntitySource_Parent(caster)
{
    public float Radius => radius;
}
