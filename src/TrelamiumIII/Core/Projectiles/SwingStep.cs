using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.Projectiles;

/// <summary>
///     One strike in a weapon's combo: which way the blade goes round, and how hard.
/// </summary>
/// <remarks>
///     <para>
///         A sword's combo is a loop of these, and the default loop is two of them --
///         <see cref="Down" /> then <see cref="Up" />. That is all alternating slashes are: the
///         same arc, mirrored, played one after the other. Expressing it as a two-element array
///         rather than as a boolean that flips is what makes the third and fourth steps free; a
///         katana that opens with two draws and finishes on a spin is a longer array, not a longer
///         state machine.
///     </para>
///     <para>
///         Every field here is a modifier on <see cref="SwordProfile" /> rather than a value of its
///         own. A step does not know how long a swing takes or how far the blade reaches -- it knows
///         it is a fifth faster and a quarter longer than whatever the weapon's numbers say. That
///         split is what lets one combo shape be shared across weapons of wildly different weight:
///         <see cref="Thrust" /> lunges on a dagger and on a broadsword alike, and reads correctly on
///         both because it never states an absolute.
///     </para>
///     <para>
///         The scale fields treat zero as "unset" and answer one, so a step written as
///         <c>new SwingStep { Arc = -1f }</c> is a plain mirrored slash rather than a motionless
///         weapon that deals no damage. The cost is that a step cannot ask for zero damage by
///         setting <see cref="Damage" /> to zero; a step that should not hurt anything wants
///         <see cref="HeldSwordProjectile.CanDamage" /> instead.
///     </para>
/// </remarks>
public readonly record struct SwingStep
{
    /// <summary>
    ///     The profile's arc, scaled and signed. Negative mirrors it -- that is the up-slash.
    /// </summary>
    /// <remarks>
    ///     Signed rather than paired with a separate direction flag because the sign is the only
    ///     thing that distinguishes a down-slash from an up-slash, and a magnitude of anything other
    ///     than one is a shallower or wider version of the same motion. A step at <c>0.3f</c> barely
    ///     rotates, which is a thrust.
    /// </remarks>
    public float Arc { get; init; }

    /// <summary>
    ///     Radians this step's arc is rotated off the profile's centre, before facing is applied.
    /// </summary>
    /// <remarks>
    ///     Positive is toward the ground. Where a combo wants its steps to land at different
    ///     heights -- a high opener into a low finisher -- rather than retreading the same line.
    /// </remarks>
    public float Offset { get; init; }

    /// <summary>How much faster this step runs than the profile's timing. Zero means unscaled.</summary>
    /// <remarks>Higher is faster: every phase's tick count is divided by it.</remarks>
    public float Speed { get; init; }

    /// <summary>Damage multiplier for this step. Zero means unscaled.</summary>
    /// <remarks>
    ///     Applied to source damage, so it composes with crits and class bonuses instead of
    ///     replacing them.
    /// </remarks>
    public float Damage { get; init; }

    /// <summary>Knockback multiplier. Zero means unscaled.</summary>
    public float Knockback { get; init; }

    /// <summary>Reach multiplier -- a lunge extends, a chop pulls in. Zero means unscaled.</summary>
    public float Reach { get; init; }

    /// <summary>Swing curve for this step. Null takes the profile's.</summary>
    /// <remarks>
    ///     The seam for a finisher that should not move like the strikes before it: two clean draws
    ///     on the profile's curve and then a heavy one that winds up and lands, without the weapon
    ///     needing a second profile to say so.
    /// </remarks>
    public EaseFunction Ease { get; init; }

    /// <inheritdoc cref="Speed" />
    public float SpeedScale => Speed > 0f ? Speed : 1f;

    /// <inheritdoc cref="Damage" />
    public float DamageScale => Damage > 0f ? Damage : 1f;

    /// <inheritdoc cref="Knockback" />
    public float KnockbackScale => Knockback > 0f ? Knockback : 1f;

    /// <inheritdoc cref="Reach" />
    public float ReachScale => Reach > 0f ? Reach : 1f;

    /// <summary>Whether the blade sweeps toward the ground. Drives the body frames.</summary>
    public bool Downward => Arc >= 0f;

    /// <summary>An overhead slash coming down. The opening half of the default loop.</summary>
    public static SwingStep Down => new() { Arc = 1f };

    /// <summary>The same arc coming back up. The closing half of the default loop.</summary>
    public static SwingStep Up => new() { Arc = -1f };

    /// <summary>
    ///     A lunge: almost no rotation, extra reach, and quick.
    /// </summary>
    /// <remarks>
    ///     Slightly under-damaged on purpose. A thrust ends a combo pointing at the target with the
    ///     weapon already out, so it is the cheapest step to land and should not also be the best.
    /// </remarks>
    public static SwingStep Thrust => new() { Arc = 0.3f, Speed = 1.35f, Reach = 1.3f, Damage = 0.85f };

    /// <summary>
    ///     A full turn of the shoulders: twice the arc, slower, and worth landing.
    /// </summary>
    /// <remarks>
    ///     A finisher. The extra arc is what pays for the speed loss -- it sweeps behind the player
    ///     as well as in front, so a spin is the step that answers being surrounded.
    /// </remarks>
    public static SwingStep Spin => new() { Arc = 2f, Speed = 0.8f, Damage = 1.4f, Knockback = 1.6f };

    /// <summary>
    ///     Down, then up. The loop every weapon gets unless it says otherwise.
    /// </summary>
    /// <remarks>
    ///     A stored array rather than a fresh one per call: this is read through
    ///     <see cref="SwordProfile.Combo" /> on every tick of every swing, by every weapon that did
    ///     not name its own pattern. Treat it as immutable -- it is shared by all of them.
    /// </remarks>
    public static SwingStep[] Alternating { get; } = [Down, Up];
}
