namespace TrelamiumIII.Core.ExperientialSystem;

/// <summary>
/// Every tunable number the Experiential System reads, in one place.
///
/// <para>
/// The system has three nouns. <b>Exp</b> is a spendable resource earned by committing acts.
/// <b>Threshold</b> is the meter's capacity and the gate skills unlock behind; it rises when the
/// meter fills and never falls. <b>Skills</b> are bought with Exp out of the live meter, so buying
/// one actively delays your next Threshold - the tension between buffing and progressing is the point.
/// </para>
///
/// <para>
/// Three independent brakes stop any one activity from carrying a player, each doing a different job:
/// <list type="number">
///   <item><b>Familiarity</b> (<see cref="FamiliarityScale"/>) - lifetime, smooth, per NPC type. The
///   hundredth Green Slime is worth almost nothing and stays that way forever.</item>
///   <item><b>Source allowance</b> (<see cref="AllowanceFor"/>) - a hard wall on how much of a single
///   Threshold any one source may supply. Resets on ascend, so it re-opens each Threshold.</item>
///   <item><b>Act tiers</b> (<see cref="ActPayoutFor"/>) - the authored layer. An act pays out
///   <see cref="ActPayoutTiers"/> times and is then exhausted for good.</item>
/// </list>
/// </para>
/// </summary>
public static class ExperientialConstants
{
    #region Threshold curve

    /// <summary>Threshold every character starts at.</summary>
    public const int MinThreshold = 0;

    /// <summary>Highest reachable Threshold. The meter still fills at max, it just stops ascending.</summary>
    public const int MaxThreshold = 50;

    /// <summary>Meter capacity at <see cref="MinThreshold"/>.</summary>
    public const int BaseCapacity = 100;

    /// <summary>Meter capacity at <see cref="MaxThreshold"/>. Doubles as the endgame skill budget.</summary>
    public const int PeakCapacity = 500_000;

    /// <summary>
    /// TUNING: whether Exp past the capacity carries into the next Threshold, or is burned by the ascend.
    /// Carrying is the forgiving option and stops a single large award from feeling wasted.
    /// </summary>
    public const bool CarryOverflowOnAscend = true;

    /// <summary>
    /// Per-Threshold growth ratio, solved so the curve hits both anchors exactly:
    /// <c>BaseCapacity * ratio^MaxThreshold == PeakCapacity</c>. Works out to ~1.1857.
    /// Retune the anchors, not this.
    /// </summary>
    public static double CapacityRatio => Math.Pow((double)PeakCapacity / BaseCapacity, 1d / MaxThreshold);

    private static readonly int[] Capacities;

    /// <summary>Total Exp a character banks crossing every Threshold from 0 to <see cref="MaxThreshold"/>.</summary>
    public static readonly int TotalExpToMaxThreshold;

    static ExperientialConstants()
    {
        Capacities = new int[MaxThreshold + 1];

        double ratio = CapacityRatio;
        for (int t = 0; t <= MaxThreshold; t++)
            Capacities[t] = Quantise(BaseCapacity * Math.Pow(ratio, t));

        // Pin the anchors so rounding can never drift them off the authored values.
        Capacities[MinThreshold] = BaseCapacity;
        Capacities[MaxThreshold] = PeakCapacity;

        // The last Threshold's capacity is a wallet, not a gate, so it is not part of the climb.
        int total = 0;
        for (int t = 0; t < MaxThreshold; t++)
            total += Capacities[t];

        TotalExpToMaxThreshold = total;
    }

    /// <summary>
    /// Rounds a raw curve value onto a grid coarse enough to read as hand-authored:
    /// 100, 120, 140, 165, 200 ... 355500, 421500, 500000 rather than 100, 118.6, 140.6, 166.7.
    /// </summary>
    private static int Quantise(double value)
    {
        int step = value switch
        {
            < 100d => 1,
            < 1_000d => 5,
            < 10_000d => 50,
            _ => 500
        };

        return (int)(Math.Round(value / step) * step);
    }

