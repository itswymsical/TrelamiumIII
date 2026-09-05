using System.Collections.Generic;
using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII.Content.Acts;

/// <summary>
/// Mining, as two overlapping ladders.
///
/// <para>
/// Every ore has its own act, and every ore also feeds the group act for its tier band. Breaking one
/// copper advances the copper act and the pre-hardmode metals group at the same time, so a player who
/// mines broadly earns from both - that overlap is the completionism bonus.
/// </para>
///
/// <para>
/// The group acts are not simply the individual acts summed. Their payout scales with how many member
/// ores the player has actually mined, so five thousand copper cannot master "the pre-hardmode metals"
/// while the player has never touched tin. See <see cref="Act.CoverageMultiplier"/>.
/// </para>
///
/// <para>
/// Names and descriptions live in <c>Localization/ActLang</c>, keyed off each act's id. Only the id, the
/// tiles and the curve are decided here.
/// </para>
/// </summary>
internal sealed class OreActs : IActProvider
{
    public IEnumerable<Act> Provide() =>
    [
        // --- Pre-hardmode, in tier order. Alternates share a rung but keep separate acts. ---
        Ore("copper", TileID.Copper),
        Ore("tin", TileID.Tin),
        Ore("iron", TileID.Iron),
        Ore("lead", TileID.Lead),
        Ore("silver", TileID.Silver),
        Ore("tungsten", TileID.Tungsten),
        Ore("gold", TileID.Gold),
        Ore("platinum", TileID.Platinum),

        // --- Hardmode, in tier order. Floored so they do not open on the 2% rung deep into a run. ---
        Ore("cobalt", TileID.Cobalt, hardmode: true),
        Ore("palladium", TileID.Palladium, hardmode: true),
        Ore("mythril", TileID.Mythril, hardmode: true),
        Ore("orichalcum", TileID.Orichalcum, hardmode: true),
        Ore("adamantite", TileID.Adamantite, hardmode: true),
        Ore("titanium", TileID.Titanium, hardmode: true),

        // --- Groups. Fed by every member above, weighted by how many of them you have met. ---
        new TileBreakDataAct("act.ore.metals.prehardmode", ActSets.PreHardmodeMetalOre, ActCurves.OreGroup),
        new TileBreakDataAct("act.ore.metals.hardmode", ActSets.HardmodeMetalOre, ActCurves.OreGroup, hardmodeContent: true),
        new TileBreakDataAct("act.ore.exotic", ActSets.ExoticOre, ActCurves.Ore),
        new TileBreakDataAct("act.gems", ActSets.GemTiles, ActCurves.Ore)
    ];

    private static Act Ore(string key, int tile, bool hardmode = false)
        => new TileBreakDataAct($"act.ore.{key}", [tile], ActCurves.Ore, hardmodeContent: hardmode);
}

/// <summary>
/// Digging that is about volume rather than variety. These match on a tile set rather than a fixed member
/// list, so they carry no group coverage - "every soil type in the game" is not a goal anybody would chase.
/// </summary>
public sealed class SoilAct : TileBreakPredicateAct
{
    public override string ID => "act.soil";
    public override ActCategory Category => ActCategory.Obstruction;
    public override int[] Thresholds => ActCurves.Bulk;

    protected override bool TestTile(int tileType) => TileID.Sets.CanBeDugByShovel[tileType];
}
/// <summary>
/// Smashing pots - the most mundane act in the game, and the one the whole system was described around.
/// </summary>
public sealed class PotAct : TileBreakPredicateAct
{
    public override string ID => "act.pots";
    public override ActCategory Category => ActCategory.Obstruction;
    public override int[] Thresholds => ActCurves.Scavenge;

    /// <summary>
    /// A pot is a 2x2 multitile, so one smash raises four tile breaks - confirmed in game, four breaks at
    /// the four corners of one footprint. Pots are worldgen-only and never placeable, so vanilla registers
    /// no TileObjectData for them and the fallback is what actually answers here.
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

/// <summary>
/// Felling trees. Also the worked example of a bespoke ladder: an act that does not fit any of the shared
/// <see cref="ActCurves"/> just declares its own array, held in a static field so it is allocated once.
/// </summary>
public sealed class LumberAct : TileBreakPredicateAct
{
    /// <summary>Counted in trees felled, not trunk tiles broken. See <see cref="EventsPerAction"/>.</summary>
    private static readonly int[] Trees = [5, 15, 35, 70, 120, 190, 290, 420, 580, 780];

    public override string ID => "act.lumber";
    public override ActCategory Category => ActCategory.Obstruction;
    public override int[] Thresholds => Trees;

    /// <summary>
    /// A tree is killed one trunk tile at a time, so felling one raises a dozen or so breaks. This is what
    /// forced the old tree challenge into thresholds of 500 for what was really forty trees.
    /// </summary>
    public override int EventsPerAction => ActUnits.AverageTreeTiles;

    protected override bool TestTile(int tileType) => TileID.Sets.IsATreeTrunk[tileType];
}
