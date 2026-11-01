using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Graphics.Fields.Layers;
using TrelamiumIII.Core.Graphics.Particles;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

public class GravelBlock : ModItem
{
    public override void SetDefaults()
        => Item.DefaultToPlaceableTile(ModContent.TileType<GravelTile>());
}

/// <summary> The highland footpaths. Kicks pebbles and dust up behind anyone running over it; see <see cref="GravelFootfall"/>. </summary>
public class GravelTile : ModTile
{
    /// <summary> Vanilla silt until gravel has its own sprite. </summary>
    public override string Texture => $"Terraria/Images/Tiles_{TileID.Silt}";

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;
        Main.tileBlockLight[Type] = true;

        this.Merge(TileID.Stone, TileID.ClayBlock, ModContent.TileType<PeatTile>(), ModContent.TileType<SlateTile>(), ModContent.TileType<HighlandsGrass>(),
            ModContent.TileType<SnowHighlandsGrass>(), ModContent.TileType<IngressGrass>());

        MineResist = 0.6f;
        DustType = DustID.Stone;
        HitSound = SoundID.Dig;
        VanillaFallbackOnModDeletion = TileID.Silt;

        AddMapEntry(new Color(132, 124, 112));
    }

    public override void FloorVisuals(Player player) => player.GetModPlayer<GravelFootfall>().Step();
}

/// <summary>
/// What a player's feet throw off gravel: pebbles in proportion to their speed, a dust cloud on a run, and a burst on a
/// hard landing. <see cref="GravelTile.FloorVisuals"/> calls <see cref="Step"/> on every grounded tick.
/// </summary>
public sealed class GravelFootfall : ModPlayer
{
    private const float ScuffSpeed = 1.6f;
    private const float PebblesPerSpeed = 0.14f;
    private const float RunSpeed = 3.4f;
    private const int DustCooldown = 7;

    /// <summary> Downward speed of the tick before touching down that counts as a hard landing. </summary>
    private const float LandingSpeed = 6f;

    private const int LandingPebbles = 9;
    private const int LandingPuffs = 3;

    /// <summary> Vertical speed at the end of the last tick; on the tick a player lands, the speed it fell at. </summary>
    private float lastFallSpeed;

    private int dustCooldown;

    public override void PostUpdate()
    {
        lastFallSpeed = Player.velocity.Y;

        if (dustCooldown > 0)
            dustCooldown--;
    }

    internal void Step()
    {
        if (Main.dedServ)
            return;

        Vector2 feet = Player.Bottom;

        if (lastFallSpeed > LandingSpeed)
        {
            Land(feet, lastFallSpeed);
            lastFallSpeed = 0f;
            return;
        }

        float speed = MathF.Abs(Player.velocity.X);

        if (speed < ScuffSpeed)
            return;

        float behind = -MathF.Sign(Player.velocity.X);

        if (Main.rand.NextFloat() < (speed - ScuffSpeed) * PebblesPerSpeed)
            KickPebble(feet, new Vector2(behind * Main.rand.NextFloat(0.4f, 1.1f) * speed * 0.45f, -Main.rand.NextFloat(1.2f, 2.8f)));

        if (speed < RunSpeed || dustCooldown > 0)
            return;

        dustCooldown = DustCooldown;
        FieldSystem.GetLayer<GravelDustField>()?.SpawnNode(
            feet + new Vector2(behind * Main.rand.NextFloat(4f, 10f), -4f),
            new Vector2(behind * Main.rand.NextFloat(0.4f, 1f), -Main.rand.NextFloat(0.2f, 0.6f)),
            radius: Main.rand.NextFloat(10f, 16f),
            life: Main.rand.NextFloat(28f, 42f));
    }

    /// <summary> Pebbles fanned both ways and a ring of dust, scaled by how hard the player came down. </summary>
    private static void Land(Vector2 feet, float fallSpeed)
    {
        float force = MathF.Min(fallSpeed / LandingSpeed, 2f);

        for (int i = 0; i < LandingPebbles * force; i++)
        {
            float side = Main.rand.NextBool() ? 1f : -1f;
            KickPebble(feet + new Vector2(Main.rand.NextFloat(-8f, 8f), 0f), new Vector2(side * Main.rand.NextFloat(0.6f, 2.6f), -Main.rand.NextFloat(1.5f, 3.5f) * force));
        }

        if (FieldSystem.GetLayer<GravelDustField>() is not { } dust)
            return;

        for (int i = 0; i < LandingPuffs; i++)
        {
            float side = i - (LandingPuffs - 1) * 0.5f;
            dust.SpawnNode(feet + new Vector2(side * 8f, -4f), new Vector2(side * 0.9f * force, -0.3f), radius: 14f * force, life: Main.rand.NextFloat(34f, 48f));
        }
    }

    private static void KickPebble(Vector2 position, Vector2 velocity)
        => ParticleSystem.Spawn<GravelPebbleParticle>(position + new Vector2(0f, -2f), velocity, Main.rand.Next(40, 70), rotationVelocity: Main.rand.NextFloat(-0.3f, 0.3f));
}

/// <summary> A chip of gravel: a couple of pixels of grey that bounce and roll to rest. </summary>
public sealed class GravelPebbleParticle : Particle
{
    private static readonly Color[] Shades = [new(156, 148, 134), new(122, 116, 106), new(182, 174, 158), new(98, 94, 90)];

    private Vector2 pixels;

    public override string[] Textures => ["Images/MagicPixel"];

    public override Vector2 Size => new(4f);

    public override float Gravity => 0.24f;

    public override float Bounce => 0.35f;

    public override float GroundFriction => 0.8f;

    public override int FadeOutTicks => 16;

    public override void OnSpawn()
    {
        Color = Shades[Main.rand.Next(Shades.Length)];
        pixels = new Vector2(Main.rand.Next(2, 4), Main.rand.Next(2, 4));
    }

    public override void OnTileCollide(Vector2 oldVelocity) => RotationVelocity *= 0.5f;

    public override bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor)
    {
        spriteBatch.Draw(texture, drawPosition, new Rectangle(0, 0, 1, 1), lightColor, Rotation, new Vector2(0.5f), pixels * Scale, SpriteEffects.None, 0f);
        return false;
    }
}
