using ReLogic.Content;

namespace TrelamiumIII.Core.Graphics.Particles;

/// <summary>
///     A short-lived piece of world debris: a texture with physics, a lifetime, and no gameplay
///     weight.
/// </summary>
/// <remarks>
///     <para>
///         This exists instead of <see cref="ModGore" /> because the two are not the same thing.
///         Gore is viscera, and a player who turns gore off in settings is asking not to see
///         viscera -- they are not asking for the shattered remains of a thrown sac to stop
///         existing. Debris routed through here is unaffected by that setting.
///     </para>
///     <para>
///         One class covers a whole family of debris. <see cref="Textures" /> lists a texture per
///         variant, and <see cref="Variant" /> selects between them, so a shattered sac is one
///         class with three chunk sprites rather than three near-identical classes. Anything that
///         differs per variant -- <see cref="Size" />, <see cref="Bounce" /> -- can just switch on
///         <see cref="Variant" />, which is assigned before <see cref="OnSpawn" /> runs.
///     </para>
///     <para>
///         Subclasses are reflection-loaded: <see cref="Particle" /> is an <see cref="ILoadable" />,
///         so tModLoader finds every concrete subclass in the assembly and registers it, variants
///         and all. The autoloaded instance is only a prototype -- it is never simulated. Spawned
///         particles are fresh instances from <see cref="ParticleSystem.Spawn{T}" />.
///     </para>
/// </remarks>
public abstract class Particle : ILoadable
{
    /// <summary>
    ///     One texture path per variant. Defaults to a single texture at the class's own path,
    ///     matching how tModLoader resolves content textures.
    /// </summary>
    public virtual string[] Textures => [GetType().FullName!.Replace('.', '/')];

    /// <summary>Vertical frames within the current variant's sheet.</summary>
    public virtual int FrameCount => 1;

    /// <summary>
    ///     Collision box in pixels. Deliberately separate from the texture size -- debris usually
    ///     wants a footprint tighter than its sprite so it beds into corners instead of hovering.
    /// </summary>
    public virtual Vector2 Size => new(8f);

    public virtual float Gravity => 0.22f;

    /// <summary>Per-tick velocity multiplier. Air resistance.</summary>
    public virtual float Drag => 0.995f;

    /// <summary>Fraction of speed kept when bouncing. 0 = dead stop, 1 = perfectly elastic.</summary>
    public virtual float Bounce => 0.4f;

    /// <summary>Horizontal damping applied while resting on a surface.</summary>
    public virtual float GroundFriction => 0.86f;

    /// <summary>Ticks of fade-out at the end of life.</summary>
    public virtual int FadeOutTicks => 20;

    public virtual bool UseLighting => true;

    public virtual bool TileCollide => true;

    /// <summary>
    ///     Whether walking into this shoves it around. The sweep is one rectangle test per player,
    ///     so it is cheap enough for anything that lives long enough to be walked into.
    /// </summary>
    public virtual bool PlayerCollide => false;

    /// <summary>
    ///     Opt-in: this sweeps every active NPC. Leave off unless the debris genuinely needs it.
    /// </summary>
    public virtual bool NPCCollide => false;

    /// <summary>
    ///     Opt-in: this sweeps every active projectile. Leave off unless the debris genuinely
    ///     needs it.
    /// </summary>
    public virtual bool ProjectileCollide => false;

    /// <summary>Single tile lookup, so it stays on by default.</summary>
    public virtual bool LiquidCollide => true;

    /// <summary>Impulse the player transfers on contact.</summary>
    public virtual float KickForce => 3.2f;

    /// <summary>Per-tick velocity multiplier while submerged.</summary>
    public virtual float LiquidDrag => 0.86f;

    /// <summary>Fraction of gravity cancelled while submerged. 1 = neutrally buoyant.</summary>
    public virtual float Buoyancy => 0.55f;

    public Vector2 Position;
    public Vector2 Velocity;
    public float Rotation;
    public float RotationVelocity;
    public float Scale = 1f;
    public Color Color = Color.White;
    public int TimeLeft;
    public int MaxTimeLeft;
    public int Frame;

    /// <summary>Index into <see cref="Textures" />. Set before <see cref="OnSpawn" />.</summary>
    public int Variant { get; internal set; }

    public bool Active { get; internal set; }

    /// <summary>True while resting on a surface.</summary>
    public bool OnGround { get; private set; }

    public bool InLiquid { get; private set; }

    internal Asset<Texture2D>[]? VariantTextures;

    private int kickCooldown;
    private int liquidType;

