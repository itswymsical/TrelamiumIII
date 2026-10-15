using System.Collections.Generic;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ObjectData;
using TrelamiumIII.Common.Globals;
using TrelamiumIII.Common.Systems;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

public class HighlandsGrass : GrassTile
{
    protected override int DirtType => ModContent.TileType<PeatTile>();
    public override List<int> PlantTypes => [ModContent.TileType<TemperatePlants>()];
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

        AddMapEntry(new Color(35, 120, 84));
    }
}

public class SnowHighlandsGrass : GrassTile
{
    protected override int DirtType => ModContent.TileType<PeatTile>();
    public override List<int> PlantTypes => [ModContent.TileType<TemperatePlants>()];
    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;

        MergeGrass(TileID.IceBlock, TileID.SnowBlock, Type, ModContent.TileType<HighlandsGrass>());
        Main.tileBlockLight[Type] = true;
        TileID.Sets.Conversion.Grass[Type] = true;

        DustType = DustID.Mud;
        HitSound = SoundID.Dig;

        VanillaFallbackOnModDeletion = TileID.SnowBlock;

        AddMapEntry(new Color(190, 223, 232));
    }

    public override bool HasWalkDust()
    {
        return true;
    }

    public override void WalkDust(ref int dustType, ref bool makeDust, ref Color color)
    {
        dustType = DustID.Snow;
    }
    public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        if (!effectOnly)
        {
            fail = true;
            Framing.GetTileSafely(i, j).TileType = (ushort)ModContent.TileType<HighlandsGrass>();
        }
    }
}

public class CorruptHighlandsGrass : GrassTile
{
    protected override int DirtType => ModContent.TileType<PeatTile>();
    public override List<int> PlantTypes => [ModContent.TileType<TemperatePlants>()];
    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;

        MergeGrass(TileID.IceBlock, TileID.SnowBlock, Type, ModContent.TileType<HighlandsGrass>());
        Main.tileBlockLight[Type] = true;
        TileID.Sets.Conversion.Grass[Type] = true;
        DustType = DustID.CorruptPlants;
        HitSound = SoundID.Dig;

        VanillaFallbackOnModDeletion = TileID.CorruptGrass;

        //TileID.Sets.CorruptBiome[Type] = Type;
        AddMapEntry(new Color(144, 124, 197));
    }

    public override bool HasWalkDust()
    {
        return true;
    }

    public override void WalkDust(ref int dustType, ref bool makeDust, ref Color color)
    {
        dustType = DustID.CorruptPlants;
    }
    public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        if (!effectOnly)
        {
            fail = true;
            Framing.GetTileSafely(i, j).TileType = (ushort)ModContent.TileType<PeatTile>();
        }
    }
}

public class CrimsonHighlandsGrass : GrassTile
{
    protected override int DirtType => ModContent.TileType<PeatTile>();
    public override List<int> PlantTypes => [ModContent.TileType<TemperatePlants>()];
    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;

        MergeGrass(TileID.IceBlock, TileID.SnowBlock, Type, ModContent.TileType<HighlandsGrass>());
        Main.tileBlockLight[Type] = true;
        TileID.Sets.Conversion.Grass[Type] = true;

        DustType = DustID.CrimsonPlants;
        HitSound = SoundID.Dig;

        VanillaFallbackOnModDeletion = TileID.CrimsonGrass;

        //TileID.Sets.CrimsonBiome[Type] = Type;
        AddMapEntry(new Color(238, 98, 98));
    }

    public override bool HasWalkDust()
    {
        return true;
    }

    public override void WalkDust(ref int dustType, ref bool makeDust, ref Color color)
    {
        dustType = DustID.CrimsonPlants;
    }
    public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        if (!effectOnly)
        {
            fail = true;
            Framing.GetTileSafely(i, j).TileType = (ushort)ModContent.TileType<PeatTile>();
        }
    }
}

public class HallowHighlandsGrass : GrassTile
{
    protected override int DirtType => ModContent.TileType<PeatTile>();
    public override List<int> PlantTypes => [ModContent.TileType<TemperatePlants>()];
    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;

        MergeGrass(TileID.IceBlock, TileID.SnowBlock, Type, ModContent.TileType<HighlandsGrass>());
        Main.tileBlockLight[Type] = true;
        TileID.Sets.Conversion.Grass[Type] = true;

        DustType = DustID.HallowedPlants;
        HitSound = SoundID.Dig;

        VanillaFallbackOnModDeletion = TileID.HallowedGrass;

        //TileID.Sets.HallowBiome[Type] = Type;
        AddMapEntry(new Color(6, 247, 253));
    }

    public override bool HasWalkDust()
    {
        return true;
    }

    public override void WalkDust(ref int dustType, ref bool makeDust, ref Color color)
    {
        dustType = DustID.HallowedPlants;
    }
    public override void KillTile(int i, int j, ref bool fail, ref bool effectOnly, ref bool noItem)
    {
        if (!effectOnly)
        {
            fail = true;
            Framing.GetTileSafely(i, j).TileType = (ushort)ModContent.TileType<PeatTile>();
        }
    }
}

public class TemperateRockTile : ModTile
{
    public override void SetStaticDefaults()
    {
        const int height = 34;

        Main.tileSolid[Type] = false;
        Main.tileBlockLight[Type] = false;
        Main.tileFrameImportant[Type] = true;
        Main.tileNoFail[Type] = true;

        TileID.Sets.BreakableWhenPlacing[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);

        TileObjectData.newTile.CoordinateWidth = 48;
        TileObjectData.newTile.CoordinateHeights = [height, 0];

        TileObjectData.newTile.DrawYOffset = -2;
        TileObjectData.newTile.Origin = new(1, 1);

        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, 1, 0);
        TileObjectData.newTile.AnchorValidTiles =
        [
            ModContent.TileType<HighlandsGrass>(),
            TileID.Stone
        ];

        TileObjectData.newTile.StyleHorizontal = true;

        TileObjectData.addTile(Type);

        AddMapEntry(new Color(100, 92, 80));
        DustType = DustID.Stone;
    }

    public override void NumDust(int i, int j, bool fail, ref int num)
    {
        base.NumDust(i, j, fail, ref num);

        num = fail ? 1 : 3;
    }

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects)
    {
        base.SetSpriteEffects(i, j, ref spriteEffects);

        spriteEffects = i % 2 == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
    }
}