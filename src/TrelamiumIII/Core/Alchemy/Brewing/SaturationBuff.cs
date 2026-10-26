using System.Text;

namespace TrelamiumIII.Core.Alchemy.Brewing;

/// <summary>
/// Shows a brew's timed modifiers: <see cref="AlchemyEffectPlayer"/> keeps it on the longest one and lists each in the buff
/// text. Cancelling it ends them all.
/// </summary>
public sealed class SaturationBuff : ModBuff
{
    public override void SetStaticDefaults()
    {
        Main.buffNoSave[Type] = true;
    }

    public override void ModifyBuffText(ref string buffName, ref string tip, ref int rare)
    {
        var player = Main.LocalPlayer;
        if (player is null || !player.active) return;

        var text = new StringBuilder();
        foreach (var (key, modifier) in player.GetModPlayer<AlchemyEffectPlayer>().ActiveModifiers)
        {
            if (BrewModifiers.Get(key) is not { } source) continue;
            if (text.Length > 0) text.Append('\n');
            text.Append(source.Describe(modifier.Value)).Append(" (").Append(BrewText.ShortDuration(modifier.TicksLeft)).Append(')');
        }

        if (text.Length > 0) tip = text.ToString();
    }
}
