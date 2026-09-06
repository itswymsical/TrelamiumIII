using Terraria.Audio;
using Terraria.DataStructures;
using TrelamiumIII.Core.Graphics.Interfaces;
using TrelamiumIII.Core.Graphics.Primitives;
using TrelamiumIII.Utilities;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Core.Projectiles;

/// <summary>
///     Base for a melee weapon that exists as a projectile
/// </summary>
/// <remarks>
///     <para>
///         What a subclass supplies is a <see cref="SwordProfile" /> and, usually, nothing else.
///         Everything between, being the aim, three phases, combo, etc. is here and identical across weapons
///     </para>
///     <para>
///         <b>Phases, and which clients run them.</b> Windup and swing have fixed durations, so
///         every client advances them on its own and the animation is smooth everywhere. The two
///         transitions that depend on a button -- whether a finished strike continues the combo, and
///         whether a lapsed combo window puts the weapon away -- run owner-side and announce
///         themselves. A remote that reaches the end of a recover simply holds at guard, which is
///         the pose it would be in anyway, until it is told what happened.
///     </para>
/// </remarks>
public abstract class HeldSwordProjectile : ModProjectile, IDrawPrimitive
{
    /// <summary>Where one strike is in its life.</summary>
    public enum SwingPhase
    {
        /// <summary>Carrying the blade to the far side of the arc. The last chance to aim.</summary>
        Windup,

        /// <summary>The sweep. The only phase that damages anything, and the aim is locked.</summary>
        Swing,

        /// <summary>Returning to guard. The gap the next strike opens from.</summary>
        Recover,

        /// <summary>Parked at guard, waiting to see whether the combo continues.</summary>
        Link,
    }

    /// <summary>Cosine of the aim drift worth spending a packet on.</summary>
    /// <inheritdoc cref="HeldGunProjectile" />
    private const float AIM_SYNC_TOLERANCE = 0.9995f;

    /// <summary>
    ///     How near vertical the aim may get before it is nudged back onto the side it was already on.
    /// </summary>
    /// <remarks>
    ///     The arc mirrors on the sign of the aim's X, so a cursor parked directly above the player
    ///     leaves which way the weapon swings to sub-pixel mouse jitter. Biasing the aim itself
    ///     rather than latching the facing separately is what keeps facing a pure function of the
    ///     synced aim, and therefore free of netcode.
    /// </remarks>
    private const float AIM_DEADZONE = 0.06f;

    /// <summary>Radius of the arc drawn in the twirl textures, in their own pixels.</summary>
    private const float TWIRL_RADIUS = 216f;

    /// <inheritdoc cref="TWIRL_RADIUS" />
    private const float TWIRL_CORE_RADIUS = 196f;

    /// <summary>How far inside the outer sweep the bright core sits.</summary>
    private const float TWIRL_CORE_INSET = 0.82f;

    /// <summary>Pixels of blade travel per swept-hitbox sample.</summary>
    /// <remarks>
    ///     Sampling a fixed number of times is what lets a fast weapon step over a target between
    ///     ticks. Deriving the count from how far the edge actually moved spends the samples where
    ///     the risk is, and leaves a slow weapon paying for two.
    /// </remarks>
    private const float SAMPLE_SPACING = 10f;

    /// <summary>How the weapon swings. See <see cref="SwordProfile" />.</summary>
    protected abstract SwordProfile Profile { get; }

    /// <summary>Whether releasing the button eventually puts the weapon away.</summary>
    /// <remarks>
    ///     False leaves it out until something else kills it, which is what a weapon that ends on
    ///     its own terms -- a finisher, a wind-down -- wants.
    /// </remarks>
    protected virtual bool HoldToKeep => true;

    /// <summary>Which part of the strike is running.</summary>
    public SwingPhase Phase
    {
        get => (SwingPhase)(int)Projectile.ai[0];
        private set => Projectile.ai[0] = (int)value;
    }

    /// <summary>Ticks spent in <see cref="Phase" />.</summary>
    public ref float PhaseTimer => ref Projectile.ai[1];

    /// <summary>Which step of the combo this strike is.</summary>
    /// <remarks>
    ///     In <c>ai[2]</c> rather than a field so it rides the standard projectile sync alongside the
    ///     phase and its clock. A remote without it would animate the right timing on the wrong arc.
    /// </remarks>
    public int ComboIndex
    {
        get => (int)Projectile.ai[2];
        private set => Projectile.ai[2] = value;
    }

    /// <summary>The step being swung.</summary>
    protected SwingStep Step => profile.StepAt(ComboIndex);

    /// <summary>Unit vector from the holder toward whatever they are aiming at.</summary>
    protected Vector2 Aim { get; private set; } = Vector2.UnitX;

