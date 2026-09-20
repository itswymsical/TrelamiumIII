using System.Collections.Generic;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Content.Alchemy.Reagents.Misc;
using TrelamiumIII.Content.Alchemy.Solutions;
using TrelamiumIII.Content.FarmingOverhaul.SatchelContent;

namespace TrelamiumIII.Core.Alchemy;

/// <summary>A named set of item types that one <see cref="AlchemyRecipe"/> component slot accepts.</summary>
public sealed class AlchemyComponentGroup
{
    public string Name { get; }

    private readonly HashSet<int> _itemTypes;

    public IReadOnlyCollection<int> ItemTypes => _itemTypes;

    /// <summary>Number of item types in the group. Used by the matcher to prefer specifics over groups.</summary>
    public int Count => _itemTypes.Count;

    public AlchemyComponentGroup(string name, params int[] itemTypes)
    {
        Name = name;
        _itemTypes = new HashSet<int>(itemTypes);
    }

    public AlchemyComponentGroup Add(params int[] itemTypes)
    {
        foreach (var t in itemTypes) _itemTypes.Add(t);
        return this;
    }

    public bool Contains(int itemType) => _itemTypes.Contains(itemType);

    public void Clear() => _itemTypes.Clear();
}

/// <summary>
///     Component groups shared by the alchemy recipes. Declared here and filled by <see cref="Populate" />, which
///     <c>ChemistryRecipeSystem.PostSetupContent</c> calls once content and other mods have loaded.
/// </summary>
public static class AlchemyRecipeGroups
{
    public static readonly AlchemyComponentGroup AnyOre = new("AnyOre");

    public static readonly AlchemyComponentGroup Critters = new("Critters");

    public static readonly AlchemyComponentGroup Iron = new("Iron");
    public static readonly AlchemyComponentGroup Silver = new("Silver");
    public static readonly AlchemyComponentGroup Gold = new("Gold");
    public static readonly AlchemyComponentGroup SpecularFishOrMoonglow = new("SpecularFishOrMoonglow");

    public static readonly AlchemyComponentGroup AnyEvilOre = new("AnyEvilOre");
    public static readonly AlchemyComponentGroup Mushroom = new("AnyStandardMushroom");
    public static readonly AlchemyComponentGroup AnyEvilMushroom = new("AnyEvilMushroom");
    public static readonly AlchemyComponentGroup CorruptTiles = new("CorruptTiles");
    public static readonly AlchemyComponentGroup BloodTiles = new("BloodTiles");
    public static readonly AlchemyComponentGroup AnyHerb = new("AnyHerb");
    public static readonly AlchemyComponentGroup RottenChunkVertabrae = new("AnyEvilDrop");
    public static readonly AlchemyComponentGroup AnyStandardFish = new("AnyStandardFish");
    public static readonly AlchemyComponentGroup AnySoil = new("AnySoil");
    public static readonly AlchemyComponentGroup DirtOrMud = new("DirtOrMud");
    public static readonly AlchemyComponentGroup AnyButterfly = new("AnyButterfly");
    public static readonly AlchemyComponentGroup AnyBaseBottle = new("AnyBaseBottle");

    public static readonly AlchemyComponentGroup ShiverthornOrWaterleaf = new("ShiverthornOrWaterleaf");
    public static readonly AlchemyComponentGroup ShiverthornOrSeeds = new("ShiverthornOrSeeds");

    // Conventional chemistry, so recipes can ask for "any acid" rather than naming each one.
    public static readonly AlchemyComponentGroup AnyAcid = new("AnyAcid");
    public static readonly AlchemyComponentGroup AnyAlkali = new("AnyAlkali");
    public static readonly AlchemyComponentGroup AnyMineralSalt = new("AnyMineralSalt");

    // Declared after the groups: static initializers run in textual order.
    private static readonly AlchemyComponentGroup[] All =
    [
        AnyOre, Critters, Iron, Silver, Gold, SpecularFishOrMoonglow, AnyEvilOre, Mushroom, AnyEvilMushroom,
        CorruptTiles, BloodTiles, AnyHerb, RottenChunkVertabrae, AnyStandardFish, AnySoil, DirtOrMud, AnyButterfly,
        AnyBaseBottle, ShiverthornOrWaterleaf, ShiverthornOrSeeds, AnyAcid, AnyAlkali, AnyMineralSalt,
    ];

