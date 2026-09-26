using Terraria.Audio;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Projectiles;

/// <summary>
///     The trail a chemical carrier sheds while it is still in flight.
/// </summary>
/// <remarks>
///     Separate from <see cref="BurstProfile" /> because it answers a different question. The burst
///     is what the payload does when the container fails; the trail is what leaks out of it on the
///     way, and it is what tells the player where the thing they threw actually is. A flask with no
///     trail is a sprite tumbling through the air; a flask with one is visibly leaking.
/// </remarks>
public readonly record struct TrailProfile
{
    public float NodeRadius { get; init; }

    public float NodeLife { get; init; }

    /// <summary>Speed the shed node is given along <see cref="Along" />.</summary>
    public FloatRange Speed { get; init; }

    /// <summary>Random velocity added on top, per axis.</summary>
    public Vector2 Jitter { get; init; }

    /// <summary>
    ///     Which way the trail is thrown: +1 along the carrier's heading, -1 against it.
    /// </summary>
    /// <remarks>
    ///     The two readings a leak can have. Against the heading is something being left behind --
    ///     a wick, a dribble, a scent trail. Along it is something being forced out under pressure,
    ///     which is what a flask of thermite or of pressurised mist should look like, and which puts
    ///     the payload ahead of the throw where it arrives before the glass does.
    /// </remarks>
    public float Along { get; init; }

    /// <summary>Ticks between shed nodes. 1 sheds every tick.</summary>
    public int Interval { get; init; }

    /// <summary>A thin dribble left behind the carrier.</summary>
    public static TrailProfile Shed => new()
    {
        NodeRadius = 4f,
        NodeLife = 40f,
        Speed = new FloatRange(0.4f, 1.2f),
        Jitter = new Vector2(0.5f, 0.5f),
        Along = -1f,
        Interval = 1,
    };

    /// <summary>A payload venting forward, out ahead of the carrier.</summary>
    public static TrailProfile Vented => Shed with
    {
        Speed = new FloatRange(2f, 9f),
        Jitter = new Vector2(1.5f, 1.5f),
        Along = 1f,
    };
}

/// <summary>
///     A thrown container of chemistry: it leaks on the way, and it opens when it stops.
/// </summary>
/// <remarks>
///     <para>
///         Every flask in the mod was two classes and about a hundred and forty lines, of which
///         maybe fifteen were about that particular chemical. The rest was the same throw, the same
///         normal capture, the same sound-gore-splash death, and the same fan of invisible carrier
///         projectiles whose only job was to spawn gas nodes on a timer. This is that shared body,
///         once.
///     </para>
///     <para>
///         The carriers are gone entirely. What used to be eighteen to twenty-eight
///         <see cref="ModProjectile" />s per throw is now the same number of
///         <see cref="GasEmitter" />s inside the layer's own list -- see <see cref="GasEmitter" />
///         for why that trade is worth making. A subclass that wants a shatter this does not
///         describe overrides <see cref="Shatter" /> and calls whatever it likes; a subclass that
///         only wants different numbers overrides <see cref="Burst" /> and stops there.
///     </para>
/// </remarks>
public abstract class ChemicalProjectile : ModProjectile
{
    /// <summary>The layer this carrier's payload lives in.</summary>
    /// <remarks>
    ///     Resolved per access rather than cached, because <see cref="FieldSystem" /> owns the layer
    ///     and can be torn down and rebuilt underneath a projectile that outlives a reload.
    /// </remarks>
    protected abstract GasField? Field { get; }

    /// <summary>What leaks out on the way. Null for a carrier that is sealed until it breaks.</summary>
    protected virtual TrailProfile? Trail => TrailProfile.Shed;

    /// <summary>What happens when it stops existing.</summary>
    protected virtual BurstProfile Burst => BurstProfile.FlaskShatter;

    /// <summary>How much of the flask's damage each arm of the burst carries.</summary>
    /// <remarks>
    ///     Read off <see cref="BurstProfile.DamageShare" /> unless a weapon has a reason to differ,
    ///     which is stated as an override rather than by editing a shared preset.
    /// </remarks>
    protected virtual float BurstDamageShare => Burst.DamageShare;

    /// <summary>The chemical release this carrier belongs to. Null on clients that did not spawn it.</summary>
    protected ChemicalPayload? Payload => ChemicalHandler.PayloadOf(Projectile);

    /// <summary>The surface the carrier hit, or zero if it expired in open air.</summary>
    protected Vector2 ImpactNormal { get; set; }

