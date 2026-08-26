using TrelamiumIII.Content.EyeOfCthulhu;
using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Content.Misc;

public class WoodenBuckler : ModItem
{
    public override void SetDefaults()
    {
        Item.width = Item.height = 28;
        Item.rare = ItemRarityID.White;
        Item.value = Item.sellPrice(copper: 3);
        Item.damage = 20;
        Item.knockBack = 7f;
        Item.maxStack = 10;
        Item.consumable = true;
    }

    public override void UpdateInventory(Player player)
    {
        base.UpdateInventory(player);
        player.GetModPlayer<WoodenBucklerPlayer>().BucklerEquipped = true;
    }

    public override void AddRecipes()
    {
        CreateRecipe()
        .AddIngredient(ItemID.Wood, 12)
        .AddRecipeGroup(RecipeGroupID.IronBar)
        .AddTile(TileID.Anvils)
        .Register();
    }
}

public class WoodenBucklerPlayer : ModPlayer
{
    public const int DashRight = 2;
    public const int DashLeft = 3;

    public const int DashCooldown = 50;
    public const int DashDuration = 35;

    public const float DashVelocity = 9.5f;
    /// <summary>
    /// indicates there is no current dash direction
    /// </summary>
    public int DashDir = -1;

    public int DashDelay = 0;
    public int DashTimer = 0;
    public bool BucklerEquipped;

    public override void ResetEffects()
    {
        BucklerEquipped = false;

        if (Player.controlRight && Player.releaseRight
            && Player.doubleTapCardinalTimer[DashRight] < 15 && Player.doubleTapCardinalTimer[DashLeft] == 0)
            DashDir = DashRight;

        else if (Player.controlLeft && Player.releaseLeft
            && Player.doubleTapCardinalTimer[DashLeft] < 15 && Player.doubleTapCardinalTimer[DashRight] == 0)
            DashDir = DashLeft;

        else
            DashDir = -1;

    }

    private bool CanUseDash()
        => BucklerEquipped && Player.dashType == DashID.None && !Player.setSolar && !Player.mount.Active;


    public override void PreUpdateMovement()
    {
        if (CanUseDash() && DashDir != -1 && DashDelay == 0)
        {
            Vector2 newVelocity = Player.velocity;
            switch (DashDir)
            {
                case DashLeft when Player.velocity.X > -DashVelocity:
                case DashRight when Player.velocity.X < DashVelocity:
                    {
                        float dashDirection = DashDir == DashRight ? 1 : -1;
                        newVelocity.X = dashDirection * DashVelocity;
                        break;
                    }
                default:
                    return;
            }

            DashDelay = DashCooldown;
            DashTimer = DashDuration;
            Player.velocity = newVelocity;
        }

        if (DashDelay > 0)
            DashDelay--;

        if (DashTimer > 0)
        {
            Player.eocDash = DashTimer;
            Player.armorEffectDrawShadowEOCShield = true;
            CheckDashHit();
            DashTimer--;
        }
    }

    private Item BucklerStats()
    {
        for (int i = 0; i < Player.inventory.Length; i++)
        {
            if (Player.inventory[i].type == ModContent.ItemType<WoodenBuckler>())
            {
                return Player.inventory[i];
            }
        }
        return null;
    }

    private void CheckDashHit()
    {
        Rectangle hitBox = new Rectangle(
                    (int)(Player.position.X + Player.velocity.X * 0.5f - 6f),
                    (int)(Player.position.Y + Player.velocity.Y * 0.5f - 6f),
                    Player.width + 10,
                    Player.height + 10
                );

        Item buckler = BucklerStats();

        // Safety check: if they somehow dashed but don't have the item.
        if (buckler == null) return;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC target = Main.npc[i];

            if (!target.active || target.friendly || target.dontTakeDamage || target.immune[Player.whoAmI] > 0)
                continue;

            if (hitBox.Intersects(target.Hitbox))
            {
                int hitDirection = Player.velocity.X > 0 ? 1 : -1;

                int damage = Player.GetWeaponDamage(buckler);

                float knockback = Player.GetWeaponKnockback(buckler, buckler.knockBack);

                Player.ApplyDamageToNPC(target, damage, knockback, hitDirection, false);

                // SoundEngine.PlaySound(SoundID.Item37, Player.position);

                Player.immune = true;
                Player.immuneTime = 20;
                Player.velocity.X = -hitDirection * 4f;
                Player.velocity.Y = -3f;

                ConsumeShield();

                DashTimer = 0;
                break;
            }
        }
    }

    private void ConsumeShield()
    {
        for (int i = 0; i < Player.inventory.Length; i++)
        {
            if (Player.inventory[i].type == ModContent.ItemType<WoodenBuckler>())
            {
                Player.inventory[i].stack--;
                BreakShieldVFX();
                if (Player.inventory[i].stack <= 0)
                {
                    Player.inventory[i].TurnToAir();
                }
                break;
            }
        }
    }

    private void BreakShieldVFX()
    {
        Vector2 offset = Main.rand.NextVector2Circular(7f, 14f);

        for (int i = 0; i < 8; i++)
        {
            ParticleSystem.Spawn<BucklerParticle>(
                Player.Center + offset,
                new Vector2(-DashVelocity * Player.direction, -DashVelocity) * 0.4f + Main.rand.NextVector2Circular(0.7f, 0.7f),
                timeLeft: Main.rand.Next(22, 40),
                scale: Main.rand.NextFloat(0.5f, 1.05f),
                rotation: Main.rand.NextFloat(MathHelper.TwoPi),
                rotationVelocity: Main.rand.NextFloat(-0.05f, 0.05f)
            );
        }
    }
}

public class BucklerParticle : Particle
{
    public override string[] Textures => ["Images/Item_9", "Images/Item_11"]; // placeholder lol

    public override Vector2 Size => new(10f);

    public override float Gravity => Drift == GoreDrift.Tumble ? 0.2f : 0f;

    public override bool TileCollide => Drift == GoreDrift.Tumble;

    public override float Drag => Drift == GoreDrift.Tumble ? 0.995f : 0.98f;

    public override float Bounce => 0.34f;

    public override int FadeOutTicks => 30;

    public GoreDrift Drift { get; set; } = GoreDrift.Tumble;

    public override bool UseLighting => true;

}