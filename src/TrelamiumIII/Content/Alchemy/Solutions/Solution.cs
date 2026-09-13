using ModLiquidLib.Utils;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.Alchemy.Solutions;

/// <summary>
///     Solvent-slot items and bottled solutes, each a <see cref="ChemicalItem" /> instance.
///     Internal names keep the old <c>...Item</c> spelling; <see cref="ChemicalItem.LegacyNames" /> covers earlier names.
/// </summary>
public sealed class Solution : ICustomAutoload
{
    /// <summary>Sprites are named after the item, in this folder.</summary>
    private const string DIRECTORY = $"{nameof(TrelamiumIII)}/Content/Alchemy/Solutions/";

    /// <summary>Texture for items with no sprite in <see cref="DIRECTORY" />.</summary>
    private static readonly string Placeholder = AssetReferences.Assets.Textures.PlaceholderSolute.KEY;

    #region Base solvents

    public static int Brine { get; private set; }

    public static int BottledOil { get; private set; }

    #endregion

    #region Acids

    public static int OilOfVitriol { get; private set; }

    public static int AquaFortis { get; private set; }

    public static int AquaRegia { get; private set; }

    #endregion

    #region Brewed solvents

    public static int Inert { get; private set; }

    public static int Dead { get; private set; }

    public static int BloodtoothExtract { get; private set; }

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

    #region Solutes

    public static int FibrousSlurry { get; private set; }

    #endregion

    public static void Autoload(Mod mod)
    {
        LoadBaseSolvents(mod);
        LoadAcids(mod);
        LoadBrewedSolvents(mod);
        LoadTonics(mod);
        LoadSolutes(mod);
    }

    private static void LoadBaseSolvents(Mod mod)
    {
        Brine = Add(mod, new SolventItem("BrineBottleItem")
        {
            Rare = ItemRarityID.Blue,
            Potency = 2,
            Quality = ChemicalQuality.Inert | ChemicalQuality.Alkaline,
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
            LegacyNames = ["BottledOil"],
            Recipes = self => self.CreateRecipe()
                .AddIngredient(ItemID.Bottle)
                .AddLiquid<CrudeOilLiquid>()
                .Register(),
        });
    }

    private static void LoadAcids(Mod mod)
    {
        OilOfVitriol = Add(mod, new SolventItem("OilOfVitriolItem")
        {
            TexturePath = Placeholder,
            Rare = ItemRarityID.Blue,
            Value = Item.sellPrice(silver: 2),
            Potency = 4,
            Quality = ChemicalQuality.Acidic,
        });

        AquaFortis = Add(mod, new SolventItem("AquaFortisItem")
        {
            TexturePath = Placeholder,
            Rare = ItemRarityID.Blue,
            Value = Item.sellPrice(silver: 5),
            Potency = 4,
            Quality = ChemicalQuality.Acidic | ChemicalQuality.Volatile,
        });

        AquaRegia = Add(mod, new SolventItem("AquaRegiaItem")
        {
            TexturePath = Placeholder,
            Rare = ItemRarityID.Orange,
            Value = Item.sellPrice(silver: 20),
            Potency = 5,
            Quality = ChemicalQuality.Acidic | ChemicalQuality.Catalytic,
        });
    }

    private static void LoadBrewedSolvents(Mod mod)
    {
        Inert = Add(mod, new SolventItem("InertSolutionItem")
        {
            Rare = ItemRarityID.Gray,
            Potency = 1,
            Quality = ChemicalQuality.Inert,
            AlmanacRarity = ItemRarityID.White,
            Hidden = false,
        });

        Dead = Add(mod, new SolventItem("DeadSolutionItem")
        {
            Rare = ItemRarityID.Gray,
            Potency = 4,
            Quality = ChemicalQuality.Inert,
            Principle = PrincipleType.Bane,
            AlmanacRarity = ItemRarityID.Blue,
        });

        BloodtoothExtract = Add(mod, new SolventItem("BloodtoothExtractItem")
        {
            Rare = ItemRarityID.Blue,
            Value = Item.sellPrice(copper: 40),
            Potency = 3,
            Quality = ChemicalQuality.Volatile,
            Principle = PrincipleType.Bane,
            AlmanacRarity = ItemRarityID.Green,
            LegacyNames = ["BloodtoothExtract"],
        });
    }

    /// <summary>One tonic per <see cref="PrincipleType" />, differing only in name and principle.</summary>
    private static void LoadTonics(Mod mod)
    {
        HarmoniousTonic = AddTonic(mod, "HarmoniousTonic", PrincipleType.Harmony);
        VitalTonic = AddTonic(mod, "VitalTonic", PrincipleType.Vital);
        SpecularTonic = AddTonic(mod, "SpecularTonic", PrincipleType.Specular);
        MartialTonic = AddTonic(mod, "MartialTonic", PrincipleType.Martial);
        FortunateTonic = AddTonic(mod, "FortunateTonic", PrincipleType.Fortune);
        BriskTonic = AddTonic(mod, "BriskTonic", PrincipleType.Brisk);
        BaneTonic = AddTonic(mod, "BaneTonic", PrincipleType.Bane);
    }

    private static void LoadSolutes(Mod mod)
    {
        FibrousSlurry = Add(mod, new SoluteItem("FibrousSlurryItem")
        {
            Potency = 1,
            Quality = ChemicalQuality.Inert,
            Hidden = false,
            LegacyNames = ["FibrousSlurry"],
        });
    }

    /// <param name="tonic">The old internal name, which is also the legacy name.</param>
    /// <remarks>Rated one star explicitly: 0 would draw as unrated.</remarks>
    private static int AddTonic(Mod mod, string tonic, PrincipleType principle) =>
        Add(mod, new SolventItem(tonic + "Item")
        {
            Value = Item.sellPrice(silver: 2),
            Potency = AlmanacEntryRegistry.DefaultIngredientPotency,
            Principle = principle,
            LegacyNames = [tonic],
        });

    private static int Add(Mod mod, ChemicalItem item) => ChemicalItems.Add(mod, item, DIRECTORY + item.Name);
}