    /// <summary>Whether it broke against something rather than simply running out of time.</summary>
    protected bool Grounded => ImpactNormal != Vector2.Zero;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 18;
        Projectile.friendly = true;
        Projectile.DamageType = ModContent.GetInstance<ChemicalDamageClass>();
        Projectile.penetrate = 1;
        Projectile.timeLeft = 600;
        Projectile.aiStyle = ProjAIStyleID.ThrownProjectile;
    }

    public override void PostAI()
    {
        base.PostAI();

        EmitTrail();
    }

    /// <summary>
    ///     Sheds one node of the payload behind (or ahead of) the carrier.
    /// </summary>
    /// <remarks>
    ///     Driven off the projectile's own age rather than a field, so it survives the struct being
    ///     reconstructed and needs no state of its own.
    /// </remarks>
    protected virtual void EmitTrail()
    {
        if (Trail is not { } trail || Field is not { } field)
            return;

        int interval = Math.Max(trail.Interval, 1);

        if (interval > 1 && Projectile.timeLeft % interval != 0)
            return;

        Vector2 heading = Projectile.velocity.SafeNormalize(Vector2.Zero) * trail.Along;

        Vector2 drift = heading * trail.Speed.Roll()
                      + Main.rand.NextVector2Circular(trail.Jitter.X, trail.Jitter.Y);

        field.SpawnNode(Projectile.Center, drift, trail.NodeRadius, trail.NodeLife, owner: Projectile.owner, payload: Payload);
    }

    /// <summary>
    ///     Remembers which way the surface faced, then lets the carrier break.
    /// </summary>
    /// <remarks>
    ///     The normal is worked out from which velocity components the collision zeroed, which is
    ///     the only signal available at this point and is exact for the axis-aligned tiles that
    ///     produced it. It is the difference between a splash that comes off a wall and one that
    ///     sprays back the way the throw came.
    /// </remarks>
    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        var normal = new Vector2(
            MathF.Abs(Projectile.velocity.X - oldVelocity.X) > 0.01f ? -MathF.Sign(oldVelocity.X) : 0f,
            MathF.Abs(Projectile.velocity.Y - oldVelocity.Y) > 0.01f ? -MathF.Sign(oldVelocity.Y) : 0f);

        ImpactNormal = normal.SafeNormalize(Vector2.Zero);

        return true;
    }

    public override void OnKill(int timeLeft)
    {
        PlayShatter();
        Debris();

        Collision.HitTiles(
            Projectile.position + Projectile.velocity, Projectile.velocity,
            Projectile.width, Projectile.height);

        Shatter();
    }

    /// <summary>
    ///     Opens the payload into the layer.
    /// </summary>
    /// <remarks>
    ///     Runs on every client rather than only the thrower's, which is the reverse of what the
    ///     flasks used to do and is the correct way round now. A burst of real projectiles was
    ///     spawned once and replicated by the engine; a burst of emitters is not replicated at all,
    ///     so a client that does not run this sees nothing happen. Damage stays single-sourced
    ///     inside the layer -- see <see cref="GasEmitter.IsLocallyOwned" />.
    /// </remarks>
    protected virtual void Shatter()
    {
        if (Field is not { } field)
            return;

        BurstProfile profile = Burst with { DamageShare = BurstDamageShare };

        field.SpawnBurst(
            in profile,
            Projectile.Center,
            BurstDirection(),
            Grounded,
            Projectile.owner,
            Projectile.damage,
            Projectile.knockBack,
            Payload);
    }

    /// <summary>
    ///     The axis the burst opens along.
    /// </summary>
    /// <remarks>
    ///     A surface hit uses the surface. Anything else -- a timeout, an enemy hit in mid-air --
    ///     splits the difference between where the carrier was going and straight up, so a payload
    ///     that bursts over an enemy's head falls onto it rather than continuing through it.
    /// </remarks>
    protected virtual Vector2 BurstDirection()
        => Grounded
            ? ImpactNormal
            : Vector2.Lerp(Projectile.oldVelocity.SafeNormalize(-Vector2.UnitY), -Vector2.UnitY, 0.5f)
                     .SafeNormalize(-Vector2.UnitY);

    /// <summary>The break itself. Override for a container that is not glass.</summary>
    protected virtual void PlayShatter()
    {
        SoundEngine.PlaySound(Assets.SFX.Items.SmallFlask.Asset with { Volume = 0.7f, MaxInstances = -1, PitchRange = (-0.15f, 0.15f) });
        SoundEngine.PlaySound(SoundID.Item107 with { Volume = 0.65f, PitchRange = (-0.15f, 0.15f) });
    }

    /// <summary>What is left of the container. Override for one that leaves something else.</summary>
    protected virtual void Debris()
    {
        if (Main.dedServ)
            return;

        Gore.NewGore(Projectile.GetSource_Death(), Projectile.Center, -Projectile.oldVelocity * 0.2f, GoreID.ToxicFlask);
        Gore.NewGore(Projectile.GetSource_Death(), Projectile.Center, -Projectile.oldVelocity * 0.2f, GoreID.ToxicFlask2);
    }
}
