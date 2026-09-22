using Terraria.DataStructures;

namespace TrelamiumIII.Core.Projectiles;

/// <summary>
///     Base for a modded melee weapon held as a projectile through a combo. A subclass supplies a <see cref="SwordProfile" />.
/// </summary>
/// <remarks>The logic lives in <see cref="HeldSword" />; this class forwards the ModProjectile hooks to it.</remarks>
public abstract class HeldSwordProjectile : ModProjectile
{
    /// <remarks>Created on first use, never in SetDefaults, so the cloned ModProjectile template cannot share one.</remarks>
    private Driver? sword;

    private Driver Sword => sword ??= new Driver(this);

    protected abstract SwordProfile Profile { get; }

    /// <inheritdoc cref="HeldSword.HoldToKeep" />
    protected virtual bool HoldToKeep => true;

    public HeldSword.SwingPhase Phase => Sword.Phase;

    /// <inheritdoc cref="HeldSword.PhaseTimer" />
    public ref float PhaseTimer => ref Sword.PhaseTimer;

    /// <inheritdoc cref="HeldSword.ComboIndex" />
    public int ComboIndex => Sword.ComboIndex;

    protected SwingStep Step => Sword.Step;

    /// <inheritdoc cref="HeldSword.Aim" />
    protected Vector2 Aim => Sword.Aim;

    protected float AimAngle => Sword.AimAngle;

    protected Player Owner => Sword.Owner;

    /// <inheritdoc cref="HeldSword.TotalScale" />
    protected float TotalScale => Sword.TotalScale;

    /// <inheritdoc cref="HeldSword.Reach" />
    protected float Reach => Sword.Reach;

    /// <inheritdoc cref="HeldSword.BladeAngle" />
    protected float BladeAngle => Sword.BladeAngle;

    /// <inheritdoc cref="HeldSword.HandPosition" />
    protected Vector2 HandPosition => Sword.HandPosition;

    protected Vector2 BladeTip => Sword.BladeTip;

    /// <inheritdoc cref="HeldSword.SweptFraction" />
    protected float SweptFraction => Sword.SweptFraction;

    public override void SetDefaults()
    {
        HeldSword.ApplyDefaults(Projectile, Profile);

        Projectile.aiStyle = -1;
        Projectile.DamageType = DamageClass.Melee;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void OnSpawn(IEntitySource source) => Sword.OnSpawn();

    public override bool PreAI()
    {
        Sword.Update();

        return false;
    }

    /// <inheritdoc cref="HeldSword.CanKeepHolding" />
    protected virtual bool CanKeepHolding(Player owner) => Sword.BaseCanKeepHolding(owner);

    /// <inheritdoc cref="HeldSword.PoseOwner" />
    protected virtual void PoseOwner(Player owner) => Sword.BasePoseOwner(owner);

    public override bool? CanDamage() => Sword.CanDamage();

    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox) => Sword.Colliding(targetHitbox);

    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers) => Sword.ModifyHitNPC(target, ref modifiers);

    public override bool PreDraw(Player player, ref Color lightColor)
    {
        Sword.Draw(lightColor);

        return false;
    }

    #region hooks

    /// <inheritdoc cref="HeldSword.OnSwingStart" />
    protected virtual void OnSwingStart(Player owner, SwingStep step) { }

    /// <inheritdoc cref="HeldSword.OnSwingEnd" />
    protected virtual void OnSwingEnd(Player owner, SwingStep step) { }

    /// <inheritdoc cref="HeldSword.WhileHeld" />
    protected virtual void WhileHeld(Player owner) { }

    /// <inheritdoc cref="HeldSword.DrawBehindBlade" />
    protected virtual void DrawBehindBlade(SpriteBatch spriteBatch, Vector2 hand, Color lightColor) { }

    /// <inheritdoc cref="HeldSword.DrawOverBlade" />
    protected virtual void DrawOverBlade(SpriteBatch spriteBatch, Vector2 hand, Color lightColor) { }

    #endregion

    /// <summary>Routes the engine's virtuals to the ModProjectile's, and exposes the engine defaults for them to fall back on.</summary>
    private sealed class Driver(HeldSwordProjectile host) : HeldSword(host.Projectile)
    {
        protected override SwordProfile Profile => host.Profile;

        protected override Texture2D Texture => ModContent.Request<Texture2D>(host.Texture).Value;

        protected override bool HoldToKeep => host.HoldToKeep;

        public bool BaseCanKeepHolding(Player owner) => base.CanKeepHolding(owner);

        public void BasePoseOwner(Player owner) => base.PoseOwner(owner);

        protected override bool CanKeepHolding(Player owner) => host.CanKeepHolding(owner);

        protected override void PoseOwner(Player owner) => host.PoseOwner(owner);

        protected override void OnSwingStart(Player owner, SwingStep step) => host.OnSwingStart(owner, step);

        protected override void OnSwingEnd(Player owner, SwingStep step) => host.OnSwingEnd(owner, step);

        protected override void WhileHeld(Player owner) => host.WhileHeld(owner);

        protected override void DrawBehindBlade(SpriteBatch spriteBatch, Vector2 hand, Color lightColor)
            => host.DrawBehindBlade(spriteBatch, hand, lightColor);

        protected override void DrawOverBlade(SpriteBatch spriteBatch, Vector2 hand, Color lightColor)
            => host.DrawOverBlade(spriteBatch, hand, lightColor);
    }
}
