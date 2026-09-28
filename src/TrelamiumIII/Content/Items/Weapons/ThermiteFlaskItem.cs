using Terraria.Audio;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Content.Rarities;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Projectiles;
using TrelamiumIII.Utilities;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.Items.Weapons;

public class ThermiteFlaskItem : ModItem
{
    /// <summary>Pixels of line a reaction pours under its target.</summary>
    private const float REACTION_VOLUME = 96f;

    public override void SetDefaults()
    {
        Item.damage = 11;
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.width = Item.height = 30;
        Item.rare = ModContent.RarityType<EmberRarity>();

        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = Item.useTime = 27;

        Item.UseSound = SoundID.Item106;
        Item.shoot = ModContent.ProjectileType<ThermiteFlaskProjectile>();
        Item.shootSpeed = 7f;
        Item.autoReuse = true;
        Item.consumable = true;
        Item.maxStack = Item.CommonMaxStack;
        Item.noUseGraphic = true;
        Item.noMelee = true;

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        component.ChemicalType = ChemicalDamageType.Thermal;
        component.ChemicalDamage = 2;
        component.debuffRate = 10;
        component.debuffDuration = TimeConversion.SetTime(secs: 2);
        component.ReactionChance = 7;
        component.ReactionDamage = 5;
        component.ReactionHandler = PourUnderTarget;
        component.Modifiers = [TemperamentModifier.Kinetic];
    }

    private static void PourUnderTarget(ChemicalReactionEvent e)
    {
        if (FieldSystem.GetLayer<ThermiteFlaskField>() is not { } layer)
            return;

        SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath, e.Target.Center);

        layer.Pour(e.Target.Center, e.Target.velocity * 0.25f, REACTION_VOLUME, e.Owner, payload: e.Payload?.Derived);
    }
}

/// <summary>A flask of thermite that breaks into a line of molten metal along whatever it lands on.</summary>
public class ThermiteFlaskProjectile : ChemicalProjectile
{
    /// <summary>Pixels of line one flask pours, before the payload's burst size.</summary>
    private const float POUR_VOLUME = 208f;

    public override string Texture => AssetReferences.Content.Items.Weapons.ThermiteFlaskItem.KEY;

    protected override GasField? Field => FieldSystem.GetLayer<ThermiteFlaskField>();

    protected override TrailProfile? Trail => TrailProfile.Shed with
    {
        NodeRadius = 3.5f,
        NodeLife = 18f,
    };

    public override void PostAI()
    {
        base.PostAI();

        if (Main.dedServ || !Main.rand.NextBool(3))
            return;

        Dust spark = Dust.NewDustPerfect(
            Projectile.Center + Main.rand.NextVector2Circular(4f, 4f),
            DustID.FireworkFountain_Yellow,
            -Projectile.velocity * 0.2f + Main.rand.NextVector2Circular(1.2f, 1.2f),
            0,
            default,
            Main.rand.NextFloat(0.5f, 0.8f));

        spark.noGravity = true;
    }

    /// <summary>Pours instead of bursting: into the surface it hit, carrying a quarter of the throw's horizontal speed.</summary>
    protected override void Shatter()
    {
        if (Field is not ThermiteFlaskField field)
            return;

        Vector2 momentum = Grounded && ImpactNormal.Y <= 0f ? -ImpactNormal * 2f : Vector2.Zero;
        momentum.X += Projectile.oldVelocity.X * 0.25f;

        float volume = POUR_VOLUME * (Payload?.Stats.BurstCountMultiplier ?? 1f);

        field.Pour(Projectile.Center, momentum, volume, Projectile.owner, Projectile.damage, Projectile.knockBack, Payload);
    }
}
