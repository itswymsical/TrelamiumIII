using Terraria.DataStructures;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Fungore;

/// <summary>
///     A chunk of whatever <see cref="GirthquakeProj" /> smashed, drawn as an 8x8 crop of the struck tile's sheet.
///     <see cref="Entity.ai" />[0] is the tile type and [1] the crop, so the spawn packet is enough to sync it.
/// </summary>
public class EarthShardProj : ModProjectile
{

    public const int CHUNK_SEEDS = TileTextureUtils.CHUNK_SEEDS;

    public override string Texture => "Terraria/Images/Item_" + ItemID.StoneBlock;

    private const float GRAVITY = 0.32f;
    private const float MAX_FALL = 14f;
    private const float AIR_DRAG = 0.992f;

    /// <summary>Speed kept per bounce, and how many before it has spent itself.</summary>
    private const float BOUNCE = 0.42f;
    private const int MAX_BOUNCES = 2;

    private ushort SourceTile => (ushort)Projectile.ai[0];

    private int ChunkSeed => (int)Projectile.ai[1];

    /// <summary>Local because its derived from physics every client already runs.</summary>
    private ref float Bounces => ref Projectile.localAI[0];

    private ref float Spin => ref Projectile.localAI[1];

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 10;

        Projectile.aiStyle = -1;
        Projectile.DamageType = DamageClass.Melee;

        Projectile.friendly = true;
        Projectile.tileCollide = true;
        Projectile.ignoreWater = false;

        Projectile.penetrate = 2;

        Projectile.timeLeft = 150;
    }

    public override void OnSpawn(IEntitySource source)
    {
        Spin = Projectile.velocity.X * 0.02f + 0.09f * Math.Sign(Projectile.velocity.X);

        Projectile.scale = 0.8f + ChunkSeed / (float)CHUNK_SEEDS * 0.6f;
        Projectile.rotation = ChunkSeed * MathHelper.TwoPi / CHUNK_SEEDS;
    }

    public override void AI()
    {
        Projectile.velocity.Y = MathHelper.Min(Projectile.velocity.Y + GRAVITY, MAX_FALL);
        Projectile.velocity.X *= AIR_DRAG;

        Projectile.rotation += Spin;
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        Collision.HitTiles(Projectile.position, oldVelocity, Projectile.width, Projectile.height);

        if (++Bounces > MAX_BOUNCES)
            return true;

        if (Projectile.velocity.X != oldVelocity.X)
            Projectile.velocity.X = -oldVelocity.X * BOUNCE;

        if (Projectile.velocity.Y != oldVelocity.Y)
            Projectile.velocity.Y = -oldVelocity.Y * BOUNCE;

        // Losing spin with speed, so a shard that has stopped skipping is not still whirling.
        Spin *= BOUNCE;

        return false;
    }

    public override void OnKill(int timeLeft)
    {
        for (int i = 0; i < 3; i++)
            Dust.NewDustPerfect(Projectile.Center, DustID.Stone, Main.rand.NextVector2Circular(2f, 2f), 100, Color.White, 0.9f);
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TileTextureUtils.ResolveChunk(SourceTile, ChunkSeed, out Rectangle source);

        Main.EntitySpriteDraw(
            texture,
            Projectile.Center - Main.screenPosition,
            source,
            Projectile.GetAlpha(lightColor),
            Projectile.rotation,
            source.Size() * 0.5f,
            Projectile.scale,
            SpriteEffects.None,
            0
        );

        return false;
    }
}
