using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.Projectiles;

/// <summary>
///     How a gun throws itself around when it goes off: the shape of one firing cycle, as numbers.
/// </summary>
/// <remarks>
///     <para>
///         This is the half of a held gun that has nothing to do with what it shoots. Two weapons
///         that fire the same projectile can feel completely unalike, and all of the difference
///         lives here -- how far the body snaps back, how fast it comes home, how hard the muzzle
///         climbs, how far back the arm is thrown, where the barrel ends. Pulling it out into a
///         value means a new weapon is a preset and a <see cref="HeldGunProjectile.Fire" /> body,
///         rather than another copy of the same recoil arithmetic with different constants buried
///         in it.
///     </para>
///     <para>
///         The recoil itself is two eased phases rather than one, and that is the part worth
///         understanding. A single ease from full kick back to rest -- which is what hand-rolled
///         recoil usually is -- has the gun at maximum displacement on the frame of the shot and
///         monotonically settling afterwards, so the punch is something the player is told about
///         after the fact. Splitting off a short <see cref="SnapTicks" /> window that eases *out*
///         to the peak gives the weapon a windup of two or three frames that reads as the shot
///         happening, and only then the long settle. Set <see cref="SnapTicks" /> to zero to get
///         the old single-phase behaviour, which is right for something with no mechanism to speak
///         of.
///     </para>
///     <para>
///         A record struct so presets compose: start from <see cref="Pistol" /> and adjust with a
///         <see langword="with" /> expression rather than restating twelve fields.
///     </para>
/// </remarks>
public readonly record struct GunShotProfile
{
    /// <summary>Ticks from one shot to the next. Also the length of the recoil animation.</summary>
    public int UseSpeed { get; init; }

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

        float settle = Math.Max(UseSpeed - SnapTicks, 1);
        float progress = MathHelper.Clamp((ticksSinceShot - SnapTicks) / settle, 0f, 1f);

        return MathHelper.Clamp(1f - (SettleEase ?? EaseFunction.Linear).Ease(progress), 0f, 1f);
    }
}