    /// <summary>
    ///     Fills every group. Cannot be folded into the declarations above: <see cref="SoluteID" />,
    ///     <see cref="SolventID" /> and <see cref="ModContent.ItemType{T}" /> all read 0 until content
    ///     autoloads, and the cross-mod herb depends on which mods are loaded this session.
    /// </summary>
    public static void Populate()
    {
        AnyAcid.Add(
            SolventID.OilOfVitriol, SolventID.AquaFortis,
            SoluteID.SpiritOfSalt, SolventID.AquaRegia,
            SoluteID.BlueVitriol);

        AnyAlkali.Add(
            SoluteID.SlakedLime, SoluteID.CausticLye,
            SoluteID.Quicklime, SoluteID.Potash);

        AnyMineralSalt.Add(
            SoluteID.RockSalt, SoluteID.Niter,
            SoluteID.FrigorificSalt);

        Iron.Add(ItemID.IronOre, ItemID.LeadOre);

        Silver.Add(ItemID.SilverOre, ItemID.TungstenOre);

        Gold.Add(ItemID.GoldOre, ItemID.PlatinumOre);

        Critters.Add(
            ItemID.Bunny, ItemID.Squirrel, ItemID.Mouse, ItemID.Rat, ItemID.Frog, ItemID.Worm, ItemID.Snail);

        AnyOre.Add(
            ItemID.CopperOre, ItemID.TinOre, ItemID.IronOre, ItemID.LeadOre,
            ItemID.SilverOre, ItemID.TungstenOre, ItemID.GoldOre, ItemID.PlatinumOre);

        AnyEvilOre.Add(ItemID.DemoniteOre, ItemID.CrimtaneOre);

        AnyStandardFish.Add(
            ItemID.Bass, ItemID.RedSnapper, ItemID.AtlanticCod, ItemID.NeonTetra,
            ItemID.Flounder, ItemID.Ebonkoi, ItemID.DoubleCod, ItemID.Salmon);

        Mushroom.Add(ItemID.Mushroom, ModContent.ItemType<FlyAgaricItem>());

        AnyEvilMushroom.Add(ItemID.VileMushroom, ItemID.ViciousMushroom);

        AnyHerb.Add(
            ItemID.Daybloom, ItemID.Blinkroot, ItemID.Moonglow, ItemID.Deathweed,
            ItemID.Waterleaf, ItemID.Fireblossom, ItemID.Shiverthorn);

        // Cross-mod herbs go through the flower registry, so a bad internal name is reported rather than silently missing.
        int cloudstalk = FlowerRegistry.TypeOf("SpiritReforged", "Cloudstalk");
        if (cloudstalk > 0)
            AnyHerb.Add(cloudstalk);

        RottenChunkVertabrae.Add(ItemID.RottenChunk, ItemID.Vertebrae);

        BloodTiles.Add(
            ItemID.CrimsandBlock, ItemID.CrimstoneBlock);

        CorruptTiles.Add(
            ItemID.EbonsandBlock, ItemID.EbonstoneBlock);

        ShiverthornOrWaterleaf.Add(ItemID.Shiverthorn, ItemID.Waterleaf);

        ShiverthornOrSeeds.Add(ItemID.Shiverthorn, ItemID.ShiverthornSeeds);

        AnySoil.Add(
            ItemID.DirtBlock, ItemID.MudBlock,
            ItemID.EbonsandBlock, ItemID.CrimsandBlock, ItemID.PearlsandBlock,
            ItemID.EbonstoneBlock, ItemID.CrimstoneBlock, ItemID.PearlstoneBlock);

        DirtOrMud.Add(ItemID.DirtBlock, ItemID.MudBlock);

        AnyButterfly.Add(
            ItemID.GoldButterfly, ItemID.HellButterfly, ItemID.JuliaButterfly,
            ItemID.MonarchButterfly, ItemID.PurpleEmperorButterfly, ItemID.RedAdmiralButterfly,
            ItemID.SulphurButterfly, ItemID.TreeNymphButterfly, ItemID.UlyssesButterfly,
            ItemID.ZebraSwallowtailButterfly);

        AnyBaseBottle.Add(ItemID.Bottle, ItemID.BottledWater, SolventID.Inert);

        SpecularFishOrMoonglow.Add(ItemID.SpecularFish, ItemID.Moonglow);
    }

    public static void Clear()
    {
        foreach (AlchemyComponentGroup group in All)
            group.Clear();
    }
}
