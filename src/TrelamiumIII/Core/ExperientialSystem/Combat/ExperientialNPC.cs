using System.Collections.Generic;

namespace TrelamiumIII.Core.ExperientialSystem.Combat;

/// <summary>
/// Attributes damage on an NPC and turns its death into Exp for whoever earned it.
///
/// <para>
/// The award is sized as a <i>fraction of one meter</i> rather than a number of Exp. That is what lets the
/// same value be computed where the facts live (the server, which sees NPC stats and damage shares) and
/// spent where the state lives (the owning client, which holds Threshold, history and allowance). It also
/// means an award stays correct if the player ascends between the kill and the packet arriving.
/// </para>
///
/// <para>
/// Bosses are not a separate code path here. A boss is simply a very high menace and a larger base
/// fraction, which is why this class has no boss XP curve, no level compression and no level-delta table.
/// </para>
/// </summary>
public sealed class ExperientialNPC : GlobalNPC
{
    public override bool InstancePerEntity => true;

    private readonly Dictionary<int, int> _playerDamage = [];
    private int _totalDamage;

    #region Damage attribution

    public override void OnHitByItem(NPC npc, Player player, Item item, NPC.HitInfo hit, int damageDone)
    {
        if (!npc.immortal)
            TrackDamage(player.whoAmI, hit.Damage);
    }

    public override void OnHitByProjectile(NPC npc, Projectile projectile, NPC.HitInfo hit, int damageDone)
    {
        if (npc.immortal)
            return;

        if (projectile.owner >= 0 && projectile.owner < Main.maxPlayers)
            TrackDamage(projectile.owner, hit.Damage);
    }

    private void TrackDamage(int playerIndex, int damage)
    {
        if (damage <= 0)
            return;

        _playerDamage[playerIndex] = _playerDamage.GetValueOrDefault(playerIndex) + damage;
        _totalDamage += damage;
    }

    #endregion

    #region Award

    public override void OnKill(NPC npc)
    {
        // Clients see deaths too, but the server is the one that has everybody's damage.
        if (Main.netMode == NetmodeID.MultiplayerClient)
        {
            Reset();
            return;
        }

        if (ShouldSkipExperience(npc) || !ResolveContributors(npc))
        {
            Reset();
            return;
        }

        NpcStatBoard npcBoard = StatBoard.Of(npc);
        ExpSource source = ExpSource.Of(npc);

        float baseFraction = npc.boss
            ? ExperientialConstants.BossKillFraction
            : 1f / ExperientialConstants.KillsPerThreshold;

        float difficulty = ExperientialConstants.DifficultyModifier();
        float totalDamage = Math.Max(_totalDamage, 1);

        foreach (var (playerIndex, damageDealt) in _playerDamage)
        {
            Player player = Main.player[playerIndex];
            if (!player.active)
                continue;

            float menace = StatBoard.Menace(StatBoard.Of(player), npcBoard);
            float share = DamageShare(damageDealt, totalDamage);

            float fraction = baseFraction
                           * StatBoard.MenaceMultiplier(menace)
                           * difficulty
                           * share;

            Deliver(player, fraction, source);
        }

        Reset();
    }

    /// <summary>
    /// Solo kills and near-solo kills pay in full; genuinely shared kills split by contribution. Without the
    /// 90% grace a stray hit from a passing player would quietly dock the person who did the work.
    /// </summary>
    private static float DamageShare(int damageDealt, float totalDamage)
        => damageDealt >= totalDamage * 0.9f ? 1f : damageDealt / totalDamage;

    /// <summary>
    /// Makes sure somebody gets credit. Multi-segment bosses (EoW, Destroyer, BoC) frequently die on a
    /// segment that never took a direct hit, which would otherwise drop the whole award on the floor.
    /// </summary>
    private bool ResolveContributors(NPC npc)
    {
        if (_playerDamage.Count > 0)
            return true;

        if (!npc.boss)
            return false;

        int nearest = FindNearestActivePlayer(npc);
        if (nearest < 0)
            return false;

        _playerDamage[nearest] = 1;
        _totalDamage = 1;

        return true;
    }

    /// <summary>Hands the award to the client that owns the player, or applies it directly in singleplayer.</summary>
    private static void Deliver(Player player, float fraction, in ExpSource source)
    {
        if (Main.netMode == NetmodeID.Server)
        {
            TrelamiumIII.SendExperientialGrant(player.whoAmI, fraction, source);
            return;
        }

        Apply(player, fraction, source);
    }

    /// <summary>
    /// Applies an award on the machine that owns the player: personal history first, then the Threshold's
    /// allowance. Both of those are save-file state, so this never runs for a remote player.
    /// </summary>
    public static void Apply(Player player, float fraction, in ExpSource source)
    {
        var experiential = player.GetModPlayer<ExperientialPlayer>();
        bool fromNpc = source.Kind is ExpSourceKind.Npc or ExpSourceKind.Boss;

        // Read the multiplier before booking the kill, so this kill is not already discounting itself.
        float multiplier = 1f;
        CombatLedger? ledger = null;

        if (fromNpc)
        {
            ledger = player.GetModPlayer<CombatLedger>();
            multiplier = ledger.RewardMultiplier(source.Id);
        }

        // GrantFraction reports the Exp itself, so there is nothing to announce here.
        experiential.GrantFraction(fraction * multiplier, source);

        // The kill happened whether or not it paid, so it counts against familiarity either way.
        ledger?.RegisterKill(source.Id);
    }

    #endregion

    #region Presentation

    /// <summary>
    /// How this matchup reads for <paramref name="player"/> right now. Menace depends on both sides, so
    /// unlike the old cached NPC level there is nothing meaningful to store on the NPC itself.
    /// </summary>
    public static float MenaceFor(NPC npc, Player player) => StatBoard.Menace(StatBoard.Of(player), StatBoard.Of(npc));

    public static MenaceTier TierFor(NPC npc, Player player) => StatBoard.TierOf(MenaceFor(npc, player));


    #endregion

    #region Helpers

    /// <summary>Bosses bypass the value check, because many boss segments carry a value of 0.</summary>
    private static bool ShouldSkipExperience(NPC npc)
    {
        if (npc.immortal || npc.friendly || npc.CountsAsACritter ||
            npc.SpawnedFromStatue || npc.isLikeATownNPC ||
            npc.type == NPCID.TargetDummy)
            return true;

        if (npc.boss)
            return false;

        return npc.value <= 0;
    }

    private static int FindNearestActivePlayer(NPC npc)
    {
        int nearest = -1;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player player = Main.player[i];
            if (!player.active || player.dead)
                continue;

            float distance = Vector2.DistanceSquared(npc.Center, player.Center);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest = i;
            }
        }

        return nearest;
    }

    private void Reset()
    {
        _playerDamage.Clear();
        _totalDamage = 0;
    }

    #endregion
}