    /// <summary>Rotation of <see cref="Aim" />.</summary>
    protected float AimAngle => Aim.ToRotation();

    protected Player Owner => Main.player[Projectile.owner];

    /// <summary>Melee size modifiers apply to reach, hitbox and sprite alike.</summary>
    protected float TotalScale => Projectile.scale * meleeScale;

    /// <summary>Pixels from the grip to the tip, this strike, at this size.</summary>
    protected float Reach => profile.BladeLength * Step.ReachScale * TotalScale;

    /// <summary>World rotation the blade points along right now.</summary>
    protected float BladeAngle => AngleOf(bladeOffset);

    /// <summary>Where the grip is. The blade pivots here, and so does everything drawn on it.</summary>
    protected Vector2 HandPosition => HandFor(bladeOffset);

    /// <summary>Where the point is. Worth having for anything spawned off the end of a swing.</summary>
    protected Vector2 BladeTip => HandPosition + DirectionOf(bladeOffset) * Reach;

    /// <summary>
    ///     How much of the arc the blade has actually covered, 0 to 1.
    /// </summary>
    /// <remarks>
    ///     Distance, not time. The two are very different under a back-loaded
    ///     <see cref="SwordProfile.SwingEase" />, where the first third of the clock is spent nearly
    ///     motionless -- anything keyed to the clock fires while the weapon is still hanging.
    /// </remarks>
    protected float SweptFraction
    {
        get
        {
            float span = SwingToOffset - SwingFromOffset;

            return Math.Abs(span) < 0.0001f
                ? 1f
                : MathHelper.Clamp((bladeOffset - SwingFromOffset) / span, 0f, 1f);
        }
    }

    /// <summary>Which way the weapon is pointed, and the axis the whole arc is mirrored about.</summary>
    private int facing = 1;

    /// <summary>Blade angle in facing space: radians off the aim, positive toward the ground.</summary>
    /// <remarks>
    ///     Everything animates in this space rather than in world angles, because the profile states
    ///     its arcs in it and because a facing-space value never has to be unwrapped -- a spin past
    ///     a half turn stays a number bigger than pi instead of folding back on itself and dragging
    ///     the arm the wrong way.
    /// </remarks>
    private float bladeOffset;

    /// <summary>Where the blade was last tick. The near edge of the swept hitbox.</summary>
    private float previousOffset;

    /// <summary>Where the arc VFX is anchored. Follows the blade, then stays put to burn out.</summary>
    private float arcOffset;

    /// <summary>Whether the pose taken this tick is part of a sweep. See <see cref="PreAI" />.</summary>
    private bool damaging;

    private float meleeScale = 1f;

    private float ribbonOpacity;

    private Vector2[]? ribbonPositions;

    private VertexTrail? ribbon;

    /// <summary>
    ///     <see cref="Profile" /> as of this tick, and what everything below actually reads.
    /// </summary>
    /// <remarks>
    ///     A profile is nearly always a preset with a <see langword="with" /> expression on it, which
    ///     means the property builds a fresh struct on every access. That is nothing on its own and a
    ///     great deal in aggregate: <see cref="Colliding" /> alone would ask for one per sample per
    ///     target per tick. Refreshed at the top of the tick so a weapon whose profile changes with
    ///     its state still gets the new one.
    /// </remarks>
    private SwordProfile profile;

    /// <summary>
    ///     Sensible defaults for a held melee weapon. Override and call base to adjust.
    /// </summary>
    /// <remarks>
    ///     The box is sized off the profile so that anything reading the projectile's own rectangle
    ///     gets something honest, but it is not what decides a hit --
    ///     <see cref="Colliding" /> is, and it tests the blade as a swept segment.
    /// </remarks>
    public override void SetDefaults()
    {
        profile = Profile;

        Projectile.width = Projectile.height = Math.Max(16, (int)(profile.BladeLength * 2f));

        Projectile.aiStyle = -1;
        Projectile.DamageType = DamageClass.Melee;

        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.netImportant = true;

        // Cleared at the top of every strike instead, so one enemy can be hit once per swing rather
        // than once per weapon -- which is what a cooldown of -1 would mean for something that stays
        // out across a whole combo.
        Projectile.usesLocalNPCImmunity = true;
        Projectile.localNPCHitCooldown = -1;

        Projectile.timeLeft = 2;
    }

    public override bool ShouldUpdatePosition() => false;

