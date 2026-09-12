using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Projectiles;
using TrelamiumIII.Utilities;
using Terraria.Audio;

namespace TrelamiumIII.Content.Alchemy.Weapons;

public class PrismaticStarflaskItem : ModItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.damage = 7;
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.Blue;

        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = Item.useTime = 27;

        Item.UseSound = SoundID.Item106;
        Item.shoot = ModContent.ProjectileType<PrismaticStarflaskProjectile>();
        Item.shootSpeed = 7f;
        Item.autoReuse = true;
        Item.consumable = true;
        Item.maxStack = Item.CommonMaxStack;
        Item.noUseGraphic = true;
        Item.noMelee = true;

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        component.ChemicalType = ChemicalDamageType.Kinetic;
        component.ChemicalDamage = 2f;
        component.AfflictionRate = 30;
        component.AfflictionDuration = TimeConversion.SetTime(secs: 2);
        component.ReactionChance = 0.015f;
        component.ReactionDamage = 6f;
        component.ReactionHandler = OnPrismaticReaction;
    }

    private static void OnPrismaticReaction(ChemicalReactionEvent e)
    {
        Vector2 impactDir = e.Target.oldVelocity.SafeNormalize(-Vector2.UnitY);
        Vector2 splashDir = Vector2.Lerp(impactDir, -Vector2.UnitY, 0.5f)
                                  .SafeNormalize(-Vector2.UnitY);

        SoundEngine.PlaySound(Assets.SFX.HewerReturn.Asset);
        e.Target.AddBuff(BuffID.RainbowWhipNPCDebuff, 60);

        int count = Main.rand.Next(8, 16);
        for (int i = 0; i < count; i++)
        {
            Vector2 vel = splashDir.RotatedByRandom(MathHelper.PiOver4)
                        * Main.rand.NextFloat(4f, 2f);

            Dust.NewDustDirect(vel, e.Target.width, e.Target.height, DustID.Frost, vel.X, vel.Y);
        }
    }
}

/// <summary>A flask of unstable starlight: sheds sparks in flight and scatters them on breaking.</summary>
public class PrismaticStarflaskProjectile : ChemicalProjectile
{
    public override string Texture => AssetReferences.Content.Alchemy.Weapons.PrismaticStarflaskItem.KEY;

    protected override GasField? Field => GasSystem.GetLayer<StarLayer>();

    protected override TrailProfile? Trail => TrailProfile.Vented with
    {
        NodeRadius = 9f,
        Speed = new FloatRange(1f, 2f),
        Jitter = new Vector2(2.5f, 2.5f),
    };

    protected override BurstProfile Burst => BurstProfile.Vent with
    {
        AirCount = new FloatRange(7f, 8f),
        GroundCount = new FloatRange(7f, 8f),
        AirSpeed = new FloatRange(1f, 7f),
        GroundSpeed = new FloatRange(1f, 7f),
        Core = EmitterProfile.Default with
        {
            Lifetime = 30,
            NodeRadius = 12f,
        },
        DamageShare = 1f,
    };

    protected override void PlayShatter()
    {
        SoundEngine.PlaySound(Assets.SFX.Items.BigFlask.Asset with { Volume = 0.7f, MaxInstances = -1, PitchRange = (-0.15f, 0.15f) });
        SoundEngine.PlaySound(SoundID.Item107 with { Volume = 0.65f });
        SoundEngine.PlaySound(Assets.SFX.HewerReturn.Asset with { Volume = 0.2f, MaxInstances = -1, PitchRange = (-0.15f, 0.15f) });
    }
}
