using TrelamiumIII.Content.Alchemy.Solutions;
using TrelamiumIII.Core;

namespace TrelamiumIII.Content.Alchemy.Reagents;

/// <summary>
///     The raw mineral tier of the conventional reagent line. Each is roasted or boiled out of
///     something the world already provides, so this tier adds no ores, tiles or worldgen.
/// </summary>
/// <remarks>
///     Design record: <c>docs/Alchemy/ConventionalReagents.md</c>. Star ratings live on the
///     almanac entry, not here.
/// </remarks>
public abstract class MineralReagentItem : ModItem
{
    public override string Texture => AssetReferences.Assets.Textures.PlaceholderSolute.KEY;

    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.White;
        Item.maxStack = Item.CommonMaxStack;
    }
}

public class SulfurItem : MineralReagentItem
{
    public override string Texture => AssetReferences.Content.Alchemy.Reagents.Sulfur.KEY;
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.value = Item.sellPrice(copper: 40);
        Item.DefaultToPlaceableTile(ModContent.TileType<SulfurTile>());
    }

    public override void AddRecipes()
    {
        base.AddRecipes();

        CreateRecipe(2)
            .AddIngredient(ItemID.AshBlock, 10)
            .AddTile(TileID.Furnaces)
            .Register();
    }
}

public class SulfurTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;

        Main.tileMerge[TileID.Stone][Type] = true;
        Main.tileMerge[Type][TileID.Stone] = true;

        Main.tileBlockLight[Type] = true;

        DustType = DustID.GreenMoss;
        MineResist = 0.25f;
        HitSound = SoundID.Dig;

        AddMapEntry(new Color(94, 64, 56));
    }
}

/// <summary>
///     Saltpetre. Barely burns on its own; makes everything beside it burn harder. Six parts of
///     this to one of fuel is what black powder is.
/// </summary>
public class NiterItem : MineralReagentItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.value = Item.sellPrice(copper: 60);
    }

    public override void AddRecipes()
    {
        base.AddRecipes();

        CreateRecipe(2)
            .AddIngredient(ItemID.RottenChunk, 3)
            .AddIngredient(ItemID.StoneBlock, 5)
            .AddTile(TileID.Furnaces)
            .Register();

        CreateRecipe(2)
            .AddIngredient(ItemID.Vertebrae, 3)
            .AddIngredient(ItemID.StoneBlock, 5)
            .AddTile(TileID.Furnaces)
            .Register();
    }
}

/// <summary>
///     Boiled out of brine, which until now had a recipe and nothing to do with itself.
/// </summary>
public class RockSaltItem : MineralReagentItem
{
    public override string Texture => AssetReferences.Content.Alchemy.Reagents.RockSalt.KEY;
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.value = Item.sellPrice(copper: 20);
    }

    public override void AddRecipes()
    {
        base.AddRecipes();

        CreateRecipe(3)
            .AddIngredient<BrineBottleItem>()
            .AddTile(TileID.Furnaces)
            .Register();
    }
}

/// <summary>
///     Burnt limestone. Meets water and gets hot about it.
/// </summary>
public class QuicklimeItem : MineralReagentItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.value = Item.sellPrice(copper: 50);
    }

    public override void AddRecipes()
    {
        base.AddRecipes();

        CreateRecipe(2)
            .AddIngredient(ItemID.MarbleBlock, 5)
            .AddTile(TileID.Furnaces)
            .Register();
    }
}

public class BoneAshItem : MineralReagentItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.value = Item.sellPrice(copper: 50);
    }

    public override void AddRecipes()
    {
        base.AddRecipes();

        CreateRecipe(2)
            .AddIngredient(ItemID.Bone, 5)
            .AddTile(TileID.Furnaces)
            .Register();
    }
}

public class PotashItem : MineralReagentItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.rare = ItemRarityID.Gray;
        Item.value = Item.sellPrice(copper: 10);
    }

    public override void AddRecipes()
    {
        base.AddRecipes();

        CreateRecipe(2)
            .AddIngredient(ItemID.Wood, 20)
            .AddTile(TileID.Furnaces)
            .Register();
    }
}
