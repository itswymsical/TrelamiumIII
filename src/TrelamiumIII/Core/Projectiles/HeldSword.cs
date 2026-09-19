using Terraria.Audio;
using Terraria.GameContent;
using TrelamiumIII.Core.Graphics;
using TrelamiumIII.Core.Graphics.Primitives;
using TrelamiumIII.Utilities;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Core.Projectiles;

/// <summary>Combo state machine, owner pose, swept hitbox and swing trail of a held melee projectile.</summary>
/// <remarks>
///     Independent of projectile type: <see cref="HeldSwordProjectile" /> hosts it for modded weapons, a GlobalProjectile for
///     vanilla ones. Phase, timer and combo index live in <c>ai[0..2]</c>. Continuing and ending the combo are owner-side
///     decisions; remote clients hold in <see cref="SwingPhase.Link" /> until the packet arrives.
/// </remarks>
public abstract class HeldSword(Projectile projectile)
{
    public enum SwingPhase
    {
        /// <summary>The blade moves from its entry pose to the start of the arc while the aim converges on the cursor.</summary>
        Windup,

        /// <summary>The damaging sweep. The aim is locked.</summary>
        Swing,

        /// <summary>A strike is queued: the blade moves from the end of the last arc to the start of the next.</summary>
        Recover,

        /// <summary>Nothing is queued: the blade holds at the end of the arc for <see cref="SwordProfile.ComboWindow" /> ticks.</summary>
        Link,
    }

    /// <summary>Cosine of the aim drift that triggers a net update.</summary>
    private const float AIM_SYNC_TOLERANCE = 0.9995f;

    /// <summary>Minimum |X| of the aim, pushed toward the current facing so a vertical cursor cannot flip it every tick.</summary>
    private const float AIM_DEADZONE = 0.06f;

    /// <summary>Per-tick lerp factor of the aim during <see cref="SwingPhase.Link" />.</summary>
    private const float LINK_AIM_TRACKING = 0.3f;

    /// <summary>Pixels of blade travel per swept-hitbox sample.</summary>
    private const float SAMPLE_SPACING = 10f;

    /// <summary>Trail arc in radians below which the trail's opacity scales down to zero.</summary>
    private const float TRAIL_FULL_ARC = 0.4f;

    /// <summary>Radians of arc per repeat of <see cref="SwordProfile.TrailTexture" />.</summary>
    private const float TRAIL_TEXTURE_ARC = MathHelper.Pi;

    /// <summary>Distance of the trail's rim from its outer edge, as a fraction of the trail's width.</summary>
    private const float TRAIL_EDGE_WIDTH = 0.12f;

    public Projectile Projectile { get; } = projectile;

    protected abstract SwordProfile Profile { get; }

    /// <summary>Blade sprite. Only read while drawing.</summary>
    protected abstract Texture2D Texture { get; }

    /// <summary>Whether strikes continue only while the button is used. False chains strikes until something else kills the weapon.</summary>
    protected virtual bool HoldToKeep => true;

    public SwingPhase Phase
    {
        get => (SwingPhase)(int)Projectile.ai[0];
        private set => Projectile.ai[0] = (int)value;
    }

    /// <summary>Ticks elapsed in <see cref="Phase" />.</summary>
    public ref float PhaseTimer => ref Projectile.ai[1];

    /// <summary>Index of the current or upcoming strike in <see cref="SwordProfile.Combo" />.</summary>
    public int ComboIndex
    {
        get => (int)Projectile.ai[2];
        private set => Projectile.ai[2] = value;
    }

    public SwingStep Step => profile.StepAt(ComboIndex);

    /// <summary>Unit vector of the displayed aim.</summary>
    public Vector2 Aim => aim.Angle.ToRotationVector2();

    public float AimAngle => aim.Angle;

    public Player Owner => Main.player[Projectile.owner];

    /// <summary>Projectile scale times the melee size modifier. Applies to reach, hitbox and sprite.</summary>
    public float TotalScale => Projectile.scale * meleeScale;

    /// <summary>Pixels from the grip to the tip for the current step.</summary>
    public float Reach => profile.BladeLength * Step.ReachScale * TotalScale;

