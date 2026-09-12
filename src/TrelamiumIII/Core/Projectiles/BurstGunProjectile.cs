using Terraria.DataStructures;

namespace TrelamiumIII.Core.Projectiles;

/// <summary>
///     A held gun that fires in committed bursts: a fixed number of rounds at the profile's fire
///     rate, then a recovery long enough that the burst is the unit of play rather than the round.
/// </summary>
/// <remarks>
///     <para>
///         Three properties make burst fire its own archetype rather than a fast gun with pauses:
///         <list type="bullet">
///             <item><b>The burst is committed.</b> Once the first round leaves, all of them do.
///             Releasing the button cancels the next burst, never the current one.</item>
///             <item><b>Recovery is the balance lever, not fire rate.</b> Sustained output is
///             <c>BurstSize / BurstPeriod</c> and the recovery dominates it, so the burst itself can
///             be as fast as it looks. A two-tick cadence with a 36-tick breath is under two shots a
///             second sustained.</item>
///             <item><b>Recoil climbs across the burst.</b> Each round resets the recoil timer, so
///             without a rising ceiling the burst reads as a stutter. The ceiling scales from
///             <see cref="GunShotProfile.BurstClimb" /> to full, and the longer
///             <see cref="GunShotProfile.RecoilTicks" /> lets the settle play out across the
///             recovery.</item>
///         </list>
///     </para>
///     <para>
///         Everything else -- aim, arm, draw, netcode -- is <see cref="HeldGunProjectile" />'s.
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
    ///     Without the escape, a burst interrupted by an empty magazine never completes and the player
    ///     is stuck holding a dry gun that won't lower. Asks the same side-effect-free question the
    ///     firing path does.
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
    ///         Sealed because the counter must reopen at exactly one moment: after the recovery
    ///         interval and before the next round is counted. The base only calls this once the
    ///         interval gate has passed, immediately before firing. A tick earlier skips the recovery;
    ///         a tick later never ends it.
    ///     </para>
    ///     <para>
    ///         Weapons override <see cref="CanFireBurst" /> instead, so an ammo check that forgets to
    ///         call base can't weld the weapon into permanent recovery.
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
