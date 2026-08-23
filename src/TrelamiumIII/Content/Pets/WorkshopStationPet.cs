using System.Collections.Generic;
using Terraria.Audio;
using Terraria.DataStructures;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics;

namespace TrelamiumIII.Content.Pets;

/// <summary>
///     How a given station carries itself. Chosen from the tile, so two anvils behave alike and a
///     modded one that reads as an anvil is treated as one.
/// </summary>
public enum StationPersona
{
    /// <summary>An ordinary little hop.</summary>
    Hop,

    /// <summary>Hop variant for anvils and larger stations</summary>
    Stomp,

    /// <summary>Rides on top of a station broad enough to sit on.</summary>
    Perch,
}

/// <summary>
///     One station out of the workshop, up and walking around under its own power. Drawn with the
///     real tile's sprite, animation and all.
/// </summary>
/// <remarks>
///     These are decoration. The crafting adjacency comes off the slots in
///     <see cref="PossessedWorkshop" />, never off whether a pet happens to be alive, so their whole
///     state machine can run locally and out of sync without costing anything.
/// </remarks>
public class WorkshopStationPet : ModProjectile
{
    #region tuning

    /// <summary>
    ///     How close this station has to be before it counts as one you are standing at. Generous
    ///     enough that a station keeping pace always counts.
    /// </summary>
    public const float STATION_RANGE = 180f;

    /// <summary>
    ///     How far it has to get before it stops counting. Deliberately looser than
    ///     <see cref="STATION_RANGE" />: one hard edge would have a station bouncing along beside a
    ///     running player cross it repeatedly, and every crossing rebuilds the recipe list and loses
    ///     whatever the player had selected.
    /// </summary>
    public const float STATION_RANGE_KEEP = 260f;

    private const float GRAVITY = 0.35f;
    private const float MAX_FALL = 10f;

    private const float GROUND_FRICTION = 0.86f;

    private const float ARRIVAL_SLACK = 44f;

    /// <summary>Spread, so five stations do not pile onto one pixel behind the player.</summary>
    private const float FOLLOW_SPREAD = 42f;

    private const float HOP_IMPULSE = 3.2f;
    private const float HOP_LIFT = -5f;
    private const int HOP_INTERVAL = 26;

    private const float STOMP_IMPULSE = 2.6f;
    private const float STOMP_LIFT = -6.6f;
    private const int STOMP_INTERVAL = 46;

    /// <summary>Ticks of crouch before a stomp launches. The wind-up is the whole joke.</summary>
    private const int STOMP_CROUCH = 12;

    /// <summary>Forward lean at the top of a hop, in radians.</summary>
    private const float STOMP_LEAN = 0.22f;
    private const float HOP_LEAN = 0.09f;

    private const float FLY_DISTANCE = 380f;
    private const int FLY_PATIENCE = 40;
    private const float FLY_SPEED = 9f;
    private const float FLY_ACCELERATION = 0.4f;
    private const float LAND_DISTANCE = 140f;

    private const float TELEPORT_RANGE = 2400f;

    /// <summary>How far a perched station sinks into the one holding it, so it sits rather than hovers.</summary>
    private const float PERCH_SETTLE = 2f;

    private static readonly Vector2 LandSquish = new(1.3f, 0.74f);
    private static readonly Vector2 LaunchSquish = new(0.76f, 1.28f);
    private static readonly Vector2 StompLandSquish = new(1.5f, 0.6f);
    private static readonly Vector2 CrouchSquish = new(1.22f, 0.8f);

    #endregion

    /// <summary>Tiles broad and flat enough for something to sit on top of.</summary>
    private static readonly HashSet<int> Surfaces = [TileID.WorkBenches, TileID.Tables, TileID.Tables2];

    /// <summary>Tiles that would rather be carried than hop.</summary>
    private static readonly HashSet<int> Riders = [TileID.Bottles];

    private enum MoveState
    {
        Ground,
        Fly,
    }

    /// <summary>The station item this pet is. Synced, since it decides how the thing looks.</summary>
    public int StationType => (int)Projectile.ai[0];

    /// <summary>Which slot it came out of, so it knows when it has been swapped away.</summary>
    public int Slot => (int)Projectile.ai[1];

    // Never drawn. Instead, PreDraw hands over to the tile sheet.
    public override string Texture => AssetReferences.Content.Pets.PossessedWorkshop.KEY;

    public Player Owner => Main.player[Projectile.owner];

    private TileObjectSprite? sprite;

    private StationPersona persona;

    private MoveState state;

