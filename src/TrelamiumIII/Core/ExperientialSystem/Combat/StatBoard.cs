namespace TrelamiumIII.Core.ExperientialSystem.Combat;

/// <summary>How a matchup reads at a glance. Derived from menace, for meter icons and NPC nameplates.</summary>
public enum MenaceTier
{
    /// <summary>Outclassed. You kill it far faster than it could kill you.</summary>
    Trivial,

    /// <summary>A fair trade.</summary>
    Even,

    /// <summary>It is winning the exchange, but the fight is takeable.</summary>
    Dangerous,

    /// <summary>It kills you several times over in the time you would need to kill it.</summary>
    Overwhelming
}

/// <summary>The player's half of a matchup: what they can put out, and what they can absorb.</summary>
public readonly record struct PlayerStatBoard(
    float PerHit,
    float AttacksPerSecond,
    int MaxLife,
    int Defense,
    float DamageReduction)
{
    /// <summary>Raw damage per second before the target's defense is considered.</summary>
    public float Dps => PerHit * AttacksPerSecond;
}

/// <summary>The NPC's half of a matchup, read off the live instance so difficulty scaling is already baked in.</summary>
public readonly record struct NpcStatBoard(int MaxLife, int ContactDamage, int Defense, bool IsBoss);

/// <summary>
/// Reduces a player and an NPC to one dimensionless number: <b>menace</b>.
///
/// <para>
/// <c>menace = secondsToKillNpc / secondsToDie</c>. Above 1 the NPC is out-pacing the player; below 1
/// the player is out-pacing the NPC. That single ratio replaces the old
/// "derive an absolute level 1-100 from a stat composite, then compress it back toward the player"
/// pair of steps, which existed only to approximate this and needed a hand-anchored piecewise table to do it.
/// </para>
///
/// <para>
/// Because it is a ratio, it absorbs the things that used to need special cases. Expert and Master inflate
/// NPC health and damage, which raises menace on its own. Better gear lowers it on its own. A boss is not
/// a special category, it is just a very high menace - so the dedicated boss XP curve and the boss level
/// compression both stop being necessary.
/// </para>
///
/// <para>
/// It is an estimate, and honest about it: contact damage stands in for a boss's real projectile threat,
/// and the offense estimate reads one weapon. Both are documented at their call sites.
/// </para>
/// </summary>
public static class StatBoard
{
    private static readonly PlayerStatBoard[] Cache = new PlayerStatBoard[Main.maxPlayers];
    private static readonly int[] CacheStamp = new int[Main.maxPlayers];

    #region Boards

    /// <summary>
    /// The player's board, recomputed at most every <see cref="ExperientialConstants.StatBoardCacheTicks"/>.
    /// The weapon scan walks the inventory, and this is hit on every kill, so the cache matters.
    /// </summary>
    public static PlayerStatBoard Of(Player player)
    {
        int slot = player.whoAmI;
        int now = (int)Main.GameUpdateCount;

        if (slot >= 0 && slot < Cache.Length)
        {
            if (CacheStamp[slot] != 0 && now - CacheStamp[slot] < ExperientialConstants.StatBoardCacheTicks)
                return Cache[slot];

            PlayerStatBoard fresh = Build(player);

            Cache[slot] = fresh;
            CacheStamp[slot] = now;

            return fresh;
        }

        return Build(player);
    }

    public static NpcStatBoard Of(NPC npc) => new(npc.lifeMax, npc.damage, npc.defense, npc.boss);

    /// <summary>Drops a player's cached board. Call when gear changes in a way that should read immediately.</summary>
    public static void Invalidate(int playerIndex)
    {
        if (playerIndex >= 0 && playerIndex < CacheStamp.Length)
            CacheStamp[playerIndex] = 0;
    }

    private static PlayerStatBoard Build(Player player)
    {
        ScoreBestWeapon(player, out float perHit, out float attacksPerSecond);

        return new PlayerStatBoard(
            perHit,
            attacksPerSecond,
            Math.Max(player.statLifeMax2, 1),
            player.statDefense,
            Math.Clamp(player.endurance, 0f, 0.9f));
    }

    /// <summary>
    /// "Potential damage": the best sustained output the player could bring to bear right now, scored across
    /// the held item and the inventory rather than only what happens to be swinging.
    ///
    /// <para>
    /// Deliberately shallow. It does not model piercing, homing, multi-hit or true melee, so a build that
    /// leans on those reads weaker than it plays, and <see cref="ExperientialConstants.PlayerDpsFloor"/>
    /// keeps that from bottoming out. Summon weapons get the one correction that would otherwise be badly
    /// wrong - minion count - because a summoner holding a whip would otherwise read as nearly harmless.
    /// </para>
    /// </summary>
    private static void ScoreBestWeapon(Player player, out float perHit, out float attacksPerSecond)
    {
        perHit = ExperientialConstants.PlayerDpsFloor;
        attacksPerSecond = 1f;

        float bestDps = ExperientialConstants.PlayerDpsFloor;

        Score(player, player.HeldItem, ref perHit, ref attacksPerSecond, ref bestDps);

        for (int i = 0; i < player.inventory.Length; i++)
            Score(player, player.inventory[i], ref perHit, ref attacksPerSecond, ref bestDps);
    }

