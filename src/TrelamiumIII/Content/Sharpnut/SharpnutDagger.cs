using Terraria.GameContent;

namespace TrelamiumIII.Content.Sharpnut;

public class SharpnutDaggerItem : ModItem
{
    public override void SetDefaults()
    {
        Item.damage = 8;
        Item.crit = 1; // %5
        Item.width = Item.height = 32;
        Item.useTime = Item.useAnimation = 20;

        Item.knockBack = 0.5f;
        Item.shoot = ModContent.ProjectileType<SharpnutDaggerProjectile>();
        Item.shootSpeed = 10f;
        Item.value = Item.sellPrice(copper: 85);
        Item.UseSound = SoundID.Item1;
        Item.DamageType = DamageClass.Ranged;
        Item.useStyle = ItemUseStyleID.Swing;
        Item.rare = ItemRarityID.White;

        Item.useTurn = 
            Item.noUseGraphic = 
            Item.noMelee = true;
    }
    public override void AddRecipes()
    {
        base.AddRecipes();
        CreateRecipe()
            .AddIngredient(ItemID.Acorn, 8)
            .AddRecipeGroup(RecipeGroups.Wood, 18)
            .AddTile(TileID.Anvils)
            .Register();
    }
}

public class SharpnutDaggerProjectile : ModProjectile
{
    public static int MaxStickies => 6;
    private bool thisDaggerChecked = false;
    protected NPC Target => Main.npc[(int)Projectile.ai[1]];

    protected bool stickToNPC;
    protected bool stickingToNPC;

    private Vector2 offset;
    private float oldRotation;

    private readonly float gravity = 0.125f;
    private bool arcCompleted = false;

    private bool fallOffStarted = false;
    private float spinRate = 0f;
    private readonly float fallOffGravity = 0.35f;

    private float prevVelocityY = 0f;