    private int facing = 1;

    private bool grounded;

    private bool blocked;

    private float stepSpeed = 1f;

    private int hopTimer;

    private int patience;

    private float followOffset;

    private float lean;

    private Vector2 squish = Vector2.One;

    public override void SetStaticDefaults() => Main.projPet[Type] = true;

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 16;

        Projectile.friendly = true;
        Projectile.penetrate = -1;
        Projectile.timeLeft = 2;

        Projectile.tileCollide = false;
        Projectile.aiStyle = -1;
        Projectile.netImportant = true;
    }

    public override bool? CanCutTiles() => false;

    public override void OnSpawn(IEntitySource source)
        => followOffset = Main.rand.NextFloat(-FOLLOW_SPREAD, FOLLOW_SPREAD);

    public override void AI()
    {
        Player owner = Owner;

        if (!Resolve(owner))
            return;

        Projectile.timeLeft = 2;

        if (persona == StationPersona.Perch && TryPerch())
            return;

        switch (state)
        {
            case MoveState.Fly:
                UpdateFly(owner);
                break;

            default:
                UpdateGround(owner);
                break;
        }

        Relax();
    }

    /// <summary>
    ///     Works out what this pet is on the first tick, and checks every tick after that it is still
    ///     something the workshop is carrying.
    /// </summary>
    private bool Resolve(Player owner)
    {
        // The apparition is what is carrying these. Without it they have nothing to be. Counted
        // rather than found: every station asks this every tick, and none of them need the instance.
        if (owner.ownedProjectileCounts[ModContent.ProjectileType<PossessedWorkshopPet>()] <= 0)
        {
            Projectile.Kill();

            return false;
        }

        // Only the owner decides a pet is stale. A remote client whose copy of the workshop has not
        // caught up yet would otherwise kill a pet everyone else can still see.
        if (Main.myPlayer == Projectile.owner && !StillCarried(owner))
        {
            Projectile.Kill();

            return false;
        }

        if (sprite is not null)
            return true;

        var station = new Item(StationType);

        sprite = TileObjectSprite.For(station);

        if (sprite is null)
        {
            Projectile.Kill();

            return false;
        }

        persona = PersonaFor(sprite.TileType);

        // The hitbox is the object's real footprint, so a furnace takes up a furnace's worth of room.
        Vector2 centre = Projectile.Center;

        Projectile.width = sprite.Width;
        Projectile.height = sprite.Height;
        Projectile.Center = centre;

        return true;
    }

    private bool StillCarried(Player owner)
    {
        if (owner.GetModPlayer<WorkshopPlayer>().ActiveWorkshop?.ModItem is not PossessedWorkshop workshop)
            return false;

        return Slot >= 0 && Slot < workshop.Stations.Length && workshop.Stations[Slot].type == StationType;
    }

    private static StationPersona PersonaFor(int tileType)
    {
        if (Riders.Contains(tileType))
            return StationPersona.Perch;

        return IsAnvil(tileType) ? StationPersona.Stomp : StationPersona.Hop;
    }

    /// <summary>
    ///     Anvils of any make, including modded ones, which declare themselves by standing in for the
    ///     vanilla anvil rather than by being it.
    /// </summary>
    private static bool IsAnvil(int tileType)
    {
        if (tileType is TileID.Anvils or TileID.MythrilAnvil)
            return true;

        if (TileLoader.GetTile(tileType) is not { AdjTiles: { Length: > 0 } adjacent })
            return false;

        foreach (int other in adjacent)
        {
            if (other is TileID.Anvils or TileID.MythrilAnvil)
                return true;
        }

        return false;
    }

    /// <summary>
    ///     Rides whichever sibling is broad enough to sit on. A bottle and a workbench that turn up
    ///     together end up travelling together.
    /// </summary>
    private bool TryPerch()
    {
        if (FindSurface() is not { } host || host.ModProjectile is not WorkshopStationPet mount || mount.sprite is null)
            return false;

        Projectile.velocity = Vector2.Zero;

        // Ridden off the host's *drawn* top rather than its hitbox top. The host squashes about its
        // base, so its lid drops as it lands -- against the hitbox the passenger would hang in the
        // air every time the thing underneath it bounced.
        float lid = host.Bottom.Y - mount.sprite.Height * mount.squish.Y;

        // Fanned out a little by slot, so two of the same rider do not stack into one sprite.
        float lateral = (Slot - PossessedWorkshop.SLOT_COUNT * 0.5f) * 4f;

        Projectile.Bottom = new Vector2(host.Center.X + lateral, lid + PERCH_SETTLE);
        Projectile.gfxOffY = host.gfxOffY;

        facing = host.spriteDirection == -1 ? -1 : 1;

        // Borrows the host's squash and lean, so the pair read as one object travelling together
        // rather than a passenger sitting rigid on something bouncing underneath it.
        squish = Vector2.Lerp(squish, mount.squish, 0.4f);
        lean = MathHelper.Lerp(lean, mount.lean, 0.3f);

        Projectile.spriteDirection = facing;

        return true;
    }

    private Projectile? FindSurface()
    {
        for (int i = 0; i < Main.maxProjectiles; i++)
        {
            Projectile other = Main.projectile[i];

            if (!other.active || other.type != Type || other.owner != Projectile.owner || i == Projectile.whoAmI)
                continue;

            if (other.ModProjectile is WorkshopStationPet pet && pet.sprite is not null && Surfaces.Contains(pet.sprite.TileType))
                return other;
        }

        return null;
    }

    private void UpdateGround(Player owner)
    {
        Recall(owner);

        Vector2 goal = owner.Center + new Vector2(followOffset, 0f);

        float delta = goal.X - Projectile.Center.X;

        // Arrival is judged on the horizontal alone. A station stood on the ground is always a good
        // twenty pixels below the player's centre, and against a straight-line distance that gap
        // alone can clear the slack -- so it would hop back and forth over a spot it was already
        // standing on, forever, while the player did nothing.
        float reach = Math.Abs(delta);

        Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + GRAVITY, MAX_FALL);

        if (grounded)
        {
            Projectile.velocity.X *= GROUND_FRICTION;

            if (reach > ARRIVAL_SLACK)
                Advance(Math.Sign(delta));
            else
                hopTimer = 0;
        }

        // Giving up, on the other hand, is judged on the real distance: a station stranded on a
        // ledge overhead is horizontally right there and still cannot walk to you.
        bool struggling = (grounded && blocked) || Projectile.Distance(goal) > FLY_DISTANCE;

        patience = struggling ? patience + 1 : 0;

        if (patience >= FLY_PATIENCE)
        {
            state = MoveState.Fly;
            patience = 0;
        }

        ResolveTerrain();
    }

    /// <summary>Counts down to the next hop, and crouches into it if this station is a stomper.</summary>
    private void Advance(int direction)
    {
        bool stomping = persona == StationPersona.Stomp;
        int interval = stomping ? STOMP_INTERVAL : HOP_INTERVAL;

        hopTimer++;

        if (stomping && hopTimer >= interval - STOMP_CROUCH && hopTimer < interval)
        {
            float crouch = (hopTimer - (interval - STOMP_CROUCH)) / (float)STOMP_CROUCH;

            squish = Vector2.Lerp(squish, CrouchSquish, crouch * 0.4f);
            facing = direction == 0 ? facing : direction;

            return;
        }

        if (hopTimer < interval)
            return;

        hopTimer = 0;

        if (direction != 0)
            facing = direction;

        Projectile.velocity.X = direction * (stomping ? STOMP_IMPULSE : HOP_IMPULSE);
        Projectile.velocity.Y = stomping ? STOMP_LIFT : HOP_LIFT;

        squish = LaunchSquish;

        // Tipped into the direction of travel. With mirroring off the lean is the only thing saying
        // which way the station is going, so every persona gets some of it and stompers get a lot.
        lean = (stomping ? STOMP_LEAN : HOP_LEAN) * facing;

        //SoundEngine.PlaySound(
        //    stomping
        //        ? SoundID.Dig with { Volume = 0.5f, Pitch = -0.6f }
        //        : SoundID.Dig with { Volume = 0.28f, Pitch = 0.6f },
        //    Projectile.Center
        //);
    }

    private void UpdateFly(Player owner)
    {
        Recall(owner);

        Vector2 goal = owner.Center + new Vector2(followOffset, -34f);

        Projectile.velocity = Vector2.Lerp(Projectile.velocity, Projectile.DirectionTo(goal) * FLY_SPEED, FLY_ACCELERATION);

        if (Math.Abs(Projectile.velocity.X) > 0.4f)
            facing = Math.Sign(Projectile.velocity.X);

        lean = MathHelper.Lerp(lean, Projectile.velocity.X * 0.02f, 0.15f);

        if (Projectile.Distance(goal) > LAND_DISTANCE || !HasFloorBelow())
            return;

        state = MoveState.Ground;
        hopTimer = 0;
    }

    private bool HasFloorBelow()
    {
        int tileY = (int)(Projectile.Bottom.Y / 16f);

        // Swept across the whole footprint rather than down the centre column alone. A furnace is
        // three tiles wide, and a platform run it is half-overhanging is still somewhere it can
        // stand -- checking only the middle had wide stations refusing to come down at the end of a
        // walkway.
        int left = (int)(Projectile.Left.X / 16f);
        int right = (int)(Projectile.Right.X / 16f);

        for (int i = 0; i < 8; i++)
        {
            int y = tileY + i;

            if (y < 0 || y >= Main.maxTilesY)
                return false;

            for (int x = left; x <= right; x++)
            {
                if (IsStandable(x, y))
                    return true;
            }
        }

        return false;
    }

    /// <summary>
    ///     Whether a tile is something a station could come down onto.
    /// </summary>
    /// <remarks>
    ///     Deliberately not <see cref="WorldGen.SolidTile(int, int, bool)" />, which excludes
    ///     anything <see cref="Main.tileSolidTop" /> -- platforms, most notably. A pet that had
    ///     given up and taken off would then never find a floor over a platform, so it hovered
    ///     beside the player indefinitely instead of landing and resuming its hop.
    ///     <para>
    ///         <see cref="Collision.TileCollision" /> already stops the fall on platforms, so this
    ///         only ever needed to agree with it about what counts as ground.
    ///     </para>
    /// </remarks>
    private static bool IsStandable(int tileX, int tileY)
    {
        Tile tile = Framing.GetTileSafely(tileX, tileY);

        if (!tile.HasUnactuatedTile)
            return false;

        return Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType];
    }

    private void Recall(Player owner)
    {
        if (Projectile.Distance(owner.Center) < TELEPORT_RANGE)
            return;

        Projectile.Center = owner.Center;
        Projectile.velocity = Vector2.Zero;
    }

    private void Relax()
    {
        bool landed = grounded && Projectile.velocity.Y >= 0f && squish.Y > 1f;

        if (landed)
        {
            squish = persona == StationPersona.Stomp ? StompLandSquish : LandSquish;
            lean = 0f;

            if (persona == StationPersona.Stomp)
                Thud();
        }

        squish = Vector2.Lerp(squish, Vector2.One, 0.18f);
        lean = MathHelper.Lerp(lean, 0f, 0.12f);

        Projectile.spriteDirection = facing;
    }

    /// <summary>The weight behind a stomp landing: a puff of dust and a low knock.</summary>
    private void Thud()
    {
        // SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.7f, Pitch = -0.8f }, Projectile.Center);

        if (Main.dedServ)
            return;

        for (int i = 0; i < 6; i++)
        {
            Dust dust = Dust.NewDustDirect(
                Projectile.BottomLeft - new Vector2(0f, 4f),
                Projectile.width, 6,
                DustID.Dirt,
                Main.rand.NextFloat(-1.6f, 1.6f),
                Main.rand.NextFloat(-2.2f, -0.6f),
                Scale: Main.rand.NextFloat(0.8f, 1.2f)
            );

            dust.velocity *= 0.7f;
        }
    }

    private void ResolveTerrain()
    {
        if (Projectile.gfxOffY > 0f)
            Projectile.gfxOffY = Math.Max(Projectile.gfxOffY - stepSpeed, 0f);
        else if (Projectile.gfxOffY < 0f)
            Projectile.gfxOffY = Math.Min(Projectile.gfxOffY + stepSpeed, 0f);

        Vector2 position = Projectile.position;
        Vector2 velocity = Projectile.velocity;

        Collision.StepUp(ref position, ref velocity, Projectile.width, Projectile.height, ref stepSpeed, ref Projectile.gfxOffY);

        Projectile.position = position;
        Projectile.velocity = velocity;

        Vector2 resolved = Collision.TileCollision(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);

        blocked = Projectile.velocity.X != 0f && resolved.X == 0f;
        grounded = Projectile.velocity.Y > 0f && resolved.Y == 0f;

        Projectile.velocity = resolved;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (sprite is null)
            return false;

        // Origin on its base, so the squash presses it into the ground and the lean pivots it about
        // where it is standing rather than about its middle.
        var origin = new Vector2(sprite.Width * 0.5f, sprite.Height);

        Vector2 position = Projectile.Bottom + new Vector2(0f, Projectile.gfxOffY);

        // Never mirrored -- see TileObjectSprite.Draw. Which way it is going reads off the lean.
        sprite.Draw(position, lightColor, lean, origin, squish * Projectile.scale);

        return false;
    }
}
