namespace TrelamiumIII.Common.Globals.Items;

public sealed class AcornGlobalItem : GlobalItem
{
    public override bool AppliesToEntity(Item entity, bool lateInstantiation)
    {
        return entity.type == ItemID.Acorn;
    }

    public override void SetDefaults(Item entity)
    {
        base.SetDefaults(entity);
        
        entity.DamageType = DamageClass.Ranged;
        entity.damage = 6;
        entity.knockBack = 1f;
        entity.noMelee = true;
        entity.shootSpeed = 7f;
        
        entity.ammo = entity.type;
    }
}

public sealed class AmmoBottleItem : GlobalItem
{
    public override bool AppliesToEntity(Item entity, bool lateInstantiation)
    {
        return entity.type == ItemID.BottledWater;
    }

    public override void SetDefaults(Item entity)
    {
        base.SetDefaults(entity);

        entity.DamageType = DamageClass.Generic;
        entity.damage = 0;
        entity.knockBack = 1f;
        entity.noMelee = true;
        entity.shootSpeed = 2f;

        entity.ammo = entity.type;
    }
}