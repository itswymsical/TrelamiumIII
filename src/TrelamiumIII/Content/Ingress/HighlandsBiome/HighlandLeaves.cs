using Terraria.DataStructures;
using TrelamiumIII.Content.Ingress.HighlandsBiome.BirchFurniture;
using TrelamiumIII.Content.Ingress.HighlandsBiome.SpruceFurniture;
using TrelamiumIII.Utilities.Extensions;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

/// <summary>
/// Leaf blocks of the highland living trees. Both borrow vanilla's leaf block and tint it per tile, until they have their
/// own sprites; the tint drifts with a coarse hash so the canopy reads as clumps instead of one flat colour.
/// </summary>
public abstract class HighlandLeafTile : ModTile
{
    /// <summary> Tiles across one clump of <see cref="Shade"/>. </summary>
    protected virtual int ClumpSize => 5;

    public override string Texture => $"Terraria/Images/Tiles_{TileID.LeafBlock}";

    protected abstract Color MapColor { get; }

    /// <summary> Tint for a clump, from <paramref name="roll"/> in [0, 1). </summary>
    protected abstract Color Shade(float roll);

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileBlockLight[Type] = true;
        Main.tileMerge[Type][TileID.LeafBlock] = true;

        this.Merge(ModContent.TileType<SpruceWoodTile>(), ModContent.TileType<BirchWoodTile>(), TileID.LivingWood);

        TileID.Sets.IgnoredByGrowingSaplings[Type] = true;

        HitSound = SoundID.Grass;
        DustType = DustID.Grass;
        VanillaFallbackOnModDeletion = TileID.LeafBlock;

        AddMapEntry(MapColor);
    }

    public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData)
        => drawData.colorTint = Shade(Hash(i / ClumpSize, j / ClumpSize));

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

    protected override Color Shade(float roll) => roll < 0.8f
        ? Color.Lerp(new Color(210, 255, 170), new Color(240, 255, 200), roll / 0.8f)
        : new Color(255, 230, 150);
}