    public override void OnSpawn(IEntitySource source)
    {
        Player owner = Owner;

        facing = Math.Abs(Projectile.velocity.X) > 0.01f
            ? Math.Sign(Projectile.velocity.X)
            : owner.direction;

        Aim = Projectile.velocity.SafeNormalize(Vector2.UnitX * facing);
        Projectile.velocity = Aim;

        Phase = SwingPhase.Windup;
        PhaseTimer = 0f;
        ComboIndex = 0;

        bladeOffset = previousOffset = arcOffset = profile.Guard;
    }

    /// <summary>
    ///     Runs the whole weapon and suppresses the default projectile AI.
    /// </summary>
    /// <remarks>
    ///     Returning false is deliberate: the base AI would move the projectile by its velocity, and
    ///     velocity here is the aim, not a speed. The weapon is positioned by hand every tick.
    /// </remarks>
    public override bool PreAI()
    {
        Player owner = Owner;

        profile = Profile;

        if (!CanKeepHolding(owner))
        {
            Projectile.Kill();

            return false;
        }

        meleeScale = owner.GetAdjustedItemScale(owner.HeldItem);

        UpdateAim(owner);

        previousOffset = bladeOffset;
        bladeOffset = OffsetForPhase();

        // Latched here, before Advance may hand the phase on, because vanilla runs Damage() after
        // the AI and would otherwise ask a strike that has just finished whether it is still
        // swinging. It is not, by then -- and the answer would throw away the very last segment of
        // the arc, which under a back-loaded curve is most of it.
        damaging = Phase == SwingPhase.Swing;

        // Trails the blade through the sweep and is then left where the sweep ended, so the streak
        // burns out along the arc instead of being dragged back to guard with the weapon.
        if (damaging)
            arcOffset = bladeOffset;

        Projectile.Center = HandPosition;
        Projectile.rotation = BladeAngle;
        Projectile.direction = Projectile.spriteDirection = facing;
        Projectile.timeLeft = 2;

        PoseOwner(owner);
        DampOwner(owner);

        Advance(owner);

        WhileHeld(owner);

        return false;
    }

    /// <summary>
    ///     Whether the weapon has any business still existing.
    /// </summary>
    /// <remarks>
    ///     The held-item test is owner-side only. It is the authority on what its player is holding,
    ///     and the kill it decides on syncs like any other; a remote applying the same test against
    ///     a stale inventory would put the weapon away underneath its owner. It also assumes the
    ///     item's <c>shoot</c> is this projectile, which is the documented setup -- override for a
    ///     weapon that spawns its blade from somewhere else.
    /// </remarks>
    protected virtual bool CanKeepHolding(Player owner)
    {
        if (!owner.active || owner.dead || owner.noItems || owner.CCed)
            return false;

        return Main.myPlayer != Projectile.owner || owner.HeldItem.shoot == Projectile.type;
    }

    #region aim

    /// <summary>
    ///     Points the weapon, and tells the other clients when the answer has changed enough to matter.
    /// </summary>
    /// <remarks>
    ///     Skipped entirely during a sweep. Freezing the aim is what makes a swing a commitment
    ///     rather than a cursor-tracking cone, and it is also what lets facing be re-derived rather
    ///     than sent -- with the aim held still, no arc can change direction underneath itself.
    /// </remarks>
    private void UpdateAim(Player owner)
    {
        if (Phase != SwingPhase.Swing && Main.myPlayer == Projectile.owner)
        {
            Vector2 aim = (Main.MouseWorld - owner.Center).SafeNormalize(Vector2.UnitX * owner.direction);

            if (Math.Abs(aim.X) < AIM_DEADZONE)
                aim = new Vector2(AIM_DEADZONE * facing, aim.Y).SafeNormalize(Vector2.UnitX * facing);

            if (Vector2.Dot(aim, Projectile.velocity) < AIM_SYNC_TOLERANCE)
                Projectile.netUpdate = true;

            Projectile.velocity = aim;
        }

        Aim = Projectile.velocity.SafeNormalize(Vector2.UnitX * owner.direction);
        facing = Aim.X >= 0f ? 1 : -1;
    }

    #endregion

    #region state machine

