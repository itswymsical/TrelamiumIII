namespace TrelamiumIII.Common.Systems.Alchemy.Brewing;

/// <summary>
/// Rewrites a buff's hover description to reflect the brewed instance.
/// <see cref="GlobalBuff.ModifyBuffText"/> wraps the
/// vanilla buff-tooltip code, so it works for vanilla buffs without an IL edit.
/// </summary>
public class BrewBuffText : GlobalBuff
{
    public override void ModifyBuffText(int type, ref string buffName, ref string buffTip, ref int rare)
    {
        var player = Main.LocalPlayer;
        if (player is null || !player.active) return;
        if (!player.GetModPlayer<AlchemyEffectPlayer>().TryGetActiveBrew(type, out var fx)) return;

        string? desc = EffectBehaviors.Describe(fx);
        if (desc != null) buffTip = desc;
        buffTip += $"\nPotency {fx.Potency}/5";
    }
}