    /// <summary>Meter capacity at <paramref name="threshold"/>. Out-of-range values clamp rather than throw.</summary>
    public static int Capacity(int threshold) => Capacities[Math.Clamp(threshold, MinThreshold, MaxThreshold)];

    /// <summary>The whole curve, for tooling and debug commands.</summary>
    public static ReadOnlySpan<int> CapacityTable => Capacities;

    #endregion

    #region Skills

    /// <summary>
    /// Threshold skills first become purchasable at. A skill gated below this stays locked until here, so
    /// its <see cref="Skills.Skill.RequiredThreshold"/> only orders it within the opening band.
    /// </summary>
    public const int FirstSkillThreshold = 5;

    /// <summary>Threshold a skill gated at <paramref name="requiredThreshold"/> actually opens at.</summary>
    public static int SkillGate(int requiredThreshold)
        => Math.Clamp(Math.Max(requiredThreshold, FirstSkillThreshold), MinThreshold, MaxThreshold);

    #endregion

    #region Award sizing

    /// <summary>
    /// TUNING: how many dead-even kills (menace ~1, no familiarity penalty) fill an empty meter.
    /// Awards are sized as a fraction of the meter rather than an absolute number, so a kill stays
    /// meaningful at Threshold 29 without any per-tier multiplier table.
    /// </summary>
    public const float KillsPerThreshold = 40f;

    /// <summary>
    /// TUNING: fraction of a meter a dead-even boss kill pays before every other modifier.
    ///
    /// <para>
    /// Keep <c>BossKillFraction * (1 + MenaceOverGain)</c> just under <c>AllowanceFor(Boss)</c>. At 0.17 that
    /// is 0.595 against a 0.60 wall, so the whole menace range stays expressive and the allowance is a
    /// backstop rather than the normal operating point. Raise this past that and every boss harder than a
    /// mild threat pays the identical clamped amount - which is exactly the flattening this model exists to avoid.
    /// </para>
    /// </summary>
    public const float BossKillFraction = 0.17f;

    /// <summary>TUNING: multiplier the first time a character ever kills a given NPC type. First blood is a landmark.</summary>
    public const float FirstEncounterBonus = 2.5f;

    #endregion

    #region Matchup (menace)

    /// <summary>
    /// Menace is <c>secondsToKillNpc / secondsToDie</c> - dimensionless, so it needs no difficulty
    /// correction and no absolute "NPC level". 1.0 is a dead-even trade.
    /// </summary>
    public const float MenaceParity = 1f;

    /// <summary>TUNING: raw ratio is clamped here so a one-shot or an unkillable target cannot produce infinity.</summary>
    public const float MenaceCeiling = 25f;

    /// <summary>TUNING: award multiplier against something utterly outclassed. Never zero - trivial kills still tick over.</summary>
    public const float MenaceFloorMultiplier = 0.15f;

    /// <summary>TUNING: shape below parity. Below 1 the penalty bites later; above 1 it bites sooner.</summary>
    public const float MenaceUnderShape = 0.75f;

    /// <summary>TUNING: most a dangerous matchup can add on top of parity. 2.5 gives a 3.5x ceiling overall.</summary>
    public const float MenaceOverGain = 2.5f;

    /// <summary>TUNING: how fast the above-parity bonus saturates. Larger = the bonus keeps growing further out.</summary>
    public const float MenaceOverScale = 1.5f;

    /// <summary>Menace boundary: below this the target is no threat at all.</summary>
    public const float MenaceTrivialBelow = 0.5f;

    /// <summary>Menace boundary: below this the trade is roughly fair.</summary>
    public const float MenaceEvenBelow = 1.5f;

    /// <summary>Menace boundary: below this the target is winning but beatable.</summary>
    public const float MenaceDangerousBelow = 3.5f;

    #endregion

    #region Stat board estimation

    /// <summary>TUNING: seconds between contact hits landing. Player i-frames after a contact hit run ~60 ticks.</summary>
    public const float PlayerHitCadenceSeconds = 1f;

