using ModLiquidLib.Utils;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.Alchemy.Solutions;

/// <summary>
///     Solvents for the vial slot and the solutions that come out, each a <see cref="ChemicalItem" /> instance.
///     Internal names keep the old <c>...Item</c> spelling; <see cref="ChemicalItem.LegacyNames" /> covers earlier names.
/// </summary>
public sealed class Solution : ICustomAutoload
{
    /// <summary>Sprites are named after the item, in this folder.</summary>
    private const string DIRECTORY = $"{nameof(TrelamiumIII)}/Content/Alchemy/Solutions/";

    #region Solvents

    /// <summary>Salt water; boiled down, it starts the salt line.</summary>
    public static int Brine { get; private set; }

    /// <summary>Makes whatever it touches easier to set alight.</summary>
    public static int BottledOil { get; private set; }

    #endregion

    #region Solutions

    /// <summary>The neutral base the rest of the tree is built on.</summary>
    public static int Inert { get; private set; }

    public static int Dead { get; private set; }

    public static int Arcane { get; private set; }

    public static int BloodtoothExtract { get; private set; }

    /// <summary>Pulped plant matter; the one solute here, bottled like the rest.</summary>
    public static int FibrousSlurry { get; private set; }

    #endregion

    #region Principle tonics

    public static int HarmoniousTonic { get; private set; }

    public static int VitalTonic { get; private set; }

    public static int SpecularTonic { get; private set; }

    public static int MartialTonic { get; private set; }

    public static int FortunateTonic { get; private set; }

    public static int BriskTonic { get; private set; }

    public static int BaneTonic { get; private set; }

    #endregion

    public static void Autoload(Mod mod)
    {
        LoadSolvents(mod);
        LoadSolutions(mod);
        LoadTonics(mod);
    }

    private static void LoadSolvents(Mod mod)
    {
        Brine = Add(mod, new SolventItem("BrineBottleItem")
        {
            Rare = ItemRarityID.Blue,
            Potency = 2,
            Quality = ChemicalQuality.Inert | ChemicalQuality.Alkaline,
            Principle = PrincipleType.Harmony,
            Hidden = false,
            LegacyNames = ["BrineBottle"],
            Recipes = self => self.CreateRecipe()
                .AddIngredient(ItemID.Bottle)
                .AddLiquid<BrineLiquid>()
                .Register(),
        });

        BottledOil = Add(mod, new SolventItem("BottledOilItem")
        {
            Value = Item.sellPrice(silver: 2),
            Potency = 2,
            Quality = ChemicalQuality.Volatile,
            Principle = PrincipleType.Martial,
            LegacyNames = ["BottledOil"],
            Recipes = self => self.CreateRecipe()
                .AddIngredient(ItemID.Bottle)
                .AddLiquid<CrudeOilLiquid>()
                .Register(),
        });
    }

    private static void LoadSolutions(Mod mod)
    {
        Inert = Add(mod, new SolutionItem("InertSolutionItem")
        {
            Rare = ItemRarityID.Gray,
            Potency = 1,
            Quality = ChemicalQuality.Inert,
            AlmanacRarity = ItemRarityID.White,
            Hidden = false,
        });

        Dead = Add(mod, new SolutionItem("DeadSolutionItem")
        {
            Rare = ItemRarityID.Gray,
            Potency = 4,
            Quality = ChemicalQuality.Inert,
            Principle = PrincipleType.Bane,
            AlmanacRarity = ItemRarityID.Blue,
        });

        Arcane = Add(mod, new SolutionItem("ArcaneSolution")
        {
            Rare = ItemRarityID.Blue,
            Potency = 1,
            Quality = ChemicalQuality.Catalytic,
            AlmanacRarity = ItemRarityID.Blue,
        });

        BloodtoothExtract = Add(mod, new SolutionItem("BloodtoothExtractItem")
        {
            Rare = ItemRarityID.Blue,
            Value = Item.sellPrice(copper: 40),
            Potency = 3,
            Quality = ChemicalQuality.Volatile,
            Principle = PrincipleType.Bane,
            AlmanacRarity = ItemRarityID.Green,
            LegacyNames = ["BloodtoothExtract"],
        });

        FibrousSlurry = Add(mod, new SoluteItem("FibrousSlurryItem")
        {
            Potency = 1,
            Quality = ChemicalQuality.Inert,
            Hidden = false,
            LegacyNames = ["FibrousSlurry"],
        });
    }

    /// <summary>One tonic per <see cref="PrincipleType" />, identical but for name: station outputs and potion vials.</summary>
    private static void LoadTonics(Mod mod)
    {
        HarmoniousTonic = AddTonic(mod, "HarmoniousTonic");
        VitalTonic = AddTonic(mod, "VitalTonic");
        SpecularTonic = AddTonic(mod, "SpecularTonic");
        MartialTonic = AddTonic(mod, "MartialTonic");
        FortunateTonic = AddTonic(mod, "FortunateTonic");
        BriskTonic = AddTonic(mod, "BriskTonic");
        BaneTonic = AddTonic(mod, "BaneTonic");
    }

    /// <param name="tonic">The old internal name, which is also the legacy name.</param>
    /// <remarks>Rated one star explicitly: that was their fallback potency, and 0 would read as "unrated".</remarks>
    private static int AddTonic(Mod mod, string tonic) =>
        Add(mod, new SolutionItem(tonic + "Item")
        {
            Value = Item.sellPrice(silver: 2),
            Potency = AlmanacEntryRegistry.DefaultIngredientPotency,
            LegacyNames = [tonic],
        });

    private static int Add(Mod mod, ChemicalItem item) => ChemicalItems.Add(mod, item, DIRECTORY + item.Name);
}
