using TrelamiumIII.Core.Graphics.Particles;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Lodestone;

/// <summary>
///     draws an 8x8 crop lifted straight out of the struck
///     tile's sheet, the same trick <see cref="Fungore.EarthShardProj" /> uses and now the
///     same code.
/// <para>
///     See <see cref="TileTextureUtils.ResolveChunk" />.
/// </para>
/// </summary>
internal sealed class MagnetizedChip : Particle
{
    /// <summary>Inward acceleration per tick.</summary>
    private const float PULL = 0.85f;

    private const float MAX_SPEED = 16f;

    /// <summary>Distance at which the chip has arrived and is consumed.</summary>
    private const float ARRIVAL = 20f;

    public override string[] Textures => ["Terraria/Images/Item_" + ItemID.StoneBlock];

    public override float Gravity => 0f;

    public override float Drag => 0.99f;

    public override bool TileCollide => false;

    public override bool LiquidCollide => false;

    public override bool UseLighting => true;

    public override int FadeOutTicks => 10;

    public ushort TileType;

    public int ChunkSeed;

    public Entity? Host;

    private float spinDirection = 1f;

    public override void OnSpawn() => spinDirection = Main.rand.NextBool() ? 1f : -1f;

    public override void Update()
    {
        if (Host is not { active: true } host)
        {
            Velocity.Y += 0.34f;

            return;
        }

        Vector2 toHost = host.Center - Position;
        float distance = toHost.Length();

        if (distance <= ARRIVAL)
        {
            Kill();
            return;
        }

        Vector2 pull = toHost / distance;

        Velocity += pull * PULL;

        if (Velocity.LengthSquared() > MAX_SPEED * MAX_SPEED)
            Velocity = Velocity.SafeNormalize(Vector2.Zero) * MAX_SPEED;

        RotationVelocity = Velocity.Length() * 0.03f * spinDirection;
    }

    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor)
    {
        Texture2D sheet = TileTextureUtils.ResolveChunk(TileType, ChunkSeed, out Rectangle source);

        spriteBatch.Draw(
            sheet,
            drawPosition,
            source,
            lightColor,
            Rotation,
            source.Size() * 0.5f,
            Scale,
            SpriteEffects.None,
            0f
        );

        return false;
    }

    public static void Strike(Entity host, Vector2 from, Vector2 away, ushort tileType)
    {
        if (Main.dedServ)
            return;

        int seed = Main.rand.Next(TileTextureUtils.CHUNK_SEEDS);

        var chip = ParticleSystem.Spawn<MagnetizedChip>(
            from + Main.rand.NextVector2Circular(6f, 6f),
            away.SafeNormalize(Vector2.UnitY).RotatedByRandom(1.1f) * Main.rand.NextFloat(1.5f, 4.5f),
            timeLeft: Main.rand.Next(35, 60),
            scale: Main.rand.NextFloat(0.7f, 1.15f),
            rotation: Main.rand.NextFloat(MathHelper.TwoPi),
            rotationVelocity: Main.rand.NextFloat(-0.3f, 0.3f)
        );

        if (chip is null)
            return;

        chip.TileType = tileType;
        chip.ChunkSeed = seed;
        chip.Host = host;
    }
}
