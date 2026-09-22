using TrelamiumIII.Content.Items.Alchemy.Solutions;

namespace TrelamiumIII.Common.Globals;

public sealed class OilAmmoGlobalItem : GlobalItem
{
    public override bool AppliesToEntity(Item item, bool lateInstantiation)
    {
        return item.type == SolventID.BottledOil;
    }
    public override void SetDefaults(Item item)
    {
        base.SetDefaults(item);
        
        item.DamageType = DamageClass.Ranged;
        item.damage = 0;
        item.knockBack = 1f;
        item.noMelee = true;
        item.shootSpeed = 2f;
        item.consumable = true;
        item.ammo = AmmoID.Gel;
    }
}