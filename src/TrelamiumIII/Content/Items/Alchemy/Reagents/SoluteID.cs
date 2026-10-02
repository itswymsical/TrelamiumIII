using TrelamiumIII.Content.Items.Alchemy.Solutions;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.Items.Alchemy.Reagents;

/// <summary>
///     Conventional solutes (raw minerals, acid products, alkalis, distillates), each a <see cref="SoluteItem" /> instance.
/// </summary>
public sealed class SoluteID : ICustomAutoload
{
    private static readonly string Placeholder = AssetReferences.Assets.Textures.PlaceholderSolute.KEY;

    #region Raw minerals
    public static int Sulfur { get; private set; }
    public static int Niter { get; private set; }
    public static int RockSalt { get; private set; }
    public static int Quicklime { get; private set; }
    public static int BoneAsh { get; private set; }
    public static int Potash { get; private set; }
    public static int Cinnabar { get; private set; }
    public static int Quicksilver { get; private set; }

    #endregion

    #region Acid products
    public static int SpiritOfSalt { get; private set; }
    public static int BlueVitriol { get; private set; }

    #endregion

    #region Alkalis
    public static int SlakedLime { get; private set; }
    public static int CausticLye { get; private set; }
    public static int FrigorificSalt { get; private set; }
    #endregion

    #region Distillates and powders
    public static int SpiritOfOil { get; private set; }
    public static int Pitch { get; private set; }
    public static int BlackPowder { get; private set; }
    public static int ColdFire { get; private set; }
    #endregion

    public static void Autoload(Mod mod)
    {
        LoadMinerals(mod);
        LoadAcidProducts(mod);
        LoadAlkalis(mod);
        LoadDistillates(mod);
    }

    private static void LoadMinerals(Mod mod)
    {
        Sulfur = Add(mod, new SoluteItem("SulfurItem")
        {
            TexturePath = AssetReferences.Content.Items.Alchemy.Reagents.Sulfur.KEY,
            Value = Item.sellPrice(copper: 2),
            AlmanacPotency = 2,
            Temperaments = Temperament.Volatile | Temperament.Acidic,
            Principle = PrincipleType.Martial | PrincipleType.Bane,

            Defaults = item => item.DefaultToPlaceableTile(ModContent.TileType<SulfurTile>()),

            Recipes = self => self.CreateRecipe(2)
                .AddIngredient(ItemID.AshBlock, 10)
                .AddTile(TileID.Furnaces)
                .Register(),
        });

        Cinnabar = Add(mod, new SoluteItem("CinnabarItem")
        {
            TexturePath = AssetReferences.Content.Items.Alchemy.Reagents.Cinnabar.KEY,
            Value = Item.sellPrice(copper: 11),
            AlmanacPotency = 3,
            Temperaments = Temperament.Kinetic,
            Principle = PrincipleType.Vital | PrincipleType.Bane,

            Defaults = item => item.DefaultToPlaceableTile(ModContent.TileType<CinnabarTile>()),
        });

        Quicksilver = Add(mod, new SoluteItem("QuicksilverItem")
        {
            TexturePath = Placeholder,
            Value = Item.sellPrice(copper: 4),
            AlmanacPotency = 2,
            Temperaments = Temperament.Volatile | Temperament.Inert,
            Principle = PrincipleType.Bane | PrincipleType.Brisk,
        });

        Niter = Add(mod, new SoluteItem("NiterItem")
        {
            TexturePath = AssetReferences.Content.Items.Alchemy.Reagents.Niter.KEY,
            Value = Item.sellPrice(copper: 60),
            AlmanacPotency = 2,
            Temperaments = Temperament.Volatile | Temperament.Catalytic,
            Principle = PrincipleType.Bane,
            Recipes = self =>
            {
                self.CreateRecipe(3)
                    .AddIngredient(ItemID.RottenChunk, 2)
                    .AddIngredient(ItemID.StoneBlock, 5)
                    .AddTile(TileID.Furnaces)
                    .Register();

                self.CreateRecipe(3)
                    .AddIngredient(ItemID.Vertebrae, 2)
                    .AddIngredient(ItemID.StoneBlock, 5)
                    .AddTile(TileID.Furnaces)
                    .Register();
            },
        });

        RockSalt = Add(mod, new SoluteItem("RockSaltItem")
        {
            TexturePath = AssetReferences.Content.Items.Alchemy.Reagents.RockSalt.KEY,
            Value = Item.sellPrice(copper: 20),
            AlmanacPotency = 1,
            Temperaments = Temperament.Inert | Temperament.Alkaline,
            Principle = PrincipleType.Harmony,
            Hidden = false,

            Defaults = item => item.DefaultToPlaceableTile(ModContent.TileType<RockSaltTile>()),

            Recipes = self => self.CreateRecipe(3)
                .AddIngredient(SolventID.Brine)
                .AddTile(TileID.Furnaces)
                .Register(),
        });

        Quicklime = Add(mod, new SoluteItem("QuicklimeItem")
        {
            Value = Item.sellPrice(copper: 50),
            AlmanacPotency = 2,
            Temperaments = Temperament.Alkaline,
            Principle = PrincipleType.Bane,
            Recipes = self => self.CreateRecipe(2)
                .AddIngredient(ItemID.MarbleBlock, 5)
                .AddTile(TileID.Furnaces)
                .Register(),
        });

        BoneAsh = Add(mod, new SoluteItem("BoneAshItem")
        {
            Value = Item.sellPrice(copper: 50),
            AlmanacPotency = 2,
            Temperaments = Temperament.Inert | Temperament.Alkaline,
            Principle = PrincipleType.Bane | PrincipleType.Vital,
            Recipes = self => self.CreateRecipe(2)
                .AddIngredient(ItemID.Bone, 5)
                .AddTile(TileID.Furnaces)
                .Register(),
        });

        Potash = Add(mod, new SoluteItem("PotashItem")
        {
            TexturePath = AssetReferences.Content.Items.Alchemy.Reagents.Potash.KEY,
            Rare = ItemRarityID.Gray,
            Value = Item.sellPrice(copper: 10),
            AlmanacPotency = 1,
            Temperaments = Temperament.Alkaline,
            Principle = PrincipleType.Harmony,
            Hidden = false,
            Recipes = self => self.CreateRecipe(2)
                .AddIngredient(ItemID.Wood, 12)
                .AddTile(TileID.Furnaces)
                .Register(),
        });
    }