    /// <summary>World rotation of the blade.</summary>
    public float BladeAngle => aim.AngleOf(bladeOffset);

    /// <summary>World position of the grip, which the blade pivots about.</summary>
    public Vector2 HandPosition => HandFor(bladeOffset, aim, extension);

    public Vector2 BladeTip => HandPosition + aim.DirectionOf(bladeOffset) * Reach;

    /// <summary>Fraction of the current arc the blade has covered, 0 to 1, measured by angle rather than time.</summary>
    public float SweptFraction
    {
        get
        {
            float span = SwingToOffset - SwingFromOffset;

            return Math.Abs(span) < 0.0001f
                ? 1f
                : MathHelper.Clamp((bladeOffset - SwingFromOffset) / span, 0f, 1f);
        }
    }

    /// <summary><see cref="Profile" /> as of this tick. Cached because presets build a new struct on every access.</summary>
    private SwordProfile profile;

    private bool initialized;

    private AimFrame aim = new(1, 0f);

    /// <summary>Blade angle in facing space: radians off the aim, positive toward the ground.</summary>
    private float bladeOffset;

    private float previousOffset;

    /// <summary>Pixels the grip is pushed along the blade. See <see cref="SwordProfile.Extend" />.</summary>
    private float extension;

    // Pose at the start of the current phase. Windup, recover and link interpolate from it.
    private float entryOffset;
    private float entryExtension;
    private float entryElevation;

    /// <summary>Whether this tick's pose is part of a sweep. Latched for <see cref="Colliding" />.</summary>
    private bool damaging;

    private float meleeScale = 1f;

    /// <summary>Held item's use-animation scale as of this tick. See <see cref="UseTimeScale" />.</summary>
    private float useTimeScale = 1f;

    // Owner-side input state.
    private bool useHeld;
    private bool strikeQueued;

    // Phase and step this client last acted on. A mismatch means a packet changed them.
    private SwingPhase observedPhase;
    private int observedCombo;

    /// <summary>Sweeps started on this client. Identifies the trace that belongs to the sweep in progress.</summary>
    private int sweepCount;

    private SwingTrace[]? traces;

    private VertexStrip? trailStrip;

    /// <summary>Sets the projectile fields the engine depends on. <c>aiStyle</c> and damage class are left to the host.</summary>
    /// <remarks>The box is sized from the profile but does not decide hits; <see cref="Colliding" /> does.</remarks>
    public static void ApplyDefaults(Projectile projectile, in SwordProfile profile)
    {
        projectile.width = projectile.height = Math.Max(16, (int)(profile.BladeLength * 2f));

        projectile.friendly = true;
        projectile.penetrate = -1;
        projectile.tileCollide = false;
        projectile.ignoreWater = true;
        projectile.netImportant = true;

        // Drawn only by the owner's held-projectile layer; otherwise Main.DrawProjectiles draws it a second time.
        projectile.hide = true;

        // Cleared at the start of every sweep, so each strike can hit a target once.
        projectile.usesLocalNPCImmunity = true;
        projectile.localNPCHitCooldown = -1;

        // Phase lengths are in ticks; extra updates would advance them faster.
        projectile.extraUpdates = 0;

        projectile.timeLeft = 2;
    }

    /// <summary>Starts the combo. Call from the host's OnSpawn.</summary>
    public void OnSpawn()
    {
        profile = Profile;

        Phase = SwingPhase.Windup;
        PhaseTimer = 0f;
        ComboIndex = 0;

        Initialize(Owner);

        // The press that spawned the weapon must not also queue the second strike.
        useHeld = true;
    }

