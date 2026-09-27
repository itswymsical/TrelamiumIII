using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Projectiles;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Utilities;
using Terraria.Audio;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.Items.Weapons;

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
        component.ChemicalDamage = 1;
        component.debuffRate = 20;
        component.debuffDuration = TimeConversion.SetTime(secs: 7);
        component.ReactionChance = 4;
        component.ReactionDamage = 8;
        component.ReactionHandler = DoBloodsplosion;
        component.Modifiers = [TemperamentModifier.Volatile];
    }

    private static void DoBloodsplosion(ChemicalReactionEvent e)
    {
        var layer = FieldSystem.GetLayer<BloodField>();
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

            layer.SpawnNode(e.Target.Center, vel, radius: 33f, life: 200, owner: e.Owner, payload: e.Payload);
        }
    }
}

/// <summary>A flask of blood: dribbles in flight, then splashes in a fan along whatever it breaks against.</summary>
public class CrimsonEffusionFlaskProjectile : ChemicalProjectile
{
    public override string Texture => AssetReferences.Content.Items.Weapons.CrimsonEffusionFlaskItem.KEY;

    protected override GasField? Field => FieldSystem.GetLayer<BloodField>();

    protected override BurstProfile Burst => BurstProfile.FlaskShatter with
    {
        AirCount = new FloatRange(20f, 28f),
        GroundCount = new FloatRange(18f, 24f),
        AirSpeed = new FloatRange(4.5f, 6f),

        // Faster at the rim than the middle, so the splash runs along the surface.
        GroundSpeed = new FloatRange(5.5f, 10f),
        Arc = MathHelper.Pi * 0.45f,

        Core = EmitterProfile.Default with { NodeRadius = 9f, NodeScatter = new Vector2(1.5f, 3.5f) },
        Body = EmitterProfile.Default with { NodeRadius = 7f, NodeScatter = new Vector2(1.5f, 3.5f) },
        Droplet = EmitterProfile.Default with { NodeRadius = 2f, NodeScatter = new Vector2(1.5f, 3.5f) },

        DamageShare = 1f,
    };
}
