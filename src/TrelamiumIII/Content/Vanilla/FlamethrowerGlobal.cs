using TrelamiumIII.Core.Residues;
using TrelamiumIII.Core.Residues.Types;

namespace TrelamiumIII.Content.Vanilla;
/// <summary>Makes the flamethrower burn tiles.</summary>
public class FlamethrowerGlobal : GlobalProjectile
{
    public override bool InstancePerEntity => true;

    public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        => entity.type == ProjectileID.FlamethrowerTrap || entity.type == ProjectileID.Flames;

    public override bool OnTileCollide(Projectile entity, Vector2 oldVelocity)
    {
        var tileX = entity.Center.X / 16;
        var tileY = entity.Center.Y / 16;

        BurnedResidue.Scorch(entity.Center, 28, 5, BurnProfile.Standard);
        return entity.type == ProjectileID.FlamethrowerTrap || entity.type == ProjectileID.Flames;
    }
}
