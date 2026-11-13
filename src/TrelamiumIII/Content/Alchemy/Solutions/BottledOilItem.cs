using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Content.Liquids;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Projectiles;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Alchemy.Solutions;

internal sealed class BottledOilItem : BottledLiquidItem
{
    public BottledOilItem()
    {
        Rare = ItemRarityID.White;

        AlmanacPotency = 3;
        Temperaments = Temperament.Volatile;
        Hidden = false;

        Recipes = self => self.CreateRecipe()
            .AddIngredient(ItemID.Bottle)
            .AddCondition(OilLiquids.NearCrude)
            .Register();
    }

    public override void SetDefaults()
    {
        Item.damage = 9;
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.width = Item.height = 26;
        Item.rare = ItemRarityID.Gray;
        Item.value = Item.sellPrice(copper: 0);

        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = Item.useTime = 24;

        Item.UseSound = SoundID.Item106;
        Item.shoot = ModContent.ProjectileType<BottledOilProjectile>();
        Item.shootSpeed = 6.75f;
        Item.autoReuse = true;
        Item.consumable = true;
        Item.maxStack = Item.CommonMaxStack;
        Item.noUseGraphic = true;
        Item.noMelee = true;

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        component.ChemicalType = ChemicalDamageType.Solute;
        component.ChemicalDamage = 1;
        component.debuffRate = 30;
        component.debuffDuration = TimeConversion.SetTime(secs: 3);
        component.Modifiers = [TemperamentModifier.Volatile];
    }

    public override ChemicalRole ChemicalRole => ChemicalRole.Solvent;

    protected override BottleTemplate Template => BottleTemplate.Solution;

    protected override LiquidPalette Palette => new(new Color(14, 14, 14), new Color(36, 28, 43), new Color(40, 38, 60));

    protected override bool SurfaceIsLiquid => true;
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
