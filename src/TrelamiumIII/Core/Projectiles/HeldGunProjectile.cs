using Terraria.DataStructures;

namespace TrelamiumIII.Core.Projectiles;

/// <summary>
///     Base for a gun that exists as a projectile in the player's hands rather than as an item
///     animation: it tracks the cursor, fires on its own cadence, recoils, and drives the arm.
/// </summary>
/// <remarks>
///     <para>
///         The reason a gun is a projectile at all is that Terraria's item use styles cannot do any
///         of this. <c>ItemUseStyleID.Shoot</c> holds a sprite at a fixed angle for the length of
///         the animation and has no notion of the weapon moving independently of the arm, so recoil
///         has to be faked by the item's <c>HoldStyle</c> or not done. Spawning a projectile that
///         pins itself to the holder and paints the weapon itself buys a full transform to play
///         with -- position, rotation and origin all animatable per frame, and a composite arm
///         driven from the same clock.
///     </para>
///     <para>
///         What a subclass supplies is the shot and the numbers: a <see cref="GunShotProfile" />
///         describing how the weapon moves, and a <see cref="Fire" /> body spawning whatever comes
///         out of the barrel. Everything between those -- the aim, the cadence, the two-phase
///         recoil, the arm, the draw, the netcode -- is here and identical across weapons, which is
///         the point of having a base at all.
///     </para>
///     <para>
///         Aim is carried in <see cref="Entity.velocity" /> rather than an ai slot, and the reason
///         is netcode. Velocity is part of the standard projectile sync, so remote clients get the
///         aim for free and every one of them draws the weapon pointing where its holder is
///         actually pointing it. The temptation is to read <see cref="Main.MouseWorld" /> in the
///         draw and animation code, which works perfectly on the machine holding the mouse and has
///         every other player's gun tracking *your* cursor. Nothing here touches the mouse outside
///         the <see cref="Main.myPlayer" /> branch.
///     </para>
/// </remarks>
public abstract class HeldGunProjectile : ModProjectile
{
    /// <summary>
    ///     Cosine of the angle the aim may drift before the change is worth a packet.
    /// </summary>
    /// <remarks>
    ///     Syncing every tick, which is the obvious thing to do and what this replaced, is a packet
    ///     per frame per player holding a gun. A cursor that has not meaningfully moved does not
    ///     need announcing, and a cursor that has will cross this within a frame or two.
    /// </remarks>
    private const float AIM_SYNC_TOLERANCE = 0.9995f;

    /// <summary>How the weapon moves when it fires. See <see cref="GunShotProfile" />.</summary>
    protected abstract GunShotProfile Profile { get; }

    /// <summary>
    ///     Whether releasing the mouse button puts the weapon away.
    /// </summary>
    /// <remarks>
    ///     True wants <c>Item.channel</c> on the item. False leaves the weapon out until something
    ///     else kills it, which is what a reload or a wind-down animation needs.
    /// </remarks>
    protected virtual bool HoldToKeep => true;

    /// <summary>Ticks the weapon stays out after the last shot before it may be put away.</summary>
    protected virtual int Cooldown => 0;

    /// <summary>Ticks elapsed since the last shot. Drives every part of the recoil animation.</summary>
    protected int TicksSinceShot
    {
        get => (int)Projectile.ai[0];
        set => Projectile.ai[0] = value;
    }

    /// <summary>Recoil, 0 at rest and 1 at peak.</summary>
    protected float Recoil => Profile.RecoilAt(TicksSinceShot);

    /// <summary>Unit vector from the holder toward whatever they are aiming at.</summary>
    protected Vector2 Aim { get; private set; } = Vector2.UnitX;

    /// <summary>
    ///     "Up" relative to the barrel, whichever way the weapon is facing.
    /// </summary>
    /// <remarks>
    ///     The naive perpendicular flips to "down" the moment the holder turns around, so a rise
    ///     becomes a dip on one side of the screen and nobody can work out why the recoil looks
    ///     wrong only when walking left.
    /// </remarks>
    protected Vector2 BarrelUp
    {
        get
        {
            var perpendicular = new Vector2(Aim.Y, -Aim.X);

            return Projectile.spriteDirection == 1 ? perpendicular : -perpendicular;
        }
    }

