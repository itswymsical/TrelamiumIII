using TrelamiumIII.Content.Alchemy;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Content.Alchemy.Tiles;
using TrelamiumIII.Content.Alchemy.Solutions;
using TrelamiumIII.Content.Items.ContortionistSet;
using TrelamiumIII.Content.Items.Lodestone;
using static TrelamiumIII.Core.Alchemy.AlchemyRecipeGroups;

namespace TrelamiumIII.Core.Alchemy;

public partial class ChemistryRecipeSystem
{
    /// <summary>Tonics and the other brewed solvents. Bottled Water and Inert Solution brew the same ones.</summary>
    private static void RegisterSolvents()
    {
        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, SolventID.Inert, new RecipeVariant(Mushroom));

        int[] bases = [ItemID.BottledWater, SolventID.Inert];

        AlchemyRecipes.CreateRecipe(bases, SolventID.HarmoniousTonic,
            new RecipeVariant(ItemID.CopperOre),
            new RecipeVariant(ModContent.ItemType<OxidizedCopperItem>()));

        AlchemyRecipes.CreateRecipe(bases, SolventID.BriskTonic,
            new RecipeVariant(ModContent.ItemType<LodestoneItem>()) { OutputStack = 2 },
            new RecipeVariant(ItemID.TinOre) { OutputStack = 2 });

        AlchemyRecipes.CreateRecipe(bases, SolventID.VitalTonic,
            new RecipeVariant(ItemID.GoldOre));

        AlchemyRecipes.CreateRecipe(bases, SolventID.SpecularTonic,
            new RecipeVariant(ItemID.SilverOre),
            new RecipeVariant(ItemID.SpecularFish) { OutputStack = 4 });

        AlchemyRecipes.CreateRecipe(bases, SolventID.MartialTonic,
            new RecipeVariant(ItemID.IronOre));

        AlchemyRecipes.CreateRecipe(bases, SolventID.FortunateTonic,
            new RecipeVariant(ItemID.PlatinumOre) { OutputStack = 3 },
            new RecipeVariant(ItemID.TinOre));

        AlchemyRecipes.CreateRecipe(bases, SolventID.BaneTonic,
            new RecipeVariant(AnyEvilOre),
            new RecipeVariant(ItemID.LeadOre));

        AlchemyRecipes.CreateRecipe(bases, SolventID.Dead,
            new RecipeVariant(RottenChunkVertabrae));

        AlchemyRecipes.CreateRecipe(bases, ModContent.ItemType<MagnoliaSolutionItem>(),
            new RecipeVariant(ModContent.ItemType<MagnoliaItem>()) { OutputStack = 2 },
            new RecipeVariant(ModContent.ItemType<MagnoliaSeedsItem>()),
            new RecipeVariant(ModContent.ItemType<MagnoliaPowderItem>()) { OutputStack = 3 });

        AlchemyRecipes.CreateRecipe([ItemID.Bottle, ItemID.BottledWater, SolventID.Inert], SolventID.BloodtoothExtract,
            new RecipeVariant(ModContent.ItemType<BloodtoothFungusItem>()));
    }

    /// <summary>
    /// Mineral refinement: acid solvents and alkali/distillate solutes. None of these outputs is a potion, so no
    /// <see cref="RecipeVariant.Potency" /> applies; star ratings live on <see cref="AlmanacEntry.AlmanacPotency" />.
    /// </summary>
    private static void RegisterSolutes()
    {
        #region Reagents

        AlchemyRecipes.CreateRecipe(SolventID.BaneTonic, ModContent.ItemType<TwistedFungus>(),
            new RecipeVariant(ComponentMatcher.Of(SoluteID.RockSalt, 2), ItemID.VileMushroom) { ReturnsBottle = true});

        #endregion
        #region Acids

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, SolventID.OilOfVitriol,
            new RecipeVariant(ComponentMatcher.Of(SoluteID.Sulfur, 2), SoluteID.Niter));

        AlchemyRecipes.CreateRecipe(SolventID.OilOfVitriol, SolventID.AquaFortis,
            new RecipeVariant(ComponentMatcher.Of(SoluteID.Niter, 2)));

        AlchemyRecipes.CreateRecipe(SolventID.OilOfVitriol, SoluteID.SpiritOfSalt,
            new RecipeVariant(ComponentMatcher.Of(SoluteID.RockSalt, 2)));

        // The one that eats gold. Both of the strong acids, and nothing else.
        AlchemyRecipes.CreateRecipe(SolventID.AquaFortis, SolventID.AquaRegia,
            new RecipeVariant(SoluteID.SpiritOfSalt));

        AlchemyRecipes.CreateRecipe(SolventID.OilOfVitriol, SoluteID.BlueVitriol,
            new RecipeVariant(ComponentMatcher.Of(ModContent.ItemType<OxidizedCopperItem>(), 2)));

        #endregion

        #region Alkalis

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, SoluteID.SlakedLime,
            new RecipeVariant(ComponentMatcher.Of(SoluteID.Quicklime, 2)) { OutputStack = 2 });

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, SoluteID.CausticLye,
            new RecipeVariant(ComponentMatcher.Of(SoluteID.Potash, 2), SoluteID.Quicklime));

        AlchemyRecipes.CreateRecipe(SolventID.Inert, SoluteID.FrigorificSalt,
            new RecipeVariant(SoluteID.Niter, SoluteID.RockSalt, ItemID.IceBlock));

        #endregion

        #region Distillates and powders

        // Oil distils into two outputs, registered as two recipes over the same solvent.
        AlchemyRecipes.CreateRecipe(SolventID.BottledOil, SoluteID.SpiritOfOil,
            new RecipeVariant(ComponentMatcher.Of(ModContent.ItemType<CharcoalItem>(), 2)));

        AlchemyRecipes.CreateRecipe(SolventID.BottledOil, SoluteID.Pitch,
            new RecipeVariant(ComponentMatcher.Of(ItemID.Cobweb, 4)) { OutputStack = 2 });

        AlchemyRecipes.CreateRecipe(ItemID.BottledWater, SolventID.FibrousSlurry,
            new RecipeVariant(ComponentMatcher.Of(ItemID.Cobweb, 3)) { OutputStack = 1 });

        // Niter at maximum potency to one part fuel.
        AlchemyRecipes.CreateRecipe(SolventID.Inert, SoluteID.BlackPowder,
            new RecipeVariant(ComponentMatcher.Of(SoluteID.Niter, 3), SoluteID.Sulfur, ModContent.ItemType<CharcoalItem>())
            {
                OutputStack = 2,
            });

        AlchemyRecipes.CreateRecipe(SolventID.Inert, SoluteID.ColdFire,
            new RecipeVariant(ComponentMatcher.Of(SoluteID.BoneAsh, 2), ModContent.ItemType<CharcoalItem>(), ItemID.SandBlock));

        #endregion
    }
}
