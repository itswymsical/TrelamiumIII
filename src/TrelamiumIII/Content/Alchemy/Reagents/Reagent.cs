using TrelamiumIII.Content.Alchemy.Solutions;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.Alchemy.Reagents;

/// <summary>
///     Conventional solutes (raw minerals, acid products, alkalis, distillates), each a <see cref="SoluteItem" /> instance.
///     Internal names keep the old <c>...Item</c> spelling so saves and localisation keys still resolve.
/// </summary>
public sealed class Reagent : ICustomAutoload
{
    /// <summary>What a reagent draws while it has no sprite of its own, which is most of them.</summary>
    private static readonly string Placeholder = AssetReferences.Assets.Textures.PlaceholderSolute.KEY;

    #region Raw minerals

    /// <summary>Brimstone from ash. Also places as a tile.</summary>
    public static int Sulfur { get; private set; }

    /// <summary>Saltpetre. Barely burns on its own; makes everything beside it burn harder.</summary>
    public static int Niter { get; private set; }

    /// <summary>Boiled out of brine.</summary>
    public static int RockSalt { get; private set; }

    /// <summary>Burnt limestone. Meets water and gets hot about it.</summary>
    public static int Quicklime { get; private set; }

    public static int BoneAsh { get; private set; }

    public static int Potash { get; private set; }

    #endregion

    #region Acid products

    public static int SpiritOfSalt { get; private set; }

    public static int BlueVitriol { get; private set; }

    #endregion

    #region Alkalis

    /// <summary>Quicklime that has already had its argument with water. Inert, and a fire suppressant.</summary>
    public static int SlakedLime { get; private set; }

    public static int CausticLye { get; private set; }

    /// <summary>A freezing mixture. Draws heat in rather than giving it off.</summary>
    public static int FrigorificSalt { get; private set; }

    #endregion

    #region Distillates and powders

    /// <summary>Naphtha. The lightest thing to come off the still and the most eager to catch.</summary>
    public static int SpiritOfOil { get; private set; }

    /// <summary>What is left in the bottom of the still. Slow, sticky, and reluctant.</summary>
    public static int Pitch { get; private set; }

    /// <summary>
    ///     Six parts niter to one of fuel. The intended anchor for <c>ChemicalDamageType.Kinetic</c>,
    ///     which currently has no matchup rows at all.
    /// </summary>
    public static int BlackPowder { get; private set; }

    /// <summary>Phosphorus. Catches on contact with plain air, with no spark offered to it.</summary>
    public static int ColdFire { get; private set; }

    #endregion

    public static void Autoload(Mod mod)
    {
        LoadMinerals(mod);
        LoadAcidProducts(mod);
        LoadAlkalis(mod);
        LoadDistillates(mod);
    }

    /// <summary>
    ///     The raw tier. Each is roasted or boiled out of something the world already provides, so this
    ///     tier adds no ores, no worldgen, and only the one tile.
    /// </summary>
    private static void LoadMinerals(Mod mod)
    {
        Sulfur = Add(mod, new SoluteItem("SulfurItem")
        {
            TexturePath = AssetReferences.Content.Alchemy.Reagents.Sulfur.KEY,
            Value = Item.sellPrice(copper: 40),
            Potency = 2,
            Quality = ChemicalQuality.Volatile | ChemicalQuality.Acidic,
            Principle = PrincipleType.Martial | PrincipleType.Bane,

            // Last: this resizes the item and makes it placeable.
            Defaults = item => item.DefaultToPlaceableTile(ModContent.TileType<SulfurTile>()),

            Recipes = self => self.CreateRecipe(2)
                .AddIngredient(ItemID.AshBlock, 10)
                .AddTile(TileID.Furnaces)
                .Register(),
        });

        Niter = Add(mod, new SoluteItem("NiterItem")
        {
            Value = Item.sellPrice(copper: 60),
            Potency = 2,
            Quality = ChemicalQuality.Volatile | ChemicalQuality.Catalytic,
            Principle = PrincipleType.Bane,
            Recipes = self =>
            {
                self.CreateRecipe(2)
                    .AddIngredient(ItemID.RottenChunk, 3)
                    .AddIngredient(ItemID.StoneBlock, 5)
                    .AddTile(TileID.Furnaces)
                    .Register();

                self.CreateRecipe(2)
                    .AddIngredient(ItemID.Vertebrae, 3)
                    .AddIngredient(ItemID.StoneBlock, 5)
                    .AddTile(TileID.Furnaces)
                    .Register();
            },
        });

        RockSalt = Add(mod, new SoluteItem("RockSaltItem")
        {
            TexturePath = AssetReferences.Content.Alchemy.Reagents.RockSalt.KEY,
            Value = Item.sellPrice(copper: 20),
            Potency = 1,
            Quality = ChemicalQuality.Inert | ChemicalQuality.Alkaline,
            Principle = PrincipleType.Harmony,
            Hidden = false,
            Recipes = self => self.CreateRecipe(3)
                .AddIngredient(Solution.Brine)
                .AddTile(TileID.Furnaces)
                .Register(),
        });

        Quicklime = Add(mod, new SoluteItem("QuicklimeItem")
        {
            Value = Item.sellPrice(copper: 50),
            Potency = 2,
            Quality = ChemicalQuality.Alkaline,
            Principle = PrincipleType.Bane,
            Recipes = self => self.CreateRecipe(2)
                .AddIngredient(ItemID.MarbleBlock, 5)
                .AddTile(TileID.Furnaces)
                .Register(),
        });

        BoneAsh = Add(mod, new SoluteItem("BoneAshItem")
        {
            Value = Item.sellPrice(copper: 50),
            Potency = 2,
            Quality = ChemicalQuality.Inert | ChemicalQuality.Alkaline,
            Principle = PrincipleType.Bane | PrincipleType.Vital,
            Recipes = self => self.CreateRecipe(2)
                .AddIngredient(ItemID.Bone, 5)
                .AddTile(TileID.Furnaces)
                .Register(),
        });

        Potash = Add(mod, new SoluteItem("PotashItem")
        {
            Rare = ItemRarityID.Gray,
            Value = Item.sellPrice(copper: 10),
            Potency = 1,
            Quality = ChemicalQuality.Alkaline,
            Principle = PrincipleType.Harmony,
            Hidden = false,
            Recipes = self => self.CreateRecipe(2)
                .AddIngredient(ItemID.Wood, 20)
                .AddTile(TileID.Furnaces)
                .Register(),
        });
    }