    /// <summary>Displacement of the weapon body from where it would rest, this frame.</summary>
    protected Vector2 RecoilOffset
    {
        get
        {
            float recoil = Recoil;

            return -Aim * (Profile.Kick * recoil) + BarrelUp * (Profile.Rise * recoil);
        }
    }

    /// <summary>Centre of the weapon body in world space, recoil included.</summary>
    protected Vector2 BodyCenter => Projectile.Center + Aim * Profile.HoldDistance + RecoilOffset;

    /// <summary>
    ///     Where the shot comes out, pulled back to the holder if geometry is in the way.
    /// </summary>
    /// <remarks>
    ///     The clip is what stops a player pressed against a wall from firing through it. Without
    ///     it the muzzle is unconditionally a barrel's length ahead, which for a long weapon is
    ///     comfortably on the far side of a one-tile partition.
    /// </remarks>
    protected Vector2 Muzzle
    {
        get
        {
            Vector2 origin = Projectile.Center;
            Vector2 muzzle = origin + Aim * Profile.MuzzleLength;

            return Collision.CanHit(origin, 0, 0, muzzle, 0, 0) ? muzzle : origin;
        }
    }

    protected Player Owner => Main.player[Projectile.owner];

    /// <summary>
    ///     Sensible defaults for a held weapon. Override and call base to adjust.
    /// </summary>
    /// <remarks>
    ///     It deals no damage itself and collides with nothing: it is a sprite with a transform.
    ///     Whatever it fires is what hurts things.
    /// </remarks>
    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 32;
        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.tileCollide = false;
        Projectile.ignoreWater = true;
        Projectile.ownerHitCheck = true;
        Projectile.netImportant = true;
        Projectile.DamageType = DamageClass.Ranged;
    }

    public override bool? CanDamage() => false;

    /// <summary>Ready to fire on the frame it appears, rather than after a phantom first cycle.</summary>
    public override void OnSpawn(IEntitySource source) => TicksSinceShot = Profile.UseSpeed;

    /// <summary>
    ///     Runs the whole weapon and suppresses the default projectile AI.
    /// </summary>
    /// <remarks>
    ///     Returning false is deliberate: the base AI would move the projectile by its velocity,
    ///     and velocity here is the aim vector, not a speed. The weapon is positioned by hand every
    ///     tick instead.
    /// </remarks>
    public override bool PreAI()
    {
        Player owner = Owner;

        if (!owner.active || owner.dead)
        {
            Projectile.Kill();

            return false;
        }

        UpdateAim(owner);

        Projectile.Center = owner.Center;
        Projectile.timeLeft = 2;

        if (Main.myPlayer == Projectile.owner)
            UpdateCycle(owner);
        else if (TicksSinceShot < Profile.UseSpeed)
            TicksSinceShot++;

        HoldWeapon(owner);
        WhileHeld(owner);

        return false;
    }

    /// <summary>
    ///     Points the weapon, and tells the other clients when the answer has changed enough to
    ///     matter.
    /// </summary>
    private void UpdateAim(Player owner)
    {
        if (Main.myPlayer == Projectile.owner)
        {
            Vector2 aim = (Main.MouseWorld - owner.Center).SafeNormalize(Vector2.UnitX * owner.direction);

            if (Vector2.Dot(aim, Projectile.velocity) < AIM_SYNC_TOLERANCE)
                Projectile.netUpdate = true;

            Projectile.velocity = aim;
        }

        Aim = Projectile.velocity.SafeNormalize(Vector2.UnitX * owner.direction);

        Projectile.direction = Projectile.spriteDirection = Aim.X >= 0f ? 1 : -1;
        Projectile.rotation = Aim.ToRotation() - Profile.Climb * Recoil * Projectile.spriteDirection;
    }

    /// <summary>
    ///     Advances the firing cycle. Owner-side only -- shots are spawned once and synced.
    /// </summary>
    private void UpdateCycle(Player owner)
    {
        if (TicksSinceShot < Profile.UseSpeed)
        {
            TicksSinceShot++;

            return;
        }

        bool holding = !HoldToKeep || owner.channel;

        // Frozen, stoned or webbed keeps the weapon out but stops the cycle, which is what vanilla
        // does to an item use. Killing it instead would make every crowd-control effect also
        // disarm the player until they clicked again.
        if (owner.CCed)
            return;

        if (holding && CanFire(owner))
        {
            Fire(owner, Aim, Muzzle);

            TicksSinceShot = 0;
            Projectile.netUpdate = true;

            return;
        }

        // Out of ammo mid-hold, or the button came up. Either way the cycle has nothing left to do,
        // so the only question is whether the weapon stays out -- and a cooldown past the end of
        // the cycle is what keeps a burst from snapping the sprite away the instant it runs dry.
        if (!holding && TicksSinceShot >= Profile.UseSpeed + Cooldown)
            Projectile.Kill();
        else
            TicksSinceShot++;
    }

    /// <summary>
    ///     Pins the weapon to the holder: turns them, keeps the item animation alive, and drives the
    ///     front arm from the same recoil the body is using.
    /// </summary>
    /// <remarks>
    ///     <c>itemTime</c> and <c>itemAnimation</c> are refreshed every tick rather than set once.
    ///     They are what keeps the player in a "using an item" state; let them run out and the arm
    ///     drops back to its idle pose for a frame in the middle of the hold.
    /// </remarks>
    private void HoldWeapon(Player owner)
    {
        owner.ChangeDir(Projectile.direction);
        owner.heldProj = Projectile.whoAmI;
        owner.itemTime = 2;
        owner.itemAnimation = 2;

        float armAngle = (-Aim).ToRotation()
                         + MathHelper.PiOver2
                         - Profile.ArmKick * Recoil * Projectile.spriteDirection;

        owner.SetCompositeArmFront(true, Player.CompositeArmStretchAmount.Full, armAngle);
    }

    /// <summary>
    ///     Draws the weapon body with the recoil offset applied.
    /// </summary>
    /// <remarks>
    ///     Flipped vertically rather than horizontally when facing left, because the sprite is
    ///     already rotated to the aim: a horizontal flip would mirror the barrel back through the
    ///     holder. This is the same reason vanilla guns do it.
    /// </remarks>
    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = ModContent.Request<Texture2D>(Texture).Value;
        var origin = new Vector2(texture.Width * Profile.Origin.X, texture.Height * Profile.Origin.Y);

        Vector2 drawPosition = BodyCenter - Main.screenPosition + new Vector2(0f, Owner.gfxOffY);
        SpriteEffects effects = Projectile.spriteDirection == 1 ? SpriteEffects.None : SpriteEffects.FlipVertically;

        DrawBehindWeapon(Main.spriteBatch, drawPosition, lightColor);

        Main.EntitySpriteDraw(texture, drawPosition, null, lightColor, Projectile.rotation, origin, Projectile.scale, effects, 0);

        DrawOverWeapon(Main.spriteBatch, drawPosition, lightColor);

        return false;
    }

    /// <summary>Spawns whatever comes out of the barrel, and makes whatever noise it makes.</summary>
    /// <param name="direction">Unit vector along the barrel.</param>
    /// <param name="muzzle">World position of the barrel end, already clipped against geometry.</param>
    /// <remarks>Owner-side only, so this is the place to consume ammo.</remarks>
    protected abstract void Fire(Player owner, Vector2 direction, Vector2 muzzle);

    /// <summary>Whether the weapon may fire this cycle -- ammo, resources, a charge state.</summary>
    /// <remarks>Answering false holds the cycle rather than ending it; the weapon stays out.</remarks>
    protected virtual bool CanFire(Player owner) => true;

    /// <summary>Per-tick hook for anything the weapon does while it is out: idle glow, dust, hum.</summary>
    /// <remarks>Runs on every client, so guard anything that should only happen once.</remarks>
    protected virtual void WhileHeld(Player owner) { }

    /// <summary>Drawn under the weapon body, in the batch already running.</summary>
    protected virtual void DrawBehindWeapon(SpriteBatch spriteBatch, Vector2 drawPosition, Color lightColor) { }

    /// <inheritdoc cref="DrawBehindWeapon" />
    protected virtual void DrawOverWeapon(SpriteBatch spriteBatch, Vector2 drawPosition, Color lightColor) { }
}
