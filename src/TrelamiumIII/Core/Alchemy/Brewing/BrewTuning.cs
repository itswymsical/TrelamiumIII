using System;

namespace TrelamiumIII.Core.Alchemy.Brewing;

/// <summary>
/// Maps a recipe's potency tier to a concrete brewed <see cref="AlchemicalEffect"/>.
/// Duration scales off the potion's own vanilla buff time; per-effect params
/// (e.g. Spelunker's ore-tier gate) come from the buff's <see cref="IEffectBehavior"/>. Placeholder
/// curve; tune freely.
/// </summary>
public static class BrewTuning
{
    public static AlchemicalEffect Create(int baseBuff, int baseDurationTicks, int potency)
    {
        potency = Math.Clamp(potency, 1, 5);
        // Strength is a percent of the vanilla effect. p1=80 .. p5=120.
        int strengthPercent = 70 + potency * 10;
        float durationMul = 0.6f + potency * 0.18f;     // p1 ~ 0.78x, p5 = 1.5x of the potion's stock time
        int duration = Math.Max(60, (int)(baseDurationTicks * durationMul));
        var pars = EffectBehaviors.Get(baseBuff)?.DefaultParams(potency) ?? EffectParams.None;
        return new AlchemicalEffect(baseBuff, potency, duration, strengthPercent, pars);
    }

    /// <summary>
    /// Brewed effect for a restorative (healLife/healMana) potion: no buff to ride, so duration is 0
    /// and StrengthPercent scales the instant heal instead (same p1=80..p5=120 curve).
    /// </summary>
    public static AlchemicalEffect CreateRestoration(int potency)
    {
        potency = Math.Clamp(potency, 1, 5);
        return new AlchemicalEffect(VirtualBuff.Restoration, potency, 0, 70 + potency * 10);
    }
}