    /// <summary>Solutes produced by the acid solvents in <see cref="Solution" />.</summary>
    private static void LoadAcidProducts(Mod mod)
    {
        SpiritOfSalt = Add(mod, new SoluteItem("SpiritOfSaltItem")
        {
            Rare = ItemRarityID.Blue,
            Value = Item.sellPrice(silver: 2),
            Potency = 3,
            Quality = ChemicalQuality.Acidic,
            Principle = PrincipleType.Bane,
        });

        BlueVitriol = Add(mod, new SoluteItem("BlueVitriolItem")
        {
            Rare = ItemRarityID.Blue,
            Value = Item.sellPrice(silver: 4),
            Potency = 3,
            Quality = ChemicalQuality.Catalytic,
            Principle = PrincipleType.Specular,
        });
    }

    /// <summary>
    ///     The neutralising and heat-suppressing half of the line; the only reagents that push a burn metric
    ///     <em>down</em>.
    /// </summary>
    private static void LoadAlkalis(Mod mod)
    {
        SlakedLime = Add(mod, new SoluteItem("SlakedLimeItem")
        {
            Value = Item.sellPrice(copper: 80),
            Potency = 2,
            Quality = ChemicalQuality.Alkaline | ChemicalQuality.Inert,
            Principle = PrincipleType.Harmony,
        });

        CausticLye = Add(mod, new SoluteItem("CausticLyeItem")
        {
            Rare = ItemRarityID.Blue,
            Value = Item.sellPrice(silver: 2),
            Potency = 3,
            Quality = ChemicalQuality.Alkaline,
            Principle = PrincipleType.Bane | PrincipleType.Harmony,
        });

        FrigorificSalt = Add(mod, new SoluteItem("FrigorificSaltItem")
        {
            Rare = ItemRarityID.Blue,
            Value = Item.sellPrice(silver: 3),
            Potency = 3,
            Quality = ChemicalQuality.Inert | ChemicalQuality.Volatile,
            Principle = PrincipleType.Brisk,
        });
    }

    /// <summary>The fuel tier. Spirit of Oil and Pitch come from the same distillation, so they register as a pair.</summary>
    private static void LoadDistillates(Mod mod)
    {
        SpiritOfOil = Add(mod, new SoluteItem("SpiritOfOilItem")
        {
            Rare = ItemRarityID.Blue,
            Value = Item.sellPrice(silver: 2),
            Potency = 3,
            Quality = ChemicalQuality.Volatile,
            Principle = PrincipleType.Martial,
        });

        Pitch = Add(mod, new SoluteItem("PitchItem")
        {
            Value = Item.sellPrice(copper: 80),
            Potency = 2,
            Quality = ChemicalQuality.Inert,
        });

        BlackPowder = Add(mod, new SoluteItem("BlackPowderItem")
        {
            Rare = ItemRarityID.Orange,
            Value = Item.sellPrice(silver: 6),
            Potency = 4,
            Quality = ChemicalQuality.Volatile | ChemicalQuality.Kinetic,
            Principle = PrincipleType.Martial,
        });

        ColdFire = Add(mod, new SoluteItem("ColdFireItem")
        {
            Rare = ItemRarityID.Orange,
            Value = Item.sellPrice(silver: 15),
            Potency = 5,
            Quality = ChemicalQuality.Volatile | ChemicalQuality.Catalytic,
            Principle = PrincipleType.Martial | PrincipleType.Specular,
        });
    }

    private static int Add(Mod mod, SoluteItem reagent) => ChemicalItems.Add(mod, reagent, Placeholder);
}
