using TrelamiumIII.Core.Tiles.Conditions;
using TrelamiumIII.Core.Tiles.Conditions.Types;

namespace TrelamiumIII.Content.Vanilla;

public class FlamethrowerGlobal : GlobalProjectile
{
    public override bool InstancePerEntity => true;

    public override bool AppliesToEntity(Projectile entity, bool lateInstantiation)
        => entity.type == ProjectileID.FlamethrowerTrap || entity.type == ProjectileID.Flames;

    public override bool OnTileCollide(Projectile entity, Vector2 oldVelocity)
    {
        var tileX = entity.Center.X / 16;
        var tileY = entity.Center.Y / 16;

        BurnedCondition.Scorch(entity.Center, 28, 5, BurnProfile.Standard);
        return entity.type == ProjectileID.FlamethrowerTrap || entity.type == ProjectileID.Flames;
    }
}
