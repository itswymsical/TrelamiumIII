using Terraria.ModLoader.IO;
using TrelamiumIII.Core.UI;

namespace TrelamiumIII.Core.ExperientialSystem;

/// <summary>
/// Owns the two numbers the whole system turns on: banked <see cref="Exp"/> and reached <see cref="Threshold"/>.
///
/// <para>
/// The meter is a wallet <i>and</i> a gate. Exp accumulates in it; when it fills, the Threshold rises
/// permanently and the meter empties into the new, larger capacity. Skills are bought straight out of
/// the live meter, so every skill you take is a Threshold you are choosing to postpone.
/// </para>
///
/// <para>
/// The owning client is authoritative. In multiplayer the server sizes a raw award from the matchup
/// (it can see NPC stats and damage attribution) and hands the receiving client a <i>fraction of a meter</i>;
/// the client scales that by its own capacity and applies its own history and allowance. Nothing here
/// ever runs against another player's state, so there is no Exp to desync.
/// </para>
/// </summary>
public sealed class ExperientialPlayer : ModPlayer
{
    /// <summary>Exp currently in the meter. Spendable, and consumed by crossing a Threshold.</summary>
    public int Exp { get; private set; }

    /// <summary>Highest Threshold reached. Monotonic - spending Exp on skills never gives this back.</summary>
    public int Threshold { get; private set; }

    /// <summary>Per-Threshold record of which sources have already paid their share.</summary>
    public SourceAllowanceLedger Allowance { get; } = new();

    /// <summary>How much Exp the meter holds at the current Threshold.</summary>
    public int Capacity => ExperientialConstants.Capacity(Threshold);

    /// <summary>Meter fill as 0-1, for the UI.</summary>
    public float MeterFill => Capacity <= 0 ? 0f : Math.Clamp((float)Exp / Capacity, 0f, 1f);

    /// <summary>At max Threshold the meter stops being a gate and is purely a skill budget.</summary>
    public bool AtMaxThreshold => Threshold >= ExperientialConstants.MaxThreshold;

    /// <summary>Raised on the owning client each time a Threshold is crossed, with the new Threshold.</summary>
    public event Action<int>? OnThresholdGained;

    public override void Initialize()
    {
        Exp = 0;
        Threshold = ExperientialConstants.MinThreshold;
        Allowance.Reset();
    }

    #region Earning

    /// <summary>
    /// Grants an award expressed as a fraction of one meter. This is the form awards travel in, because
    /// a fraction is Threshold-independent: the server can size one without knowing the recipient's
    /// Threshold, and it stays correct if the player ascends between the kill and the packet landing.
    /// </summary>
    /// <returns>Exp actually granted, after the allowance clamp.</returns>
    public int GrantFraction(float meterFraction, in ExpSource source)
    {
        if (meterFraction <= 0f)
            return 0;

        return Grant((int)MathF.Round(meterFraction * Capacity), source);
    }

    /// <summary>
    /// Grants a flat amount of Exp, trimmed to what <paramref name="source"/> is still allowed to pay
    /// at this Threshold, then ascends as far as the new total reaches.
    /// </summary>
    /// <returns>Exp actually granted, after the allowance clamp.</returns>
    public int Grant(int amount, in ExpSource source)
    {
        if (amount <= 0)
            return 0;

        int granted = Allowance.Clamp(source, amount, Capacity);
        if (granted <= 0)
            return 0;

        Allowance.Record(source, granted);

        return Bank(granted);
    }

    /// <summary>
    /// Adds Exp with no source metering at all. Debug only - the allowance is what stops a single source
    /// carrying a Threshold, so every content path must go through <see cref="Grant"/>.
    /// </summary>
    internal int GrantUnmetered(int amount) => amount > 0 ? Bank(amount) : 0;

    /// <summary>
    /// Puts Exp in the meter, announces it, and ascends as far as the new total reaches. The one place
    /// the meter actually moves, so nothing can add Exp without the popup and the Threshold check.
    /// </summary>
    private int Bank(int granted)
    {
        Exp += granted;

        // Every route into the meter reports here, so no caller has to remember to announce itself and
        // no two of them can announce the same Exp differently.
        if (Player.whoAmI == Main.myPlayer)
            ExpPopupSystem.Show(granted);

        while (!AtMaxThreshold && Exp >= Capacity)
            Ascend();

        // Past the last Threshold the meter is a wallet with a lid rather than a gate.
        if (AtMaxThreshold && Exp > Capacity)
            Exp = Capacity;

        return granted;
    }

    private void Ascend()
    {
        // Read the capacity being crossed before the Threshold moves - it is the old meter that is spent.
        int spent = Capacity;

        Exp = ExperientialConstants.CarryOverflowOnAscend ? Exp - spent : 0;
        Threshold++;

        // A new Threshold re-opens every source's budget. This is what makes an exhausted enemy
        // worth killing again next Threshold, while familiarity keeps it worth less than it was.
        Allowance.Reset();

        OnThresholdGained?.Invoke(Threshold);

        // No panel and no banner. Crossing a Threshold is meant to be felt and then asked about, not read.
        ThresholdAscendEffect.PlayThreshold(Player);
    }

    #endregion

    #region Spending

    /// <summary>True when the meter currently holds enough to pay <paramref name="amount"/>.</summary>
    public bool CanAfford(int amount) => amount >= 0 && Exp >= amount;

    /// <summary>
    /// Spends Exp out of the live meter. The Threshold is untouched, so this can leave the player
    /// holding a Threshold their current Exp would not re-earn - which is the intended feel.
    /// </summary>
    public bool TrySpend(int amount)
    {
        if (!CanAfford(amount))
            return false;

        Exp -= amount;
        return true;
    }

    /// <summary>
    /// Returns Exp to the meter on a respec. Capped at the meter, so refunding into a nearly full meter
    /// can waste the overflow rather than handing out a free Threshold for un-picking skills.
    /// </summary>
    /// <returns>Exp actually returned.</returns>
    public int Refund(int amount)
    {
        if (amount <= 0)
            return 0;

        int room = Math.Max(Capacity - Exp, 0);
        int returned = Math.Min(amount, room);

        Exp += returned;
        return returned;
    }

    #endregion

    #region Save data

    public override void SaveData(TagCompound tag)
    {
        tag["exp"] = Exp;
        tag["threshold"] = Threshold;
        Allowance.Save(tag, "allowance");
    }

    public override void LoadData(TagCompound tag)
    {
        Exp = tag.GetInt("exp");
        Threshold = Math.Clamp(tag.GetInt("threshold"), ExperientialConstants.MinThreshold, ExperientialConstants.MaxThreshold);
        Allowance.Load(tag, "allowance");

        // A save written under a different curve can hold more Exp than the meter now takes.
        Exp = Math.Clamp(Exp, 0, Capacity);
    }

    #endregion
}
