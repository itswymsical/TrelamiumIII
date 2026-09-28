using Terraria.Localization;

namespace TrelamiumIII.Common.DamageMeter;

/// <summary>Lists the DPS meter's damage per <see cref="DamageMeterSource" /> under its hover name, once debuffs or afflictions add to it.</summary>
public class DamageMeterDisplay : GlobalInfoDisplay
{
    private static LocalizedText hitsText = null!;

    private static LocalizedText debuffsText = null!;

    private static LocalizedText afflictionsText = null!;

    public override void SetStaticDefaults()
    {
        hitsText = Mod.GetLocalization("DamageMeter.Hits");
        debuffsText = Mod.GetLocalization("DamageMeter.Debuffs");
        afflictionsText = Mod.GetLocalization("DamageMeter.Afflictions");
    }

    public override void ModifyDisplayParameters(InfoDisplay currentDisplay, ref string displayValue, ref string displayName, ref Color displayColor, ref Color displayShadowColor)
    {
        if (currentDisplay != InfoDisplay.DPSMeter)
            return;

        Player player = Main.LocalPlayer;
        DamageMeterPlayer meter = player.GetModPlayer<DamageMeterPlayer>();
        int dps = player.getDPS();
        int debuffs = (int)MathF.Round(dps * meter.ShareOf(DamageMeterSource.Debuff));
        int afflictions = (int)MathF.Round(dps * meter.ShareOf(DamageMeterSource.Affliction));

        if (debuffs <= 0 && afflictions <= 0)
            return;

        displayName += Line(hitsText, dps - debuffs - afflictions) + Line(debuffsText, debuffs) + Line(afflictionsText, afflictions);
    }

    private static string Line(LocalizedText text, int dps) => dps > 0 ? "\n" + text.Format(dps) : "";
}
