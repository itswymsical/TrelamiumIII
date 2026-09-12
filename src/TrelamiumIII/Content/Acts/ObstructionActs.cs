using System.Collections.Generic;
using System.Linq;
using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII.Content.Acts;

/// <summary>
/// Mining acts: one per ore, plus a group act per tier band that each ore also feeds. Group payout scales with how
/// many member ores were mined (see <see cref="Act.CoverageMultiplier"/>). Names live in <c>Localization/ActLang</c>.
/// </summary>
internal sealed class OreActs : IActProvider
{
    public IEnumerable<Act> Provide() =>
    [
        Ore("copper", TileID.Copper),
        Ore("tin", TileID.Tin),
        Ore("iron", TileID.Iron),
        Ore("lead", TileID.Lead),
        Ore("silver", TileID.Silver),
        Ore("tungsten", TileID.Tungsten),
        Ore("gold", TileID.Gold),
        Ore("platinum", TileID.Platinum),

        Ore("cobalt", TileID.Cobalt, hardmode: true),
        Ore("palladium", TileID.Palladium, hardmode: true),
        Ore("mythril", TileID.Mythril, hardmode: true),
        Ore("orichalcum", TileID.Orichalcum, hardmode: true),
        Ore("adamantite", TileID.Adamantite, hardmode: true),
        Ore("titanium", TileID.Titanium, hardmode: true),

        new TileBreakDataAct("act.ore.metals.prehardmode", ActSets.PreHardmodeMetalOre, ActCurves.OreGroup),
        new TileBreakDataAct("act.ore.metals.hardmode", ActSets.HardmodeMetalOre, ActCurves.OreGroup, hardmodeContent: true),
        new TileBreakDataAct("act.ore.exotic", ActSets.ExoticOre, ActCurves.Ore),
        new TileBreakDataAct("act.gems", ActSets.GemTiles, ActCurves.Ore)
    ];

    private static Act Ore(string key, int tile, bool hardmode = false)
        => new TileBreakDataAct($"act.ore.{key}", [tile], ActCurves.Ore, hardmodeContent: hardmode);
}

public sealed class SoilAct : TileBreakPredicateAct
{
    public override string ID => "act.soil";
    public override ActCategory Category => ActCategory.Obstruction;
    public override int[] Thresholds => ActCurves.Bulk;

    protected override bool TestTile(int tileType) => TileID.Sets.CanBeDugByShovel[tileType];
}

public sealed class PotAct : TileBreakPredicateAct
{
    public override string ID => "act.pots";
    public override ActCategory Category => ActCategory.Obstruction;
    public override int[] Thresholds => ActCurves.Scavenge;

    /// <summary>
    /// A pot is a 2x2 multitile, so one smash raises four tile breaks. Pots have no TileObjectData, so the fallback is
    /// what actually applies.
    /// </summary>
    public override int EventsPerAction => ActUnits.MultiTileFootprint(TileID.Pots, fallback: ActUnits.TwoByTwo);

    protected override bool TestTile(int tileType) => tileType == TileID.Pots;
}

public sealed class TundraAct : TileBreakPredicateAct
{
    public override string ID => "act.tundra";
    public override ActCategory Category => ActCategory.Obstruction;
    public override int[] Thresholds => ActCurves.Bulk;

    protected override bool TestTile(int tileType)
        => TileID.Sets.Ices[tileType] || TileID.Sets.IcesSlush[tileType] || TileID.Sets.IcesSnow[tileType];
}

public sealed class EvilBiomeAct : TileBreakPredicateAct
{
    public override string ID => "act.evilbiomes";
    public override ActCategory Category => ActCategory.Obstruction;
    public override int[] Thresholds => ActCurves.Bulk;

    protected override bool TestTile(int tileType)
        => TileID.Sets.Corrupt[tileType] || TileID.Sets.Crimson[tileType];
}

public sealed class HellBlocksAct : TileBreakPredicateAct
{
    public override string ID => "act.hellblocks";
    public override ActCategory Category => ActCategory.Obstruction;
    public override int[] Thresholds => ActCurves.Bulk;

    protected override bool TestTile(int tileType)
        => TileID.Sets.HellSpecial[tileType] || TileID.Sets.Ash[tileType];
}

public sealed class JungleBlocksAct : TileBreakPredicateAct
{
    private static bool IsJungleBlock(int tileType) => tileType switch
    {
        TileID.JungleGrass or TileID.Mudstone or TileID.Hive or
        TileID.HoneyBlock or TileID.CrispyHoneyBlock or
        TileID.RichMahogany or TileID.LivingMahogany => true,
        _ => false
    };

    public override string ID => "act.jungleblocks";
    public override ActCategory Category => ActCategory.Obstruction;
    public override int[] Thresholds => ActCurves.Bulk;

    protected override bool TestTile(int tileType)
        => TileID.Sets.Mud[tileType] || IsJungleBlock(tileType) || TileID.Sets.JungleSpecial[tileType];
}

public sealed class StoneBlocksAct : TileBreakPredicateAct
{

    public override string ID => "act.stoneblocks";
    public override ActCategory Category => ActCategory.Obstruction;
    public override int[] Thresholds => ActCurves.Bulk;
    public override float ExpMultiplier => 0.5f;

    protected override bool TestTile(int tileType)
        => TileID.Sets.Stone[tileType];
}

public sealed class FoliageCutterAct : TileBreakPredicateAct
{
    public override string ID => "act.foliagecutter";
    public override ActCategory Category => ActCategory.Obstruction;
    public override int[] Thresholds => ActCurves.Bulk;
    public override float ExpMultiplier => 0.5f;
    protected override bool TestTile(int tileType)
        => TileID.Sets.TileCutIgnore.Regrowth[tileType];
}

public sealed class LumberAct : TileBreakPredicateAct
{
    /// <summary>Counted in trees felled, not trunk tiles broken. See <see cref="EventsPerAction"/>.</summary>
    private static readonly int[] Trees = [5, 15, 35, 70, 120, 190, 290, 420, 580, 780];

    public override string ID => "act.lumber";
    public override ActCategory Category => ActCategory.Obstruction;
    public override int[] Thresholds => Trees;
    public override float ExpMultiplier => 0.75f;

    /// <summary>Felling a tree breaks a dozen or so trunk tiles, so thresholds count trees rather than breaks.</summary>
    public override int EventsPerAction => ActUnits.AverageTreeTiles;

    protected override bool TestTile(int tileType) => TileID.Sets.IsATreeTrunk[tileType];
}
