using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrelamiumIII.Content.Misc;

public class ShortswordRework : GlobalProjectile
{
    public override bool AppliesToEntity(Projectile proj, bool lateInstantiation)
    {
        return proj.type == ProjectileID.CopperShortswordStab || proj.type == ProjectileID.TinShortswordStab ||
            proj.type == ProjectileID.IronShortswordStab || proj.type == ProjectileID.LeadShortswordStab ||
            proj.type == ProjectileID.SilverShortswordStab || proj.type == ProjectileID.TungstenShortswordStab ||
            proj.type == ProjectileID.GoldShortswordStab || proj.type == ProjectileID.PlatinumShortswordStab;
    }
    public override void AI(Projectile projectile)
    {
        base.AI(projectile);
        
    }
}