    /// <summary>
    ///     Steps the strike along.
    /// </summary>
    /// <remarks>
    ///     Windup and swing end on a clock every client can read, so they advance everywhere and the
    ///     animation never waits on a packet. The transitions out of recover and link are answers to
    ///     a button, so only the owner may give them.
    /// </remarks>
    private void Advance(Player owner)
    {
        switch (Phase)
        {
            case SwingPhase.Windup:
                if (Elapsed)
                    BeginSwing(owner);
                else
                    PhaseTimer++;

                break;

            case SwingPhase.Swing:
                if (!Elapsed)
                {
                    PhaseTimer++;

                    break;
                }

                SetPhase(SwingPhase.Recover);
                OnSwingEnd(owner, Step);

                break;

            case SwingPhase.Recover:
                if (!Elapsed)
                {
                    PhaseTimer++;

                    break;
                }

                if (Main.myPlayer != Projectile.owner)
                    break;

                if (Swinging(owner))
                    NextStrike();
                else
                    SetPhase(SwingPhase.Link);

                break;

            case SwingPhase.Link:
                if (Main.myPlayer != Projectile.owner)
                {
                    PhaseTimer++;

                    break;
                }

                // Pressing again inside the window picks the combo up where it left off; letting it
                // lapse puts the weapon away, so the next press opens on the first step again.
                if (Swinging(owner))
                    NextStrike();
                else if (Elapsed)
                    Projectile.Kill();
                else
                    PhaseTimer++;

                break;
        }
    }

    /// <summary>Whether the holder is still asking for strikes.</summary>
    /// <remarks>
    ///     <see cref="Player.channel" /> covers the button being held through a channelled use;
    ///     <see cref="Player.controlUseItem" /> covers a fresh press inside the combo window, which
    ///     the item's own use may refuse to start while a weapon is already out. Owner-side only --
    ///     neither means anything about anybody else's mouse.
    /// </remarks>
    private bool Swinging(Player owner) => !HoldToKeep || owner.channel || owner.controlUseItem;

    /// <remarks>
    ///     Only the owner announces. Every client runs this for the two fixed-length transitions, and
    ///     a remote flagging a projectile it does not own queues a packet nobody asked it for.
    /// </remarks>
    private void SetPhase(SwingPhase phase)
    {
        Phase = phase;
        PhaseTimer = 0f;

        if (Main.myPlayer == Projectile.owner)
            Projectile.netUpdate = true;
    }

    /// <summary>
    ///     Opens the sweep: clears immunity, makes noise, and hands the step to the subclass.
    /// </summary>
    /// <remarks>
    ///     Immunity is cleared here rather than on spawn because the weapon outlives the strike. A
    ///     projectile that stays out for a whole combo and never resets would hit each enemy once
    ///     and then be decoration.
    /// </remarks>
    private void BeginSwing(Player owner)
    {
        SetPhase(SwingPhase.Swing);

        Projectile.ResetLocalNPCHitImmunity();

        if (profile.SwingSound is { } sound)
            SoundEngine.PlaySound(sound, owner.Center);

        OnSwingStart(owner, Step);
    }

    /// <summary>Wrapped at assignment so the synced value stays small and exact.</summary>
    private void NextStrike()
    {
        ComboIndex = (ComboIndex + 1) % profile.ComboLength;

        SetPhase(SwingPhase.Windup);
    }

    /// <summary>Ticks the current phase runs for, attack speed and step speed included.</summary>
    private int PhaseTicks => Phase switch
    {
        SwingPhase.Windup => Ticks(profile.WindupTicks),
        SwingPhase.Swing => Ticks(profile.SwingTicks),
        SwingPhase.Recover => Ticks(profile.RecoverTicks),

        // The combo window follows attack speed but not the step's own, so a quick step does not
        // also hand the player a tighter window to hit.
        _ => Math.Max(1, (int)(profile.ComboWindow * StatFactor)),
    };

    /// <summary>
    ///     Whether the current phase has taken its last pose and may hand over.
    /// </summary>
    /// <remarks>
    ///     Asked <i>after</i> the tick's pose has been computed, and one tick early on purpose. The
    ///     obvious ordering -- advance the clock, then test it -- ends a phase on the tick it reaches
    ///     its final position, which is before <see cref="Projectile.Damage" /> has run for that
    ///     tick. The end of the arc would be drawn and never tested. That is barely visible under an
    ///     even curve and severe under a back-loaded one, where the last tick of
    ///     <see cref="SwordProfile.Heavy" /> is most of the sweep and would land on nothing.
    /// </remarks>
    private bool Elapsed => PhaseTimer >= PhaseSpan;

    /// <summary>
    ///     Where the phase is, 0 to 1.
    /// </summary>
    /// <remarks>
    ///     Divided by the span rather than by the tick count so a phase reaches exactly one on its
    ///     last tick, and therefore runs for the number of ticks the profile asked for rather than
    ///     that plus one.
    /// </remarks>
    private float Progress => MathHelper.Clamp(PhaseTimer / PhaseSpan, 0f, 1f);

    /// <summary>Ticks between the first pose of a phase and its last.</summary>
    private float PhaseSpan => Math.Max(PhaseTicks - 1, 1);

    private float StatFactor
    {
        get
        {
            float speed = MathHelper.Clamp(Owner.GetTotalAttackSpeed(DamageClass.Melee), 0.25f, 4f);

            return MathHelper.Lerp(1f, 1f / speed, profile.SpeedInfluence);
        }
    }

