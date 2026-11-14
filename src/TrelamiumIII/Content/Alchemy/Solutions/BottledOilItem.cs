using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Projectiles;

namespace TrelamiumIII.Content.Alchemy.Solutions;

public class BottledOilItem : ModItem, IAlmanacDefined
{
    AlmanacEntry IAlmanacDefined.AlmanacEntry => new()
    {
        Temperaments = Temperament.Inert | Temperament.Volatile,
        AlmanacPotency = 3,
        Hidden = true,
    };

    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.value = Item.sellPrice(silver: 14);
    }

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.Bottle)
            .AddCondition(OilLiquids.NearCrude)
            .Register();
    }
}

public class BottledOilProjectile : ChemicalProjectile
{
    public override string Texture => AssetReferences.Content.Alchemy.Solutions.BottledOilItem.KEY;

    protected override GasField? Field => FieldSystem.GetLayer<CrudeOilField>();

    protected override TrailProfile? Trail => TrailProfile.Shed with
    {
        NodeRadius = 3.5f,
        NodeLife = 20f,
    };

    protected override BurstProfile Burst => BurstProfile.FlaskShatter with
    {
        AirCount = new FloatRange(12f, 16f),
        GroundCount = new FloatRange(9f, 13f),
        AirSpeed = new FloatRange(2f, 3.5f),
        GroundSpeed = new FloatRange(3.5f, 5f),
        Arc = MathHelper.Pi * 0.4f,
        UpwardBias = 0.5f,
        Core = Glob with { NodeRadius = 16f },
        Body = Glob with { NodeRadius = 12f },
        Droplet = Glob with { NodeRadius = 8f, NodeLife = 90f },
        DamageShare = 0.5f,
    };

    private static EmitterProfile Glob => EmitterProfile.Default with
    {
        Lifetime = 8,
        NodeLife = 200f,
        NodeScatter = new Vector2(1f, 1f),
        Inheritance = 0.6f,
        Drag = 0.9f,
    };
}