    private static void LoadAcidProducts(Mod mod)
    {
        SpiritOfSalt = Add(mod, new SoluteItem("SpiritOfSaltItem")
        {
            Rare = ItemRarityID.Blue,
            Value = Item.sellPrice(silver: 2),
            AlmanacPotency = 3,
            Temperaments = Temperament.Acidic,
            Principle = PrincipleType.Bane,
        });

        BlueVitriol = Add(mod, new SoluteItem("BlueVitriolItem")
        {
            Rare = ItemRarityID.Blue,
            Value = Item.sellPrice(silver: 4),
            AlmanacPotency = 3,
            Temperaments = Temperament.Catalytic,
            Principle = PrincipleType.Specular,
        });
    }

    private static void LoadAlkalis(Mod mod)
    {
        SlakedLime = Add(mod, new SoluteItem("SlakedLimeItem")
        {
            Value = Item.sellPrice(copper: 80),
            AlmanacPotency = 2,
            Temperaments = Temperament.Alkaline | Temperament.Inert,
            Principle = PrincipleType.Harmony,
        });

        CausticLye = Add(mod, new SoluteItem("CausticLyeItem")
        {
            Rare = ItemRarityID.Blue,
            Value = Item.sellPrice(silver: 2),
            AlmanacPotency = 3,
            Temperaments = Temperament.Alkaline,
            Principle = PrincipleType.Bane | PrincipleType.Harmony,
        });

        FrigorificSalt = Add(mod, new SoluteItem("FrigorificSaltItem")
        {
            Rare = ItemRarityID.Blue,
            Value = Item.sellPrice(silver: 3),
            AlmanacPotency = 3,
            Temperaments = Temperament.Inert | Temperament.Volatile,
            Principle = PrincipleType.Brisk,
        });
    }

    private static void LoadDistillates(Mod mod)
    {
        SpiritOfOil = Add(mod, new SoluteItem("SpiritOfOilItem")
        {
            TexturePath = Assets.Textures.PlaceholderBrew.KEY,
            Rare = ItemRarityID.Blue,
            Value = Item.sellPrice(silver: 2),
            AlmanacPotency = 3,
            Temperaments = Temperament.Volatile,
            Principle = PrincipleType.Martial,
        });

        Pitch = Add(mod, new SoluteItem("PitchItem")
        {
            TexturePath = AssetReferences.Content.Items.Alchemy.Reagents.Pitch.KEY,
            Value = Item.sellPrice(copper: 80),
            AlmanacPotency = 2,
            Temperaments = Temperament.Inert,
        });

        BlackPowder = Add(mod, new SoluteItem("BlackPowderItem")
        {
            Rare = ItemRarityID.Orange,
            Value = Item.sellPrice(silver: 6),
            AlmanacPotency = 4,
            Temperaments = Temperament.Volatile | Temperament.Kinetic,
            Principle = PrincipleType.Martial,
        });

        ColdFire = Add(mod, new SoluteItem("ColdFireItem")
        {
            Rare = ItemRarityID.Orange,
            Value = Item.sellPrice(silver: 15),
            AlmanacPotency = 5,
            Temperaments = Temperament.Volatile | Temperament.Catalytic,
            Principle = PrincipleType.Martial | PrincipleType.Specular,
        });
    }

    private static int Add(Mod mod, SoluteItem reagent) => ChemicalItems.Add(mod, reagent, Placeholder);
}