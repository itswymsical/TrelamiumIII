using Terraria.GameContent;

namespace TrelamiumIII.Core.Projectiles;

public abstract class StickyProjectile : ModProjectile
{
    protected NPC Target => Main.npc[(int)Projectile.ai[1]];

    protected bool stickToNPC = true;
    protected bool stickingToNPC;

    protected Vector2 offset;
    protected float oldRotation;

    protected virtual float Gravity => 0.125f;
    protected bool arcCompleted;

    protected bool fallOffStarted;
    protected float spinRate;
    protected virtual float FallOffGravity => 0.35f;
    protected virtual int FallOffDelay => 45; // frames after spawn before it starts tumbling

    protected bool deathAnimationStarted;
    protected int deathAnimationTimer;
    protected virtual int DeathAnimationDuration => 55;
    protected Vector2 deathStartPosition;
    protected Vector2 deathOffset;
    protected float deathRotation;
    protected float deathAlpha = 1f;

    protected virtual bool DeathAnimationOnTileHit => true;

    protected virtual int MaxStickies => 3;

    public override void AI()
    {
        if (!deathAnimationStarted && stickingToNPC && Projectile.timeLeft <= 60)
            BeginDeathAnimation();

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
        else if (!stickingToNPC && !arcCompleted)
        {
            if (!fallOffStarted && Projectile.timeLeft <= Projectile.timeLeft - FallOffDelay + Projectile.timeLeft)
            {
                // see note below re: this condition
            }

            TickFlight();
        }


        if (stickingToNPC && !deathAnimationStarted)
            Projectile.rotation = oldRotation;

        Projectile.spriteDirection = Projectile.direction;

        PostAI();
    }

    private void TickFlight()
    {
        if (fallOffStarted)
        {
            Projectile.velocity.Y += FallOffGravity;

            spinRate += 0.003f;
            spinRate = Math.Min(spinRate, 0.3f);

            float actualSpinRate = Projectile.velocity.X < 0 ? -spinRate : spinRate;
            Projectile.rotation += actualSpinRate;

            Projectile.velocity.X *= 0.985f;
        }
        else
        {
            Projectile.velocity.Y += Gravity;
            Projectile.rotation = Projectile.velocity.ToRotation();

            if (Projectile.velocity.X < 0)
                Projectile.rotation += MathHelper.Pi;

            Projectile.velocity.X *= 0.995f;
        }
    }

    protected void BeginDeathAnimation()
    {
        deathAnimationStarted = true;
        deathAnimationTimer = 0;
        deathStartPosition = Projectile.Center;
        deathOffset = new Vector2(Main.rand.NextFloat(-20f, 20f), 0f);
        deathRotation = Projectile.rotation;
    }

    protected virtual void PostAI() { }

    public override bool? CanHitNPC(NPC target)
    {
        return !stickingToNPC && !target.friendly;
    }

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        if (!stickingToNPC && stickToNPC)
        {
            Projectile.ai[1] = target.whoAmI;
            oldRotation = Projectile.velocity.ToRotation();

            offset = target.Center - Projectile.Center + Projectile.velocity * 0.75f;
            Projectile.velocity = Vector2.Zero;
            arcCompleted = true;
            stickingToNPC = true;
            Projectile.netUpdate = true;

            OnStuck(target, ref modifiers);
        }
        else
        {
            RemoveStackProjectiles();
        }
    }

    /// <summary>
    /// Called once, the moment this projectile sticks into a target.
    /// Override for on-stick effects (damage multipliers, debuffs, sounds, etc).
    /// </summary>
    protected virtual void OnStuck(NPC target, ref NPC.HitModifiers modifiers) { }

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

        if (!DeathAnimationOnTileHit)
        {
            Projectile.velocity = Vector2.Zero;
            arcCompleted = true;
            return false;
        }

        if (!deathAnimationStarted)
        {
            BeginDeathAnimation();
            Projectile.velocity = Vector2.Zero;
            Projectile.friendly = false;
            Projectile.timeLeft = Math.Min(Projectile.timeLeft, 30);
        }

        return false;
    }

    public override bool PreDraw(Player player, ref Color lightColor)
    {
        var texture = TextureAssets.Projectile[Type].Value;
        Vector2 drawPosition;
        float drawRotation;
        Color drawColor;

        var spriteEffects = SpriteEffects.None;
        var origin = GetDrawOrigin(texture);

        if (Projectile.spriteDirection == -1)
        {
            spriteEffects = SpriteEffects.FlipHorizontally;
            origin.X = texture.Width - origin.X;
        }

        if (deathAnimationStarted)
        {
            deathAnimationTimer++;
            float progress = (float)deathAnimationTimer / DeathAnimationDuration;

            var fallOffset = new Vector2(
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

        Main.EntitySpriteDraw(texture, drawPosition, null, drawColor, drawRotation, origin, Projectile.scale, spriteEffects, 0);

        return false;
    }

    /// <summary>
    /// Override if the projectile's texture needs a non-center draw origin.
    /// </summary>
    protected virtual Vector2 GetDrawOrigin(Texture2D texture) => new(12);
}