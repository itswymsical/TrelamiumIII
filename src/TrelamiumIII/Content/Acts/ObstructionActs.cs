using System.Collections.Generic;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII.Content.Acts;

/// <summary>
/// Tile-breaking acts: one per ore plus group acts per ore tier (each ore feeds both), and block families matched by
/// tile sets. Names live in <c>Localization/Experiential/Acts</c>.
/// </summary>
internal sealed class ObstructionActs : IActProvider
{
    /// <summary>Lumber counts trees felled, not trunk tiles broken (see <see cref="ActUnits.AverageTreeTiles"/>).</summary>
    private static readonly int[] Trees = [5, 15, 35, 70, 120, 190, 290, 420, 580, 780];

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

        Group("act.ore.metals.prehardmode", ActSets.PreHardmodeMetalOre, ActCurves.OreGroup),
        Group("act.ore.metals.hardmode", ActSets.HardmodeMetalOre, ActCurves.OreGroup, hardmode: true),
        Group("act.ore.exotic", ActSets.ExoticOre, ActCurves.Ore),
        Group("act.gems", ActSets.GemTiles, ActCurves.Ore),

        Blocks("act.soil", ActCurves.Bulk, static type => TileID.Sets.CanBeDugByShovel[type]),

        new DataAct("act.pots", ActCategory.Obstruction, GameEventType.TileBreak, ActCurves.Scavenge)
        {
            Test = static type => type == TileID.Pots,

            // One break per tile of the 2x2 pot. Pots have no TileObjectData, so the fallback applies.
            Events = static () => ActUnits.MultiTileFootprint(TileID.Pots, fallback: ActUnits.TwoByTwo),
        },

        Blocks("act.tundra", ActCurves.Bulk,
            static type => TileID.Sets.Ices[type] || TileID.Sets.IcesSlush[type] || TileID.Sets.IcesSnow[type]),
        Blocks("act.evilbiomes", ActCurves.Bulk, static type => TileID.Sets.Corrupt[type] || TileID.Sets.Crimson[type]),
        Blocks("act.hellblocks", ActCurves.Bulk, static type => TileID.Sets.HellSpecial[type] || TileID.Sets.Ash[type]),
        Blocks("act.jungleblocks", ActCurves.Bulk,
            static type => TileID.Sets.Mud[type] || IsJungleBlock(type) || TileID.Sets.JungleSpecial[type]),
        Blocks("act.stoneblocks", ActCurves.Bulk, static type => TileID.Sets.Stone[type], multiplier: 0.5f),

        new DataAct("act.lumber", ActCategory.Obstruction, GameEventType.TileBreak, Trees)
        {
            Test = static type => TileID.Sets.IsATreeTrunk[type],
            Multiplier = 0.75f,
            Events = static () => ActUnits.AverageTreeTiles,
        },
    ];

    private static DataAct Ore(string key, int tile, bool hardmode = false)
        => Group($"act.ore.{key}", [tile], ActCurves.Ore, hardmode);

    private static DataAct Group(string id, int[] tiles, int[] curve, bool hardmode = false)
        => new(id, ActCategory.Obstruction, GameEventType.TileBreak, curve) { Ids = tiles, Hardmode = hardmode };

    private static DataAct Blocks(string id, int[] curve, Func<int, bool> test, float? multiplier = null)
        => new(id, ActCategory.Obstruction, GameEventType.TileBreak, curve) { Test = test, Multiplier = multiplier };

    private static bool IsJungleBlock(int tileType) => tileType switch
    {
        TileID.JungleGrass or TileID.Mudstone or TileID.Hive or
        TileID.HoneyBlock or TileID.CrispyHoneyBlock or
        TileID.RichMahogany or TileID.LivingMahogany => true,
        _ => false
    };
}
