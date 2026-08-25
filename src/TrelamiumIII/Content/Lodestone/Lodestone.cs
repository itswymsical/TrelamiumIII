using TrelamiumIII.Utilities;
using Terraria.DataStructures;

namespace TrelamiumIII.Content.Lodestone;

public class LodestoneItem : ModItem
{
    private const float BASE_MAGNET_RANGE = 95f;
    private const float BASE_MAX_SPEED = 1.7f;

    private const float MAX_MAGNET_RANGE = 380f;
    private const float MAX_SPEED = 6f;

    private const float EXPONENTIAL_BASE = 1.09f;

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.value = Item.sellPrice(silver: 2);

        Item.DefaultToPlaceableTile(ModContent.TileType<LodestoneTile>());

        Item.rare = ItemRarityID.Blue;
        Item.holdStyle = ItemHoldStyleID.HoldFront;
    }
  
    public override void HoldItem(Player player)
    {
        base.HoldItem(player);

        MagnetizeItemsHeld(player);
    }

    public override void Update(ref float gravity, ref float maxFallSpeed)
    {
        base.Update(ref gravity, ref maxFallSpeed);
        if (Item.IsAir && !Item.active)
        {
            return;
        }

        MagnetizeItems_World();
    }

    private void MagnetizeItemsHeld(Player player)
    {
        (var range, var maxSpeed) = CalculateEnhancedValues();

        MagnetizeItems(player.Center, range, maxSpeed);
    }

    private void MagnetizeItems_World()
    {
        (var range, var maxSpeed) = CalculateEnhancedValues();

        MagnetizeItems(Item.Center, range, maxSpeed);
    }

    private void MagnetizeItems(Vector2 center, float range, float maxSpeed)
    {
        for (var i = 0; i < Main.maxItems; i++)
        {
            var targetItem = Main.item[i];

            if (targetItem.active && !targetItem.beingGrabbed && targetItem.noGrabDelay == 0 &&
                targetItem != Item && ItemUtils.IsAMetalItem(targetItem))
            {
                var distance = Vector2.Distance(center, targetItem.Center);
                if (distance <= range)
                {
                    var movement = Vector2.Normalize(center - targetItem.Center);
                    var speedFactor = 1f - distance / range;
                    var speed = maxSpeed * speedFactor;
                    targetItem.velocity = movement * speed;
                }
            }
        }
    }

    private(float range, float maxSpeed) CalculateEnhancedValues()
    {
        float stackMultiplier = Math.Min(Item.stack, 500) - 1;

        var enhancedRange = Math.Min(BASE_MAGNET_RANGE * (float)Math.Pow(EXPONENTIAL_BASE, stackMultiplier), MAX_MAGNET_RANGE);
        var enhancedMaxSpeed = Math.Min(BASE_MAX_SPEED * (float)Math.Pow(EXPONENTIAL_BASE, stackMultiplier), MAX_SPEED);

        return (enhancedRange, enhancedMaxSpeed);
    }
}

public class LodestoneTile : ModTile
{
    public override void SetStaticDefaults()
    {
        Main.tileSolid[Type] = true;
        Main.tileMergeDirt[Type] = true;
        Main.tileMerge[TileID.Stone][Type] = true;
        Main.tileBlockLight[Type] = true;
        DustType = DustID.Tin;
        MineResist = 2f;
        MinPick = 42;
        HitSound = SoundID.Tink;
        Main.tileSpelunker[Type] = true;
        AddMapEntry(new Color(55, 55, 55));
    }
    public override bool IsTileSpelunkable(int i, int j) => true;
}

public class MagnetizedCoil : ModItem
{
    public override void SetDefaults()
    {
        Item.rare = ItemRarityID.White;
        Item.value = Item.sellPrice(silver: 3);
        Item.width = Item.height = 28;
        Item.maxStack = 999;
    }

    public override void AddRecipes()
    {

        var recipe = CreateRecipe(2);
        recipe.AddIngredient(ItemID.Chain, 2);
        recipe.AddRecipeGroup(nameof(ItemID.CopperBar), 1);
        recipe.AddTile(TileID.Anvils);
        recipe.Register();
    }
}
