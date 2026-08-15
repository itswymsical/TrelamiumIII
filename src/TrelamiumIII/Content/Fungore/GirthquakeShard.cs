using Terraria.DataStructures;
using Terraria.GameContent;

namespace TrelamiumIII.Content.Fungore;

/// <summary>
///     A chunk of whatever <see cref="GirthquakeProj" /> just smashed, thrown out in the impact fan.
/// </summary>
/// <remarks>
///     <para>
///         The shard has no sprite of its own, it draws an 8x8 crop lifted straight out of the
///         struck tile's own sheet.
///     </para>
///     <para>
///         <see cref="Entity.ai" />[0] carries the tile type and [1] the crop to take from it. Both
///         come across in the spawn packet, so every client cuts the same shard out of the same
///         sheet without any extra syncing.
///     </para>
/// </remarks>
public class GirthquakeShardProj : ModProjectile
{

    public const int CHUNK_SEEDS = 36;

    public override string Texture => "Terraria/Images/Item_" + ItemID.StoneBlock;

    /// <summary>Pixel pitch of a tile sheet. accounts for the 2px of padding.</summary>
    private const int TILE_PITCH = 18;

    /// <summary>Side of the crop taken out of a frame. A quarter block reads as a shard; a full one reads as a dropped block.</summary>
    private const int CHUNK = 8;

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

        Projectile.penetrate = 1;

        Projectile.timeLeft = 150;
    }

    public override void OnSpawn(IEntitySource source)
    {
        // Spin from the throw rather than at random
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
        Texture2D texture = ResolveChunk(out Rectangle source);

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

    /// <summary>
    ///     Picks the crop this shard is cut from. The frames at the top-left of a standard tile sheet
    ///     are all fully opaque blocks whichever way the tile blends, so any of them is safe to
    ///     quarter; anything that does not fit that layout falls back to a stone block.
    /// </summary>
    private Texture2D ResolveChunk(out Rectangle source)
    {
        ushort tile = SourceTile;

        if (tile < TileLoader.TileCount)
        {
            Main.instance.LoadTiles(tile);

            if (TextureAssets.Tile[tile] is { Value: { } sheet })
            {
                int seed = ChunkSeed;

                source = new Rectangle(
                    seed % 3 * TILE_PITCH + seed / 9 % 2 * CHUNK,
                    seed / 3 % 3 * TILE_PITCH + seed / 18 % 2 * CHUNK,
                    CHUNK,
                    CHUNK
                );

                if (source.Right <= sheet.Width && source.Bottom <= sheet.Height)
                    return sheet;
            }
        }

        Main.instance.LoadItem(ItemID.StoneBlock);

        Texture2D fallback = TextureAssets.Item[ItemID.StoneBlock].Value;
        source = fallback.Bounds;

        return fallback;
    }
}
