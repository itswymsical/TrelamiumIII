using System.Collections.Generic;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII.Content.Acts;

public sealed class EvilFishAct : FishingAct
{
    public override string ID => "act.evilfish";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    protected override int[] Ids => ActSets.EvilBiomeFish;

}

public sealed class HallowFishAct : FishingAct
{
    public override string ID => "act.hallowfish";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    protected override int[] Ids => ActSets.HallowFish;
}

public sealed class JellyfishingAct : FishingAct
{
    public override string ID => "act.jellyfishing";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    protected override int[] Ids => ActSets.JellyFih;

}

public sealed class HellfishAct : FishingAct
{
    public override string ID => "act.hellfishing";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    protected override int[] Ids => [ItemID.Obsidifish, ItemID.FlarefinKoi];
}

public sealed class CommonFishAct : FishingAct
{
    public override string ID => "act.commonfish";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    protected override int[] Ids => ActSets.CommonFish;
}

public sealed class UncommonFishAct : FishingAct
{
    public override string ID => "act.uncommonfish";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    protected override int[] Ids => ActSets.UncommonFish;
}

public sealed class RareFishAct : FishingAct
{
    public override string ID => "act.rarefish";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    protected override int[] Ids => ActSets.RareFish;
}

public sealed class LegendaryFishAct : FishingAct
{
    public override string ID => "act.legendfish";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    protected override int[] Ids => ActSets.LegendaryFish;
}

public sealed class TundraFishAct : FishingAct
{
    public override string ID => "act.icefish";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    public override float ExpMultiplier => 0.8f;
    protected override int[] Ids => [ItemID.AtlanticCod, ItemID.FrostMinnow];
}
public sealed class JungleFishAct : FishingAct
{
    public override string ID => "act.junglefish";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    public override float ExpMultiplier => 0.8f;
    protected override int[] Ids => [ItemID.NeonTetra, ItemID.DoubleCod, ItemID.VariegatedLardfish];
}

public sealed class PHMCratesAct : FishingCrateAct
{
    public override string ID => "act.phmcrates";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    protected override bool CrateType(int type) => ItemID.Sets.IsFishingCrate[type];
}

public sealed class HMCratesAct : FishingCrateAct
{
    public override string ID => "act.hmcrates";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    protected override bool CrateType(int type) => ItemID.Sets.IsFishingCrateHardmode[type];
}