    /// <summary>Defense converts to flat damage reduction at this rate. Vanilla: half in Classic, three quarters in Expert+.</summary>
    public static float DefenseFactor => Main.masterMode || Main.expertMode ? 0.75f : 0.5f;

    /// <summary>TUNING: a hit can never be reduced below this fraction of its raw value, in either direction.</summary>
    public const float MinimumHitFraction = 0.1f;

    /// <summary>TUNING: floor on either side's DPS, so a division cannot blow up on an unarmed player or a 0-damage NPC.</summary>
    public const float MinimumDps = 1f;

    /// <summary>TUNING: assumed fraction of the time a summoner's minions are actually on target.</summary>
    public const float SummonUptime = 0.75f;

    /// <summary>TUNING: floor on estimated player DPS, covering builds the estimator cannot read (thrown, mounts, exotic hits).</summary>
    public const float PlayerDpsFloor = 5f;

    /// <summary>Ticks a player's stat board is cached for. Weapons and accessories do not change mid-swing.</summary>
    public const int StatBoardCacheTicks = 30;

    #endregion

    #region Combat ledger (win/loss history)

    /// <summary>TUNING: familiarity never decays past this, so a farmed enemy is worth a trickle rather than nothing.</summary>
    public const float FamiliarityFloor = 0.08f;

    /// <summary>TUNING: kills for familiarity to fall by 1/e. Lower = grinding one enemy dries up faster.</summary>
    public const float FamiliarityScale = 30f;

    /// <summary>TUNING: most a grudge can add. 1.5 means avenging yourself on a nemesis pays up to 2.5x.</summary>
    public const float GrudgeMax = 1.5f;

    /// <summary>TUNING: deaths for the grudge to saturate. It approaches <see cref="GrudgeMax"/> and never exceeds it.</summary>
    public const float GrudgeScale = 2.5f;

    /// <summary>
    /// TUNING: deaths cleared per avenging kill. Non-zero on purpose - without it, one death inflates every
    /// future kill of that type forever, and dying deliberately becomes the best Exp strategy in the game.
    /// </summary>
    public const int GrudgeSpentPerKill = 1;

    #endregion

    #region Source allowance (per Threshold)

    /// <summary>
    /// TUNING: ceiling on how much of one Threshold a single source may supply, as a fraction of the meter.
    /// Resets on ascend. This is the brake that forces variety inside a Threshold; familiarity is the one
    /// that forces it across a whole run.
    /// </summary>
    public static float AllowanceFor(ExpSourceKind kind) => kind switch
    {
        ExpSourceKind.Boss => 0.60f,
        ExpSourceKind.Npc => 0.25f,
        ExpSourceKind.Tile => 0.15f,
        ExpSourceKind.Item => 0.15f,
        ExpSourceKind.Fish => 0.20f,
        // Acts are already capped for life at ActPayoutTiers payouts, so a per-Threshold allowance on top
        // is redundant - and at 0.35 it clamped every tier past the sixth to the same number, flattening
        // exactly the rungs the curve exists to make feel big. 1.0 leaves it as a sanity bound only.
        ExpSourceKind.Act => 1.00f,
        ExpSourceKind.Housing => 0.15f,
        _ => 0.20f
    };

    #endregion

    #region Act tiers

    /// <summary>How many times a single authored act can ever pay out before it is exhausted.</summary>
    public const int ActPayoutTiers = 10;

    /// <summary>TUNING: fraction of the meter the first act tier pays. Deliberately a token amount.</summary>
    public const float ActPayoutFirstTier = 0.02f;

    /// <summary>TUNING: fraction of the meter the final act tier pays. Mastering an act is a landmark.</summary>
    public const float ActPayoutFinalTier = 0.60f;

