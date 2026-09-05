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
public sealed class PotAct : TileBreakPredicateAct
{
    public override string ID => "act.pots";
    public override ActCategory Category => ActCategory.Obstruction;
    public override int[] Thresholds => ActCurves.Bulk;

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

public sealed class LumberAct : TileBreakPredicateAct
{
    public override string ID => "act.lumber";
    public override ActCategory Category => ActCategory.Obstruction;

    // Deliberate rather than Bulk: a trunk is many tiles, but felling a tree is one decision.
    public override int[] Thresholds => ActCurves.Deliberate;

    protected override bool TestTile(int tileType) => TileID.Sets.IsATreeTrunk[tileType];
}
