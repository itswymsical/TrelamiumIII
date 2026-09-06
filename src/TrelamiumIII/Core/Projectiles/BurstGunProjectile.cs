using Terraria.DataStructures;

namespace TrelamiumIII.Core.Projectiles;

/// <summary>
///     A held gun that fires in committed bursts: a fixed number of rounds at the profile's fire
///     rate, then a recovery long enough that the burst is the unit of play rather than the round.
/// </summary>
/// <remarks>
///     <para>
///         Burst fire is not "a fast gun with pauses in it", and the difference is what this class
///         exists to encode. Three properties make it its own archetype:
///         <list type="bullet">
///             <item><b>The burst is committed.</b> Once the first round leaves, all of them do.
///             Releasing the button cancels the next burst, never the current one, so the player
///             is choosing when to spend a burst rather than metering a stream.</item>
///             <item><b>Recovery is the balance lever, not fire rate.</b> Sustained output is
///             <c>BurstSize / BurstPeriod</c> and the recovery dominates that fraction, which means
///             the inside of the burst can be as fast as it looks without the weapon being
///             oppressive. A two-tick cadence with a 36-tick breath is under two shots a second
///             sustained.</item>
///             <item><b>Recoil climbs across the burst.</b> Each round resets the recoil timer, so
///             without a rising ceiling three shots produce three identical twitches and the burst
///             reads as a stutter. Scaling the ceiling from
///             <see cref="GunShotProfile.BurstClimb" /> up to full over the burst gives the
///             staircase that makes the three rounds read as one event, and the profile's longer
///             <see cref="GunShotProfile.RecoilTicks" /> lets the settle play out across the
///             recovery so the weapon is visibly recomposing rather than idle.</item>
///         </list>
///     </para>
///     <para>
///         Everything else -- aim, arm, draw, netcode -- is <see cref="HeldGunProjectile" />'s. This
///         class adds a counter and three overrides, which is the amount of code the archetype is
///         actually worth.
///     </para>
/// </remarks>
public abstract class BurstGunProjectile : HeldGunProjectile
{
    /// <summary>Rounds already fired in the current burst.</summary>
    /// <remarks>
    ///     In <c>ai[1]</c> rather than a field, so it rides the standard projectile sync with the
    ///     shot timer in <c>ai[0]</c>. A remote client that only had the timer would run the burst's
    ///     recoil envelope off the wrong shot index and draw a different weapon to the one its owner
    ///     is holding.
    /// </remarks>
    protected int BurstShot
    {
        get => (int)Projectile.ai[1];
        set => Projectile.ai[1] = value;
    }

    /// <summary>Rounds in one burst. Defaults to the profile, at a floor of one.</summary>
    protected virtual int BurstSize => Math.Max(Profile.BurstSize, 1);

    /// <summary>Ticks between the last round of a burst and the first of the next.</summary>
    protected virtual int BurstRecovery => Math.Max(Profile.BurstRecovery, 0);

    /// <summary>Whether the weapon is partway through a burst it has committed to.</summary>
    protected bool MidBurst => BurstShot > 0 && BurstShot < BurstSize;

    /// <summary>
    ///     The profile's fire rate inside a burst, the recovery once the burst is spent.
    /// </summary>
    protected override int ShotInterval => BurstShot >= BurstSize ? BurstRecovery : Profile.UseSpeed;

    /// <summary>
    ///     A committed burst finishes before the weapon may be put away -- unless it cannot finish.
    /// </summary>
    /// <remarks>
    ///     The second half of that is not a nicety. Commitment alone means a burst interrupted by
    ///     the magazine running out is a burst that never completes, and a weapon that never
    ///     completes its burst never becomes holsterable: the player is left holding a dry gun that
    ///     will not go away no matter what they press. Asking the same side-effect-free question the
    ///     firing path asks lets the weapon admit it is stuck and lower.
    /// </remarks>
    protected override bool CanHolster => !MidBurst || !CanFireBurst(Owner);

    /// <summary>
    ///     The base envelope, ceilinged by how far into the burst this round is.
    /// </summary>
    /// <remarks>
    ///     Indexed off the round that last fired, so the ceiling is the one that round earned and
    ///     stays put while the shot settles. During the recovery <see cref="BurstShot" /> has been
    ///     reset to zero, so the settle plays out under the full ceiling the final round reached --
    ///     which is the intended reading, since that is the kick actually being recovered from.
    /// </remarks>
    protected override float Recoil
    {
        get
        {
            float envelope = base.Recoil;

            if (BurstSize <= 1)
                return envelope;

            int fired = BurstShot > 0 ? BurstShot : BurstSize;
            float progress = (fired - 1) / (float)Math.Max(BurstSize - 1, 1);

            return envelope * MathHelper.Lerp(MathHelper.Clamp(Profile.BurstClimb, 0f, 1f), 1f, progress);
        }
    }

    /// <summary>Starts ready to fire, with a fresh burst rather than mid-recovery.</summary>
    public override void OnSpawn(IEntitySource source)
    {
        BurstShot = 0;

        base.OnSpawn(source);
    }

    /// <summary>
    ///     Opens the next burst, then defers to the weapon's own conditions.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Sealed, and the reason matters. The counter has to be reopened at exactly one moment:
    ///         after the recovery interval has elapsed and before the round that follows it is
    ///         counted. This hook is that moment -- the base class only asks it once the interval
    ///         gate has passed and immediately before firing -- and there is no other. Put the reset
    ///         a tick earlier and the recovery is skipped, a tick later and it never ends.
    ///     </para>
    ///     <para>
    ///         Weapons therefore override <see cref="CanFireBurst" /> instead. Leaving
    ///         <see cref="HeldGunProjectile.CanFire" /> open would mean any subclass that forgot to
    ///         call base -- the normal way to write an ammo check -- silently welded the weapon into
    ///         permanent recovery, which is a bug that looks like bad tuning.
    ///     </para>
    /// </remarks>
    protected sealed override bool CanFire(Player owner)
    {
        if (BurstShot >= BurstSize)
            BurstShot = 0;

        return CanFireBurst(owner);
    }

    /// <inheritdoc cref="HeldGunProjectile.CanFire" />
    /// <remarks>
    ///     Must be free of side effects. <see cref="CanHolster" /> asks it too, on ticks where
    ///     nothing is being fired, to find out whether an unfinished burst is unfinishable.
    /// </remarks>
    protected virtual bool CanFireBurst(Player owner) => true;

    /// <summary>
    ///     Counts the round, and announces the burst once it was the last one.
    /// </summary>
    /// <remarks>
    ///     Reaching <see cref="BurstSize" /> is what makes <see cref="ShotInterval" /> return the
    ///     recovery, so the counter is left standing at its ceiling for the whole of it and only
    ///     reopened by <see cref="CanFire" /> on the far side.
    /// </remarks>
    protected sealed override void OnShotFired(Player owner)
    {
        BurstShot++;

        if (BurstShot >= BurstSize)
            OnBurstComplete(owner);
    }

    /// <summary>
    ///     The last round of a burst has left. Fires before the recovery begins.
    /// </summary>
    /// <remarks>
    ///     The natural place for whatever punctuates a burst -- a shell eject, a vent of steam, a
    ///     heavier report on the last round.
    /// </remarks>
    protected virtual void OnBurstComplete(Player owner) { }
}
