using TrelamiumIII.Common.Systems;

namespace TrelamiumIII.Content.FarmingOverhaul.Seedbeds;

// Items
public class SeedBedItem_Dirt : SeedBedItem
{
    protected override int PlaceTileType => ModContent.TileType<SeedBedTile_Dirt>();
    protected override Recipe BuildRecipe()
        => CreateRecipe(2)
            .AddIngredient(ItemID.DirtBlock, 5)
            .AddIngredient(ItemID.RedBrick, 2)
            .AddTile(TileID.WorkBenches);
}

public class SeedBedItem_Mud : SeedBedItem
{
    protected override int PlaceTileType => ModContent.TileType<SeedBedTile_Mud>();
    protected override Recipe BuildRecipe()
        => CreateRecipe(2)
            .AddIngredient(ItemID.MudBlock, 5)
            .AddIngredient(ItemID.RedBrick, 2)
            .AddTile(TileID.WorkBenches);
}

public class SeedBedItem_Sand : SeedBedItem
{
    protected override int PlaceTileType => ModContent.TileType<SeedBedTile_Sand>();
    protected override Recipe BuildRecipe()
        => CreateRecipe(2)
            .AddIngredient(ItemID.SandBlock, 5)
            .AddIngredient(ItemID.RedBrick, 2)
            .AddTile(TileID.WorkBenches);
}

public class SeedBedItem_Ash : SeedBedItem
{
    protected override int PlaceTileType => ModContent.TileType<SeedBedTile_Ash>();
    protected override Recipe BuildRecipe()
        => CreateRecipe(2)
            .AddIngredient(ItemID.AshBlock, 5)
            .AddIngredient(ItemID.RedBrick, 2)
            .AddTile(TileID.WorkBenches);
}

// Tiles
public class SeedBedTile_Dirt : SeedBedTile
{
    public override Color MapColor => new(180, 140, 100);
    public override HerbType[] PreferredHerbs => [HerbType.Daybloom, HerbType.Blinkroot];

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        RegisterItemDrop(ModContent.ItemType<SeedBedItem_Dirt>());
    }
}

public class SeedBedTile_Mud : SeedBedTile
{
    public override Color MapColor => new(100, 80, 60);
    public override int GrowthAttempts => 6;
    public override int GrowthChance => 9;
    public override int SoilDust => DustID.Mud;

    public override HerbType[] PreferredHerbs => [HerbType.Moonglow, HerbType.Waterleaf];

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        RegisterItemDrop(ModContent.ItemType<SeedBedItem_Mud>());
    }
}

public class SeedBedTile_Sand : SeedBedTile
{
    public override Color MapColor => new(220, 200, 140);
    public override int SoilDust => DustID.Sand;

    public override HerbType[] PreferredHerbs => [HerbType.Waterleaf];

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        RegisterItemDrop(ModContent.ItemType<SeedBedItem_Sand>());
    }
}

public class SeedBedTile_Ash : SeedBedTile
{
    public override Color MapColor => new(80, 70, 70);
    public override int GrowthAttempts => 7;
    public override int GrowthChance => 8;
    public override int SoilDust => DustID.Ash;

    public override HerbType[] PreferredHerbs => [HerbType.Fireblossom, HerbType.Deathweed];

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        RegisterItemDrop(ModContent.ItemType<SeedBedItem_Ash>());
    }
}