    /// <summary>
    /// TUNING: shape of the ramp between the two, as the curvature of an exponential interpolation.
    /// Positive holds the payout down and spikes it late; 0 is a straight line; negative front-loads it.
    ///
    /// <para>
    /// -0.25 is the value that fits the authored intent of "2% at tier 1, around 16% by tier 3, 60% at
    /// tier 10" - which is very nearly a straight line, and in fact slightly <i>decelerating</i>. A truly
    /// exponential ramp (k above 0) puts tier 3 at 7-10%, well under that mark, so the two goals cannot
    /// both hold. This keeps the stated numbers; raise k if the late spike matters more than tier 3 does.
    /// </para>
    /// </summary>
    public const float ActPayoutCurvature = -0.25f;

    /// <summary>
    /// TUNING: multiplier on a universal (group) act, which spans a whole family of content rather than
    /// one piece of it. Stacks with the coverage weighting, so a group pays 1.25x at full breadth and
    /// less than that until the player has actually met its members.
    /// </summary>
    public const float GroupActMultiplier = 1.25f;

    /// <summary>
    /// TUNING: lowest tier a hardmode act is ever paid at once hardmode is active, as a tier index.
    ///
    /// <para>
    /// Hardmode content only becomes reachable deep into a run, so its acts would otherwise open at the
    /// 2% token rung while the player is climbing Thresholds that need far more than that - the payout
    /// falls off the map exactly when the content is at its most demanding. Flooring the tier index means
    /// a fresh hardmode act starts paying as though it were already partway up its own ladder.
    /// </para>
    /// </summary>
    public const int HardmodeFloorTierIndex = 2;

    /// <summary>
    /// TUNING: what a group act pays when the player has engaged with none of its members, as a fraction
    /// of the full payout. Coverage scales the payout from here up to 1.0 at full membership.
    ///
    /// <para>
    /// This is what makes a group act about breadth rather than volume - without it, five thousand copper
    /// would master "pre-hardmode metals" without the player ever touching tin. Set to 1 to switch the
    /// whole mechanic off and let group acts pay purely on volume.
    /// </para>
    /// </summary>
    public const float GroupCoverageFloor = 0.5f;

    /// <summary>
    /// Base payout fraction for act tier <paramref name="tierIndex"/> (0-based), or 0 once the act is
    /// exhausted. Per-act multipliers, the hardmode floor and group coverage are applied on top by
    /// <see cref="Acts.Act.PayoutFraction"/>.
    /// </summary>
    public static float ActPayoutFor(int tierIndex)
    {
        if (tierIndex < 0 || tierIndex >= ActPayoutTiers)
            return 0f;

        float u = tierIndex / (float)(ActPayoutTiers - 1);

        return ActPayoutFirstTier + (ActPayoutFinalTier - ActPayoutFirstTier) * Ramp(u, ActPayoutCurvature);
    }

    /// <summary>Exponential interpolation over 0-1 with curvature <paramref name="k"/>; a straight line at k = 0.</summary>
    private static float Ramp(float u, float k)
    {
        if (MathF.Abs(k) < 0.000001f)
            return u;

        return (MathF.Exp(k * u) - 1f) / (MathF.Exp(k) - 1f);
    }

    #endregion

    #region Difficulty

    /// <summary>
    /// TUNING: world difficulty nudge.
    ///
    /// <para>
    /// Deliberately close to 1.0. Menace is a <i>ratio</i> of two time-to-kill numbers, so Expert and
    /// Master inflating NPC health and damage already raises the award on its own. Anything large here
    /// double-counts that. Treat these as intent ("Master should feel a little more generous"), not as
    /// the mechanism that makes harder modes pay more.
    /// </para>
    /// </summary>
    public static float DifficultyModifier()
    {
        float modifier = Main.GameMode switch
        {
            GameModeID.Creative => 0.75f,
            GameModeID.Master => 1.10f,
            GameModeID.Expert => 1.05f,
            _ => 1f
        };

        if (Main.getGoodWorld)
            modifier *= 1.10f;

        if (Main.zenithWorld)
            modifier *= 1.15f;

        return modifier;
    }

    #endregion
}
