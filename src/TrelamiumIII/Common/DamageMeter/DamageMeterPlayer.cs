namespace TrelamiumIII.Common.DamageMeter;

/// <summary>Each <see cref="DamageMeterSource" />'s share of the damage held by the DPS meter.</summary>
public class DamageMeterPlayer : ModPlayer
{
    private float debuffShare;

    private float afflictionShare;

    public float ShareOf(DamageMeterSource source) => source switch
    {
        DamageMeterSource.Debuff => debuffShare,
        DamageMeterSource.Affliction => afflictionShare,
        _ => 1f - debuffShare - afflictionShare,
    };

    /// <summary>
    ///     Folds <paramref name="damage" /> into the shares of a meter that held <paramref name="before" />. The shares
    ///     survive <see cref="Player.getDPS" />, whose rescale scales every source alike.
    /// </summary>
    public void Record(DamageMeterSource source, int before, int damage)
    {
        float total = before + damage;

        if (total <= 0f)
            return;

        debuffShare = (debuffShare * before + (source == DamageMeterSource.Debuff ? damage : 0)) / total;
        afflictionShare = (afflictionShare * before + (source == DamageMeterSource.Affliction ? damage : 0)) / total;
    }

    /// <summary>Runs for item, projectile and <see cref="Player.ApplyDamageToNPC" /> hits.</summary>
    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
        => DamageMeterSystem.AttributeDebuffs(Player.whoAmI, target);
}
