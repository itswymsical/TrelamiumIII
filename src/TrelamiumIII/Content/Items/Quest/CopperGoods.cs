using TrelamiumIII.Content.Jobs;

namespace TrelamiumIII.Content.Items.Quest;

// Placeholder goods for the Merchant's copper trials (CopperStandard). No sprites yet: PlaceholderTextures draws them until
// a PNG named after the class is added next to this file.

/// <summary>
///     Something the player already knows, with copper in it. It works like <see cref="Counterpart" /> and sells for one
///     copper, which is what the job finds out.
/// </summary>
public abstract class CopperGood : ModItem
{
    protected abstract int Counterpart { get; }

    /// <summary>The <see cref="CopperStandard" /> set that asks for this. Its recipe stays hidden until then.</summary>
    protected abstract int Trial { get; }

    public override void SetDefaults()
    {
        Item.CloneDefaults(Counterpart);
        Item.value = Item.sellPrice(copper: 1);
    }

    protected Recipe Forge(int bars, int amount = 1)
        => CreateRecipe(amount)
            .AddIngredient(ItemID.CopperBar, bars)
            .AddTile(TileID.Anvils)
            .AddCondition(CopperStandard.AskedFor(Trial));
}

public class CopperBullet : CopperGood
{
    protected override int Counterpart => ItemID.MusketBall;

    protected override int Trial => CopperStandard.ArmoryTrial;

    public override void AddRecipes() => Forge(bars: 1, amount: 25).Register();
}

public class CopperBoomerang : CopperGood
{
    protected override int Counterpart => ItemID.WoodenBoomerang;

    protected override int Trial => CopperStandard.ArmoryTrial;

    public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] < 1;

    public override void AddRecipes() => Forge(bars: 6).AddRecipeGroup(RecipeGroupID.Wood, 4).Register();
}

/// <summary>Copper burns green, so this places the vanilla green torch.</summary>
public class CopperTorch : CopperGood
{
    protected override int Counterpart => ItemID.GreenTorch;

    protected override int Trial => CopperStandard.HouseholdTrial;

    public override void SetDefaults()
    {
        base.SetDefaults();

        // The held flame is a sprite of its own, which this doesn't have yet.
        Item.flame = false;
    }

    public override void AddRecipes() => Forge(bars: 1, amount: 3).AddIngredient(ItemID.Torch, 3).Register();
}

public class CopperRope : CopperGood
{
    protected override int Counterpart => ItemID.Rope;

    protected override int Trial => CopperStandard.HouseholdTrial;

    public override void AddRecipes() => Forge(bars: 1, amount: 10).AddIngredient(ItemID.Rope, 10).Register();
}

/// <summary>Worse than the potion that went into it, and it doesn't sit well.</summary>
public class CopperHealingPotion : CopperGood
{
    protected override int Counterpart => ItemID.LesserHealingPotion;

    protected override int Trial => CopperStandard.HouseholdTrial;

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.healLife = 40;
        Item.buffType = BuffID.Poisoned;
        Item.buffTime = 60 * 3;
    }

    public override void AddRecipes() => Forge(bars: 1).AddIngredient(ItemID.LesserHealingPotion).Register();
}

/// <summary>A gold bunny is worth ten gold, so the Merchant has a theory. Released, it's a bunny.</summary>
public class CopperBunny : CopperGood
{
    protected override int Counterpart => ItemID.Bunny;

    protected override int Trial => CopperStandard.BunnyTrial;

    public override void AddRecipes() => Forge(bars: 5).AddIngredient(ItemID.Bunny).Register();
}
