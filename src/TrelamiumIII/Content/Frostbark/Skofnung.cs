using Terraria.Audio;
using Terraria.DataStructures;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Components.Items;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Graphics.Fields.Layers;
using TrelamiumIII.Core.Graphics.Particles;
using TrelamiumIII.Core.Projectiles;

namespace TrelamiumIII.Content.Frostbark;

public class Skofnung : ModItem
{
    public override void SetDefaults()
    {
        Item.DamageType = DamageClass.Melee;
        Item.damage = 12;
        Item.knockBack = 6.5f;
        Item.width = Item.height = 52;

        // Read off the projectile's profile rather than the preset it was built from, or the
        // item's clock silently drifts from the weapon's the moment the profile is tuned.
        Item.useTime = Item.useAnimation = SkofnungProjectile.Swing.ComboPeriod;
        Item.useStyle = ItemUseStyleID.Shoot;

        Item.UseSound = null;

        Item.rare =  ItemRarityID.Blue;
        Item.value = Item.sellPrice(silver: 22);
        Item.shoot = ModContent.ProjectileType<SkofnungProjectile>();
        Item.autoReuse = false;
        Item.noMelee = true;
        Item.noUseGraphic = true;
        Item.channel = true;

        if (!Item.TryEnable(out ChemicalSourceComponent component))
            return;

        component.ChemicalType = ChemicalDamageType.Cryonic;
        component.ReactionChance = 0.013f;
        component.ChemicalDamage = 0.35f;
    }

    public override bool MeleePrefix() => true;

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        Projectile.NewProjectile(source, player.Center, Vector2.Zero, Item.shoot, damage, knockback, player.whoAmI);

        return false;
    }

    public override bool CanUseItem(Player player) => player.ownedProjectileCounts[Item.shoot] <= 0;

    public override void AddRecipes()
    {
        CreateRecipe()
            .AddIngredient(ItemID.BorealWood, 10)
            .AddIngredient(ItemID.IceBlock, 12)
            .AddRecipeGroup(RecipeGroupID.IronBar, 8)
            .Register();
    }
}

public class SkofnungProjectile : HeldSwordProjectile
{
    /// <summary>Shared with the item so the two can't disagree about tempo.</summary>
    public static readonly SwordProfile Swing = SwordProfile.Dagger with
    {
        BladeLength = 60f,
        ArcOffset = 0,
        SwingTicks = 14,
        SwingSound = SoundID.Item1,
        Steps = [SwingStep.SlashDown, SwingStep.SlashUp],

        //TrailOpacity = 0.75f,
        //TrailInner = 0.35f,
        //TrailOuter = 0.15f,
        //TrailTexture = Assets.Textures.VFX.r.KEY,
        //TrailLength = 0.85f,
        TrailColor = Color.AliceBlue,
    };

    public override string Texture => AssetReferences.Content.Frostbark.Skofnung.KEY;

    protected override SwordProfile Profile => Swing;

    public override void SetDefaults()
    {
        base.SetDefaults();

        Projectile.scale = 1f;       
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        var layer = FieldSystem.GetLayer<ArcticField>();

        if (layer == null) return;
        SoundEngine.PlaySound(SoundID.DD2_WitherBeastCrystalImpact with { Volume = 0.35f });
        Vector2 impactDir = Projectile.oldVelocity.SafeNormalize(-Vector2.UnitY);
        Vector2 splashDir = Vector2.Lerp(impactDir, -Vector2.UnitY, 0.5f).SafeNormalize(-Vector2.UnitY);

        if (Projectile.owner == Main.myPlayer)
        {
            Vector2 vel = splashDir.RotatedByRandom(MathHelper.PiOver2)
                            * Main.rand.NextFloat(2f, 9f);

            int count = Main.rand.Next(16, 22);

            for (int i = 0; i < count; i++)
            {
                Vector2 vel2 = splashDir.RotatedByRandom(MathHelper.PiOver2)
                            * Main.rand.NextFloat(4f, 8f);

                layer.SpawnNode(BladeTip, vel2, radius: 12, life: 40);
            }

            for (int i = 0; i < 4; i++)
                ParticleSystem.Spawn<IceShardParticle>(BladeTip, vel, 70, scale: 0.5f, rotation: vel.X);
            
        }

        if (hit.Crit)
        {
            target.AddBuff(BuffID.Frostburn, 40 + damageDone / 10);
        }
    }
}

public class IceShardParticle : Particle
{
    public override string[] Textures => ["Images/Projectile_557"];

    public override Vector2 Size => new(7f);

    public override float Gravity => 0.2f;

    public override bool TileCollide => true;

    public override float Drag => 0.98f;

    public override float Bounce => 0.34f;

    public override int FadeOutTicks => 30;
}