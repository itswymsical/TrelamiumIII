using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII.Content.Acts;

public sealed class BlacksmithAct : ItemCraftAct
{
    public override string ID => "act.bars";
    public override ActCategory Category => ActCategory.Crafting;
    public override int[] Thresholds => ActCurves.Craft;

    protected override int[] Ids => ActSets.Bars;
}

public sealed class BotanistAct : TilePlaceAct
{
    public override string ID => "act.planting";
    public override ActCategory Category => ActCategory.Building;
    public override int[] Thresholds => ActCurves.Deliberate;

    protected override int[] Ids => ActSets.HerbTiles;
}