    /// <summary>Advances the weapon one tick. Returns false if it was killed.</summary>
    /// <remarks>The host must stop vanilla from moving the projectile by its velocity, which carries the aim.</remarks>
    public bool Update()
    {
        Player owner = Owner;

        profile = Profile;

        if (!CanKeepHolding(owner))
        {
            Projectile.Kill();

            return false;
        }

        if (!initialized)
            Initialize(owner);

        Item held = owner.HeldItem;

        meleeScale = owner.GetAdjustedItemScale(held);
        useTimeScale = UseTimeScale(owner, held);

        FollowSyncedState(owner);

        if (!Transition(owner))
            return false;

        // Advanced before posing, so a phase's first tick moves off its entry pose and its last tick reaches its target.
        PhaseTimer++;

        UpdateAim(owner);

        previousOffset = bladeOffset;
        bladeOffset = OffsetForPhase();
        extension = ExtensionForPhase();

        // Colliding runs after AI and must test the pose taken this tick.
        damaging = Phase == SwingPhase.Swing;

        UpdateTraces();

        PoseOwner(owner);
        DampOwner(owner);

        Projectile.Center = HandPosition;
        Projectile.rotation = BladeAngle;
        Projectile.direction = Projectile.spriteDirection = aim.Facing;
        Projectile.timeLeft = 2;

        WhileHeld(owner);

        return true;
    }

    /// <summary>Whether the weapon should stay out.</summary>
    /// <remarks>
    ///     The held-item test is owner-side only, since remote inventories can be stale. It assumes the item's
    ///     <c>shoot</c> is this projectile; override for a weapon spawned some other way.
    /// </remarks>
    protected virtual bool CanKeepHolding(Player owner)
    {
        if (!owner.active || owner.dead || owner.noItems || owner.CCed)
            return false;

        return Main.myPlayer != Projectile.owner || owner.HeldItem.shoot == Projectile.type;
    }

    /// <summary>Sets up local pose state. Runs from <see cref="OnSpawn" /> on the spawning client and on the first tick elsewhere.</summary>
    private void Initialize(Player owner)
    {
        initialized = true;

        Vector2 fallback = Vector2.UnitX * owner.direction;
        Vector2 direction = Projectile.velocity.SafeNormalize(fallback);

        if (Math.Abs(direction.X) < AIM_DEADZONE)
            direction = new Vector2(AIM_DEADZONE * owner.direction, direction.Y).SafeNormalize(fallback);

        Projectile.velocity = direction;
        aim = AimFrame.From(direction);

        bladeOffset = previousOffset = profile.Guard;
        extension = 0f;

        CaptureEntry();

        observedPhase = Phase;
        observedCombo = ComboIndex;
    }

    /// <summary>Re-bases the pose on the current one when a packet has changed the phase or step.</summary>
    private void FollowSyncedState(Player owner)
    {
        if (Phase == observedPhase && ComboIndex == observedCombo)
            return;

        bool sweepStarted = Phase == SwingPhase.Swing && (observedPhase != SwingPhase.Swing || ComboIndex != observedCombo);

        CaptureEntry();

        observedPhase = Phase;
        observedCombo = ComboIndex;

        if (sweepStarted)
            StartSweep(owner);
    }

    #region phases

    /// <summary>Changes phase once the current one has run its ticks. Returns false if the weapon was put away.</summary>
    /// <remarks>Only the owner reads input. Elsewhere a finished sweep goes to Link until a packet says otherwise.</remarks>
    private bool Transition(Player owner)
    {
        bool local = Main.myPlayer == Projectile.owner;

        if (local)
            ReadInput(owner);

        bool elapsed = PhaseTimer >= PhaseTicks;

        switch (Phase)
        {
            case SwingPhase.Windup or SwingPhase.Recover when elapsed:
                SetPhase(SwingPhase.Swing);
                StartSweep(owner);

                break;

            case SwingPhase.Swing when elapsed:
                OnSwingEnd(owner, Step);

                if (local && (strikeQueued || WantsStrike(owner)))
                {
                    AdvanceCombo();
                    SetPhase(SwingPhase.Recover);
                }
                else
                {
                    SetPhase(SwingPhase.Link);
                }

                strikeQueued = false;

                break;

            case SwingPhase.Link when local:
                if (WantsStrike(owner))
                {
                    AdvanceCombo();
                    SetPhase(SwingPhase.Windup);
                }
                else if (elapsed)
                {
                    Projectile.Kill();

                    return false;
                }

                break;
        }

        return true;
    }

    /// <summary>Buffers a fresh press made during a strike, so the combo continues when the sweep ends.</summary>
    private void ReadInput(Player owner)
    {
        bool held = owner.controlUseItem;

        if (held && !useHeld && Phase != SwingPhase.Link)
            strikeQueued = true;

        useHeld = held;
    }