    private bool deathAnimationStarted = false;
    private int deathAnimationTimer = 0;
    private const int DEATH_ANIMATION_DURATION = 55;
    private Vector2 deathStartPosition;
    private Vector2 deathOffset;
    private float deathRotation;
    private float deathAlpha = 1f;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 3;
        ProjectileID.Sets.TrailingMode[Type] = 1;
    }

    public override void SetDefaults()
    {
        Projectile.DamageType = DamageClass.Ranged;
        Projectile.width = 18;
        Projectile.height = 18;
        Projectile.drawLayer = ProjectileDrawLayerID.BehindNPCs;
        stickToNPC =
        Projectile.friendly =
        Projectile.tileCollide =
        Projectile.usesLocalNPCImmunity = true;

        Projectile.timeLeft = 240;
        Projectile.penetrate = Projectile.aiStyle = -1;
        Projectile.light = 0.2f;
        prevVelocityY = Projectile.velocity.Y;
    }

    public override void AI()
    {

        if (!deathAnimationStarted && stickingToNPC && Projectile.timeLeft <= 60)
        {
            deathAnimationStarted = true;
            deathAnimationTimer = 0;
            deathStartPosition = Projectile.Center;
            deathOffset = new Vector2(Main.rand.NextFloat(-20f, 20f), 0f);
            deathRotation = Projectile.rotation;
        }

        if (stickingToNPC && !deathAnimationStarted)
        {
            if (Target.active && !Target.dontTakeDamage)
            {
                Projectile.tileCollide = false;
                Projectile.Center = Target.Center - offset;
                Projectile.gfxOffY = Target.gfxOffY;
            }
            else
            {
                Projectile.Kill();
            }
        }
        else if (!stickingToNPC)
        {
            if (!arcCompleted)
            {
                // Check if 0.75 seconds (45 frames) have passed since spawn
                if (!fallOffStarted && Projectile.timeLeft <= 260 - 45)
                {
                    fallOffStarted = true;
                    spinRate = 0.1f;
                }

                if (fallOffStarted)
                {
                    Projectile.velocity.Y += fallOffGravity;

                    spinRate += 0.003f;
                    spinRate = Math.Min(spinRate, 0.3f);

                    float actualSpinRate = Projectile.velocity.X < 0 ? -spinRate : spinRate;
                    Projectile.rotation += actualSpinRate;

                    Projectile.velocity.X *= 0.985f;
                }
                else
                {
                    Projectile.velocity.Y += gravity;

                    Projectile.rotation = Projectile.velocity.ToRotation();

                    if (Projectile.velocity.X < 0)
                    {
                        Projectile.rotation += MathHelper.Pi;
                    }

                    Projectile.velocity.X *= 0.995f;
                }

                prevVelocityY = Projectile.velocity.Y;
            }
        }

        if (stickingToNPC && !deathAnimationStarted)
            Projectile.rotation = oldRotation;

        var player = Main.player[Projectile.owner];
        var distanceToPlayer = Vector2.Distance(Projectile.Center, player.Center);

        Projectile.spriteDirection = Projectile.direction;
    }

    public override bool? CanHitNPC(NPC target)
    {
        return !stickingToNPC && !target.friendly;
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        if (!stickingToNPC && stickToNPC)
        {
            if (!thisDaggerChecked)
            {
                float damageMultiplier = CalculateDamageMultiplier(target);
                modifiers.FinalDamage *= damageMultiplier;
                thisDaggerChecked = true;
            }

            Projectile.ai[1] = target.whoAmI;

            oldRotation = Projectile.velocity.ToRotation();

            offset = target.Center - Projectile.Center + Projectile.velocity * 0.75f;
            Projectile.velocity = Vector2.Zero;
            arcCompleted = true;
            stickingToNPC = true;
            Projectile.netUpdate = true;
        }
        else
        {
            RemoveStackProjectiles();
        }
    }

    private float CalculateDamageMultiplier(NPC target)
    {
        int stuckDaggers = 0;
        const float dmgBoost = 0.1f;

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            var currentProjectile = Main.projectile[i];

            if (i != Projectile.whoAmI
                && currentProjectile.active
                && currentProjectile.type == Projectile.type
                && currentProjectile.ModProjectile is SharpnutDaggerProjectile otherDagger
                && otherDagger.stickingToNPC
                && otherDagger.Target.whoAmI == target.whoAmI)
            {
                stuckDaggers++;
            }
        }

        float multiplier = 1f + (stuckDaggers * dmgBoost);

        return Math.Min(multiplier, 2.5f);
    }

    protected void RemoveStackProjectiles()
    {
        var sticking = new Point[MaxStickies];
        var index = 0;

        for (var i = 0; i < Main.maxProjectiles; i++)
        {
            var currentProjectile = Main.projectile[i];

            if (i != Projectile.whoAmI
                && currentProjectile.active
                && currentProjectile.owner == Main.myPlayer
                && currentProjectile.type == Projectile.type
                && currentProjectile.ai[0] == 1f
                && currentProjectile.ai[1] == Target.whoAmI
            )
            {
                sticking[index++] = new Point(i, currentProjectile.timeLeft);

                if (index >= sticking.Length)
                    break;
            }
        }

        if (index >= sticking.Length)
        {
            var oldIndex = 0;

            for (var i = 1; i < sticking.Length; i++)
                if (sticking[i].Y < sticking[oldIndex].Y)
                    oldIndex = i;

            Main.projectile[sticking[oldIndex].X].Kill();
        }
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if (stickingToNPC)
        {
            Projectile.velocity = Vector2.Zero;
            arcCompleted = true;
            return false;
        }
        else
        {
            if (!deathAnimationStarted)
            {
                deathAnimationStarted = true;
                deathAnimationTimer = 0;
                deathStartPosition = Projectile.Center;
                deathOffset = new Vector2(Main.rand.NextFloat(-20f, 20f), 0f);
                deathRotation = Projectile.rotation;

                Projectile.velocity = Vector2.Zero;
                Projectile.friendly = false;
                Projectile.timeLeft = Math.Min(Projectile.timeLeft, 30);
            }

            return false;
        }
    }

    public override bool PreDraw(Player player, ref Color lightColor)
    {
        var texture = TextureAssets.Projectile[Type].Value;
        Vector2 drawPosition;
        float drawRotation;
        Color drawColor;

        var spriteEffects = SpriteEffects.None;
        var origin = new Vector2(22f, 9f);

        if (Projectile.spriteDirection == -1)
        {
            spriteEffects = SpriteEffects.FlipHorizontally;
            origin.X = texture.Width - origin.X;
        }

        if (deathAnimationStarted)
        {
            deathAnimationTimer++;

            float progress = (float)deathAnimationTimer / DEATH_ANIMATION_DURATION;

            Vector2 fallOffset = new Vector2(
                deathOffset.X * progress,
                deathOffset.Y + (progress * progress * 90f)
            );

            drawPosition = deathStartPosition + fallOffset - Main.screenPosition;
            drawRotation = deathRotation + (progress * MathHelper.TwoPi * 0.1f);

            deathAlpha = 1f - progress;
            drawColor = lightColor * deathAlpha;
        }
        else
        {
            drawPosition = Projectile.Center - Main.screenPosition;
            drawRotation = Projectile.rotation;
            drawColor = lightColor;
        }


        Main.EntitySpriteDraw(
            texture,
            drawPosition,
            null,
            drawColor,
            drawRotation,
            origin,
            Projectile.scale,
            spriteEffects,
            0
        );

        return false;
    }

    public override bool PreKill(int timeLeft)
    {
        return base.PreKill(timeLeft);
    }
}