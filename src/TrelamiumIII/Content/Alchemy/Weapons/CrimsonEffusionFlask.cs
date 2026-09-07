using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Projectiles;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Utilities;
using Terraria.Audio;

namespace TrelamiumIII.Content.Alchemy.Weapons;

public class CrimsonEffusionFlaskItem : ModItem
{
    public override void SetDefaults()
    {
        Item.damage = 8;
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.Blue;

        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = Item.useTime = 25;

        Item.UseSound = SoundID.Item106;
        Item.shoot = ModContent.ProjectileType<CrimsonEffusionFlaskProjectile>();
        Item.shootSpeed = 7f;
        Item.autoReuse = true;
        Item.consumable = true;
        Item.maxStack = Item.CommonMaxStack;
        Item.noUseGraphic = true;
        Item.noMelee = true;

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        component.ChemicalType = ChemicalDamageType.Necrotic;
        component.ChemicalDamage = 3f;
        component.AfflictionRate = 50;
        component.AfflictionDuration = TimeConversion.SetTime(secs: 2);
        component.ReactionChance = 0.012f;
        component.ReactionDamage = 12f;
        component.ReactionHandler = DoBloodsplosion;
    }

    /// <summary>
    /// Per-source reaction: detonates a blood splash into the BloodLayer.
    /// </summary>
    private static void DoBloodsplosion(ChemicalReactionEvent e)
    {
        var layer = GasSystem.GetLayer<BloodLayer>();
        if (layer == null) return;

        Vector2 impactDir = e.Target.oldVelocity.SafeNormalize(-Vector2.UnitY);
        Vector2 splashDir = Vector2.Lerp(impactDir, -Vector2.UnitY, 0.5f)
                                  .SafeNormalize(-Vector2.UnitY);

        CameraSystem.shake = 12;
        SoundEngine.PlaySound(SoundID.Item74);

        int count = Main.rand.Next(12, 16);
        for (int i = 0; i < count; i++)
        {
            Vector2 vel = splashDir.RotatedByRandom(MathHelper.PiOver4)
                        * Main.rand.NextFloat(4f, 7f);

            layer.SpawnNode(e.Target.Center, vel, radius: 33f, life: 200);
        }
    }
}

/// <summary>
///     A flask of blood: it dribbles behind itself, then splashes off whatever it breaks against.
/// </summary>
/// <remarks>
///     The clearest case for the fan shape. Blood thrown at a floor should run along the floor, and
///     the even spacing plus the centre-to-edge speed gradient is what gives the splash a heavy
///     middle and thin fast outriders rather than a uniform spray.
/// </remarks>
public class CrimsonEffusionFlaskProjectile : ChemicalProjectile
{
    public override string Texture => AssetReferences.Content.Alchemy.Weapons.CrimsonEffusionFlaskItem.KEY;

    protected override GasField? Field => GasSystem.GetLayer<BloodLayer>();

    protected override BurstProfile Burst => BurstProfile.FlaskShatter with
    {
        AirCount = new FloatRange(20f, 28f),
        GroundCount = new FloatRange(18f, 24f),
        AirSpeed = new FloatRange(4.5f, 6f),

        // Faster at the rim than at the middle: the splash spreads out across the surface instead
        // of standing up off it, which is what blood does and what frost mist does not.
        GroundSpeed = new FloatRange(5.5f, 10f),
        Arc = MathHelper.Pi * 0.45f,

        Core = EmitterProfile.Default with { NodeRadius = 9f, NodeScatter = new Vector2(1.5f, 3.5f) },
        Body = EmitterProfile.Default with { NodeRadius = 7f, NodeScatter = new Vector2(1.5f, 3.5f) },
        Droplet = EmitterProfile.Default with { NodeRadius = 2f, NodeScatter = new Vector2(1.5f, 3.5f) },

        DamageShare = 1f,
    };
}
