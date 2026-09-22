using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ObjectData;

namespace TrelamiumIII.Content.Ingress;

public class RootFoliageTile : ModTile
{
    public override string Texture => $"Terraria/Images/Tiles_{TileID.JunglePlants2}";
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
            ModContent.TileType<IngressGrass>(),
            Type
        ];

        TileObjectData.newTile.LavaDeath = true;
        TileObjectData.newTile.WaterDeath = false;

        TileObjectData.newTile.StyleHorizontal = true;

        TileObjectData.newTile.CoordinateHeights = [36];
        TileObjectData.newTile.DrawYOffset = -14;
        TileObjectData.addTile(Type);

        DustType = DustID.JungleGrass;
        HitSound = SoundID.Grass;

        AddMapEntry(new Color(93, 106, 33));
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

public class RootFernTile : ModTile
{
    public override void SetStaticDefaults()
    {
        const int height = 26;

        Main.tileSolid[Type] = false;
        Main.tileBlockLight[Type] = false;
        Main.tileFrameImportant[Type] = true;
        Main.tileNoFail[Type] = true;
        Main.SettingsEnabled_TilesSwayInWind = true;
        TileID.Sets.SwaysInWindBasic[Type] = true;
        TileID.Sets.BreakableWhenPlacing[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);

        TileObjectData.newTile.CoordinateWidth = 34;
        TileObjectData.newTile.CoordinateHeights = [height];

        TileObjectData.newTile.DrawYOffset = -(height - 18 - 2);

        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, 1, 0);

        TileObjectData.newTile.AnchorValidTiles =
        [
            ModContent.TileType<IngressGrass>(),
        ];

        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(93, 106, 33));
        DustType = DustID.JungleGrass;
        HitSound = SoundID.Grass;

    }

    public override void DropCritterChance(int i, int j, ref int wormChance, ref int grassHopperChance, ref int jungleGrubChance)
    {
        wormChance = 18;
        grassHopperChance = 12;
    }

    public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 8 : 12;

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects)
    {
        if (i % 2 == 1)
        {
            spriteEffects = SpriteEffects.FlipHorizontally;
        }
    }
}

public class RootRainplantTile : ModTile
{
    public override void SetStaticDefaults()
    {
        const int height = 34;

        Main.tileSolid[Type] = false;
        Main.tileBlockLight[Type] = false;
        Main.tileFrameImportant[Type] = true;
        Main.tileNoFail[Type] = true;
        Main.SettingsEnabled_TilesSwayInWind = true;
        TileID.Sets.SwaysInWindBasic[Type] = true;
        TileID.Sets.BreakableWhenPlacing[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);

        TileObjectData.newTile.CoordinateWidth = 32;
        TileObjectData.newTile.CoordinateHeights = [height];

        TileObjectData.newTile.DrawYOffset = -(height - 22);

        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, 1, 0);

        TileObjectData.newTile.AnchorValidTiles =
        [
            ModContent.TileType<IngressGrass>(),
        ];

        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(93, 106, 33));
        DustType = DustID.JungleGrass;
        HitSound = SoundID.Grass;

    }

    public override void DropCritterChance(int i, int j, ref int wormChance, ref int grassHopperChance, ref int jungleGrubChance)
    {
        wormChance = 18;
        grassHopperChance = 12;
    }

    public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 8 : 12;

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects)
    {
        if (i % 2 == 1)
        {
            spriteEffects = SpriteEffects.FlipHorizontally;
        }
    }
}

public class RootRockTile : ModTile
{
    public const int Width = 4;
    public const int Height = 3;
    private const int SinkDepth = 2;

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = false;
        Main.tileBlockLight[Type] = false;
        Main.tileFrameImportant[Type] = true;
        Main.tileNoFail[Type] = true;
        TileID.Sets.BreakableWhenPlacing[Type] = true;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x3);
        TileObjectData.newTile.Width = Width;
        TileObjectData.newTile.Height = Height;
        TileObjectData.newTile.Origin = new Point16(1, Height - 4);
        TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, Width, 0);
        TileObjectData.newTile.AnchorValidTiles = [ModContent.TileType<IngressGrass>()];
        TileObjectData.newTile.LavaDeath = true;
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(93, 106, 33));
        DustType = DustID.Stone;
        HitSound = SoundID.Dig;
    }

    public override void DropCritterChance(int i, int j, ref int wormChance, ref int grassHopperChance, ref int jungleGrubChance)
    {
        wormChance = 18;
        grassHopperChance = 12;
    }

    public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 8 : 12;

    public override bool PreDraw(int i, int j, SpriteBatch spriteBatch)
    {
        Tile tile = Main.tile[i, j];

        if (tile.TileFrameX != 0 || tile.TileFrameY != (Height - 1) * 18)
            return false;

        Texture2D texture = Terraria.GameContent.TextureAssets.Tile[Type].Value;
        Vector2 offscreen = Main.drawToScreen ? Vector2.Zero : new Vector2(Main.offScreenRange);
        var position = new Vector2(
            i * 16 + (Width * 16 - texture.Width) / 2 - (int)Main.screenPosition.X,
            (j + 1) * 16 + SinkDepth - texture.Height - (int)Main.screenPosition.Y);

        SpriteEffects effects = i % 2 == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
        Color light = Lighting.GetColor(i + Width / 2, j - Height / 2);

        spriteBatch.Draw(texture, position + offscreen, null, light, 0f, Vector2.Zero, 1f, effects, 0f);
        return false;
    }
}