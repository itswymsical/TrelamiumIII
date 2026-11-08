using ModLiquidLib.Utils;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.Alchemy.Solutions;

/// <summary>
///     Solvent-slot items and bottled solutes, each a <see cref="ChemicalItem" /> instance.
///     Internal names keep the old <c>...Item</c> spelling; <see cref="ChemicalItem.LegacyNames" /> covers earlier names.
/// </summary>
public sealed class SolventID : ICustomAutoload
{
    /// <summary>Sprites are named after the item, in this folder.</summary>
    private const string DIRECTORY = $"{nameof(TrelamiumIII)}/Content/Alchemy/Solutions/";

    /// <summary>Texture for items with no sprite in <see cref="DIRECTORY" />.</summary>
    private static readonly string Placeholder = Assets.Textures.PlaceholderBrew.KEY;

    #region Base solvents

    public static int AquaSalis => ModContent.ItemType<AquaSalis>();

    public static int BottledOil { get; private set; }

    #endregion

    #region Acids

    public static int OilOfVitriol { get; private set; }
    public static int AquaRegia { get; private set; }

    #endregion

    #region Brewed solvents

    public static int AquaInertis => ModContent.ItemType<AquaInertis>();

    public static int Dead { get; private set; }

    public static int BloodtoothExtract { get; private set; }

    #endregion

    #region Principle tonics

    public static int AquaHarmonia { get; private set; }

    public static int AquaVitae { get; private set; }

    public static int AquaSpecularis { get; private set; }

    public static int AquaFortis => ModContent.ItemType<AquaFortis>();

    public static int AquaFortunae { get; private set; }

    public static int AquaVivida { get; private set; }

    public static int AquaPestis { get; private set; }

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
        BottledOil = Add(mod, new SolventItem("BottledOilItem")
        {
            Value = Item.sellPrice(silver: 2),
            AlmanacPotency = 2,
            Temperaments = Temperament.Volatile,
            LegacyNames = ["BottledOil"],
            Recipes = self => self.CreateRecipe()
                .AddIngredient(ItemID.Bottle)
                .AddLiquid<OilLiquids>()
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
            AlmanacPotency = 4,
            Temperaments = Temperament.Acidic,
        });

        AquaRegia = Add(mod, new SolventItem("AquaRegiaItem")
        {
            TexturePath = Placeholder,
            Rare = ItemRarityID.Orange,
            Value = Item.sellPrice(silver: 20),
            AlmanacPotency = 5,
            Temperaments = Temperament.Acidic | Temperament.Catalytic,
        });
    }

    private static void LoadBrewedSolvents(Mod mod)
    {
        Dead = Add(mod, new SolventItem("DeadSolutionItem")
        {
            Rare = ItemRarityID.Gray,
            AlmanacPotency = 4,
            Temperaments = Temperament.Inert,
            Principle = PrincipleType.Bane,
            AlmanacRarity = ItemRarityID.Blue,
        });

        BloodtoothExtract = Add(mod, new SolventItem("BloodtoothExtractItem")
        {
            Rare = ItemRarityID.Blue,
            Value = Item.sellPrice(copper: 40),
            AlmanacPotency = 3,
            Temperaments = Temperament.Volatile,
            Principle = PrincipleType.Bane,
            AlmanacRarity = ItemRarityID.Green,
            LegacyNames = ["BloodtoothExtract"],
        });
    }

    /// <summary>One tonic per <see cref="PrincipleType" />, differing only in name and principle.</summary>
    private static void LoadTonics(Mod mod)
    {
        AquaHarmonia = AddTonic(mod, "HarmoniousTonic", PrincipleType.Harmony);
        AquaVitae = AddTonic(mod, "VitalTonic", PrincipleType.Vital);
        AquaSpecularis = AddTonic(mod, "SpecularTonic", PrincipleType.Specular);
        AquaFortunae = AddTonic(mod, "FortunateTonic", PrincipleType.Fortune);
        AquaVivida = AddTonic(mod, "BriskTonic", PrincipleType.Brisk);
        AquaPestis = AddTonic(mod, "BaneTonic", PrincipleType.Bane);
    }

    private static void LoadSolutes(Mod mod)
    {
        FibrousSlurry = Add(mod, new SolventItem("FibrousSlurryItem")
        {
            AlmanacPotency = 1,
            Temperaments = Temperament.Inert,
            Hidden = false,
            LegacyNames = ["FibrousSlurry"],
        });
    }

    /// <param name="tonic">The old internal name, which is also the legacy name.</param>
    /// <remarks>Rated one star explicitly: 0 would draw as unrated.</remarks>
    private static int AddTonic(Mod mod, string tonic, PrincipleType principle) =>
        Add(mod, new TonicItem(tonic + "Item", principle)
        {
            Value = Item.sellPrice(silver: 2),
            AlmanacPotency = AlmanacEntryRegistry.DefaultAlmanacPotency,
            LegacyNames = [tonic],
        });

    private static int Add(Mod mod, ChemicalItem item) => ChemicalItems.Add(mod, item, DIRECTORY + item.Name);
}
