using System.Collections.Generic;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;

namespace TrelamiumIII.Core.Projectiles;

/// <summary>
/// A tool thrown as a boomerang that mines the tiles it passes through.
/// <see cref="Entity.ai"/>[2] is the source item type, which supplies the sprite, the pick power and the
/// <see cref="ThrownToolProfile"/>, so the spawn packet is enough to sync it.
/// </summary>
/// <remarks>
///     Flight is vanilla's boomerang AI, which owns <c>ai[0]</c> and <c>ai[1]</c> - hence the item type
///     riding in <c>ai[2]</c>. Mining and its tile packets are owner-side; every client runs the flight.
/// </remarks>
public class ThrownTool : ModProjectile
{
    /// <summary>Pixels of travel per mining sample. Under a tile, so no tile on the path is stepped over.</summary>
    private const float SAMPLE_SPACING = 6f;

    private static readonly Dictionary<int, ThrownToolProfile> Profiles = [];

    /// <summary>Registers the profile an item is thrown with. Call from the rework's <c>Load</c>.</summary>
    public static void Register(int itemType, in ThrownToolProfile profile) => Profiles[itemType] = profile;

    public static ThrownToolProfile ProfileFor(int itemType)
        => Profiles.TryGetValue(itemType, out ThrownToolProfile profile) ? profile : ThrownToolProfile.Pickaxe;

    public override void Unload() => Profiles.Clear();

    /// <remarks>Placeholder: <see cref="PreDraw"/> draws the source item instead.</remarks>
    public override string Texture => "Terraria/Images/Item_" + ItemID.CopperPickaxe;

    public int SourceItem => (int)Projectile.ai[2];

    private ThrownToolProfile Profile => ProfileFor(SourceItem);

    private Player Owner => Main.player[Projectile.owner];

    /// <summary>Pick power of the source item, read from the sample so a prefixed copy still matches.</summary>
    private int PickPower
        => ContentSamples.ItemsByType.TryGetValue(SourceItem, out Item? sample) ? sample.pick : 0;

    /// <summary>Last tile the head occupied, so each tile is mined once per pass rather than once per tick.</summary>
    private ref float MinedX => ref Projectile.localAI[0];

    private ref float MinedY => ref Projectile.localAI[1];

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 22;

        Projectile.aiStyle = ProjAIStyleID.Boomerang;

        // A plain boomerang, so none of vanilla's per-type dust or sound branches in that style apply.
        AIType = ProjectileID.WoodenBoomerang;

        Projectile.DamageType = DamageClass.Melee;

        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.ignoreWater = true;

        // It cuts through terrain, so bouncing off it would strand the tool in the wall it just opened.
        Projectile.tileCollide = false;

        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = 20;

        Projectile.timeLeft = 600;
    }

    public override void OnSpawn(IEntitySource source)
    {
        MinedX = MinedY = -1f;

        if (Profile.ThrowSound is { } sound)
            SoundEngine.PlaySound(sound, Projectile.Center);
    }

    /// <remarks>Runs after vanilla's boomerang AI has moved the tool this tick.</remarks>
    public override void AI()
    {
        Player owner = Owner;

        if (!owner.active || owner.dead)
        {
            Projectile.Kill();

            return;
        }

        if (Projectile.owner == Main.myPlayer)
            Mine(owner, Profile);
    }

    /// <summary>
    /// Mines along this tick's travel, once per tile entered. AI runs before the projectile is moved, so
    /// the segment ahead is what has not been cut yet; sampling it rather than the head alone is what
    /// keeps a fast throw from leaving gaps on the diagonal.
    /// </summary>
    private void Mine(Player owner, in ThrownToolProfile profile)
    {
        int pick = PickPower;

        if (pick <= 0 || owner.noBuilding)
            return;

        Vector2 start = Projectile.Center;
        Vector2 travel = Projectile.velocity;

        int samples = (int)(travel.Length() / SAMPLE_SPACING) + 1;

        for (int i = 0; i <= samples; i++)
        {
            Point head = (start + travel * (i / (float)samples)).ToTileCoordinates();

            // Consecutive samples usually land in the tile just cut; only a new one is worth the walk.
            if (head.X == (int)MinedX && head.Y == (int)MinedY)
                continue;

            MinedX = head.X;
            MinedY = head.Y;

            MineAround(owner, head, pick, profile);
        }
    }

    private static void MineAround(Player owner, Point head, int pick, in ThrownToolProfile profile)
    {
        int radius = profile.MineRadius;

        for (int x = head.X - radius; x <= head.X + radius; x++)
        {
            for (int y = head.Y - radius; y <= head.Y + radius; y++)
            {
                if (!WorldGen.InWorld(x, y, 1))
                    continue;

                Tile tile = Main.tile[x, y];

                // A pickaxe cannot take down what wants an axe or a hammer, the same as at the cursor.
                if (!tile.HasTile || Main.tileAxe[tile.TileType] || Main.tileHammer[tile.TileType])
                    continue;

                for (int hit = 0; hit < profile.MineHits; hit++)
                    owner.PickTile(x, y, pick);
            }
        }
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Main.instance.LoadItem(SourceItem);

        Texture2D texture = TextureAssets.Item[SourceItem].Value;

        Main.EntitySpriteDraw(
            texture,
            Projectile.Center - Main.screenPosition,
            null,
            Projectile.GetAlpha(lightColor),
            Projectile.rotation,
            texture.Size() * 0.5f,
            Projectile.scale,
            Projectile.direction < 0 ? SpriteEffects.FlipHorizontally : SpriteEffects.None,
            0
        );

        return false;
    }
}