    public Rectangle Hitbox => new(
        (int)(Position.X - Size.X * 0.5f),
        (int)(Position.Y - Size.Y * 0.5f),
        (int)Size.X,
        (int)Size.Y
    );

    /// <summary>0 at spawn, 1 at death.</summary>
    public float Progress => MaxTimeLeft <= 0 ? 1f : 1f - TimeLeft / (float)MaxTimeLeft;

    public float Opacity => FadeOutTicks <= 0 ? 1f : Math.Min(TimeLeft / (float)FadeOutTicks, 1f);

    public void Kill() => TimeLeft = 0;

    /// <summary>Called once after the particle is placed, before its first update.</summary>
    public virtual void OnSpawn() { }

    /// <summary>Per-tick hook, run after physics.</summary>
    public virtual void Update() { }

    /// <summary><paramref name="oldVelocity" /> is the velocity from before the surface stopped it.</summary>
    public virtual void OnTileCollide(Vector2 oldVelocity) { }

    /// <summary>Fired after the kick impulse has been applied.</summary>
    public virtual void OnPlayerCollide(Player player) { }

    /// <summary>Requires <see cref="NPCCollide" />. No default physics -- the hook is the behaviour.</summary>
    public virtual void OnNPCCollide(NPC npc) { }

    /// <summary>Requires <see cref="ProjectileCollide" />. No default physics -- the hook is the behaviour.</summary>
    public virtual void OnProjectileCollide(Projectile projectile) { }

    /// <summary><paramref name="liquid" /> is a <see cref="LiquidID" />.</summary>
    public virtual void OnEnterLiquid(int liquid) { }

    public virtual void OnExitLiquid(int liquid) { }

    /// <summary>Return false to take over drawing entirely.</summary>
    public virtual bool PreDraw(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, Color lightColor) => true;

    internal void Tick()
    {
        if (LiquidCollide)
            ResolveLiquid();

        Velocity.Y += InLiquid ? Gravity * (1f - Buoyancy) : Gravity;
        Velocity *= InLiquid ? LiquidDrag : Drag;

        if (TileCollide)
            ResolveTileCollision();
        else
            OnGround = false;

        if (PlayerCollide)
            ResolvePlayerCollision();

        if (NPCCollide)
            ResolveNPCCollision();

        if (ProjectileCollide)
            ResolveProjectileCollision();

        Position += Velocity;
        Rotation += RotationVelocity;

        Update();

        if (--TimeLeft <= 0)
            Active = false;
    }

    private void ResolveLiquid()
    {
        Tile tile = Framing.GetTileSafely(Position.ToTileCoordinates());
        bool submerged = tile.LiquidAmount > 0;

        if (submerged == InLiquid)
            return;

        InLiquid = submerged;

        if (submerged)
        {
            liquidType = tile.LiquidType;
            OnEnterLiquid(liquidType);
        }
        else
            OnExitLiquid(liquidType);
    }

    private void ResolveTileCollision()
    {
        // Below this a bounce is indistinguishable from jitter, so the chunk is treated as landed.
        const float MIN_BOUNCE_SPEED = 1.6f;

        Vector2 topLeft = Position - Size * 0.5f;
        Vector2 oldVelocity = Velocity;

        Velocity = Collision.TileCollision(topLeft, Velocity, (int)Size.X, (int)Size.Y, false, false, 1);

        bool hit = false;

        if (Velocity.X != oldVelocity.X)
        {
            Velocity.X = -oldVelocity.X * Bounce;
            RotationVelocity = -RotationVelocity * Bounce;
            hit = true;
        }

        if (Velocity.Y != oldVelocity.Y)
        {
            hit = true;

            if (Math.Abs(oldVelocity.Y) > MIN_BOUNCE_SPEED)
                Velocity.Y = -oldVelocity.Y * Bounce;
            else
            {
                Velocity.Y = 0f;
                OnGround = true;
            }

            Velocity.X *= GroundFriction;
        }
        else if (OnGround)
        {
            // Walked off the edge of whatever it was sitting on.
            OnGround = Velocity.Y == 0f;
        }

        if (OnGround)
        {
            // Rolling, not sliding: tying spin to travel means a chunk that skitters along the
            // floor turns as it goes and settles pointing somewhere plausible.
            RotationVelocity = Velocity.X * 0.08f;

            if (Math.Abs(Velocity.X) < 0.05f)
                Velocity.X = 0f;
        }

        if (hit)
            OnTileCollide(oldVelocity);
    }