    private int Ticks(int ticks)
        => Math.Max(1, (int)(ticks * StatFactor / Math.Max(Step.SpeedScale, 0.05f)));

    #endregion

    #region pose

    /// <summary>Radians off the aim the sweep begins at, anticipation included.</summary>
    /// <remarks>
    ///     The anticipation is subtracted against the sweep, so the windup overshoots the nominal
    ///     start and the swing has that extra ground to cover in the same number of frames.
    /// </remarks>
    private float SwingFromOffset
    {
        get
        {
            float sweep = profile.Arc * Step.Arc;

            return profile.ArcOffset + Step.Offset - sweep * 0.5f - profile.Anticipation * MathF.Sign(sweep);
        }
    }

    /// <summary>Radians off the aim the sweep ends at.</summary>
    private float SwingToOffset => profile.ArcOffset + Step.Offset + profile.Arc * Step.Arc * 0.5f;

    /// <summary>World rotation for a facing-space offset.</summary>
    private float AngleOf(float offset) => AimAngle + offset * facing;

    /// <summary>World direction for a facing-space offset.</summary>
    private Vector2 DirectionOf(float offset) => AngleOf(offset).ToRotationVector2();

    /// <summary>
    ///     Composite arm rotation for a blade offset, damped by
    ///     <see cref="SwordProfile.ArmFollow" />.
    /// </summary>
    /// <remarks>
    ///     Two corrections, and both are easy to get wrong. Arm rotation is measured from straight
    ///     down, so a direction maps onto it by subtracting a quarter turn. And vanilla applies
    ///     <see cref="Player.gravDir" /> to composite arms itself, so the angle handed over has to
    ///     be the pre-mirror one -- doing it here as well cancels out and the arm points at the
    ///     floor under reversed gravity.
    /// </remarks>
    private float ArmRotationFor(float offset)
    {
        Vector2 direction = AngleOf(offset * profile.ArmTracking).ToRotationVector2();

        if (Owner.gravDir < 0f)
            direction.Y = -direction.Y;

        return direction.ToRotation() - MathHelper.PiOver2;
    }

