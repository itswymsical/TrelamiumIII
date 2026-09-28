using TrelamiumIII.Common.Globals;
using TrelamiumIII.Content.Items.Alchemy.Reagents;
using TrelamiumIII.Content.Items.Alchemy.Reagents.Misc;
using TrelamiumIII.Content.Items.Alchemy.Solutions;
using TrelamiumIII.Content.Items.Alchemy.Tiles;
using TrelamiumIII.Content.Items.Ammo;
using TrelamiumIII.Content.Items.Rusted;
using TrelamiumIII.Content.Items.Weapons;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;

namespace TrelamiumIII.Core.Alchemy;

public partial class ChemistryRecipeSystem
{
    /// <summary>Miscellaneous craftable items (e.g. unholy water, love potion).</summary>
    private static void RegisterMisc()
    {
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.UnholyWater,
            new RecipeVariant(CorruptTiles, ItemID.CorruptSeeds) { OutputStack = 4 },
            new RecipeVariant(ItemID.VileMushroom, DirtOrMud, ItemID.Worm) { OutputStack = 4 });

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.BloodWater,
            new RecipeVariant(BloodTiles, ItemID.CrimsonSeeds) { OutputStack = 4 },
            new RecipeVariant(ItemID.ViciousMushroom, DirtOrMud, ItemID.Worm) { OutputStack = 4 });

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.HolyWater,
            new RecipeVariant(ItemID.PixieDust, ItemID.HallowedSeeds) { OutputStack = 4 });

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.LovePotion,
            new RecipeVariant(ItemID.LifeCrystal, ItemID.FallenStar) { OutputStack = 4 },
            new RecipeVariant(ItemID.PrincessFish, ItemID.Shiverthorn) { OutputStack = 8 });

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, ItemID.StinkPotion,
            new RecipeVariant(ItemID.Stinkfish, ItemID.Deathweed) { OutputStack = 2 },
            new RecipeVariant(ItemID.Stinkfish, ItemID.DeathweedSeeds));

        AlchemyRecipes.CreateRecipe(ItemID.Bottle, ItemID.ViciousPowder,
            new RecipeVariant(ItemID.ViciousMushroom) { OutputStack = 5, CustomOutput = BottleOf(ItemID.ViciousPowder) });

        AlchemyRecipes.CreateRecipe(ItemID.Bottle, ItemID.VilePowder,
            new RecipeVariant(ItemID.VileMushroom) { OutputStack = 5, CustomOutput = BottleOf(ItemID.VilePowder) });

        AlchemyRecipes.CreateRecipe(ItemID.Bottle, ModContent.ItemType<RustedNugget>(),
            new RecipeVariant(ItemID.IronOre) { CustomOutput = BottleOf(ModContent.ItemType<RustedNugget>()) });
    }

    private static void RegisterWeapons()
    {
        int crimsonFlask = ModContent.ItemType<CrimsonEffusionFlaskItem>();
        int bloodtooth = ModContent.ItemType<BloodtoothFungusItem>();
        int starflask = ModContent.ItemType<PrismaticStarflaskItem>();
        int phytoncide = ModContent.ItemType<PhytoncideFlaskItem>();
        int canister = ModContent.ItemType<GasCanisterBase>();
        int therm = ModContent.ItemType<ThermiteFlaskItem>();

        int[] tonics = [SolventID.HarmoniousTonic, SolventID.MartialTonic, SolventID.AquaFortis];
        int hyperboreanFlask = ModContent.ItemType<ArcticFlaskItem>();
        int vilesporeFlask = ModContent.ItemType<VilesporeFlaskItem>();
        int[] basics = [ItemID.BottledWater, SolventID.Inert];

        // Effusion Flask
        AlchemyRecipes.CreateRecipe(SolventID.BloodtoothExtract, crimsonFlask,
        [
            new RecipeVariant(ItemID.ViciousPowder, ItemID.Deathweed) { OutputStack = 16 },
            new RecipeVariant(ItemID.Deathweed) { OutputStack = 8 },
            .. AnyTwoOf(ItemID.ViciousPowder, ItemID.Deathweed, bloodtooth, potency: 2, stack: 18),
        ]);

        AlchemyRecipes.CreateRecipe(SolventID.Dead, crimsonFlask,
            AnyTwoOf(ItemID.ViciousPowder, ItemID.Deathweed, bloodtooth, potency: 2, stack: 16));
        AlchemyRecipes.CreateRecipe(SolventID.BaneTonic, crimsonFlask,
            AnyTwoOf(ItemID.ViciousPowder, ItemID.Deathweed, bloodtooth, potency: 1, stack: 16));

        // Vilespore
        AlchemyRecipes.CreateRecipe(SolventID.Dead, vilesporeFlask,
            AnyTwoOf(ItemID.VilePowder, ItemID.Deathweed, ItemID.RottenChunk, potency: 2, stack: 16));
        AlchemyRecipes.CreateRecipe(SolventID.BaneTonic, vilesporeFlask,
            AnyTwoOf(ItemID.VilePowder, ItemID.Deathweed, ItemID.RottenChunk, potency: 1, stack: 16));

        // Hyperborean
        AlchemyRecipes.CreateRecipe(basics, hyperboreanFlask,
            new RecipeVariant(ComponentMatcher.Of(ShiverthornOrSeeds, 2), ItemID.CopperOre, ComponentMatcher.Of(SoluteID.Niter, 2))
            { OutputStack = 16, });

        AlchemyRecipes.CreateRecipe(tonics, hyperboreanFlask,
            new RecipeVariant(ComponentMatcher.Of(ShiverthornOrSeeds, 2), ComponentMatcher.Of(SoluteID.Niter, 2)) { OutputStack = 16 });

        // Starflask
        AlchemyRecipes.CreateRecipe(tonics, starflask,
            new RecipeVariant(ComponentMatcher.Of(ItemID.FallenStar, 2), ComponentMatcher.Of(SoluteID.Niter, 1)) { OutputStack = 16 });

        // Thermite Flask
        AlchemyRecipes.CreateRecipe(SolventID.FibrousSlurry, therm,
            new RecipeVariant(
                ComponentMatcher.Of(SoluteID.BlackPowder, 2), 
                ComponentMatcher.Of(SoluteID.Sulfur, 1), 
                ComponentMatcher.Of(ItemID.Hellstone, 1)) { OutputStack = 16 },
            new RecipeVariant(
                ComponentMatcher.Of(SoluteID.Pitch, 2),
                ComponentMatcher.Of(SoluteID.Sulfur, 1),
                ComponentMatcher.Of(ItemID.Hellstone, 1)) { OutputStack = 16 },
            new RecipeVariant(
                ComponentMatcher.Of(SoluteID.Pitch, 2),
                ComponentMatcher.Of(ModContent.ItemType<CharcoalItem>(), 2),
                ComponentMatcher.Of(ItemID.Hellstone, 1)) { OutputStack = 16 });

        // Phytoncide
        AlchemyRecipes.CreateRecipe([SolventID.VitalTonic, SolventID.MartialTonic], phytoncide,
            new RecipeVariant(ComponentMatcher.Of(ItemID.JungleSpores, 2), ComponentMatcher.Of(SoluteID.Potash, 2)) { OutputStack = 16 });

        AlchemyRecipes.CreateRecipe(basics, phytoncide,
            new RecipeVariant(ComponentMatcher.Of(ItemID.JungleSpores, 2), 
            ComponentMatcher.Of(ItemID.Stinger, 1), 
            ComponentMatcher.Of(SoluteID.Potash, 1)) { OutputStack = 16 });

        // --- Gas Canisters ---
        // Vilespore
        AlchemyRecipes.CreateRecipe(canister, ModContent.ItemType<ContactVilesporeCanister>(),
            new RecipeVariant(ComponentMatcher.Of(ItemID.VileMushroom, 2),
            ComponentMatcher.Of(ItemID.Deathweed, 1),
            ComponentMatcher.Of(SoluteID.Niter, 1)));

        AlchemyRecipes.CreateRecipe(canister, ModContent.ItemType<TimedVilesporeCanister>(),
            new RecipeVariant(ComponentMatcher.Of(ItemID.VileMushroom, 2),
            ComponentMatcher.Of(ItemID.Deathweed, 1),
            ComponentMatcher.Of(SoluteID.Pitch, 1)));

        AlchemyRecipes.CreateRecipe(canister, ModContent.ItemType<ContactVilesporeCanister>(),
            new RecipeVariant(ComponentMatcher.Of(ModContent.ItemType<VilesporeFlaskItem>(), 1)));

        // Hyperborean
        AlchemyRecipes.CreateRecipe(canister, ModContent.ItemType<ContactHyperboreanCanister>(),
            new RecipeVariant(ComponentMatcher.Of(ShiverthornOrSeeds, 2),
            ComponentMatcher.Of(SoluteID.Niter, 1)));

        AlchemyRecipes.CreateRecipe(canister, ModContent.ItemType<TimedHyperboreanCanister>(),
            new RecipeVariant(ComponentMatcher.Of(ShiverthornOrSeeds, 2),
            ComponentMatcher.Of(SoluteID.Pitch, 1)));

        AlchemyRecipes.CreateRecipe(canister, ModContent.ItemType<ContactHyperboreanCanister>(),
            new RecipeVariant(ComponentMatcher.Of(ModContent.ItemType<ArcticFlaskItem>(), 1)));
    }

    /// <summary>One variant per pair of <paramref name="a" />, <paramref name="b" /> and <paramref name="c" />, each at <paramref name="potency" />.</summary>
    private static RecipeVariant[] AnyTwoOf(int a, int b, int c, int potency, int stack) =>
    [
        new RecipeVariant(ComponentMatcher.Of(a, potency), ComponentMatcher.Of(b, potency)) { OutputStack = stack },
        new RecipeVariant(ComponentMatcher.Of(b, potency), ComponentMatcher.Of(c, potency)) { OutputStack = stack },
        new RecipeVariant(ComponentMatcher.Of(a, potency), ComponentMatcher.Of(c, potency)) { OutputStack = stack },
    ];

    /// <summary>An empty Bottle holding <paramref name="storedType" />, for recipes whose product stays in the bottle.</summary>
    private static Func<Item> BottleOf(int storedType) => () =>
    {
        var bottle = new Item();
        bottle.SetDefaults(ItemID.Bottle);
        bottle.GetGlobalItem<ContainerBottleItem>().StoredItemType = storedType;

        return bottle;
    };
}
