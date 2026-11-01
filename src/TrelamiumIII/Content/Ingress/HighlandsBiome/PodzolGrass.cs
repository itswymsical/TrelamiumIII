using System.Collections.Generic;
using Terraria.DataStructures;
using TrelamiumIII.Common.Globals;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

/// <summary>
/// The taiga's floor, grass gone brown under a litter of needles. Borrows the highlands grass sheet, browned by
/// <see cref="Tint"/>, until it has its own sprite.
/// </summary>
public class PodzolGrass : GrassTile
{
    private static readonly Color Tint = new(204, 156, 108);

    public override string Texture => $"{nameof(TrelamiumIII)}/Content/Ingress/HighlandsBiome/HighlandsGrass";

    protected override int DirtType => ModContent.TileType<PeatTile>();

    /// <summary> Temperate plants don't root in podzol, so nothing sprouts from it on its own. </summary>
    public override List<int> PlantTypes => [ModContent.TileType<TemperatePlants>()];

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;

        MergeGrass(TileID.Stone, TileID.Dirt, Type, ModContent.TileType<PeatTile>(), ModContent.TileType<HighlandsGrass>());
        Main.tileBlockLight[Type] = true;

        DustType = DustID.Mud;
        HitSound = SoundID.Dig;

        VanillaFallbackOnModDeletion = TileID.Grass;
        TileID.Sets.Conversion.Grass[Type] = true;

        AddMapEntry(new Color(112, 92, 50));
    }

    public override void DrawEffects(int i, int j, SpriteBatch spriteBatch, ref TileDrawInfo drawData) => drawData.colorTint = Tint;
}
