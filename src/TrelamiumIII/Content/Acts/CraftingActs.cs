using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII.Content.Acts;

/// <summary>
/// Smelting, as a group act over every bar in the game. Coverage makes this a tech-tree act rather than a
/// volume one: mastering it means having smelted across the whole progression, not having made twenty
/// thousand copper bars.
/// </summary>
public sealed class BlacksmithAct : ItemCraftAct
{
    public override string ID => "act.bars";
    public override ActCategory Category => ActCategory.Crafting;
    public override int[] Thresholds => ActCurves.Craft;

    protected override int[] Ids => ActSets.Bars;
}

/// <summary>
/// Planting herbs. Placement rather than harvest on purpose - the act is the act of tending, and
/// rewarding the harvest instead would just reward standing next to an alchemy plot.
/// </summary>
public sealed class BotanistAct : TilePlaceAct
{
    public override string ID => "act.planting";
    public override ActCategory Category => ActCategory.Building;
    public override int[] Thresholds => ActCurves.Deliberate;

    protected override int[] Ids => ActSets.HerbTiles;
}