    private static void Score(Player player, Item item, ref float perHit, ref float attacksPerSecond, ref float bestDps)
    {
        if (item is null || item.IsAir || item.damage <= 0 || item.useAnimation <= 0)
            return;

        int damage = player.GetWeaponDamage(item);
        if (damage <= 0)
            return;

        // Vanilla crits double the hit, so expected damage is damage * (1 + critChance).
        float crit = Math.Clamp(player.GetWeaponCrit(item) / 100f, 0f, 1f);
        float hit = damage * (1f + crit);

        float attackSpeed = player.GetWeaponAttackSpeed(item);
        if (attackSpeed <= 0f)
            attackSpeed = 1f;

        float rate = 60f / (item.useAnimation / attackSpeed);

        // A summon weapon's listed damage is per minion hit; what the player actually fields is the
        // whole squad, attacking on its own. Without this a summoner scores as an unarmed player.
        if (item.DamageType.CountsAsClass(DamageClass.Summon))
            hit *= Math.Max(player.maxMinions, 1) * ExperientialConstants.SummonUptime;

        float dps = hit * rate;
        if (dps <= bestDps)
            return;

        bestDps = dps;
        perHit = hit;
        attacksPerSecond = rate;
    }

    #endregion

    #region Menace

    /// <summary>
    /// How much harder this NPC hits the player than the player hits it, as a ratio of time-to-kill.
    /// 1 is a dead-even trade; 3 means it kills the player three times over in the time they need to drop it.
    /// </summary>
    public static float Menace(in PlayerStatBoard player, in NpcStatBoard npc)
    {
        float secondsToKillNpc = npc.MaxLife / PlayerDpsAgainst(player, npc);
        float secondsToDie = player.MaxLife / NpcDpsAgainst(npc, player);

        return Math.Clamp(secondsToKillNpc / secondsToDie, 0f, ExperientialConstants.MenaceCeiling);
    }

    /// <summary>Player output after the NPC's defense, which applies per hit rather than per second.</summary>
    private static float PlayerDpsAgainst(in PlayerStatBoard player, in NpcStatBoard npc)
    {
        float floor = player.PerHit * ExperientialConstants.MinimumHitFraction;
        float landed = MathF.Max(player.PerHit - npc.Defense * ExperientialConstants.DefenseFactor, floor);

        return MathF.Max(landed * player.AttacksPerSecond, ExperientialConstants.MinimumDps);
    }

    /// <summary>
    /// Incoming pressure, modelled as contact hits landing on the player's i-frame cadence.
    ///
    /// <para>
    /// Contact damage understates a boss that fights mostly through projectiles. It is used anyway because
    /// it is the one damage figure every NPC actually carries; a projectile-aware estimate would need a
    /// per-boss table, which is exactly the hand-anchored bookkeeping this model exists to delete.
    /// </para>
    /// </summary>
    private static float NpcDpsAgainst(in NpcStatBoard npc, in PlayerStatBoard player)
    {
        float floor = npc.ContactDamage * ExperientialConstants.MinimumHitFraction;
        float landed = MathF.Max(npc.ContactDamage - player.Defense * ExperientialConstants.DefenseFactor, floor);

        landed *= 1f - player.DamageReduction;

        return MathF.Max(landed / ExperientialConstants.PlayerHitCadenceSeconds, ExperientialConstants.MinimumDps);
    }

    /// <summary>
    /// Turns menace into an award multiplier: a soft falloff below parity so trivial kills still tick over,
    /// and a saturating bonus above it so an unwinnable target cannot be farmed for unbounded Exp.
    /// </summary>
    public static float MenaceMultiplier(float menace)
    {
        if (menace <= ExperientialConstants.MenaceParity)
        {
            float shaped = MathF.Pow(Math.Max(menace, 0f) / ExperientialConstants.MenaceParity, ExperientialConstants.MenaceUnderShape);

            return ExperientialConstants.MenaceFloorMultiplier
                 + (1f - ExperientialConstants.MenaceFloorMultiplier) * shaped;
        }

        float over = (menace - ExperientialConstants.MenaceParity) / ExperientialConstants.MenaceOverScale;

        return 1f + ExperientialConstants.MenaceOverGain * (1f - MathF.Exp(-over));
    }

    public static MenaceTier TierOf(float menace) => menace switch
    {
        < ExperientialConstants.MenaceTrivialBelow => MenaceTier.Trivial,
        < ExperientialConstants.MenaceEvenBelow => MenaceTier.Even,
        < ExperientialConstants.MenaceDangerousBelow => MenaceTier.Dangerous,
        _ => MenaceTier.Overwhelming
    };

    #endregion
}
