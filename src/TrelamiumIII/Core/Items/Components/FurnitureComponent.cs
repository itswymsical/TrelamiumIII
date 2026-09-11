using TrelamiumIII.Core.Tiles;

namespace TrelamiumIII.Core.Items.Components;

public abstract class FurnitureItem : ModItem
{
    protected abstract FurnitureType FurnitureType { get; }
    protected abstract int TileType { get; }
    protected abstract int MaterialType { get; }
    protected virtual int TorchType { get; }
    protected virtual bool IsTorch { get; set; }
    protected virtual bool IsWaterTorch { get; set; }
    protected virtual Vector3 TorchColor { get; set; } = new Vector3(0.5f, 1.5f, 0.5f);
    protected virtual int TorchDustType { get; set; } = DustID.Torch;

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        if (IsTorch || IsWaterTorch)
        {
            ItemID.Sets.ShimmerTransformToItem[Type] = ItemID.ShimmerTorch;
            ItemID.Sets.SingleUseInGamepad[Type] = true;
            ItemID.Sets.Torches[Type] = true;
        }
    }
    public override void SetDefaults()
    {
        Item.value = 150;

        if (IsTorch)
            Item.DefaultToTorch(TileType, 0, false);
        else if (IsWaterTorch)
            Item.DefaultToTorch(TileType, 1, true);
        else
            Item.DefaultToPlaceableTile(TileType);
        
    }
    public override void PostUpdate()
    {
        // Create a white (1.0, 1.0, 1.0) light when the item is in world, and isn't underwater.
        if (!Item.wet && !IsWaterTorch)
        {
            Lighting.AddLight(Item.Center, TorchColor.X, TorchColor.Y, TorchColor.Z);
        }
    }

    public override void HoldItem(Player player)
    {
        if (player.wet && !IsWaterTorch)
            return;
        
        if (IsTorch || IsWaterTorch)
        {
            if (Main.rand.NextBool(player.itemAnimation > 0 ? 7 : 30))
            {
                Dust dust = Dust.NewDustDirect(new Vector2(player.itemLocation.X + (player.direction == -1 ? -16f : 6f), player.itemLocation.Y - 14f * player.gravDir), 4, 4, TorchDustType, 0f, 0f, 100);
                if (!Main.rand.NextBool(3))
                {
                    dust.noGravity = true;
                }

                dust.velocity *= 0.3f;
                dust.velocity.Y -= 1.5f;
                dust.position = player.RotatedRelativePoint(dust.position);
            }

            // Create a greenish (0.5, 1.5, 0.5) light at the torch's approximate position, when the item is held.
            Vector2 position = player.RotatedRelativePoint(new Vector2(player.itemLocation.X + 12f * player.direction + player.velocity.X, player.itemLocation.Y - 14f + player.velocity.Y), true);

            Lighting.AddLight(position, TorchColor.X, TorchColor.Y, TorchColor.Z);
        }
    }
    public override void AddRecipes()
    {
        var recipe = CreateRecipe();

        switch (FurnitureType)
        {
            case FurnitureType.Chair:
                recipe.AddIngredient(MaterialType, 4);
                recipe.AddTile(TileID.WorkBenches);
                break;

            case FurnitureType.Table:
                recipe.AddIngredient(MaterialType, 8);
                recipe.AddTile(TileID.WorkBenches);
                break;

            case FurnitureType.Workbench:
                recipe.AddIngredient(MaterialType, 10);
                break;

            case FurnitureType.Bed:
                recipe.AddIngredient(MaterialType, 15);
                recipe.AddIngredient(ItemID.Silk, 5);
                recipe.AddTile(TileID.Sawmill);
                break;

            case FurnitureType.Dresser:
                recipe.AddIngredient(MaterialType, 16);
                recipe.AddTile(TileID.Sawmill);
                break;

            case FurnitureType.Sink:
                recipe.AddIngredient(MaterialType, 6);
                recipe.AddIngredient(ItemID.WaterBucket);
                recipe.AddTile(TileID.WorkBenches);
                break;

            case FurnitureType.Bathtub:
                recipe.AddIngredient(MaterialType, 14);
                recipe.AddTile(TileID.Sawmill);
                break;

            case FurnitureType.Bookcase:
                recipe.AddIngredient(MaterialType, 20);
                recipe.AddIngredient(ItemID.Book, 10);
                recipe.AddTile(TileID.Sawmill);
                break;

            case FurnitureType.Piano:
                recipe.AddIngredient(ItemID.Bone, 4);
                recipe.AddIngredient(MaterialType, 15);
                recipe.AddIngredient(ItemID.Book);
                recipe.AddTile(TileID.Sawmill);
                break;

            case FurnitureType.Lamp:
                recipe.AddIngredient(MaterialType, 3);
                recipe.AddIngredient(ItemID.Torch);
                recipe.AddTile(TileID.WorkBenches);
                break;

            case FurnitureType.Sofa:
                recipe.AddIngredient(MaterialType, 5);
                recipe.AddIngredient(ItemID.Silk, 2);
                recipe.AddTile(TileID.Sawmill);
                break;

            case FurnitureType.Chandelier:
                recipe.AddIngredient(MaterialType, 4);
                recipe.AddIngredient(ItemID.Torch, 4);
                recipe.AddIngredient(ItemID.Chain);
                recipe.AddTile(TileID.Anvils);
                break;

            case FurnitureType.Candelabra:
                recipe.AddIngredient(MaterialType, 5);
                recipe.AddIngredient(ItemID.Torch, 5);
                recipe.AddTile(TileID.WorkBenches);
                break;

            case FurnitureType.Lantern:
                recipe.AddIngredient(MaterialType, 6);
                recipe.AddIngredient(ItemID.Torch, 1);
                recipe.AddTile(TileID.WorkBenches);
                break;

            case FurnitureType.Clock:
                recipe.AddIngredient(MaterialType, 10);
                recipe.AddIngredient(ItemID.Glass, 6);
                recipe.AddRecipeGroup(RecipeGroupID.IronBar, 3);
                recipe.AddTile(TileID.Sawmill);
                break;

            case FurnitureType.Chest:
                recipe.AddIngredient(MaterialType, 8);
                recipe.AddRecipeGroup(RecipeGroupID.IronBar, 2);
                recipe.AddTile(TileID.WorkBenches);
                break;

            case FurnitureType.Campfire:
                recipe.AddIngredient(MaterialType, 10);
                recipe.AddIngredient(TorchType, 5);
                break;

            case FurnitureType.Platform:
                recipe = CreateRecipe(2);
                recipe.AddIngredient(MaterialType);
                break;

            case FurnitureType.Torch:
                recipe = CreateRecipe(3);
                recipe.AddIngredient(MaterialType);
                recipe.AddIngredient(ItemID.Gel);
                break;

            case FurnitureType.ClosedDoor:
                recipe = CreateRecipe();
                recipe.AddIngredient(MaterialType, 6);
                recipe.AddTile(TileID.WorkBenches);
                break;
        }

        recipe.Register();
    }
}