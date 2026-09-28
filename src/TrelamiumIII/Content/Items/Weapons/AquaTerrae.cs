using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Projectiles;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Items.Weapons;

public class AquaTerrae : ModItem, IAlmanacDefined
{
    AlmanacEntry IAlmanacDefined.AlmanacEntry => new()
    {
        Temperaments = Temperament.Alkaline | Temperament.Catalytic,
        AlmanacPotency = 1,
    };

    public override void SetDefaults()
    {
        Item.damage = 2;
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.width = Item.height = 26;
        Item.rare = ItemRarityID.Gray;
        Item.value = Item.sellPrice(copper: 0);

        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = Item.useTime = 25;

        Item.UseSound = SoundID.Item106;
        Item.shoot = ModContent.ProjectileType<MudBottleProjectile>();
        Item.shootSpeed = 6.65f;
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
        component.Modifiers = [TemperamentModifier.Inert];
    }
    public override void AddRecipes()
    {
        CreateRecipe(5)
            .AddIngredient(ItemID.BottledWater)
            .AddIngredient(ItemID.DirtBlock, 5)
            .Register();
    }
}

/// <summary>A bottle of mud that breaks into a heavy blob, which slumps, pools and coats the ground.</summary>
public class MudBottleProjectile : ChemicalProjectile
{
    public override string Texture => AssetReferences.Content.Items.Weapons.AquaTerrae.KEY;

    protected override GasField? Field => FieldSystem.GetLayer<MudField>();

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

    /// <summary>A short, heavy arm whose nodes keep most of its speed, so the splash lands as one slumping mass.</summary>
    private static EmitterProfile Glob => EmitterProfile.Default with
    {
        Lifetime = 8,
        NodeLife = 200f,
        NodeScatter = new Vector2(1f, 1f),
        Inheritance = 0.6f,
        Drag = 0.9f,
    };
}