    private bool WantsStrike(Player owner) => !HoldToKeep || owner.controlUseItem;

    private void SetPhase(SwingPhase phase)
    {
        CaptureEntry();

        Phase = phase;
        PhaseTimer = 0f;

        observedPhase = phase;
        observedCombo = ComboIndex;

        if (Main.myPlayer == Projectile.owner)
            Projectile.netUpdate = true;
    }

    private void CaptureEntry()
    {
        entryOffset = bladeOffset;
        entryExtension = extension;
        entryElevation = aim.Elevation;
    }

    /// <summary>Per-sweep effects: clears hit immunity, plays the swing sound and calls <see cref="OnSwingStart" />.</summary>
    private void StartSweep(Player owner)
    {
        sweepCount++;

        Projectile.ResetLocalNPCHitImmunity();

        if (profile.SwingSound is { } sound)
            SoundEngine.PlaySound(sound, owner.Center);

        OnSwingStart(owner, Step);
    }

    private void AdvanceCombo() => ComboIndex = (ComboIndex + 1) % profile.ComboLength;

    private int PhaseTicks => Phase switch
    {
        SwingPhase.Windup => Ticks(profile.WindupTicks),
        SwingPhase.Swing => Ticks(profile.SwingTicks),
        SwingPhase.Recover => Ticks(profile.RecoverTicks),

        // Scaled by use speed only, so a fast step does not also shorten the window.
        _ => Math.Max(1, (int)MathF.Round(profile.ComboWindow * StatFactor)),
    };

    /// <summary>Progress through the phase, in (0, 1].</summary>
    private float Progress => MathHelper.Clamp(PhaseTimer / PhaseTicks, 0f, 1f);

    /// <summary>Multiplier on phase lengths: <see cref="useTimeScale" /> damped by <see cref="SwordProfile.SpeedInfluence" />.</summary>
    private float StatFactor => MathHelper.Lerp(1f, useTimeScale, profile.SpeedInfluence);

    /// <summary>
    ///     Held item's use-animation length relative to its type's default: speed prefixes (<c>useAnimation</c> against
    ///     <see cref="ContentSamples.ItemsByType" />), use-animation and use-speed hooks, and attack speed of the projectile's
    ///     damage class. <see cref="CombinedHooks.TotalAnimationTime" /> without its integer rounding.
    /// </summary>
    private float UseTimeScale(Player owner, Item item)
    {
        float animation = 1f;
        float speed = owner.GetTotalAttackSpeed(Projectile.DamageType);

        if (!item.IsAir)
        {
            if (ContentSamples.ItemsByType.TryGetValue(item.type, out Item? sample) && sample.useAnimation > 0)
                animation = item.useAnimation / (float)sample.useAnimation;

            animation *= PlayerLoader.UseAnimationMultiplier(owner, item) * ItemLoader.UseAnimationMultiplier(item, owner);
            speed *= PlayerLoader.UseSpeedMultiplier(owner, item) * ItemLoader.UseSpeedMultiplier(item, owner);
        }

        return MathHelper.Clamp(animation / Math.Max(speed, 0.01f), 0.25f, 4f);
    }

    /// <summary>Phase length for the current step, with the item's use speed and step speed applied.</summary>
    private int Ticks(int ticks)
        => Math.Max(1, (int)MathF.Round(ticks * StatFactor / Math.Max(Step.SpeedScale, 0.05f)));

    private float Eased(EaseFunction? ease) => (ease ?? Linear).Ease(Progress);

    #endregion

    #region aim

