using TrelamiumIII.Common.Globals;
using TrelamiumIII.Content.Alchemy;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Content.Alchemy.Tiles;
using TrelamiumIII.Content.Alchemy.Solutions;
using TrelamiumIII.Content.Items.Ammo;
using TrelamiumIII.Content.Items.Rusted;
using TrelamiumIII.Content.Items.Weapons;
using TrelamiumIII.Core.Gas.Layers;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;

namespace TrelamiumIII.Core.Alchemy;

public partial class ChemistryRecipeSystem
{
    private static void RegisterWeapons()
    {
        int crimsonFlask = ModContent.ItemType<CrimsonEffusionFlaskItem>();
        int bloodtooth = ModContent.ItemType<BloodtoothFungusItem>();
        int starflask = ModContent.ItemType<PrismaticStarflaskItem>();
        int phytoncide = ModContent.ItemType<PhytoncideFlaskItem>();
        int canister = ModContent.ItemType<GasCanisterBase>();
        int therm = ModContent.ItemType<ThermiteFlaskItem>();

        int[] tonics = [SolventID.AquaHarmonia, SolventID.AquaFortis, SolventID.AquaFortis];
        int hyperboreanFlask = ModContent.ItemType<ArcticFlaskItem>();
        int vilesporeFlask = ModContent.ItemType<VilesporeFlaskItem>();
        int[] simpleSolvents = [ItemID.BottledWater, SolventID.AquaInertis, SolventID.AquaVitae, SolventID.AquaFortis];
        int[] flowerStuffs = [simpleSolvents[0], simpleSolvents[1], ModContent.ItemType<AquaTerrae>()];
        int[] deadSolvents = [SolventID.Dead, SolventID.AquaPestis, SolventID.AquaFortis, SolventID.BloodtoothExtract];
        // Effusion Flask
        AlchemyCore.AddResult(SolventID.BloodtoothExtract, crimsonFlask,
        [
            new BrewRecipe(ItemID.ViciousPowder, ItemID.Deathweed) { OutputStack = 16 },
            new BrewRecipe(ItemID.Deathweed) { OutputStack = 8 },
            .. AnyTwoOf(ItemID.ViciousPowder, ItemID.Deathweed, bloodtooth, potency: 2, stack: 18),
        ]);

        AlchemyCore.AddResult(deadSolvents, crimsonFlask,
            AnyTwoOf(ItemID.ViciousPowder, ItemID.Deathweed, bloodtooth, potency: 2, stack: 16));
        AlchemyCore.AddResult(deadSolvents, crimsonFlask,
            AnyTwoOf(ItemID.ViciousPowder, ItemID.Deathweed, bloodtooth, potency: 1, stack: 16));

        // Vilespore
        AlchemyCore.AddResult(deadSolvents, vilesporeFlask,
            AnyTwoOf(ItemID.VilePowder, ItemID.Deathweed, ItemID.VileMushroom, potency: 2, stack: 16));
        AlchemyCore.AddResult(deadSolvents, vilesporeFlask,
            AnyTwoOf(ItemID.VilePowder, ItemID.Deathweed, ItemID.VileMushroom, potency: 1, stack: 16));

        // Hyperborean
        AlchemyCore.AddResult(simpleSolvents, hyperboreanFlask,
            new BrewRecipe(ComponentMatcher.Of(ShiverthornOrSeeds, 2), ItemID.CopperOre, ComponentMatcher.Of(SoluteID.Niter, 2))
            { OutputStack = 16, });

        AlchemyCore.AddResult(tonics, hyperboreanFlask,
            new BrewRecipe(ComponentMatcher.Of(ShiverthornOrSeeds, 2), ComponentMatcher.Of(SoluteID.Niter, 2)) { OutputStack = 16 });

        // Starflask
        AlchemyCore.AddResult(tonics, starflask,
            new BrewRecipe(ComponentMatcher.Of(ItemID.FallenStar, 2), ComponentMatcher.Of(SoluteID.Niter, 1)) { OutputStack = 16 },
            new BrewRecipe(ComponentMatcher.Of(ItemID.FallenStar, 2), ComponentMatcher.Of(ItemID.Gel, 3)) { OutputStack = 16 });

        // Thermite Flask
        AlchemyCore.AddResult([SolventID.FibrousSlurry, SolventID.AquaFortis, SolventID.AquaFortis], therm,
            new BrewRecipe(
                ComponentMatcher.Of(BlackPowderOrRust, 1), 
                ComponentMatcher.Of(SoluteID.Sulfur, 2), 
                ComponentMatcher.Of(ItemID.Hellstone, 2)) { OutputStack = 16 });

        // Phytoncide
        AlchemyCore.AddResult(simpleSolvents, phytoncide,
            new BrewRecipe(ComponentMatcher.Of(ItemID.JungleSpores, 2), ComponentMatcher.Of(SoluteID.Potash, 1)) { OutputStack = 16 },

            new BrewRecipe(ComponentMatcher.Of(ItemID.JungleSpores, 2), ComponentMatcher.Of(ItemID.Stinger, 1), 
            ComponentMatcher.Of(SoluteID.Potash, 1)) { OutputStack = 24 });

        // Mud Bottle
        int mudBottle = ModContent.ItemType<AquaTerrae>();

        AlchemyCore.AddResult(SolventID.AquaInertis, mudBottle,
            new BrewRecipe(ComponentMatcher.Of(Dirts, 2)) { OutputStack = 16 });

        AlchemyCore.AddResult(ItemID.BottledWater, mudBottle,
            new BrewRecipe(Dirts, Mushroom) { OutputStack = 16 });

        // Overgrowth
        int magnolia = ModContent.ItemType<MagnoliaItem>();
        int grassFlask = OvergrowthFlaskID.Of(VineStrain.All[0]);

        foreach (VineStrain strain in VineStrain.All)
        {
            int flask = OvergrowthFlaskID.Of(strain);

            AlchemyCore.AddResult(flowerStuffs, flask,
                new BrewRecipe(strain.Seed, Dirts, magnolia) { OutputStack = 16 });

            if (!strain.IsDefault)
                AlchemyCore.AddResult(grassFlask, flask, new BrewRecipe(strain.Seed));
        }

        // --- Gas Canisters ---
        // Vilespore
        AlchemyCore.AddResult(canister, GasCanisterAmmo.ContactVilespore,
            new BrewRecipe(ComponentMatcher.Of(ItemID.VileMushroom, 2),
            ComponentMatcher.Of(ItemID.Deathweed, 1),
            ComponentMatcher.Of(SoluteID.Niter, 1)));

        AlchemyCore.AddResult(canister, GasCanisterAmmo.TimedVilespore,
            new BrewRecipe(ComponentMatcher.Of(ItemID.VileMushroom, 2),
            ComponentMatcher.Of(ItemID.Deathweed, 1),
            ComponentMatcher.Of(SoluteID.Pitch, 1)));

        AlchemyCore.AddResult(canister, GasCanisterAmmo.ContactVilespore,
            new BrewRecipe(ComponentMatcher.Of(ModContent.ItemType<VilesporeFlaskItem>(), 1)));

        // Hyperborean
        AlchemyCore.AddResult(canister, GasCanisterAmmo.ContactHyperborean,
            new BrewRecipe(ComponentMatcher.Of(ShiverthornOrSeeds, 2),
            ComponentMatcher.Of(SoluteID.Niter, 1)));

        AlchemyCore.AddResult(canister, GasCanisterAmmo.TimedHyperborean,
            new BrewRecipe(ComponentMatcher.Of(ShiverthornOrSeeds, 2),
            ComponentMatcher.Of(SoluteID.Pitch, 1)));

        AlchemyCore.AddResult(canister, GasCanisterAmmo.ContactHyperborean,
            new BrewRecipe(ComponentMatcher.Of(ModContent.ItemType<ArcticFlaskItem>(), 1)));
    }

    /// <summary>One variant per pair of <paramref name="a" />, <paramref name="b" /> and <paramref name="c" />, each at <paramref name="potency" />.</summary>
    private static BrewRecipe[] AnyTwoOf(int a, int b, int c, int potency, int stack) =>
    [
        new BrewRecipe(ComponentMatcher.Of(a, potency), ComponentMatcher.Of(b, potency)) { OutputStack = stack },
        new BrewRecipe(ComponentMatcher.Of(b, potency), ComponentMatcher.Of(c, potency)) { OutputStack = stack },
        new BrewRecipe(ComponentMatcher.Of(a, potency), ComponentMatcher.Of(c, potency)) { OutputStack = stack },
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