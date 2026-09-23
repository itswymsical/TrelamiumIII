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
    public override void SetDefaults()
    {
        Item.damage = 10;
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
        component.ChemicalDamage = 2f;
        component.AfflictionRate = 10;
        component.AfflictionDuration = TimeConversion.SetTime(secs: 2);
        component.ReactionChance = 0.007f;
        component.ReactionDamage = 5f;
        component.ReactionHandler = DoThermiteSpread;
    }

    private static void DoThermiteSpread(ChemicalReactionEvent e)
    {
        var layer = FieldSystem.GetLayer<ThermiteFlaskField>();
        if (layer == null) return;

        Vector2 impactDir = e.Target.oldVelocity.SafeNormalize(-Vector2.UnitY);
        Vector2 splashDir = Vector2.Lerp(impactDir, -Vector2.UnitY, 0.5f)
                                  .SafeNormalize(-Vector2.UnitY);

        SoundEngine.PlaySound(SoundID.DD2_BetsyFlameBreath);

        int count = Main.rand.Next(6, 10);
        for (int i = 0; i < count; i++)
        {
            Vector2 vel = splashDir.RotatedByRandom(MathHelper.PiOver4)
                        * Main.rand.NextFloat(4f, 7f);

            layer.SpawnNode(e.Target.Center, vel, radius: 11f, life: 120);
        }
    }
}

/// <summary>
///     A flask of thermite: vents forward on the way in, then throws molten arms that arc, bounce twice and char
///     what they touch. The scorch lives on <see cref="ThermiteFlaskField" />.
/// </summary>
public class ThermiteFlaskProjectile : ChemicalProjectile
{
    public override string Texture => AssetReferences.Content.Items.Weapons.ThermiteFlaskItem.KEY;

    protected override GasField? Field => FieldSystem.GetLayer<ThermiteFlaskField>();

    protected override TrailProfile? Trail => TrailProfile.Vented with
    {
        Jitter = new Vector2(1.5f, 0.5f),
    };

    /// <summary>A tight cone of heavy, bouncing arms, aimed off the surface normal so thermite runs down walls.</summary>
    protected override BurstProfile Burst => BurstProfile.Vent with
    {
        AirCount = new FloatRange(8f, 11f),
        GroundCount = new FloatRange(8f, 11f),
        AirSpeed = new FloatRange(5.25f, 8.05f),
        GroundSpeed = new FloatRange(5.25f, 8.05f),
        Arc = 1.25f,
        UpwardBias = 0.35f,
        Core = EmitterProfile.Ballistic with { NodeRadius = 6f },
        DamageShare = 1f,
    };
}
