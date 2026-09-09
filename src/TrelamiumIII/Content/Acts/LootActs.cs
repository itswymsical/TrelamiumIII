using TrelamiumIII.Core.ExperientialSystem.Acts;

namespace TrelamiumIII.Content.Acts;

public sealed class HeartPickupAct : ItemPickupAct
{
    public override string ID => "act.heartpickup";

    public override ActCategory Category => ActCategory.Collection;

    public override int[] Thresholds => ActCurves.Scavenge;

    protected override int[] Ids => [ItemID.Heart, ItemID.CandyApple, ItemID.CandyCane];
}

public sealed class ManaPickupAct : ItemPickupAct
{
    public override string ID => "act.manapickup";

    public override ActCategory Category => ActCategory.Collection;

    public override int[] Thresholds => ActCurves.Scavenge;

    protected override int[] Ids => [ItemID.Star, ItemID.SoulCake, ItemID.SugarPlum];
}

public sealed class CoinPickupAct : ItemPickupAct
{
    public override string ID => "act.coins";

    public override ActCategory Category => ActCategory.Exploration;

    public override int[] Thresholds => ActCurves.Scavenge;

    protected override int[] Ids => [ItemID.CopperCoin, ItemID.SilverCoin, ItemID.GoldCoin, ItemID.PlatinumCoin];
}