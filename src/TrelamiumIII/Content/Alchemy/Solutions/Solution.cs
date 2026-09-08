using ModLiquidLib.Utils;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.Alchemy.Solutions;

/// <summary>
///     The bottled half of the alchemy system: the solvents that go in the vial slot, and the
///     solutions that come back out of it. Station recipes live in
///     <c>ChemistryRecipeSystem.RegisterSolutions</c>.
/// </summary>
/// <remarks>
///     <para>
///         Each of these is one <see cref="ChemicalItem" /> instance rather than a class of its own --
///         the seven principle tonics were seven identical classes, which is what prompted this. An
///         item that needs real code subclasses the role class instead and autoloads under its own
///         name; <see cref="MagnoliaSolutionItem" /> is the one that does.
///     </para>
///     <para>
///         Internal names keep their old <c>...Item</c> spelling so saved items, sprites and
///         localisation keys still resolve, and <see cref="ChemicalItem.LegacyNames" /> carries
///         forward the names these went by before that.
///     </para>
/// </remarks>
public sealed class Solution : ICustomAutoload
{
    /// <summary>Sprites are named after the item, in this folder, as they were when each had a class.</summary>
    private const string DIRECTORY = $"{nameof(TrelamiumIII)}/Content/Alchemy/Solutions/";

    #region Solvents

    /// <summary>Salt water in a bottle. Boil it down and it is where half the salt line begins.</summary>
    public static int Brine { get; private set; }

    /// <summary>Thick, black, and patient. Everything it touches becomes easier to set alight.</summary>
    public static int BottledOil { get; private set; }

    #endregion

    #region Solutions

    /// <summary>The neutral base the rest of the tree is built on.</summary>
    public static int Inert { get; private set; }

    public static int Dead { get; private set; }

    public static int Arcane { get; private set; }

    public static int BloodtoothExtract { get; private set; }

    /// <summary>
    ///     Pulped plant matter. Thickens, binds, and asks for nothing. The one solute in this file --
    ///     it is bottled like the rest, and its sprite has always lived here.
    /// </summary>
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
        // Brine had two conflicting entries in AlmanacDefinitions; these are the values of the one
        // that was winning, plus the Hidden = false the second was reaching for.
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

    /// <summary>
    ///     One per <see cref="PrincipleType" />. They are outputs of the station and vials for the
    ///     potion recipes both, and differ from one another in nothing but name.
    /// </summary>
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

    /// <param name="tonic">
    ///     Its old internal name, which each of these was renamed away from to the <c>...Item</c>
    ///     spelling they carry now, and so is also the legacy name they need.
    /// </param>
    /// <remarks>
    ///     The tonics had no almanac entry at all before this, and fell through to
    ///     <see cref="AlmanacEntryRegistry.DefaultIngredientPotency" />. Stating that one star here
    ///     keeps them drawing as they did; authoring an entry of 0 would have read as "unrated".
    /// </remarks>
    private static int AddTonic(Mod mod, string tonic) =>
        Add(mod, new SolutionItem(tonic + "Item")
        {
            Value = Item.sellPrice(silver: 2),
            Potency = AlmanacEntryRegistry.DefaultIngredientPotency,
            LegacyNames = [tonic],
        });

    private static int Add(Mod mod, ChemicalItem item) => ChemicalItems.Add(mod, item, DIRECTORY + item.Name);
}
