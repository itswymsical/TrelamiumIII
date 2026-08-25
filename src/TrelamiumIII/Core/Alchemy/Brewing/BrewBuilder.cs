using System;

namespace TrelamiumIII.Core.Alchemy.Brewing;

/// <summary>
/// Fluent authoring for a recipe's brew. A recipe stamps a base effect from
/// its potency, then optionally runs one of these to boost, re-gate, or combine further effects. The
/// resolved effect list is what gets stored on the item (augmentations aren't persisted as layers).
/// </summary>
public sealed class BrewBuilder
{
    private readonly PotionBrew _brew;
    private readonly int _baseDurationTicks;

    public BrewBuilder(PotionBrew brew, int baseDurationTicks)
    {
        _brew = brew;
        _baseDurationTicks = baseDurationTicks;
    }

    /// <summary>Boost (or cut) the primary effect's magnitude, as a percent of the vanilla effect.</summary>
    public BrewBuilder Amplify(int strengthPercent)
    {
        _brew.Effects[0] = _brew.Effects[0] with { StrengthPercent = strengthPercent };
        return this;
    }

    /// <summary>Rewrite the primary effect's params: e.g. <c>Detour(p =&gt; p with { MaxOreTier = 2 })</c>.</summary>
    public BrewBuilder Detour(Func<EffectParams, EffectParams> edit)
    {
        _brew.Effects[0] = _brew.Effects[0] with { Params = edit(_brew.Effects[0].Params) };
        return this;
    }

    /// <summary>
    /// Combine another brewed effect onto the potion ("intermix"). <paramref name="durationTicks"/>
    /// is the base time fed to the potency curve; omitted, it falls back to the potion's own buff
    /// time, or 60s for items with none (heal potions).
    /// </summary>
    public BrewBuilder Add(int baseBuff, int potency, int durationTicks = 0)
    {
        int baseDur = durationTicks > 0 ? durationTicks
            : _baseDurationTicks > 0 ? _baseDurationTicks
            : 60 * 60;
        _brew.Effects.Add(BrewTuning.Create(baseBuff, baseDur, potency));
        return this;
    }
}
