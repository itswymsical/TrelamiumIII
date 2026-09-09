using System.Collections.Generic;
using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII.Content.Acts;

public sealed class BassAct : FishingAct
{
    public override string ID => "act.bassfish";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    public override float ExpMultiplier => 0.8f;

    protected override bool FishCaught(int fish) => fish == ItemID.Bass;
}

public sealed class SalmonAct : FishingAct
{
    public override string ID => "act.salmon";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    public override float ExpMultiplier => 0.8f;
    protected override bool FishCaught(int fish) => fish == ItemID.Salmon;
}

public sealed class EvilFishAct : FishingAct
{
    private readonly HashSet<int> EviiiiilFish = [ItemID.CrimsonTigerfish, ItemID.Ebonkoi, ItemID.Hemopiranha];

    private bool EvilFih(int fish)
        => EviiiiilFish.Contains(fish);

    public override string ID => "act.evilfish";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    protected override bool FishCaught(int fish) => EvilFih(fish);
}

public sealed class JellyfishingAct : FishingAct
{
    public override string ID => "act.jellyfishing";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    protected override bool FishCaught(int fish) => fish == ItemID.GreenJellyfish 
        || fish == ItemID.BlueJellyfish || fish == ItemID.PinkJellyfish;
}

public sealed class GeneralFishingAct : FishingAct
{
    private readonly HashSet<int> BasicFish = [ItemID.Tuna, ItemID.Bass, ItemID.Shrimp, ItemID.AtlanticCod, 
        ItemID.Trout, ItemID.SpecularFish, ItemID.RockLobster, ItemID.RedSnapper, ItemID.NeonTetra, ItemID.Flounder, ItemID.CrimsonTigerfish];

    private bool IsBasicFishy(int fish) 
        => BasicFish.Contains(fish);

    public override string ID => "act.basicfish";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    public override float ExpMultiplier => 0.8f;
    protected override bool FishCaught(int fish) => IsBasicFishy(fish);
}

public sealed class HallowFishAct : FishingAct
{
    private readonly HashSet<int> HallowFih = [ItemID.Prismite, ItemID.PrincessFish, ItemID.ChaosFish];

    private bool IsHallowFih(int fish)
        => HallowFih.Contains(fish);

    public override string ID => "act.hallowfish";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    public override float ExpMultiplier => 0.8f;
    protected override bool FishCaught(int fish) => IsHallowFih(fish);
}

public sealed class GoldenCarpAct : FishingAct
{
    public override string ID => "act.goldencarp";
    public override ActCategory Category => ActCategory.Fishing;
    public override int[] Thresholds => ActCurves.Fishing;
    public override float ExpMultiplier => 1.25f;
    protected override bool FishCaught(int fish) => fish == ItemID.GoldenCarp;
}