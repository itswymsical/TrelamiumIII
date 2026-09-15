using System.Collections.Generic;
using TrelamiumIII.Common.Globals;
using TrelamiumIII.Common.Systems;

namespace TrelamiumIII.Content.Ingress;

public class IngressGrass : GrassTile
{
    protected override int DirtType => ModContent.TileType<PeatTile>();
    public override List<int> PlantTypes => [TileID.Plants];
    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;

        MergeGrass(TileID.Stone, TileID.Dirt, Type, ModContent.TileType<PeatTile>());
        Main.tileBlockLight[Type] = true;

        DustType = DustID.Mud;
        HitSound = SoundID.Dig;

        VanillaFallbackOnModDeletion = TileID.Grass;
        TileID.Sets.Conversion.Grass[Type] = true;
        WaterPlantAnchorSystem.AddAnchorAlias(Type, TileID.Grass);

        AddMapEntry(new Color(77, 122, 34));
    }
}