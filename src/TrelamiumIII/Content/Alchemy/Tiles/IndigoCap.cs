using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ObjectData;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.Alchemy.Tiles;

/// <summary>The corruption's fungus, as bloodtooth is the crimson's. Grows on corrupt grass only.</summary>
public class IndigoCapItem : ModItem, IAlmanacDefined
{
    public override string Texture => AssetReferences.Content.Alchemy.Reagents.IndigoCapItem.KEY;

    AlmanacEntry IAlmanacDefined.AlmanacEntry => new()
    {
        AlmanacPotency = 4,
        Temperaments = Temperament.Acidic,
        Principle = PrincipleType.Specular,
        Rarity = ItemRarityID.Blue,
    };

    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.width = 24;
        Item.height = 26;
        Item.value = Item.sellPrice(copper: 15);
        Item.rare = ItemRarityID.Blue;
        Item.maxStack = Item.CommonMaxStack;
        Item.DefaultToPlaceableTile(ModContent.TileType<IndigoCapTileSmall>());
    }
}

public class IndigoCapTileSpores : ModTile
{
    private const int HEIGHT = 20;

    public override string Texture => AssetReferences.Content.Alchemy.Tiles.IndigoCapTile_Spores.KEY;

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoFail[Type] = true;
        Main.tileObsidianKill[Type] = true;

        DustType = DustID.CorruptPlants;
        HitSound = SoundID.Grass;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
        TileObjectData.newTile.CoordinateHeights = [HEIGHT];
        TileObjectData.newTile.DrawYOffset = -(HEIGHT - 18);
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.RandomStyleRange = 2;
        TileObjectData.addTile(Type);

        AddMapEntry(new Color(70, 84, 160));
    }

    public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 3;

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects)
        => spriteEffects = i % 2 == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
}

public class IndigoCapTileSmall : ModTile
{
    public override string Texture => AssetReferences.Content.Alchemy.Tiles.IndigoCapTile_Small.KEY;

    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = false;
        Main.tileMergeDirt[Type] = false;
        Main.tileBlockLight[Type] = false;
        Main.tileFrameImportant[Type] = true;
        Main.tileNoFail[Type] = true;
        Main.tileObsidianKill[Type] = true;

        DustType = DustID.CorruptPlants;
        HitSound = SoundID.Grass;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style1x1);
        TileObjectData.newTile.DrawYOffset = 2;
        TileObjectData.newTile.AnchorBottom = new AnchorData(AnchorType.SolidTile, 1, 0);
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.RandomStyleRange = 2;
        TileObjectData.addTile(Type);

        RegisterItemDrop(ModContent.ItemType<IndigoCapItem>());
        AddMapEntry(new Color(70, 84, 160));
    }

    public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 6;

    public override void SetSpriteEffects(int i, int j, ref SpriteEffects spriteEffects)
        => spriteEffects = i % 2 == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
}

public class IndigoCapTileLarge : ModTile
{
    public override string Texture => AssetReferences.Content.Alchemy.Tiles.IndigoCapTile_Large.KEY;

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoFail[Type] = true;
        Main.tileObsidianKill[Type] = true;

        DustType = DustID.CorruptPlants;
        HitSound = SoundID.Grass;

        TileObjectData.newTile.CopyFrom(TileObjectData.Style3x2);
        TileObjectData.newTile.DrawYOffset = 2;
        TileObjectData.newTile.StyleHorizontal = true;
        TileObjectData.newTile.RandomStyleRange = 2;
        TileObjectData.addTile(Type);

        RegisterItemDrop(ModContent.ItemType<IndigoCapItem>());
        AddMapEntry(new Color(70, 84, 160));
    }

    public override void NumDust(int i, int j, bool fail, ref int num) => num = fail ? 1 : 7;
}