    /// <summary>Updates the synced target aim (owner-side, outside sweeps) and the displayed aim derived from it.</summary>
    /// <remarks>
    ///     Windup and recover interpolate the displayed elevation from its entry value to the target, so a sweep starts
    ///     exactly on target without a snap. Facing follows the target immediately.
    /// </remarks>
    private void UpdateAim(Player owner)
    {
        if (Phase != SwingPhase.Swing && Main.myPlayer == Projectile.owner)
        {
            Vector2 fallback = Vector2.UnitX * aim.Facing;
            Vector2 cursor = (Main.MouseWorld - owner.Center).SafeNormalize(fallback);

            if (Math.Abs(cursor.X) < AIM_DEADZONE)
                cursor = new Vector2(AIM_DEADZONE * aim.Facing, cursor.Y).SafeNormalize(fallback);

            if (Vector2.Dot(cursor, Projectile.velocity) < AIM_SYNC_TOLERANCE)
                Projectile.netUpdate = true;

            Projectile.velocity = cursor;
        }

        AimFrame target = AimFrame.From(Projectile.velocity.SafeNormalize(Vector2.UnitX * aim.Facing));

        float elevation = Phase switch
        {
            SwingPhase.Swing => target.Elevation,
            SwingPhase.Windup => MathHelper.Lerp(entryElevation, target.Elevation, Eased(profile.WindupEase)),
            SwingPhase.Recover => MathHelper.Lerp(entryElevation, target.Elevation, Eased(profile.RecoverEase)),
            _ => MathHelper.Lerp(aim.Elevation, target.Elevation, LINK_AIM_TRACKING),
        };

        aim = new AimFrame(target.Facing, elevation);
    }

    /// <summary>An aim split into a facing sign and an elevation mirrored into that facing.</summary>
    /// <remarks>Elevation is in [-pi/2, pi/2], negative toward world up. Blade offsets are applied in this frame.</remarks>
    private readonly record struct AimFrame(int Facing, float Elevation)
    {
        public float Angle => Facing == 1 ? Elevation : MathHelper.Pi - Elevation;

        public float AngleOf(float offset) => Angle + offset * Facing;

        public Vector2 DirectionOf(float offset) => AngleOf(offset).ToRotationVector2();

        public static AimFrame From(Vector2 direction)
            => new(direction.X >= 0f ? 1 : -1, MathF.Atan2(direction.Y, Math.Abs(direction.X)));
    }

    #endregion

    #region pose

    /// <summary>Radians off the aim a step's sweep starts at, anticipation included.</summary>
    private float SwingFromOffsetFor(SwingStep step)
    {
        float sweep = profile.Arc * step.Arc;

        return profile.ArcOffset + step.Offset - sweep * 0.5f - profile.Anticipation * MathF.Sign(sweep);
    }

    /// <summary>Radians off the aim a step's sweep ends at.</summary>
    private float SwingToOffsetFor(SwingStep step)
        => profile.ArcOffset + step.Offset + profile.Arc * step.Arc * 0.5f;

    private float SwingFromOffset => SwingFromOffsetFor(Step);

    private float SwingToOffset => SwingToOffsetFor(Step);

    /// <summary>Blade offset for this tick. Phases entered from an arbitrary pose interpolate from it.</summary>
    private float OffsetForPhase() => Phase switch
    {
        SwingPhase.Windup => MathHelper.Lerp(entryOffset, SwingFromOffset, Eased(profile.WindupEase)),
        SwingPhase.Swing => MathHelper.Lerp(SwingFromOffset, SwingToOffset, Eased(Step.Ease ?? profile.SwingEase)),
        SwingPhase.Recover => MathHelper.Lerp(entryOffset, SwingFromOffset, Eased(profile.RecoverEase)),
        _ => entryOffset,
    };

    /// <summary>Grip push for this tick: follows the swept angle during a sweep, and unwinds from its entry value otherwise.</summary>
    private float ExtensionForPhase()
    {
        if (Phase == SwingPhase.Swing)
            return profile.Extend * Step.Extend * TotalScale * EaseQuadOut.Ease(SweptFraction);

        float unwind = Phase == SwingPhase.Link
            ? MathHelper.Clamp(PhaseTimer / Ticks(profile.RecoverTicks), 0f, 1f)
            : Progress;

        return entryExtension * (1f - EaseQuadOut.Ease(unwind));
    }

    /// <summary>Composite arm rotation for a blade offset, damped by <see cref="SwordProfile.ArmFollow" />.</summary>
    /// <remarks>Measured from straight down. Vanilla applies <see cref="Player.gravDir" /> itself, so the angle is un-mirrored first.</remarks>
    private float ArmRotationFor(float offset, AimFrame frame)
    {
        Vector2 direction = frame.DirectionOf(offset * profile.ArmTracking);

        if (Owner.gravDir < 0f)
            direction.Y = -direction.Y;

        return direction.ToRotation() - MathHelper.PiOver2;
    }

