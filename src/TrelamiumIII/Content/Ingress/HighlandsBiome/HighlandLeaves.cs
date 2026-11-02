using Terraria.DataStructures;
using TrelamiumIII.Content.Ingress.HighlandsBiome.BirchFurniture;
using TrelamiumIII.Content.Ingress.HighlandsBiome.SpruceFurniture;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

/// <summary>
/// Leaf blocks of the highland living trees. Both borrow vanilla's leaf block and tint it per tile, until they have their
/// own sprites; the tint drifts with a coarse hash so the canopy reads as clumps instead of one flat colour. Sunlight passes
/// through them, since blocking it leaves the middle of a crown pitch black, and each leaf darkens under the ones over it
/// instead, which gives every bough a lit top and a shaded underside.
/// </summary>
public abstract class HighlandLeafTile : ModTile
{
    private const float ShadePerRow = 0.1f;
    private const int ShadedRows = 4;

    /// <summary> Tiles across one clump of <see cref="Shade"/>. </summary>
    protected virtual int ClumpSize => 5;

    public override string Texture => $"Terraria/Images/Tiles_{TileID.LeafBlock}";

    public static bool IsLeaf(Tile tile) => tile.HasTile && TileLoader.GetTile(tile.TileType) is HighlandLeafTile;

    protected abstract Color MapColor { get; }

    /// <summary> Tint for a clump, from <paramref name="roll"/> in [0, 1). </summary>
    protected abstract Color Shade(float roll);

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileBlockLight[Type] = false;
        Main.tileMerge[Type][TileID.LeafBlock] = true;

        this.Merge(ModContent.TileType<SpruceWoodTile>(), ModContent.TileType<BirchWoodTile>(), TileID.LivingWood);

        TileID.Sets.IgnoredByGrowingSaplings[Type] = true;

        HitSound = SoundID.Grass;
        DustType = DustID.Grass;
        VanillaFallbackOnModDeletion = TileID.LeafBlock;

        AddMapEntry(MapColor);
    }

    /// <summary> tModLoader turns this on for every solid tile once the static defaults have run. </summary>
    public override void PostSetDefaults() => Main.tileNoSunLight[Type] = false;

    public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
    {
        int covered = 0;

        while (covered < ShadedRows && Main.tile[i, j - covered - 1] is { HasTile: true } above && above.TileType == Type)
            covered++;

        drawData.colorTint = new Color(Shade(Hash(i / ClumpSize, j / ClumpSize)).ToVector3() * (1f - covered * ShadePerRow));
    }

    private static float Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177;
        return (h ^ (h >> 16)) / (float)uint.MaxValue;
    }
}

public class SpruceLeafTile : HighlandLeafTile
{
    protected override Color MapColor => new(36, 82, 70);

    /// <summary> Small clumps with a wide range, so the needles read as fine, dark-and-light texture rather than flat masses. </summary>
    protected override int ClumpSize => 2;

    protected override Color Shade(float roll) => Color.Lerp(new Color(62, 116, 118), new Color(132, 192, 158), roll);
}

public class BirchLeafTile : HighlandLeafTile
{
    protected override Color MapColor => new(118, 160, 70);

    /// <summary> Clumps a little smaller and further apart in shade, so a big crown doesn't read as one flat mass. </summary>
    protected override int ClumpSize => 4;

    protected override Color Shade(float roll) => roll < 0.8f
        ? Color.Lerp(new Color(165, 215, 130), new Color(240, 255, 200), roll / 0.8f)
        : new Color(255, 230, 150);
}
