using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.Projectiles;

/// <summary>
///     How a gun throws itself around when it goes off: the shape of one firing cycle, as numbers.
/// </summary>
/// <remarks>
///     <para>
///         The half of a held gun that has nothing to do with what it shoots: how far the body snaps
///         back, how fast it returns, how hard the muzzle climbs, how far the arm is thrown, where the
///         barrel ends. A new weapon is a preset and a <see cref="HeldGunProjectile.Fire" /> body
///         rather than another copy of the recoil arithmetic.
///     </para>
///     <para>
///         Recoil is two eased phases. A single ease from full kick to rest puts the gun at maximum
///         displacement on the frame of the shot, so the punch reads as after the fact. A short
///         <see cref="SnapTicks" /> window easing *out* to the peak gives a two- or three-frame windup
///         that reads as the shot happening, then the long settle. Set <see cref="SnapTicks" /> to
///         zero for single-phase recoil.
///     </para>
///     <para>
///         A record struct so presets compose: start from <see cref="Pistol" /> and adjust with a
///         <see langword="with" /> expression.
///     </para>
/// </remarks>
public readonly record struct GunShotProfile
{
    /// <summary>Ticks from one shot to the next.</summary>
    public int UseSpeed { get; init; }

    /// <summary>
    ///     Ticks the recoil animation takes to run its course. Zero means "however long
    ///     <see cref="UseSpeed" /> is".
    /// </summary>
    /// <remarks>
    ///     Separate from the fire rate for burst weapons: at two ticks between shots the recoil would
    ///     be a single invisible frame. The animation needs to outlast the cadence and settle across
    ///     the recovery.
    /// </remarks>
    public int RecoilTicks { get; init; }

    /// <summary>How long the recoil animation actually runs. See <see cref="RecoilTicks" />.</summary>
    public int RecoilLength => RecoilTicks > 0 ? RecoilTicks : UseSpeed;

    /// <summary>
    ///     Ticks spent easing out to peak recoil before the settle begins. Zero peaks instantly.
    /// </summary>
    public int SnapTicks { get; init; }

    /// <summary>Pixels the body is driven back along its own barrel at peak.</summary>
    public float Kick { get; init; }

    /// <summary>Pixels the body is driven perpendicular-up at peak.</summary>
    public float Rise { get; init; }

    /// <summary>Radians the muzzle climbs at peak, on top of the aim direction.</summary>
    public float Climb { get; init; }

    /// <summary>Radians the composite front arm is thrown back at peak.</summary>
    public float ArmKick { get; init; }

    /// <summary>Pixels from the holder's centre to the end of the barrel.</summary>
    /// <remarks>
    ///     Where the shot is spawned from, and it matters more than it sounds: too short and the
    ///     projectile is born inside the player's own sprite, too long and it clears thin walls the
    ///     player is standing against. The muzzle is clipped against geometry before it is used.
    /// </remarks>
    public float MuzzleLength { get; init; }

    /// <summary>Pixels the body sits forward of the holder's centre, before recoil.</summary>
    public float HoldDistance { get; init; }

    /// <summary>Sprite origin as a fraction of the texture, so it works at any sprite size.</summary>
    /// <remarks>
    ///     X near zero puts the pivot at the grip end, which is what makes the weapon rotate about
    ///     the hand rather than about its own middle.
    /// </remarks>
    public Vector2 Origin { get; init; }

    /// <summary>Curve of the punch out to peak. Wants to be fast and front-loaded.</summary>
    public EaseFunction PunchEase { get; init; }

    /// <summary>Curve of the settle back to rest. Wants a long tail.</summary>
    public EaseFunction SettleEase { get; init; }

    /// <summary>
    ///     Shots in one burst. Read only by <see cref="BurstGunProjectile" />; 0 or 1 is not a burst.
    /// </summary>
    /// <remarks>
    ///     Burst fields live on the profile rather than the archetype class because a burst's shape is
    ///     part of the firing cycle, which lets a preset like <see cref="BurstPistol" /> describe the
    ///     whole archetype in one value.
    /// </remarks>
    public int BurstSize { get; init; }

    /// <summary>Ticks between the last shot of one burst and the first of the next.</summary>
    /// <remarks>
    ///     This is the number that balances a burst weapon. <see cref="UseSpeed" /> inside the burst
    ///     can be as fast as it likes -- the sustained rate is burst size over
    ///     (size × UseSpeed + recovery), and the recovery dominates it.
    /// </remarks>
    public int BurstRecovery { get; init; }

    /// <summary>Recoil the first shot of a burst reaches, as a fraction of the last shot's.</summary>
    /// <remarks>
    ///     The climb across the burst is what makes three shots read as one event rather than as
    ///     three unrelated ones. Each shot resets the recoil timer, so without a rising ceiling all
    ///     three kicks are identical and the burst looks like a stutter.
    /// </remarks>
    public float BurstClimb { get; init; }

    /// <summary>Ticks from the first round of one burst to the first round of the next.</summary>
    /// <remarks>
    ///     <c>(size - 1)</c> intervals, not <c>size</c>: three rounds have two gaps between them.
    ///     The recovery is measured from the last round, so it does not get one of its own.
    /// </remarks>
    public int BurstPeriod => (Math.Max(BurstSize, 1) - 1) * UseSpeed + BurstRecovery;

    /// <summary>
    ///     Light, fast, and all wrist: a short punch, a pronounced climb, and a quick settle.
    /// </summary>
    public static GunShotProfile Pistol => new()
    {
        UseSpeed = 20,
        SnapTicks = 3,
        Kick = 9f,
        Rise = 3.5f,
        Climb = 0.30f,
        ArmKick = 0.50f,
        MuzzleLength = 24f,
        HoldDistance = 8f,
        Origin = new Vector2(0.2f, 0.5f),
        PunchEase = EaseFunction.EaseCircularOut,
        SettleEase = EaseFunction.EaseCubicOut,
    };

    /// <summary>
    ///     Heavy and shoulder-braced: peaks on the frame of the shot and takes its time coming back.
    /// </summary>
    /// <remarks>
    ///     <see cref="SnapTicks" /> is zero on purpose. Something this size does not wind up -- it
    ///     is simply somewhere else the instant it fires.
    /// </remarks>
    public static GunShotProfile Cannon => new()
    {
        UseSpeed = 45,
        SnapTicks = 0,
        Kick = 14f,
        Rise = 6f,
        Climb = 0.12f,
        ArmKick = 0.45f,
        MuzzleLength = 25f,
        HoldDistance = 0f,
        Origin = new Vector2(0.15f, 0.55f),
        PunchEase = EaseFunction.Linear,
        SettleEase = EaseFunction.EaseCubicOut,
    };

    /// <summary>
    ///     A burst pistol: three shots almost on top of each other, then a beat to recover.
    /// </summary>
    /// <remarks>
    ///     Same weapon as <see cref="Pistol" /> in the hand -- same kick, same climb, same grip --
    ///     with the cadence rearranged. The recoil is deliberately allowed to outlast the burst: at
    ///     two ticks a shot, three kicks land inside six ticks, and the settle then plays across the
    ///     recovery so the weapon is visibly recomposing itself rather than idle.
    /// </remarks>
    public static GunShotProfile BurstPistol => Pistol with
    {
        UseSpeed = 2,
        RecoilTicks = 26,
        SnapTicks = 1,
        BurstSize = 3,
        BurstRecovery = 36,
        BurstClimb = 0.55f,
        Kick = 7f,
        Rise = 3f,
        Climb = 0.34f,
    };

    /// <summary>
    ///     Recoil at a point in the cycle, 0 at rest and 1 at peak.
    /// </summary>
    /// <param name="ticksSinceShot">Ticks elapsed since the last shot went off.</param>
    /// <remarks>
    ///     Everything the weapon does visually is scaled by this one number -- the offset, the
    ///     climb, the arm -- so the parts cannot drift out of phase with each other, which is the
    ///     failure mode when each is animated on its own timer.
    /// </remarks>
    public float RecoilAt(int ticksSinceShot)
    {
        if (ticksSinceShot < 0)
            return 0f;

        if (SnapTicks > 0 && ticksSinceShot < SnapTicks)
            return MathHelper.Clamp((PunchEase ?? EaseFunction.Linear).Ease(ticksSinceShot / (float)SnapTicks), 0f, 1f);

        float settle = Math.Max(RecoilLength - SnapTicks, 1);
        float progress = MathHelper.Clamp((ticksSinceShot - SnapTicks) / settle, 0f, 1f);

        return MathHelper.Clamp(1f - (SettleEase ?? EaseFunction.Linear).Ease(progress), 0f, 1f);
    }
}