    private void ResolvePlayerCollision()
    {
        if (kickCooldown > 0)
        {
            kickCooldown--;
            return;
        }

        Rectangle box = Hitbox;

        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player player = Main.player[i];

            if (!player.active || player.dead || !player.Hitbox.Intersects(box))
                continue;

            // Away from the player, falling back on their facing when a chunk is dead centre and
            // the difference vector is degenerate.
            Vector2 away = (Position - player.Center).SafeNormalize(Vector2.UnitX * player.direction);

            Velocity += away * KickForce * 0.5f + player.velocity * 0.55f;
            Velocity.Y -= 1.4f; // a small pop, so it skitters instead of grinding along the floor

            RotationVelocity += Main.rand.NextFloat(-0.2f, 0.2f) + Velocity.X * 0.05f;
            OnGround = false;

            // Without a cooldown, standing on a chunk re-kicks it every tick and it vibrates.
            kickCooldown = 6;

            OnPlayerCollide(player);
            break;
        }
    }

    private void ResolveNPCCollision()
    {
        Rectangle box = Hitbox;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (npc.active && npc.Hitbox.Intersects(box))
                OnNPCCollide(npc);
        }
    }

    private void ResolveProjectileCollision()
    {
        Rectangle box = Hitbox;

        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile projectile = Main.projectile[i];

            if (projectile.active && projectile.Hitbox.Intersects(box))
                OnProjectileCollide(projectile);
        }
    }

    /// <summary>
    ///     A texture to wear in place of the class's own variant sheet.
    /// </summary>
    /// <remarks>
    ///     Assigned after <see cref="ParticleSystem.Spawn{T}" /> returns, for debris whose sprite is
    ///     not known until it spawns -- the pieces of a dead boss wearing the very gore textures the
    ///     base game just threw, rather than a copy of them picked at load time.
    ///     <para>
    ///         Resolved per draw rather than per class, so it bypasses the registry entirely.
    ///         <see cref="FrameCount" /> still applies to it.
    ///     </para>
    /// </remarks>
    public Asset<Texture2D>? TextureOverride { get; set; }

    /// <summary>
    ///     Whether this particle draws into the additive pass. Costs a second batch across the whole
    ///     list, so it is opt-in.
    /// </summary>
    public virtual bool HasGlow => false;

    /// <summary>
    ///     Drawn under <see cref="BlendState.Additive" />, before the body pass, so whatever this
    ///     lays down sits behind the sprite.
    /// </summary>
    /// <remarks>
    ///     Requires <see cref="HasGlow" />. <paramref name="texture" /> is the particle's own
    ///     resolved sprite, which is what makes the vanilla Fallen Star trick available: sample the
    ///     sprite into a bloom by stamping it around itself, instead of needing an authored glow
    ///     texture that would have to be redrawn for every piece.
    /// </remarks>
    public virtual void DrawGlow(SpriteBatch spriteBatch, Texture2D texture, Vector2 drawPosition, float opacity) { }

    /// <summary>The sprite this particle is wearing, or null while nothing has resolved yet.</summary>
    protected Texture2D? ResolveTexture()
    {
        if (TextureOverride is { Value: { } overridden })
            return overridden;

        if (VariantTextures is null)
            return null;

        int index = Math.Clamp(Variant, 0, VariantTextures.Length - 1);

        return VariantTextures[index]?.Value;
    }

    internal void DrawGlowPass(SpriteBatch spriteBatch)
    {
        if (ResolveTexture() is not { } texture)
            return;

        DrawGlow(spriteBatch, texture, Position - Main.screenPosition, Opacity);
    }
    internal void Draw(SpriteBatch spriteBatch)
    {
        if (ResolveTexture() is not { } texture)
            return;

        int frames = Math.Max(FrameCount, 1);
        int frameHeight = texture.Height / frames;

        var source = new Rectangle(0, frameHeight * Math.Clamp(Frame, 0, frames - 1), texture.Width, frameHeight);
        var origin = new Vector2(texture.Width, frameHeight) * 0.5f;

        Color lightColor = UseLighting
            ? Lighting.GetColor(Position.ToTileCoordinates())
            : Color.White;

        Color drawColor = Color.MultiplyRGBA(lightColor) * Opacity;
        Vector2 drawPosition = Position - Main.screenPosition;

        if (!PreDraw(spriteBatch, texture, drawPosition, drawColor))
            return;

        spriteBatch.Draw(texture, drawPosition, source, drawColor, Rotation, origin, Scale, SpriteEffects.None, 0f);
    }

    void ILoadable.Load(Mod mod) => ParticleSystem.Register(this);

    void ILoadable.Unload() { }
}