    /// <summary>World position of the grip for a blade offset: the arm's hand, pushed <paramref name="push" /> pixels along the blade.</summary>
    /// <remarks>Uses the owner's current direction, so <paramref name="frame" /> must have the same facing.</remarks>
    private Vector2 HandFor(float offset, AimFrame frame, float push)
        => Owner.GetFrontHandPosition(Player.CompositeArmStretchAmount.Full, ArmRotationFor(offset, frame))
           + new Vector2(0f, Owner.gfxOffY)
           + frame.DirectionOf(offset) * push;

    /// <summary>Turns the owner, keeps the item use alive, and drives the front arm and torso from the pose.</summary>
    /// <remarks><c>itemTime</c> and <c>itemAnimation</c> are refreshed every tick; if they lapse the arm drops for a frame.</remarks>
    protected virtual void PoseOwner(Player owner)
    {
        owner.ChangeDir(aim.Facing);
        owner.heldProj = Projectile.whoAmI;
        owner.itemTime = owner.itemAnimation = 2;

        owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, ArmRotationFor(bladeOffset, aim));

        owner.bodyFrame.Y = owner.bodyFrame.Height * BodyFrame();
    }

    /// <summary>Torso frame: leans into the upcoming strike, follows the swept angle through a sweep, and holds after it.</summary>
    private int BodyFrame()
    {
        const int UP = 2;
        const int DOWN = 4;

        bool downward = Step.Downward;
        int third = Math.Clamp((int)(SweptFraction * 3f), 0, 2);

        return Phase switch
        {
            SwingPhase.Windup or SwingPhase.Recover => downward ? UP : DOWN,
            SwingPhase.Swing => downward ? UP + third : DOWN - third,
            _ => downward ? DOWN : UP,
        };
    }

    /// <summary>Damps horizontal movement during windup and sweep.</summary>
    /// <remarks>Applied to velocity because projectiles update after players, so <see cref="Player.moveSpeed" /> is already spent.</remarks>
    private void DampOwner(Player owner)
    {
        if (Phase is SwingPhase.Windup or SwingPhase.Swing)
            owner.velocity.X *= profile.MoveDamping;
    }

    #endregion

    #region damage

    public bool? CanDamage() => damaging ? null : false;

    /// <summary>
    ///     Tests the blade edge as a thick segment, swept across the angle covered since last tick so a fast sweep
    ///     cannot step over a target. The segment starts at <see cref="SwordProfile.HiltFraction" /> of the reach.
    /// </summary>
    public bool Colliding(Rectangle targetHitbox)
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

            Vector2 hand = HandFor(offset, aim, extension);
            Vector2 direction = aim.DirectionOf(offset);

            Vector2 start = hand + direction * reach * profile.HiltFraction;
            Vector2 end = hand + direction * reach;

            float _ = 0f;

            if (Collision.CheckAABBvLineCollision(targetHitbox.TopLeft(), targetHitbox.Size(), start, end, thickness, ref _))
                return true;
        }

        return false;
    }

    /// <remarks>Step damage goes through <see cref="NPC.HitModifiers.SourceDamage" /> so it composes with crits and bonuses.</remarks>
    public void ModifyHitNPC(NPC target, ref NPC.HitModifiers modifiers)
    {
        SwingStep step = Step;

        modifiers.SourceDamage *= step.DamageScale;
        modifiers.Knockback *= step.KnockbackScale;
        modifiers.HitDirectionOverride = aim.Facing;
    }

    #endregion

    #region draw

    /// <summary>Draws the trail and the blade. Hosts call it from PreDraw and return false.</summary>
    public void Draw(Color lightColor)
    {
        Player owner = Owner;
        Vector2 snap = PixelSnap(owner);

        DrawTrails(snap);

        Texture2D texture = Texture;

        bool flipX = aim.Facing < 0;
        bool flipY = owner.gravDir < 0f;

        // Pivot on the grip corner, mirrored into whichever corner the grip lands in.
        var origin = new Vector2(texture.Width * profile.Origin.X, texture.Height * profile.Origin.Y);

        if (flipX)
            origin.X = texture.Width - origin.X;

        if (flipY)
            origin.Y = texture.Height - origin.Y;

        // A horizontal flip maps a texture angle to pi minus itself; a vertical flip negates it.
        float correction = flipX ? MathHelper.Pi - profile.SpriteAngle : profile.SpriteAngle;

        if (flipY)
            correction = -correction;

        Vector2 hand = HandPosition + snap;

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
    }

    /// <summary>Offset that rounds a world position onto the whole-pixel grid vanilla draws the owner's body layers on.</summary>
    private static Vector2 PixelSnap(Player owner)
    {
        Vector2 screen = owner.position + new Vector2(0f, owner.gfxOffY) - Main.screenPosition;

        return screen.Floor() - screen;
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

    #region trail

    /// <summary>A recorded sweep: the geometry its trail is rebuilt from, and a clock that keeps running after the sweep ends.</summary>
    private struct SwingTrace
    {
        public bool Active;
        public int Sweep;
        public AimFrame Aim;
        public float From;
        public float To;
        public EaseFunction? Ease;
        public float Length;

        /// <summary>Ticks of the sweep the trail covers behind its head.</summary>
        public float Window;

        public float Reach;
        public float Extend;

        /// <summary>Ticks since the sweep started.</summary>
        public float Clock;

        public readonly float OffsetAt(float time)
            => MathHelper.Lerp(From, To, (Ease ?? Linear).Ease(MathHelper.Clamp(time / Length, 0f, 1f)));
    }

    /// <summary>Records the sweep in progress and advances or expires earlier ones.</summary>
    /// <remarks>Two slots, so a trail still clearing when the next sweep starts is not cut off.</remarks>
    private void UpdateTraces()
    {
        traces ??= new SwingTrace[2];

        bool sweeping = Phase == SwingPhase.Swing;
        bool tracked = false;

        for (int i = 0; i < traces.Length; i++)
        {
            ref SwingTrace trace = ref traces[i];

            if (!trace.Active)
                continue;

            bool current = sweeping && trace.Sweep == sweepCount;

            trace.Clock = current ? PhaseTimer : trace.Clock + 1f;
            tracked |= current;

            // The trail is built around the shoulder for its facing, which moves when the facing changes.
            if (trace.Aim.Facing != aim.Facing || trace.Clock - trace.Window >= trace.Length)
                trace.Active = false;
        }

        if (!sweeping || tracked || profile.TrailOpacity <= 0f)
            return;

        int slot = 0;

        for (int i = 1; i < traces.Length; i++)
        {
            if (RecencyOf(traces[i]) < RecencyOf(traces[slot]))
                slot = i;
        }

        float length = PhaseTicks;

        traces[slot] = new SwingTrace
        {
            Active = true,
            Sweep = sweepCount,
            Aim = aim,
            From = SwingFromOffset,
            To = SwingToOffset,
            Ease = Step.Ease ?? profile.SwingEase,
            Length = length,
            Window = profile.TrailLength * length,
            Reach = Reach,
            Extend = profile.Extend * Step.Extend * TotalScale,
            Clock = PhaseTimer,
        };

        static int RecencyOf(in SwingTrace trace) => trace.Active ? trace.Sweep : int.MinValue;
    }

    /// <summary>
    ///     Draws recorded sweeps as bands from <see cref="SwordProfile.TrailInner" /> to <see cref="SwordProfile.TrailOuter" />
    ///     of the reach. Ends the running sprite batch for the vertex draw and resumes it with the same parameters.
    /// </summary>
    private void DrawTrails(Vector2 snap)
    {
        if (traces is null || profile.TrailOpacity <= 0f)
            return;

        Effect? effect = null;
        SpriteBatch batch = Main.spriteBatch;
        SpriteBatchState state = default;
        bool resume = false;

        for (int i = 0; i < traces.Length; i++)
        {
            if (!BuildTrail(traces[i], out float opacity))
                continue;

            if (effect is null)
            {
                effect = Assets.Shaders.SwingTrail.Asset.Value;

                if (effect is null)
                    return;

                ApplyTrailParameters(effect, snap);

                if (batch.beginCalled)
                {
                    state = SpriteBatchState.Capture(batch);
                    batch.End();
                    resume = true;
                }
            }

            effect.Parameters["opacity"]?.SetValue(opacity);
            trailStrip!.Render(effect, BlendState.AlphaBlend);
        }

        if (resume)
            state.Begin(batch);
    }

    /// <summary>Fills <see cref="trailStrip" /> for a trace. Returns false if nothing visible remains.</summary>
    /// <remarks>Each point's grip is placed where it was at that angle, so the band follows the tip's actual path.</remarks>
    private bool BuildTrail(in SwingTrace trace, out float opacity)
    {
        opacity = 0f;

        if (!trace.Active)
            return false;

        float head = trace.OffsetAt(Math.Min(trace.Clock, trace.Length));
        float tail = trace.OffsetAt(Math.Max(trace.Clock - trace.Window, 0f));

        opacity = profile.TrailOpacity * MathHelper.Clamp(Math.Abs(head - tail) / TRAIL_FULL_ARC, 0f, 1f);

        if (opacity <= 0.01f)
            return false;

        int points = profile.TrailPointCount;

        if (trailStrip is null || trailStrip.PointCount != points)
            trailStrip = new VertexStrip(points);

        float span = trace.To - trace.From;

        for (int i = 0; i < points; i++)
        {
            float along = i / (points - 1f);
            float offset = MathHelper.Lerp(tail, head, along);
            float swept = Math.Abs(span) < 0.0001f ? 1f : MathHelper.Clamp((offset - trace.From) / span, 0f, 1f);

            Vector2 direction = trace.Aim.DirectionOf(offset);
            Vector2 pivot = HandFor(offset, trace.Aim, trace.Extend * EaseQuadOut.Ease(swept));
            float fade = along * along;

            trailStrip.SetPoint(
                i,
                pivot + direction * (trace.Reach * profile.TrailOuter),
                pivot + direction * (trace.Reach * profile.TrailInner),
                Math.Abs(offset - trace.From) / TRAIL_TEXTURE_ARC,
                new Color(fade, fade, fade)
            );
        }

        return true;
    }

    /// <summary>Sets the trail shader parameters shared by every trace this frame.</summary>
    private void ApplyTrailParameters(Effect effect, Vector2 snap)
    {
        Matrix transform = Matrix.CreateTranslation(new Vector3(snap - Main.screenPosition, 0f))
                           * Main.GameViewMatrix.TransformationMatrix
                           * Matrix.CreateOrthographicOffCenter(0f, Main.screenWidth, Main.screenHeight, 0f, -1f, 1f);

        Texture2D? texture = profile.TrailTexture is { } key ? ModContent.Request<Texture2D>(key).Value : null;

        effect.Parameters["transformMatrix"]?.SetValue(transform);
        effect.Parameters["bodyColor"]?.SetValue(profile.TrailColor.ToVector4());
        effect.Parameters["edgeColor"]?.SetValue(profile.TrailEdgeColor.ToVector4());
        effect.Parameters["edgeWidth"]?.SetValue(TRAIL_EDGE_WIDTH);
        effect.Parameters["sampleTexture"]?.SetValue(texture ?? TextureAssets.MagicPixel.Value);
    }

    #endregion

    #region hooks

    /// <summary>A sweep has started. Runs on every client; guard anything that must happen once.</summary>
    protected virtual void OnSwingStart(Player owner, SwingStep step) { }

    /// <summary>A sweep has ended, hit or miss. Runs on every client.</summary>
    protected virtual void OnSwingEnd(Player owner, SwingStep step) { }

    /// <summary>Called every tick the weapon is out. Runs on every client.</summary>
    protected virtual void WhileHeld(Player owner) { }

    /// <summary>Drawn under the blade, in the running batch.</summary>
    /// <param name="hand">World position of the grip, pixel-snapped like the blade.</param>
    protected virtual void DrawBehindBlade(SpriteBatch spriteBatch, Vector2 hand, Color lightColor) { }

    /// <inheritdoc cref="DrawBehindBlade" />
    protected virtual void DrawOverBlade(SpriteBatch spriteBatch, Vector2 hand, Color lightColor) { }

    #endregion
}