    /// <summary>
    ///     Where the grip sits for a blade offset.
    /// </summary>
    /// <remarks>
    ///     Taken from the arm rather than from the blade, which is the whole reason the two are
    ///     allowed to disagree: the hand goes wherever the shoulder can reach, and the blade pivots
    ///     about it at whatever angle the arc asks for.
    /// </remarks>
    private Vector2 HandFor(float offset)
        => Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, ArmRotationFor(offset))
           + new Vector2(0f, Owner.gfxOffY);

    private float OffsetForPhase() => Phase switch
    {
        SwingPhase.Windup => MathHelper.Lerp(profile.Guard, SwingFromOffset, Eased(profile.WindupEase)),
        SwingPhase.Swing => MathHelper.Lerp(SwingFromOffset, SwingToOffset, Eased(Step.Ease ?? profile.SwingEase)),
        SwingPhase.Recover => MathHelper.Lerp(SwingToOffset, profile.Guard, Eased(profile.RecoverEase)),

        _ => profile.Guard,
    };

    private float Eased(EaseFunction ease) => (ease ?? Linear).Ease(Progress);

    /// <summary>
    ///     Pins the weapon to the holder: turns them, keeps the item animation alive, and drives the
    ///     front arm and torso off the same clock the blade is on.
    /// </summary>
    /// <remarks>
    ///     <c>itemTime</c> and <c>itemAnimation</c> are refreshed every tick rather than set once.
    ///     They are what keeps the player in a "using an item" state; let them run out and the arm
    ///     drops to its idle pose for a frame in the middle of a swing. Override for a weapon that
    ///     wants the back arm as well -- a two-handed grip, a shield.
    /// </remarks>
    protected virtual void PoseOwner(Player owner)
    {
        owner.ChangeDir(facing);
        owner.heldProj = Projectile.whoAmI;
        owner.itemTime = owner.itemAnimation = 2;

        owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmRotationFor(bladeOffset));

        owner.bodyFrame.Y = owner.bodyFrame.Height * BodyFrame();
    }

    /// <summary>
    ///     Torso frames: raised through a downward sweep, lowered through an upward one.
    /// </summary>
    /// <remarks>
    ///     The front arm is composite and overrides whatever the frame draws, so this is only the
    ///     lean -- but without it a wide arc is an arm moving in front of a body standing perfectly
    ///     still.
    /// </remarks>
    private int BodyFrame()
    {
        const int UP = 2;
        const int NEUTRAL = 3;
        const int DOWN = 4;

        if (Phase == SwingPhase.Windup)
            return Step.Downward ? UP : DOWN;

        if (Phase != SwingPhase.Swing)
            return NEUTRAL;

        int third = Math.Clamp((int)(Progress * 3f), 0, 2);

        return Step.Downward ? UP + third : DOWN - third;
    }

    /// <summary>
    ///     Damps horizontal movement through a strike.
    /// </summary>
    /// <remarks>
    ///     Applied to velocity rather than to <see cref="Player.moveSpeed" /> because projectiles
    ///     update after players, so the stat is already spent by the time this runs.
    /// </remarks>
    private void DampOwner(Player owner)
    {
        if (Phase is SwingPhase.Windup or SwingPhase.Swing)
            owner.velocity.X *= profile.MoveDamping;
    }

    #endregion

    #region damage

    public override bool? CanDamage() => damaging ? null : false;

    /// <summary>
    ///     The real hitbox: the edge of the blade as a thick segment, swept across the gap between
    ///     last tick's angle and this one so a fast strike cannot skip past a target.
    /// </summary>
    /// <remarks>
    ///     <see cref="SwordProfile.HiltFraction" /> is where it starts -- the grip is not a weapon,
    ///     and on something with a long handle that gap is most of the reason it has to be aimed.
    /// </remarks>
    public override bool? Colliding(Rectangle projHitbox, Rectangle targetHitbox)
    {
        if (!damaging)
            return false;

        float reach = Reach;
        float thickness = profile.Thickness * TotalScale;
        float travel = Math.Abs(bladeOffset - previousOffset) * reach;

        int samples = Math.Clamp((int)(travel / SAMPLE_SPACING) + 2, 2, 16);

        for (int i = 0; i < samples; i++)
        {
            float offset = MathHelper.Lerp(previousOffset, bladeOffset, i / (float)(samples - 1));

            Vector2 hand = HandFor(offset);
            Vector2 direction = DirectionOf(offset);

            Vector2 start = hand + direction * reach * profile.HiltFraction;
            Vector2 end = hand + direction * reach;

            float _ = 0f;

            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, thickness, ref _))
                return true;
        }

        return false;
    }

    /// <remarks>
    ///     Damage is scaled through <see cref="NPC.HitModifiers.SourceDamage" /> so a step's
    ///     multiplier composes with crits and class bonuses instead of replacing them. The hit
    ///     direction is taken from facing rather than from the sweep, because a downward arc's own
    ///     direction would fling everything it touches at the floor.
    /// </remarks>
    public override void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        SwingStep step = Step;

        modifiers.SourceDamage *= step.DamageScale;
        modifiers.Knockback *= step.KnockbackScale;
        modifiers.HitDirectionOverride = facing;
    }

    #endregion

    #region draw

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;

        bool flipX = facing < 0;
        bool flipY = Owner.gravDir < 0f;

        if (profile.ArcStyle == SwingArcStyle.Twirl)
            DrawArc(ArcStrength, flipX, flipY);

        // Pivot on the grip corner, mirrored into whichever corner the grip actually landed in.
        var origin = new Vector2(texture.Width * profile.Origin.X, texture.Height * profile.Origin.Y);

        if (flipX)
            origin.X = texture.Width - origin.X;

        if (flipY)
            origin.Y = texture.Height - origin.Y;

        // A horizontal flip maps a texture angle to pi minus itself and a vertical one negates it,
        // so the sprite's own rest angle has to follow the mirroring or the blade detaches from the
        // arc it is supposed to be cutting.
        float correction = flipX ? MathHelper.Pi - profile.SpriteAngle : profile.SpriteAngle;

        if (flipY)
            correction = -correction;

        Vector2 hand = HandPosition;

        DrawBehindBlade(Main.spriteBatch, hand, lightColor);

        Main.EntitySpriteDraw(
            texture,
            hand - Main.screenPosition,
            null,
            Projectile.GetAlpha(lightColor),
            BladeAngle + correction,
            origin,
            TotalScale,
            Mirroring(flipX, flipY),
            0
        );

        DrawOverBlade(Main.spriteBatch, hand, lightColor);

        return false;
    }

    /// <summary>
    ///     Strength of the swing streak, 0 to 1.
    /// </summary>
    /// <remarks>
    ///     Driven by how far the edge has swept rather than by the clock, for the reason
    ///     <see cref="SweptFraction" /> gives: a time-based fade has the streak burning at full
    ///     strength while a back-loaded swing is still hanging at the top of its arc having gone
    ///     nowhere. Carried through the recover so it burns out behind the strike instead of
    ///     blinking off on the frame the blade turns around.
    /// </remarks>
    protected float ArcStrength
    {
        get
        {
            float master = profile.ArcOpacity;

            if (master <= 0f)
                return 0f;

            return Phase switch
            {
                SwingPhase.Swing => master * EaseQuadOut.Ease(
                    MathHelper.Clamp(SweptFraction / Math.Max(profile.ArcFadeIn, 0.01f), 0f, 1f)),

                SwingPhase.Recover => master * (1f - Progress),

                _ => 0f,
            };
        }
    }

    /// <summary>
    ///     The swing streak, as two passes of an arc texture: a wide warm sweep for the shape of the
    ///     swing and a tighter brighter one inside it for the edge.
    /// </summary>
    /// <remarks>
    ///     Anchored by its leading tip rather than pinned at a fixed angle, so the arc trails the
    ///     blade instead of hanging in the world waiting to be caught up with. Mirroring costs a
    ///     little bookkeeping: a horizontal flip maps a texture angle to <c>Pi - angle</c> and a
    ///     vertical one negates it, and the tip has to follow through both or the streak detaches
    ///     and sweeps the wrong way. Additive and linearly sampled -- these are soft 512px
    ///     gradients, and point sampling bands them into rings.
    /// </remarks>
    protected virtual void DrawArc(float strength, bool flipX, bool flipY)
    {
        if (strength <= 0f)
            return;


        float tip = profile.TwirlTip;

        if (flipX)
            tip = MathHelper.Pi - tip;

        if (flipY)
            tip = -tip;

        float rotation = AngleOf(arcOffset) - tip;
        Vector2 centre = HandFor(arcOffset) - Main.screenPosition;
        float reach = Reach * profile.ArcReach;

        Texture2D wide = Assets.Textures.VFX.SwordSwings.Twirl_01.Asset.Value;
        Texture2D core = Assets.Textures.VFX.SwordSwings.twirl_03.Asset.Value;

        SpriteEffects effects = Mirroring(flipX, flipY);

        Main.spriteBatch.End();
        Main.spriteBatch.Begin(
            SpriteSortMode.Deferred,
            BlendState.Additive,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer,
            null,
            Main.GameViewMatrix.TransformationMatrix
        );

        Main.spriteBatch.Draw(wide, centre, null, profile.ArcColor * (strength * 0.75f), rotation, wide.Size() * 0.5f, reach / TWIRL_RADIUS, effects, 0f);
        Main.spriteBatch.Draw(core, centre, null, profile.ArcCoreColor * strength, rotation, core.Size() * 0.5f, reach * TWIRL_CORE_INSET / TWIRL_CORE_RADIUS, effects, 0f);

        Main.spriteBatch.RestartToDefault();
    }

    private static SpriteEffects Mirroring(bool flipX, bool flipY)
    {
        SpriteEffects effects = SpriteEffects.None;

        if (flipX)
            effects |= SpriteEffects.FlipHorizontally;

        if (flipY)
            effects |= SpriteEffects.FlipVertically;

        return effects;
    }

    #endregion

    #region ribbon

    /// <summary>
    ///     The vertex-ribbon arc, for profiles that ask for one.
    /// </summary>
    /// <remarks>
    ///     Runs outside the sprite batch, from <see cref="PrimitiveRenderer" />. Silent for every
    ///     other <see cref="SwingArcStyle" />, so a weapon pays for this only by choosing it.
    /// </remarks>
    public void DrawPrimitives()
    {
        if (profile.ArcStyle != SwingArcStyle.Ribbon)
            return;

        ribbonOpacity = ArcStrength;

        if (ribbonOpacity <= 0f)
            return;

        Effect effect = ModContent.Request<Effect>(profile.RibbonShaderKey).Value;

        if (effect is null)
            return;

        BuildRibbon();
        ApplyRibbonParameters(effect);

        ribbon?.Render(effect);
    }

    /// <summary>
    ///     Lays the ribbon's spine along the arc the blade has swept.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Generated from angles rather than from a history of tip positions, which is what makes
    ///         it exact at any speed. A position cache samples once per tick, so a six-tick dagger
    ///         swing would have six points to describe a curve with -- and those six would be spaced
    ///         by however far the blade happened to move, which under a back-loaded ease means five
    ///         of them bunched at the start and one at the end.
    ///     </para>
    ///     <para>
    ///         Every point uses the current hand, so the ribbon is a true arc about the grip rather
    ///         than a smear that stretches when the player walks. The tail is clamped to the start of
    ///         the sweep so the streak grows out of the swing instead of being born at full length.
    ///     </para>
    /// </remarks>
    private void BuildRibbon()
    {
        int points = profile.RibbonPointCount;

        ribbonPositions ??= new Vector2[points];
        ribbon ??= new VertexTrail(points, RibbonWidthAt, RibbonColorAt);

        float from = SwingFromOffset;
        float head = arcOffset;
        float tail = head - MathF.Sign(SwingToOffset - from) * profile.RibbonSweep;

        tail = SwingToOffset > from ? Math.Max(tail, from) : Math.Min(tail, from);

        Vector2 hand = HandPosition;
        float reach = Reach * profile.ArcReach;

        for (int i = 0; i < points; i++)
            ribbonPositions[i] = hand + DirectionOf(MathHelper.Lerp(tail, head, i / (float)(points - 1))) * reach;

        ribbon.Positions = ribbonPositions;
    }

    /// <summary>
    ///     Feeds the arc shader.
    /// </summary>
    /// <remarks>
    ///     Both parameter sets are offered and every set is null-guarded, so the same code drives the
    ///     stock primitive shader -- which takes its transform as <c>uWorldViewProjection</c> and has
    ///     it filled in by <see cref="VertexTrail.Render" /> -- and <c>pixelTrail</c>, which wants
    ///     the matrix and its pixelation numbers by hand. A shader simply ignores what it does not
    ///     declare, which is what makes <see cref="SwordProfile.RibbonShader" /> a one-field swap.
    ///     The clock is wrapped and slowed on purpose: the stock shader scrolls its sampler by
    ///     <c>time</c> against a clamped address mode, and an unbounded one parks the whole ribbon on
    ///     the last column of the texture.
    /// </remarks>
    private void ApplyRibbonParameters(Effect effect)
    {

        Matrix world = Matrix.CreateTranslation(-Main.screenPosition.ToVector3());
        Matrix view = Main.GameViewMatrix.TransformationMatrix;
        Matrix projection = Matrix.CreateOrthographicOffCenter(0, Main.screenWidth, Main.screenHeight, 0, -1, 1);

        effect.Parameters["time"]?.SetValue(Main.GameUpdateCount % 300 / 300f * 0.2f);
        effect.Parameters["noiseSpeed"]?.SetValue(0.35f);
        effect.Parameters["sampleTexture"]?.SetValue(ModContent.Request<Texture2D>(profile.RibbonTextureKey).Value);
        effect.Parameters["noiseTexture"]?.SetValue(ModContent.Request<Texture2D>(profile.RibbonNoiseKey).Value);

        effect.Parameters["repeats"]?.SetValue(1f);
        effect.Parameters["pixelation"]?.SetValue(2f);
        effect.Parameters["resolution"]?.SetValue(new Vector2(Main.screenWidth, Main.screenHeight));
        effect.Parameters["transformMatrix"]?.SetValue(world * view * projection);
    }

    /// <summary>Widest at the blade and tapering to nothing at the tail. See <see cref="SwordProfile.RibbonTaper" />.</summary>
    private float RibbonWidthAt(float factor)
        => profile.RibbonWidth * TotalScale * MathF.Pow(factor, profile.RibbonTaper);

    /// <inheritdoc cref="RibbonWidthAt" />
    private Color RibbonColorAt(float factor)
        => profile.ArcColor * (ribbonOpacity * MathF.Pow(factor, 2f));

    #endregion

    #region hooks

    /// <summary>
    ///     A sweep has just opened. Where the whoosh, the dust, and any projectile thrown off the
    ///     blade belong.
    /// </summary>
    /// <remarks>
    ///     Runs on every client, because the swing itself does. Guard anything that must happen once
    ///     -- spawning a projectile wants <c>Main.myPlayer == Projectile.owner</c>.
    /// </remarks>
    protected virtual void OnSwingStart(Player owner, SwingStep step) { }

    /// <summary>The sweep has finished, hit or miss. The natural place to punctuate a finisher.</summary>
    /// <inheritdoc cref="OnSwingStart" />
    protected virtual void OnSwingEnd(Player owner, SwingStep step) { }

    /// <summary>Per-tick hook for anything the weapon does while it is out: glow, hum, drip.</summary>
    /// <inheritdoc cref="OnSwingStart" />
    protected virtual void WhileHeld(Player owner) { }

    /// <summary>Drawn under the blade, in the batch already running.</summary>
    /// <param name="hand">World position of the grip, which is what the blade pivots about.</param>
    protected virtual void DrawBehindBlade(SpriteBatch spriteBatch, Vector2 hand, Color lightColor) { }

    /// <inheritdoc cref="DrawBehindBlade" />
    protected virtual void DrawOverBlade(SpriteBatch spriteBatch, Vector2 hand, Color lightColor) { }

    #endregion
}
