using Terraria.Localization;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Core.Alchemy.Brewing;
using TrelamiumIII.Core.Alchemy.Brewing.Behaviors;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>
/// Registers recipes and recipe groups, including the vanilla potion recipe changes. Alchemy recipes are split by section
/// across the <c>ChemistryRecipeSystem.*.cs</c> partials; <see cref="BrewFormula" /> derives what a variant leaves unset.
/// </summary>
public partial class ChemistryRecipeSystem : ModSystem
{
    public override void PostSetupContent()
    {
        base.PostSetupContent();

        AlmanacDefinitions.Register();

        RegisterSolvents();
        RegisterMisc();

        RegisterCombatPotions();
        RegisterUtilityPotions();
        RegisterVisionPotions();
        RegisterFishingPotions();
        RegisterLuckPotions();
        RegisterAugmentations();
        RegisterTransmutations();

        RegisterWeapons();
        RegisterSolutes();

        if (ModLoader.HasMod("SpiritReforged"))
            RegisterSpiritReforged();

        if (ModLoader.HasMod("CalamityMod"))
            RegisterCalamity();

        AlchemyRecipeGroups.Populate();
    }

    public override void Unload()
    {
        base.Unload();

        AlchemyRecipes.Clear();
        AlchemyIngredients.Clear();
        AlmanacEntryRegistry.Clear();
        AlchemyRecipeGroups.Clear();
        EffectBehaviors.ClearRuntime();   // cross-mod entries hold another mod's types
        QuenchHook.Unhook();              // ...and this one lives on another mod's static event
    }

    /// <summary>A brew duration in ticks.</summary>
    private static int Time(int mins = 0, int secs = 0) => TimeConversion.SetTime(mins: mins, secs: secs);
}
