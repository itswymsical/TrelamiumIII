using Terraria.DataStructures;
using TrelamiumIII.Core.Projectiles;

namespace TrelamiumIII.Common.ChemicalClass;

/// <summary>Carries a chemical projectile's <see cref="ChemicalPayload" />, built at spawn, and delivers it on hit.</summary>
public class ChemicalHandler : GlobalProjectile
{
    public override bool InstancePerEntity => true;

    public ChemicalPayload? Payload;

    public static ChemicalPayload? PayloadOf(Projectile projectile)
        => projectile.GetGlobalProjectile<ChemicalHandler>().Payload;

    public override void OnSpawn(Projectile projectile, IEntitySource source)
    {
        if (projectile.DamageType == ModContent.GetInstance<ChemicalDamageClass>())
            Payload = ChemicalPayload.From(source);
    }

    public override void OnHitNPC(Projectile projectile, NPC target, NPC.HitInfo hit, int damageDone)
        => Payload?.Hit(target, (projectile.ModProjectile as ChemicalProjectile)?.Medium, projectile);
}
