using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.Enums;
using Terraria.ObjectData;
using TrelamiumIII.Core.UI.AlchemyStation;

namespace TrelamiumIII.Content.Items.Alchemy.AlchemyStations;

public class AlchemyStationItem : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 28;
        Item.height = 24;
        Item.maxStack = Item.CommonMaxStack;

        Item.value = Item.sellPrice(silver: 50);
        Item.DefaultToPlaceableTile(ModContent.TileType<AlchemyStationTile>());
    }
    public override void AddRecipes()
    {
        base.AddRecipes();

        CreateRecipe()
            .AddIngredient(ItemID.Wood, 10)
            .AddIngredient(ItemID.Bottle)
            .AddIngredient(ItemID.CookingPot)
            .AddTile(TileID.WorkBenches)
            .Register();
    }
}

public class AlchemyStationTile : ModTile
{
    /// <summary>Divisor applied to an output's <see cref="Core.Alchemy.AlmanacEntry.BrewTicks" />.</summary>
    public const float BrewSpeed = 1f;

    public override void SetStaticDefaults()
    {
        Main.tileFrameImportant[Type] = true;
        Main.tileNoAttach[Type] = true;
        Main.tileTable[Type] = true;
        Main.tileLavaDeath[Type] = true;

        TileID.Sets.DisableSmartCursor[Type] = true;

        DustType = DustID.WoodFurniture;
        AdjTiles = [TileID.Bottles];

        TileObjectData.newTile.Width = 4;
        TileObjectData.newTile.Height = 3;
        TileObjectData.newTile.Origin = new Point16(0, 2);
        TileObjectData.newTile.CoordinateWidth = 16;
        TileObjectData.newTile.CoordinateHeights = [16, 16, 16];
        TileObjectData.newTile.CoordinatePadding = 2;

        TileObjectData.newTile.UsesCustomCanPlace = true;
        TileObjectData.newTile.LavaDeath = true;

        TileObjectData.newTile.AnchorBottom = new AnchorData(
            AnchorType.SolidTile | AnchorType.Table,
            TileObjectData.newTile.Width,
            0);

        TileObjectData.addTile(Type);

        AddMapEntry(new Color(140, 200, 180), CreateMapEntryName());

        RegisterItemDrop(ModContent.ItemType<AlchemyStationItem>());
    }

    public override bool RightClick(int i, int j)
    {
        var player = Main.LocalPlayer;
        SoundEngine.PlaySound(SoundID.MenuTick);
        AlchemyStationSystem.ToggleUI(new Point16(i, j));
        player.mouseInterface = true;
        return true;
    }

    public override void MouseOver(int i, int j)
    {
        var player = Main.LocalPlayer;
        player.cursorItemIconEnabled = true;
        player.cursorItemIconID = ModContent.ItemType<AlchemyStationItem>();
    }
}
