using Terraria.ObjectData;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

public class TemperatePlants : ModTile
{
    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();

        Main.tileCut[Type] = true;
        Main.tileSolid[Type] = false;
        Main.tileFrameImportant[Type] = true;

        TileID.Sets.SwaysInWindBasic[Type] = true;
        TileID.Sets.IgnoredByGrowingSaplings[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);

        TileObjectData.newTile.AnchorValidTiles =
        [
            ModContent.TileType<HighlandsGrass>(),
            ModContent.TileType<SnowHighlandsGrass>(),
            ModContent.TileType<CorruptHighlandsGrass>(),
            ModContent.TileType<CrimsonHighlandsGrass>(),
            ModContent.TileType<HallowHighlandsGrass>(),
            Type
        ];

        TileObjectData.newTile.LavaDeath = true;
        TileObjectData.newTile.WaterDeath = false;

        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.StyleWrapLimit = 45;
        TileObjectData.newTile.CoordinateHeights = [36];
        TileObjectData.newTile.DrawYOffset = -14;
        TileObjectData.addTile(Type);

        DustType = DustID.GrassBlades;
        HitSound = SoundID.Grass;

        AddMapEntry(new Color(20, 141, 105));
    }

    public override void NumDust(int i, int j, bool fail, ref int num)
    {
        base.NumDust(i, j, fail, ref num);

        num = fail ? 1 : 3;
    }

    public override bool TileFrame(int i, int j, ref bool resetFrame, ref bool noBreak)
    {
        var tile = Framing.GetTileSafely(i, j);
        var anchorTile = Framing.GetTileSafely(i, j + 1);

        int frameOffset = 0;

        if (anchorTile.HasTile)
        {
            int anchorType = anchorTile.TileType;

            if (anchorType == ModContent.TileType<SnowHighlandsGrass>())
                frameOffset = 36;
            else if (anchorType == ModContent.TileType<CorruptHighlandsGrass>())
                frameOffset = 72;
            else if (anchorType == ModContent.TileType<CrimsonHighlandsGrass>())
                frameOffset = 108;
            else if (anchorType == ModContent.TileType<HallowHighlandsGrass>())
                frameOffset = 144;
        }

        tile.TileFrameY = (short)frameOffset;
        return base.TileFrame(i, j, ref resetFrame, ref noBreak);
    }

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects)
    {
        base.SetSpriteEffects(i, j, ref spriteEffects);

        spriteEffects = i % 2 == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
    }
}
