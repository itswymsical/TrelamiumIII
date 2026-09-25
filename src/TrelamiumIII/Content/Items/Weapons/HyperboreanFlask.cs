using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Projectiles;
using TrelamiumIII.Utilities;
using Terraria.Audio;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Content.Items.Weapons;

public class HyperboreanFlaskItem : ModItem
{
    public override void SetDefaults()
    {
        base.SetDefaults();
        Item.damage = 10;
        Item.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Item.width = Item.height = 30;
        Item.rare = ItemRarityID.White;

        Item.useStyle = ItemUseStyleID.Swing;
        Item.useAnimation = Item.useTime = 27;

        Item.UseSound = SoundID.Item106;
        Item.shoot = ModContent.ProjectileType<HyperboreanFlaskProjectile>();
        Item.shootSpeed = 7f;
        Item.autoReuse = true;
        Item.consumable = true;
        Item.maxStack = Item.CommonMaxStack;
        Item.noUseGraphic = true;
        Item.noMelee = true;

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        component.ChemicalType = ChemicalDamageType.Cryonic;
        component.ChemicalDamage = 2f;
        component.debuffRate = 40;
        component.debuffDuration = TimeConversion.SetTime(secs: 2);
        component.ReactionChance = 0.015f;
        component.ReactionDamage = 6f;
        component.ReactionHandler = OnCryonicReaction;
    }

    /// <summary>
    /// Per-source reaction: ice burst, frostburn and dust. Lives on this item so other Cryonic sources can react differently.
    /// </summary>
    private static void OnCryonicReaction(ChemicalReactionEvent e)
    {
        Vector2 impactDir = e.Target.oldVelocity.SafeNormalize(-Vector2.UnitY);
        Vector2 splashDir = Vector2.Lerp(impactDir, -Vector2.UnitY, 0.5f)
                                  .SafeNormalize(-Vector2.UnitY);

        SoundEngine.PlaySound(Assets.SFX.HewerReturn.Asset);
        e.Target.AddBuff(BuffID.Frostburn, 60);

        int count = Main.rand.Next(12, 32);
        for (int i = 0; i < count; i++)
        {
            Vector2 vel = splashDir.RotatedByRandom(MathHelper.PiOver4)
                        * Main.rand.NextFloat(4f, 7f);

            Dust.NewDustDirect(vel, e.Target.width, e.Target.height, DustID.Frost, vel.X, vel.Y);
        }
    }
}

/// <summary>A flask of pressurised cryogen: vents ahead of itself, then flash-freezes outward.</summary>
public class HyperboreanFlaskProjectile : ChemicalProjectile
{
    public override string Texture => AssetReferences.Content.Items.Weapons.HyperboreanFlaskItem.KEY;

    protected override GasField? Field => FieldSystem.GetLayer<IceMistField>();

    protected override TrailProfile? Trail => TrailProfile.Vented;

    protected override BurstProfile Burst => BurstProfile.Vent with
    {
        Core = EmitterProfile.Default with
        {
            Lifetime = 9,
            NodeRadius = 18f,
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
