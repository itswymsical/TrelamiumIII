using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Content.Rusted;
using TrelamiumIII.Content.Lodestone;
using TrelamiumIII.Content.Alchemy.MagnoliaContent;
using TrelamiumIII.Content.Alchemy.Solutions;
using TrelamiumIII.Content.Alchemy.Tiles;
using TrelamiumIII.Content.Alchemy.Reagents.Misc;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>
/// the almanac entry metadata (potency / chemical type / rarity) for the outputs we
/// curate. Anything not listed here falls back to <see cref="AlmanacEntryRegistry.For"/>'s
/// defaults, so this list is meant to grow incrementally as products get fleshed out.
/// <para>Rarity uses vanilla <c>ItemRarityID</c> values, chosen by the system's own
/// judgement rather than the item's own <c>Item.rare</c>.</para>
/// </summary>
internal static class AlmanacDefinitions
{
    public static void Register()
    {
        RegisterSolvents();

        RegisterIngredients();

        RegisterConventionalReagents();
    }

    private static void RegisterSolvents()
    {
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.BottledWater,
            Potency = 0,
            Hidden = false,
            Rarity = ItemRarityID.White,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Bottle,
            Potency = 0,
            Hidden = false,
            Chemical = ChemicalQuality.None,
            Rarity = ItemRarityID.White,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.BottledHoney,
            Potency = 3,
            Chemical = ChemicalQuality.Acidic,
            Rarity = ItemRarityID.Yellow,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<InertSolutionItem>(),
            Potency = 1,
            Hidden = false,
            Chemical = ChemicalQuality.Inert,
            Rarity = ItemRarityID.White,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<DeadSolutionItem>(),
            Potency = 4,
            Chemical = ChemicalQuality.Inert,
            Rarity = ItemRarityID.Blue,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<ArcaneSolution>(),
            Potency = 1,
            Chemical = ChemicalQuality.Catalytic,
            Rarity = ItemRarityID.Blue,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<MagnoliaSolutionItem>(),
            Potency = 2,
            Chemical = ChemicalQuality.Alkaline,
            Rarity = ItemRarityID.Green,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<BloodtoothExtractItem>(),
            Potency = 3,
            Chemical = ChemicalQuality.Volatile,
            Rarity = ItemRarityID.Green,
            Principle = PrincipleType.Bane,
        });

    }

    private static void RegisterIngredients()
    {
        #region Plants / Herbs
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Daybloom,
            Potency = 1,
            Chemical = ChemicalQuality.Catalytic,
            Hidden = false,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Blinkroot,
            Potency = 1,
            Chemical = ChemicalQuality.Catalytic,
            Hidden = false,
            Principle = PrincipleType.Specular | PrincipleType.Vital,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Moonglow,
            Potency = 3,
            Chemical = ChemicalQuality.Catalytic,
            Hidden = false,
            Principle = PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Deathweed,
            Potency = 2,
            Chemical = ChemicalQuality.Acidic,
            Rarity = ItemRarityID.LightPurple,
            Hidden = false,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Waterleaf,
            Potency = 2,
            Chemical = ChemicalQuality.Alkaline,
            Rarity = ItemRarityID.Blue,
            Hidden = false,
            Principle = PrincipleType.Brisk,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Fireblossom,
            Potency = 2,
            Chemical = ChemicalQuality.Volatile,
            Rarity = ItemRarityID.Orange,
            Hidden = false,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Shiverthorn,
            Potency = 3,
            Chemical = ChemicalQuality.Alkaline | ChemicalQuality.Catalytic,
            Rarity = ItemRarityID.Blue,
            Hidden = false,
            Principle = PrincipleType.Brisk,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<MagnoliaItem>(),
            Potency = 4,
            Chemical = ChemicalQuality.Alkaline | ChemicalQuality.Catalytic,
            Rarity = ItemRarityID.Green,
            Hidden = false,
            Principle = PrincipleType.Harmony | PrincipleType.Vital,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Sunflower,
            Potency = 2,
            Chemical = ChemicalQuality.Acidic,
            Principle = PrincipleType.Brisk,
        });
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.YellowMarigold,
            Potency = 2,
            Rarity = ItemRarityID.Blue,
            Principle = PrincipleType.Vital
        });
        #endregion

        #region Mushrooms
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<FlyAgaricItem>(),
            Potency = 2,
            Chemical = ChemicalQuality.Catalytic,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Mushroom,
            Potency = 1,
            Chemical = ChemicalQuality.Inert,
            Hidden = false,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.VileMushroom,
            Potency = 2,
            Chemical = ChemicalQuality.Acidic,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.ViciousMushroom,
            Potency = 2,
            Chemical = ChemicalQuality.Acidic,
            Principle = PrincipleType.Bane,
        });


        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<BloodtoothFungusItem>(),
            Potency = 4,
            Chemical = ChemicalQuality.Volatile,
            Rarity = ItemRarityID.Blue,
            Principle = PrincipleType.Martial,
        });
        #endregion

        #region Ores
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.CopperOre,
            Potency = 1,
            Chemical = ChemicalQuality.Alkaline,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.TinOre,
            Potency = 2,
            Chemical = ChemicalQuality.Catalytic,
            Principle = PrincipleType.Brisk,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.IronOre,
            Potency = 3,
            Chemical = ChemicalQuality.Kinetic,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.LeadOre,
            Potency = 3,
            Chemical = ChemicalQuality.Acidic,
            Principle = PrincipleType.Bane | PrincipleType.Harmony, // context dependent behavior
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.SilverOre,
            Potency = 3,
            Chemical = ChemicalQuality.Kinetic,
            Principle = PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.TungstenOre,
            Potency = 4,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.GoldOre,
            Potency = 4,
            Chemical = ChemicalQuality.Kinetic,
            Rarity = ItemRarityID.Orange,
            Principle = PrincipleType.Vital | PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.PlatinumOre,
            Potency = 5,
            Chemical = ChemicalQuality.Alkaline,
            Rarity = ItemRarityID.Orange,
            Principle = PrincipleType.Fortune,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.DemoniteOre,
            Potency = 4,
            Chemical = ChemicalQuality.Catalytic,
            Rarity = ItemRarityID.Blue,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.CrimtaneOre,
            Potency = 4,
            Chemical = ChemicalQuality.Catalytic,
            Rarity = ItemRarityID.Blue,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<LodestoneItem>(),
            Potency = 3,
            Chemical = ChemicalQuality.Kinetic,
            Rarity = ItemRarityID.Green,
            Principle = PrincipleType.Martial,
        });

        // Todo: hardmode & special ores
        #endregion

        #region Miscellaneous

        // Miscellaneous frfr

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.RottenChunk,
            Potency = 2,
            Chemical = ChemicalQuality.Acidic | ChemicalQuality.Inhibitor,
            Rarity = ItemRarityID.Blue,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Vertebrae,
            Potency = 2,
            Chemical = ChemicalQuality.Alkaline,
            Principle = PrincipleType.Martial,
        });

        // Fish

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.SpecularFish,
            Potency = 3,
            Chemical = ChemicalQuality.Catalytic,
            Principle = PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Damselfish,
            Potency = 4,
            Chemical = ChemicalQuality.Alkaline,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Bass,
            Potency = 1,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Stinkfish,
            Potency = 3,
            Chemical = ChemicalQuality.Acidic,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.FlarefinKoi,
            Potency = 5,
            Chemical = ChemicalQuality.Volatile,
            Principle = PrincipleType.Brisk,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Obsidifish,
            Potency = 4,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.ArmoredCavefish,
            Potency = 3,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.AtlanticCod,
            Potency = 1,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.DoubleCod,
            Potency = 2,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.NeonTetra,
            Potency = 2,
            Chemical = ChemicalQuality.Alkaline,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Ebonkoi,
            Potency = 2,
            Chemical = ChemicalQuality.Acidic | ChemicalQuality.Inhibitor,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Hemopiranha,
            Potency = 2,
            Chemical = ChemicalQuality.Volatile | ChemicalQuality.Inhibitor,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.CrimsonTigerfish,
            Potency = 1,
            Chemical = ChemicalQuality.Alkaline | ChemicalQuality.Inhibitor,
            Principle = PrincipleType.Brisk,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.FrostMinnow,
            Potency = 3,
            Chemical = ChemicalQuality.Alkaline | ChemicalQuality.Cryogen,
            Principle = PrincipleType.Brisk | PrincipleType.Vital,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.PrincessFish,
            Potency = 4,
            Chemical = ChemicalQuality.Kinetic,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Prismite,
            Potency = 5,
            Chemical = ChemicalQuality.Kinetic | ChemicalQuality.Catalytic,
            Principle = PrincipleType.Harmony,
        });

        // Materials

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Gel,
            Potency = 1,
            Chemical = ChemicalQuality.Alkaline | ChemicalQuality.Flammable,
            Principle = PrincipleType.Harmony | PrincipleType.Vital,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Stinger,
            Potency = 2,
            Chemical = ChemicalQuality.Acidic,
            Principle = PrincipleType.Martial | PrincipleType.Vital,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Amber,
            Potency = 3,
            Chemical = ChemicalQuality.Kinetic,
            Principle = PrincipleType.Martial | PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.SharkFin,
            Potency = 2,
            Chemical = ChemicalQuality.Volatile,
            Principle = PrincipleType.Brisk | PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.Lens,
            Potency = 1,
            Chemical = ChemicalQuality.None,
            Principle = PrincipleType.Specular,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ItemID.LifeCrystal,
            Potency = 3,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<CharcoalItem>(),
            Potency = 1,
            Chemical = ChemicalQuality.Kinetic,
            Principle = PrincipleType.None,
        });
        #endregion
    }

    /// <summary>
    /// The conventional chemistry line, plus the reagents it was built out of that had no entry
    /// and so were invisible to the book.
    /// </summary>
    /// <remarks>
    /// Ratings follow the Tria Prima framing in <c>docs/Alchemy/ConventionalReagents.md</c>:
    /// Aqua Regia caps the Salt line at five stars, Oil of Vitriol sits at four as the hub the
    /// rest of the tree hangs off. Only the three starter materials are unhidden -- the rest of
    /// the tree reveals itself through use.
    /// </remarks>
    private static void RegisterConventionalReagents()
    {
        #region Previously unlisted reagents
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<BottledOilItem>(),
            Potency = 2,
            Chemical = ChemicalQuality.Volatile,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<BrineBottleItem>(),
            Potency = 2,
            Chemical = ChemicalQuality.Inert | ChemicalQuality.Alkaline,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<FibrousSlurryItem>(),
            Potency = 1,
            Hidden = false,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<OxidizedCopperItem>(),
            Potency = 2,
            Chemical = ChemicalQuality.Acidic | ChemicalQuality.Catalytic,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<RustedNugget>(),
            Potency = 2,
            Chemical = ChemicalQuality.Acidic,
            Principle = PrincipleType.Bane | PrincipleType.Martial,
        });
        #endregion

        #region Raw minerals
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<SulfurItem>(),
            Potency = 2,
            Chemical = ChemicalQuality.Volatile | ChemicalQuality.Acidic,
            Principle = PrincipleType.Martial | PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<NiterItem>(),
            Potency = 2,
            Chemical = ChemicalQuality.Volatile | ChemicalQuality.Catalytic,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<RockSaltItem>(),
            Potency = 1,
            Hidden = false,
            Chemical = ChemicalQuality.Inert | ChemicalQuality.Alkaline,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<QuicklimeItem>(),
            Potency = 2,
            Chemical = ChemicalQuality.Alkaline,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<BoneAshItem>(),
            Potency = 2,
            Chemical = ChemicalQuality.Inert | ChemicalQuality.Alkaline,
            Principle = PrincipleType.Bane | PrincipleType.Vital,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<PotashItem>(),
            Potency = 1,
            Hidden = false,
            Chemical = ChemicalQuality.Alkaline,
            Principle = PrincipleType.Harmony,
        });
        #endregion

        #region Acids
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<OilOfVitriolItem>(),
            Potency = 4,
            Chemical = ChemicalQuality.Acidic,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<AquaFortisItem>(),
            Potency = 4,
            Chemical = ChemicalQuality.Acidic | ChemicalQuality.Volatile,
            Principle = PrincipleType.Bane | PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<SpiritOfSaltItem>(),
            Potency = 3,
            Chemical = ChemicalQuality.Acidic,
            Principle = PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<AquaRegiaItem>(),
            Potency = 5,
            Chemical = ChemicalQuality.Acidic | ChemicalQuality.Catalytic,
            Principle = PrincipleType.Fortune | PrincipleType.Bane,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<BlueVitriolItem>(),
            Potency = 3,
            Chemical = ChemicalQuality.Catalytic,
            Principle = PrincipleType.Specular,
        });
        #endregion

        #region Alkalis
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<SlakedLimeItem>(),
            Potency = 2,
            Chemical = ChemicalQuality.Alkaline | ChemicalQuality.Inert,
            Principle = PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<CausticLyeItem>(),
            Potency = 3,
            Chemical = ChemicalQuality.Alkaline,
            Principle = PrincipleType.Bane | PrincipleType.Harmony,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<FrigorificSaltItem>(),
            Potency = 3,
            Chemical = ChemicalQuality.Inert | ChemicalQuality.Volatile,
            Principle = PrincipleType.Brisk,
        });
        #endregion

        #region Distillates and powders
        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<SpiritOfOilItem>(),
            Potency = 3,
            Chemical = ChemicalQuality.Volatile,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<PitchItem>(),
            Potency = 2,
            Chemical = ChemicalQuality.Inert,
            Principle = PrincipleType.None,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<BlackPowderItem>(),
            Potency = 4,
            Chemical = ChemicalQuality.Volatile | ChemicalQuality.Kinetic,
            Principle = PrincipleType.Martial,
        });

        AlmanacEntryRegistry.Register(new AlmanacEntry
        {
            ItemType = ModContent.ItemType<ColdFireItem>(),
            Potency = 5,
            Chemical = ChemicalQuality.Volatile | ChemicalQuality.Catalytic,
            Principle = PrincipleType.Martial | PrincipleType.Specular,
        });
        #endregion
    }
}
