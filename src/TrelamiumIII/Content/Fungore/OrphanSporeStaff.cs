using ReLogic.Content;
using Terraria.Audio;
using Terraria.DataStructures;
using Terraria.GameContent;
using TrelamiumIII.Content.Rarities;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Fields.Layers;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Fungore;

public class OrphanSporeStaff : ModItem
{
    public override void SetStaticDefaults()
    {
        ItemID.Sets.GamepadWholeScreenUseRange[Type] = true;
        ItemID.Sets.LockOnIgnoresCollision[Type] = true;

        ItemID.Sets.StaffMinionSlotsRequired[Type] = 1f;
    }

    public override void SetDefaults()
    {
        Item.DamageType = DamageClass.Summon;
        Item.damage = 12;

        Item.knockBack = 4f;
        Item.mana = 10;

        Item.width = Item.height = 22;
        Item.useTime = Item.useAnimation = 20;
        Item.noMelee = true;

        Item.useStyle = ItemUseStyleID.Swing;
        Item.UseSound = SoundID.Item44;
        Item.rare = ModContent.RarityType<MyconidRarity>();

        Item.value = Item.sellPrice(silver: 1, copper: 75);

        Item.buffType = ModContent.BuffType<ShroomsonBuff>();
        Item.shoot = ModContent.ProjectileType<ShroomsonMinion>();
    }

    public override void ModifyShootStats(Player player, ref Vector2 position, ref Vector2 velocity, ref int type, ref int damage, ref float knockback)
    {
        position = Main.MouseWorld;
        player.LimitPointToPlayerReachableArea(ref position);
    }

    public override bool Shoot(Player player, EntitySource_ItemUse_WithAmmo source, Vector2 position, Vector2 velocity, int type, int damage, float knockback)
    {
        player.AddBuff(Item.buffType, 2);

        return true;
    }
}

public class ShroomsonBuff : ModBuff
{
    public override void SetStaticDefaults()
    {
        Main.buffNoSave[Type] = true;
        Main.buffNoTimeDisplay[Type] = true;
    }

    public override void Update(Player player, ref int buffIndex)
    {
        if (player.ownedProjectileCounts[ModContent.ProjectileType<ShroomsonMinion>()] > 0)
        {
            player.buffTime[buffIndex] = 18000;
        }
        else
        {
            player.DelBuff(buffIndex);
            buffIndex--;
        }
    }
}

public class ShroomsonMinion : ModProjectile
{
    public enum ShroomsonState
    {
        Follow,
        PrepAttack,
        Toss,
        SonShroom,
        Dive,
        Burrow,
        Surface,
        Ascend,
    }

    #region tuning

    private const float GRAVITY = 0.4f;
    private const float MAX_FALL = 10f;

    private const float WALK_SPEED = 4.4f;
    private const float WALK_ACCELERATION = 0.28f;
    private const float GROUND_FRICTION = 0.78f;

    /// <summary>
    ///     Top-speed multiplier once far behind; a walk is slower than a player in boots and would never close the gap.
    /// </summary>
    private const float CATCHUP_SPEED = 1.9f;

    private const float CATCHUP_DISTANCE = 200f;

    private const float JUMP_SPEED = 7.4f;

    private const float JUMP_REACH = 48f;

    private const float JUMP_APPROACH = 170f;

    private const float ARRIVAL_SLACK = 40f;

    private const float FOLLOW_SPREAD = 58f;

    private const int FOLLOW_REROLL = 5 * 60;

    private const float BURROW_RANGE = 250f;

    /// <summary>
    ///     Height above him a jump can't clear (a jump reaches about 68 px), so he climbs instead. Without it an owner
    ///     overhead is inside both <see cref="BURROW_RANGE" /> and <see cref="STUCK_SLACK" />, and he'd hop forever.
    /// </summary>
    private const float ASCEND_TRIGGER = 80f;

    /// <summary>Furthest to the side the owner may be for this to read as "above" and not "over there".</summary>
    private const float ASCEND_REACH = 280f;

    private const float ASCEND_SPEED = 8.5f;

    /// <summary>Horizontal steering while climbing.</summary>
    private const float ASCEND_DRIFT = 5f;

    /// <summary>Backstop, in case the owner climbs faster than he does.</summary>
    private const int ASCEND_TIMEOUT = 150;

    /// <summary>Frames before he may climb again, so he doesn't hover when he falls straight back out of range.</summary>
    private const int ASCEND_COOLDOWN = 45;

    /// <summary>Backstop for a Shroomson that is not even on the ground to dig into any more.</summary>
    private const float TELEPORT_RANGE = 2800f;

    private const float SEARCH_RANGE = 760f;

    private const float TOSS_RANGE = 430f;

    private const int WIND_TICKS = 32;

    private const int FLASH_TICKS = 9;

    private const int TOSS_TICKS = 16;

    private const int RELEASE_TICK = 5;

    private const int TOSS_COOLDOWN = 27;

    private const float THROW_SPEED = 16f;

    private const int GIFT_COOLDOWN_MIN = 40 * 60;
    private const int GIFT_COOLDOWN_MAX = 75 * 60;

    private const int GIFT_RETRY = 20 * 60;

    private const int GIFT_HOLD = 12 * 60;

    private const float GIFT_REACH = 46f;

    private const float GIFT_RANGE = 420f;

    /// <summary>Ticks of getting nowhere before he stops trying to walk it and digs instead.</summary>
    private const int STUCK_TIMEOUT = 100;

    /// <summary>Ground gained toward the destination that counts as progress and clears the count.</summary>
    private const float STUCK_PROGRESS = 24f;

    /// <summary>Inside this he is close enough that being stuck does not matter.</summary>
    private const float STUCK_SLACK = 260f;

    private const int DIVE_TICKS = 22;
    private const int SURFACE_TICKS = 20;

    /// <summary>Cap on the tunnel, so a Shroomson under a bottomless pit still comes back up.</summary>
    private const int BURROW_TIMEOUT = 5 * 60;

    private const float BURROW_SPEED = 13f;

    /// <summary>How far below the player's feet the tunnel runs.</summary>
    private const float BURROW_DEPTH = 64f;

    /// <summary>Horizontal distance to the player that ends the tunnel.</summary>
    private const float SURFACE_REACH = 36f;

    private const float SURFACE_HOP = 4.5f;

    /// <summary>Tiles searched for a floor to come up through, or a surface to kick dirt out of.</summary>
    private const int TILE_SEARCH = 40;

    private static readonly Vector2 DiveSquish = new(1.55f, 0.15f);

    private static readonly Vector2 SurfaceSquish = new(0.62f, 1.45f);

    /// <summary>The body sits a pixel left of the 42-wide frame's centre, so the origin is nudged onto it.</summary>
    private const float BODY_NUDGE_X = 1f;

    private const int FRAME_IDLE = 0;
    private const int FRAME_WALK_FIRST = 1;
    private const int FRAME_WALK_LAST = 3;
    private const int FRAME_WIND_START = 4;
    private const int FRAME_WIND_PEAK = 5;
    private const int FRAME_HOLD = 6;
    private const int FRAME_RELEASE = 7;
    private const int FRAME_AIRBORNE = 8;

    /// <summary>Pixels of ground covered per step of the walk cycle.</summary>
    private const float WALK_FRAME_PIXELS = 26f;

    /// <summary>Where the mushroom sits in each frame; only the toss frames matter.</summary>
    private static readonly Vector2[] HandOffsets =
    [
        new(6f, 4f),     // 0 | idle
        new(6f, 4f),     // 1 | walk
        new(6f, 4f),     // 2 | walk
        new(6f, 4f),     // 3 | walk
        new(-8f, -8f),   // 4 | hand coming up behind the cap
        new(-10f, -10f), // 5 | cocked
        new(13f, -5f),   // 6 | presented
        new(15f, -3f),   // 7 | released
        new(0f, -10f),   // 8 | airborne
    ];

    #endregion

    private static Asset<Texture2D>? mushroomTexture;

    private static Asset<Texture2D>? maskTexture;

    public Player Owner => Main.player[Projectile.owner];

    public ShroomsonState State
    {
        get => (ShroomsonState)(int)Projectile.ai[0];
        private set => Projectile.ai[0] = (int)value;
    }

    public ref float StateTimer => ref Projectile.ai[1];

    /// <summary>Set while the mushroom being grown is a buffed consumable mushroom.</summary>
    private bool GrowingGift
    {
        get => Projectile.ai[2] == 1f;
        set => Projectile.ai[2] = value ? 1f : 0f;
    }

    private int facing = 1;

    private bool grounded;

    private bool blocked;

    private float stepSpeed = 1f;

    private int walkFrameTimer;

    private int tossCooldown;

    private int giftCooldown;

    private int ascendCooldown;

    private Vector2 followOffset;

    private int followRerollTimer;

    /// <summary>Where the throw was aimed. Owner-only, since only the owner spawns the mushroom.</summary>
    private Vector2 aimPoint;

    /// <summary>Per-axis draw scale, driven by the burrow. Cosmetic, so it is never synced.</summary>
    private Vector2 squish = Vector2.One;

    private int stuckTimer;

    /// <summary>Closest he has ever got to where he is currently trying to be.</summary>
    private float stuckBest = float.MaxValue;

    /// <summary>How far through the wind-up the mushroom is, 0 to 1.</summary>
    private float GrowProgress => State == ShroomsonState.PrepAttack
        ? MathHelper.Clamp(StateTimer / WIND_TICKS, 0f, 1f)
        : 0f;

    /// <summary>Strength of the mask flash over the tail of the wind-up, 0 to 1.</summary>
    private float FlashProgress => State != ShroomsonState.PrepAttack
        ? 0f
        : MathHelper.Clamp((StateTimer - (WIND_TICKS - FLASH_TICKS)) / FLASH_TICKS, 0f, 1f);

    /// <summary>True once the cloud has set and there is an actual mushroom in his hand.</summary>
    private bool HoldingMushroom => State == ShroomsonState.SonShroom
        || (State == ShroomsonState.Toss && StateTimer < RELEASE_TICK);

    /// <summary>Somewhere in the dig. He is not walking, and for part of it he is not even there.</summary>
    private bool IsDigging => State is ShroomsonState.Dive or ShroomsonState.Burrow or ShroomsonState.Surface;

    /// <summary>Where the mushroom sits this frame, in world space.</summary>
    private Vector2 MushroomAnchor
    {
        get
        {
            Vector2 offset = HandOffsets[Math.Clamp(Projectile.frame, 0, HandOffsets.Length - 1)];
            offset.X *= facing;

            // gfxOffY included so the mushroom rides the step-up with the body rather than hanging
            // where the hand would have been.
            return Projectile.Center + offset + new Vector2(0f, Projectile.gfxOffY);
        }
    }

    public override void SetStaticDefaults()
    {
        // Sheet: 42x38, 9 frames. 0 idle, 1-3 walk, 4-7 toss mushroom, 8 airborne.

        Main.projFrames[Type] = 9;
        // This is necessary for right-click targeting
        ProjectileID.Sets.MinionTargettingFeature[Type] = true;

        Main.projPet[Type] = true; // Denotes that this projectile is a pet or minion

        ProjectileID.Sets.MinionSacrificable[Type] = true; // This is needed so your minion can properly spawn when summoned and replaced when other minions are summoned
    }

    public override void Load()
    {
        if (Main.dedServ)
            return;

        mushroomTexture = ModContent.Request<Texture2D>(AssetReferences.Content.Fungore.Mushroom.KEY);
        maskTexture = ModContent.Request<Texture2D>(AssetReferences.Content.Fungore.Mushroom_Mask.KEY);
    }

    public override void Unload()
    {
        mushroomTexture = null;
        maskTexture = null;
    }

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 38;
        Projectile.friendly = true;
        Projectile.minion = true;
        Projectile.minionSlots = 1f;
        Projectile.penetrate = -1;

        // Terrain is walked by hand in ResolveTerrain, so the engine is kept out of it: it would
        // fight the step-up, and a Shroomson being recalled needs to cut straight through geometry.
        Projectile.tileCollide = false;

        Projectile.aiStyle = -1;

        // Minions have to reach players who join after they were summoned.
        Projectile.netImportant = true;
    }

    public override void OnSpawn(IEntitySource source)
    {
        followOffset = new Vector2(Main.rand.NextFloat(-FOLLOW_SPREAD, FOLLOW_SPREAD), 0f);

        // Staggered on spawn so a resummoned Shroomson does not pick up mid-cycle and immediately
        // start rummaging for a gift.
        giftCooldown = Main.rand.Next(GIFT_COOLDOWN_MIN, GIFT_COOLDOWN_MAX);
    }

    public override bool? CanCutTiles() => false;

    /// <summary>Nothing gets hit by a Shroomson who is halfway into the floor.</summary>
    public override bool? CanHitNPC(NPC target) => IsDigging ? false : null;

    private bool CheckActive(Player owner)
    {
        if (owner.dead || !owner.active)
        {
            owner.ClearBuff(ModContent.BuffType<ShroomsonBuff>());

            return false;
        }

        if (owner.HasBuff(ModContent.BuffType<ShroomsonBuff>()))
            Projectile.timeLeft = 2;

        return true;
    }

    public override void AI()
    {
        Player owner = Owner;

        if (!CheckActive(owner))
            return;

        if (tossCooldown > 0)
            tossCooldown--;

        if (giftCooldown > 0)
            giftCooldown--;

        if (ascendCooldown > 0)
            ascendCooldown--;

        NPC? target = FindTarget(owner);

        // Only Follow has anywhere to be; the rest are performed standing still, and a null
        // destination is what tells Move to plant his feet.
        Vector2? destination = null;

        switch (State)
        {
            case ShroomsonState.PrepAttack:
                UpdateWind(owner, target);
                break;

            case ShroomsonState.Toss:
                UpdateToss();
                break;

            case ShroomsonState.SonShroom:
                UpdateOffer(owner, target);
                break;

            case ShroomsonState.Dive:
                UpdateDive();
                break;

            case ShroomsonState.Burrow:
                UpdateBurrow(owner);
                break;

            case ShroomsonState.Surface:
                UpdateSurface();
                break;

            case ShroomsonState.Ascend:
                UpdateAscend(owner, target);
                break;

            default:
                destination = UpdateFollow(owner, target);
                break;
        }

        // Burrowing and climbing both move themselves: no gravity, no terrain, no walking.
        if (State is not (ShroomsonState.Burrow or ShroomsonState.Ascend))
        {
            Move(destination);
            TrackProgress(destination);
        }

        Animate();
    }

    /// <returns>Where the Shroomson wants to be standing, or null if he is happy where he is.</returns>
    private Vector2? UpdateFollow(Player owner, NPC? target)
    {
        Recall(owner);

        // No digging or climbing with a target: both travel to the owner, away from the fight, and the distance trigger
        // fires most while fighting. Unreachable things can wait.
        if (target is null && Main.myPlayer == Projectile.owner)
        {
            // Climbing is tried first: it handles open air overhead, where a dig has no floor to surface through.
            if (ShouldAscend(owner))
            {
                SetState(ShroomsonState.Ascend);

                return null;
            }

            if (ShouldDig(owner))
            {
                SetState(ShroomsonState.Dive);

                return null;
            }
        }

        if (target is not null)
        {
            if (!InThrowingRange(target))
                return target.Center;

            FaceToward(target.Center);

            if (tossCooldown <= 0)
                BeginWind(gift: false, target.Center);

            // Holds the line either way. Walking in during the cooldown would end with a thrower
            // stood in melee range of everything he was throwing at from a distance.
            return null;
        }

        if (TryBeginGift(owner))
            return null;

        // Re-rolled rather than fixed, so a Shroomson trailing you for a while mills about instead
        // of welding itself to one shoulder.
        if (++followRerollTimer >= FOLLOW_REROLL)
        {
            followRerollTimer = 0;
            followOffset = new Vector2(Main.rand.NextFloat(-FOLLOW_SPREAD, FOLLOW_SPREAD), 0f);
        }

        return owner.Center + followOffset;
    }

    private bool InThrowingRange(NPC target)
    {
        if (Projectile.Distance(target.Center) > TOSS_RANGE)
            return false;

        return Collision.CanHitLine(
            Projectile.position, Projectile.width, Projectile.height,
            target.position, target.width, target.height
        );
    }

    /// <summary>Last resort when digging can't help: no ground under him, or left far behind while the player flew.</summary>
    private void Recall(Player owner)
    {
        if (Projectile.Distance(owner.Center) < TELEPORT_RANGE)
            return;

        Projectile.Center = owner.Center;
        Projectile.velocity = Vector2.Zero;
        Projectile.netUpdate = true;
    }

    /// <summary>
    ///     Whether the owner is straight up through open air. Owner-only, since it uses positions remote clients approximate.
    /// </summary>
    private bool ShouldAscend(Player owner)
    {
        if (!grounded || ascendCooldown > 0)
            return false;

        float rise = Projectile.Bottom.Y - owner.Bottom.Y;

        // Above him, by more than he can jump.
        if (rise < ASCEND_TRIGGER)
            return false;

        // Far enough to the side and this is not a climb, it is a walk he has not tried yet.
        if (Math.Abs(owner.Center.X - Projectile.Center.X) > ASCEND_REACH)
            return false;

        return ColumnIsClear(Projectile.Center.X, Projectile.Top.Y, owner.Bottom.Y);
    }

    /// <summary>Whether the vertical run is unobstructed. Platforms don't count, since they're passable from below.</summary>
    private static bool ColumnIsClear(float worldX, float fromY, float toY)
    {
        int tileX = (int)(worldX / 16f);

        int top = (int)(Math.Min(fromY, toY) / 16f);
        int bottom = (int)(Math.Max(fromY, toY) / 16f);

        for (int y = top; y <= bottom; y++)
        {
            if (!WorldGen.InWorld(tileX, y, 2))
                return false;

            Tile tile = Framing.GetTileSafely(tileX, y);

            if (!tile.HasUnactuatedTile)
                continue;

            if (Main.tileSolidTop[tile.TileType])
                continue;

            if (Main.tileSolid[tile.TileType])
                return false;
        }

        return true;
    }

    /// <summary>
    ///     Climbs to the owner while steering. A state rather than a precomputed leap, which breaks when the owner moves.
    /// </summary>
    private void UpdateAscend(Player owner, NPC? target)
    {
        StateTimer++;

        Projectile.velocity = new Vector2(
            MathHelper.Clamp((owner.Bottom.X - Projectile.Center.X) * 0.08f, -ASCEND_DRIFT, ASCEND_DRIFT),
            -ASCEND_SPEED
        );

        if (!Main.dedServ && StateTimer % 4f == 0f)
        {
            SporeField.Trail(
                Projectile.Bottom + Main.rand.NextVector2Circular(6f, 4f),
                new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(0.6f, 1.6f)),
                Main.rand.NextFloat(5f, 9f)
            );
        }

        bool arrived = Projectile.Bottom.Y <= owner.Bottom.Y;
        bool blocked = !ColumnIsClear(Projectile.Center.X, Projectile.Top.Y, owner.Bottom.Y);

        // A target found mid-climb ends it. Whatever he was going up for, there is a fight now.
        if (!arrived && !blocked && target is null && StateTimer < ASCEND_TIMEOUT)
            return;

        // Cooled down on the way out rather than on arrival, so a climb that ended against a ceiling
        // or ran out of time does not immediately start over.
        ascendCooldown = ASCEND_COOLDOWN;

        SetState(ShroomsonState.Follow);
    }

    /// <summary>
    ///     Whether walking has stopped working: far away, or stuck long enough that something is in the way. Owner-only,
    ///     since the stuck count uses positions remote clients approximate.
    /// </summary>
    private bool ShouldDig(Player owner)
    {
        // Nothing to dig into mid-air, and a Shroomson still falling is making progress by default.
        if (!grounded)
            return false;

        return Projectile.Distance(owner.Center) > BURROW_RANGE || stuckTimer >= STUCK_TIMEOUT;
    }

    /// <summary>Tracks whether he's getting anywhere, so an unclimbable obstacle doesn't trap him walking into it.</summary>
    private void TrackProgress(Vector2? destination)
    {
        if (destination is not { } goal)
        {
            ResetProgress();

            return;
        }

        float distance = Projectile.Distance(goal);

        // Close enough that whatever is in the way is not worth tunnelling around.
        if (distance <= STUCK_SLACK)
        {
            ResetProgress();

            return;
        }

        // Measured against his best rather than against last tick, so pacing back and forth in front
        // of an obstacle does not count as getting somewhere.
        if (distance < stuckBest - STUCK_PROGRESS)
        {
            stuckBest = distance;
            stuckTimer = 0;

            return;
        }

        stuckTimer++;
    }

    private void ResetProgress()
    {
        stuckTimer = 0;
        stuckBest = float.MaxValue;
    }

    /// <summary>
    ///     Decides whether to make a gift. Owner-only: the cooldown re-roll is random, and the result syncs through ai[].
    /// </summary>
    private bool TryBeginGift(Player owner)
    {
        if (Main.myPlayer != Projectile.owner || giftCooldown > 0 || !grounded)
            return false;

        if (Projectile.Distance(owner.Center) > GIFT_RANGE)
            return false;

        BeginWind(gift: true, owner.Center);

        return true;
    }

    private void BeginWind(bool gift, Vector2 aim)
    {
        aimPoint = aim;
        GrowingGift = gift;

        SetState(ShroomsonState.PrepAttack);
    }

    private void UpdateWind(Player owner, NPC? target)
    {
        // Keyed off the timer rather than off the transition, so a client that only learned about
        // the wind-up through ai[] still hears it start.
        if (StateTimer == 0f)
            SoundEngine.PlaySound(SoundID.Grass with { Volume = 0.75f, Pitch = 0.45f }, Projectile.Center);

        StateTimer++;

        if (GrowingGift)
            FaceToward(owner.Center);
        else if (target is not null)
        {
            FaceToward(target.Center);

            // Tracked for the whole wind-up, so a target that keeps moving is still led correctly;
            // one that dies mid-grow leaves the throw aimed at where it last stood.
            aimPoint = target.Center;
        }

        if (!Main.dedServ)
            GrowSpores();

        if (StateTimer < WIND_TICKS)
            return;

        Resolve();

        SetState(GrowingGift ? ShroomsonState.SonShroom : ShroomsonState.Toss);
    }

    /// <summary>The cloud drawing itself in around his hand, tighter and thicker as it sets.</summary>
    private void GrowSpores()
    {
        float progress = GrowProgress;

        // Thickens as it goes: a wisp or two at the start, a solid knot by the end.
        int puffs = 1 + (int)(progress * 2f);

        for (int i = 0; i < puffs; i++)
            SporeField.Coalesce(MushroomAnchor, progress);

        Lighting.AddLight(MushroomAnchor, 0.25f * progress, 0.14f * progress, 0.04f * progress);
    }

    /// <summary>The moment the cloud stops being a cloud: one puff outward, and a mushroom in hand.</summary>
    private void Resolve()
    {
        SoundEngine.PlaySound(Assets.SFX.Sporeclot_Splash.Asset with { Volume = 0.55f, Pitch = 0.7f }, Projectile.Center);

        if (Main.dedServ)
            return;

        Vector2 anchor = MushroomAnchor;

        for (int i = 0; i < 7; i++)
            SporeField.Trail(anchor, Main.rand.NextVector2Circular(1.7f, 1.7f), Main.rand.NextFloat(6f, 11f));
    }

    private void UpdateToss()
    {
        StateTimer++;

        if (StateTimer == RELEASE_TICK)
        {
            SoundEngine.PlaySound(SoundID.Item1 with { Volume = 0.6f, Pitch = 0.8f }, Projectile.Center);

            if (Main.myPlayer == Projectile.owner)
                ReleaseMushroom();
        }

        if (StateTimer < TOSS_TICKS)
            return;

        tossCooldown = TOSS_COOLDOWN;
        SetState(ShroomsonState.Follow);
    }

    private void ReleaseMushroom()
    {
        Vector2 origin = MushroomAnchor;

        Projectile.NewProjectile(
            Projectile.GetSource_FromAI(),
            origin,
            LobTo(origin, aimPoint),
            ModContent.ProjectileType<ShroomsonMushroom>(),
            Projectile.damage,
            Projectile.knockBack,
            Projectile.owner
        );
    }

    /// <summary>Launch velocity that lands on <paramref name="aim" /> under the mushroom's gravity.</summary>
    private static Vector2 LobTo(Vector2 from, Vector2 aim)
    {
        Vector2 toTarget = aim - from;

        // The flight a flat throw would take, floored so something standing underfoot does not get
        // hit with a railgun shot instead of a lob.
        float flight = MathHelper.Clamp(toTarget.Length() / THROW_SPEED, 12f, 70f);

        Vector2 velocity = toTarget / flight;

        // Lifted by exactly what the drop over that flight is going to take back out of it.
        velocity.Y -= 0.5f * ShroomsonMushroom.GRAVITY * flight;

        return velocity;
    }

    private void UpdateOffer(Player owner, NPC? target)
    {
        StateTimer++;

        FaceToward(owner.Center);

        if (!Main.dedServ)
            Lighting.AddLight(MushroomAnchor, 0.3f, 0.17f, 0.05f);

        // A fight is not the time. The mushroom keeps.
        if (target is not null || StateTimer >= GIFT_HOLD)
        {
            EndOffer(given: false);
            return;
        }

        // Owner-only: the item is spawned once, by the client that is going to be holding it.
        if (Main.myPlayer == Projectile.owner && Projectile.Distance(owner.Center) <= GIFT_REACH)
            Hand(owner);
    }

    private void Hand(Player owner)
    {
        // The Item overload rather than the type one: the whole point is that the instance carries
        // the gift with it.
        owner.QuickSpawnItem(Projectile.GetSource_FromAI(), SonsMushroom.Create());

        SoundEngine.PlaySound(SoundID.Grab with { Volume = 0.85f, Pitch = 0.4f }, Projectile.Center);

        if (!Main.dedServ)
        {
            Vector2 anchor = MushroomAnchor;

            for (int i = 0; i < 10; i++)
                SporeField.Trail(anchor, Main.rand.NextVector2Circular(1.4f, 1.4f), Main.rand.NextFloat(6f, 12f));
        }

        EndOffer(given: true);
    }

    private void EndOffer(bool given)
    {
        // An offer nobody came for is not a favour spent, so it comes back around sooner.
        giftCooldown = given ? Main.rand.Next(GIFT_COOLDOWN_MIN, GIFT_COOLDOWN_MAX) : GIFT_RETRY;

        SetState(ShroomsonState.Follow);
    }

    private void SetState(ShroomsonState state)
    {
        State = state;
        StateTimer = 0f;

        // Progress only means anything while he is walking somewhere.
        ResetProgress();

        // The pose and the mushroom are both read straight off the state, so a client that misses a
        // change is left showing the wrong Shroomson until the next one.
        Projectile.netUpdate = true;
    }

    /// <summary>He folds himself down into the ground, feet first, until there is nothing left.</summary>
    private void UpdateDive()
    {
        if (StateTimer == 0f)
            SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.85f, Pitch = -0.45f }, Projectile.Center);

        StateTimer++;

        float progress = MathHelper.Clamp(StateTimer / DIVE_TICKS, 0f, 1f);

        // Eased in, so he compresses slowly and then drops through, rather than melting at a
        // constant rate for twenty ticks.
        squish = Vector2.Lerp(Vector2.One, DiveSquish, EaseFunction.EaseQuadIn.Ease(progress));

        if (!Main.dedServ && StateTimer % 3f == 0f)
            DigDust(Projectile.Bottom, progress);

        if (StateTimer >= DIVE_TICKS)
            SetState(ShroomsonState.Burrow);
    }

    /// <summary>Underground: no gravity, terrain or sprite. Runs below the player's feet so he surfaces through their floor.</summary>
    private void UpdateBurrow(Player owner)
    {
        StateTimer++;

        Vector2 goal = owner.Bottom + new Vector2(0f, BURROW_DEPTH);

        Projectile.velocity = Projectile.DirectionTo(goal) * BURROW_SPEED;

        if (!Main.dedServ && StateTimer % 2f == 0f)
            MoleTrail();

        bool arrived = Math.Abs(owner.Center.X - Projectile.Center.X) <= SURFACE_REACH;

        if (arrived || StateTimer >= BURROW_TIMEOUT)
            SetState(ShroomsonState.Surface);
    }

    private void UpdateSurface()
    {
        if (StateTimer == 0f)
            Erupt();

        StateTimer++;

        float progress = MathHelper.Clamp(StateTimer / SURFACE_TICKS, 0f, 1f);

        // Springs back out of the stretch rather than easing out of it, so the landing has a bounce.
        squish = Vector2.Lerp(SurfaceSquish, Vector2.One, EaseFunction.EaseCubicOut.Ease(progress));

        if (StateTimer < SURFACE_TICKS)
            return;

        squish = Vector2.One;
        SetState(ShroomsonState.Follow);
    }

    /// <summary>Puts him back on solid ground under the player, and throws the hole up after him.</summary>
    private void Erupt()
    {
        Player owner = Owner;

        // Searches from just above the player's feet so it finds their floor; with nothing underneath, he arrives at their feet.
        Projectile.Bottom = TryFindFloor(owner.Bottom - new Vector2(0f, 8f), out float floorY)
            ? new Vector2(owner.Bottom.X, floorY)
            : owner.Bottom;

        Projectile.velocity = new Vector2(0f, -SURFACE_HOP);
        Projectile.netUpdate = true;

        SoundEngine.PlaySound(SoundID.Dig with { Volume = 0.9f, Pitch = -0.15f }, Projectile.Center);

        if (Main.dedServ)
            return;

        DigDust(Projectile.Bottom, 1f, count: 18);

        for (int i = 0; i < 8; i++)
        {
            SporeField.Trail(
                Projectile.Bottom,
                new Vector2(Main.rand.NextFloat(-1.6f, 1.6f), Main.rand.NextFloat(-2.4f, -0.4f)),
                Main.rand.NextFloat(7f, 13f)
            );
        }
    }

    /// <summary>Dirt kicked up above the tunnel, so the trip reads as a trail rather than vanishing.</summary>
    private void MoleTrail()
    {
        if (!TryFindSurface(Projectile.Center, out float surfaceY))
            return;

        var position = new Vector2(Projectile.Center.X - 6f, surfaceY - 4f);

        Dust dust = Dust.NewDustDirect(
            position, 12, 4, DustID.Dirt,
            Main.rand.NextFloat(-1.2f, 1.2f), Main.rand.NextFloat(-3.2f, -1f),
            Scale: 1.1f
        );

        dust.velocity.Y -= 0.5f;
    }

    private static void DigDust(Vector2 position, float intensity, int count = 4)
    {
        for (int i = 0; i < count; i++)
        {
            Dust dust = Dust.NewDustDirect(
                position - new Vector2(11f, 6f), 22, 8, DustID.Dirt,
                Main.rand.NextFloat(-2.4f, 2.4f) * intensity,
                Main.rand.NextFloat(-5f, -1f) * intensity,
                Scale: Main.rand.NextFloat(0.9f, 1.35f)
            );

            dust.velocity *= 0.9f;
        }
    }

    /// <summary>First tile below <paramref name="from" /> that can be stood on, as a world Y.</summary>
    private static bool TryFindFloor(Vector2 from, out float floorY)
    {
        int tileX = (int)(from.X / 16f);
        int tileY = (int)(from.Y / 16f);

        for (int i = 0; i <= TILE_SEARCH; i++)
        {
            int y = tileY + i;

            if (y < 0 || y >= Main.maxTilesY)
                break;

            if (!IsStandable(tileX, y))
                continue;

            floorY = y * 16f;

            return true;
        }

        floorY = from.Y;

        return false;
    }

    /// <summary>
    ///     Whether he could surface onto this tile. Unlike <see cref="WorldGen.SolidTile(int, int, bool)" />, platforms,
    ///     slopes and half blocks count.
    /// </summary>
    private static bool IsStandable(int tileX, int tileY)
    {
        Tile tile = Framing.GetTileSafely(tileX, tileY);

        if (!tile.HasUnactuatedTile)
            return false;

        return Main.tileSolid[tile.TileType] || Main.tileSolidTop[tile.TileType];
    }

    /// <summary>Open air above <paramref name="from" />: where a tunnel underneath would break out.</summary>
    private static bool TryFindSurface(Vector2 from, out float surfaceY)
    {
        int tileX = (int)(from.X / 16f);
        int tileY = (int)(from.Y / 16f);

        for (int i = 0; i <= TILE_SEARCH; i++)
        {
            int y = tileY - i;

            if (y < 0)
                break;

            // Same standable test as the floor search, so the dirt is thrown out at the surface the
            // Shroomson is actually going to arrive on rather than at the one above it.
            if (IsStandable(tileX, y))
                continue;

            surfaceY = (y + 1) * 16f;

            return true;
        }

        surfaceY = from.Y;

        return false;
    }

    /// <summary>A whip mark wins outright; otherwise the nearest thing worth throwing at.</summary>
    private NPC? FindTarget(Player owner)
    {
        if (owner.HasMinionAttackTargetNPC)
        {
            NPC marked = Main.npc[owner.MinionAttackTargetNPC];

            if (marked is { active: true } && marked.CanBeChasedBy(Projectile))
                return marked;
        }

        NPC? closest = null;
        float closestDistance = SEARCH_RANGE;

        for (int i = 0; i < Main.maxNPCs; i++)
        {
            NPC npc = Main.npc[i];

            if (npc is not { active: true } || !npc.CanBeChasedBy(Projectile))
                continue;

            float distance = Projectile.Distance(npc.Center);

            if (distance >= closestDistance)
                continue;

            // Only checked for unmarked targets: without it he wanders off after things through
            // walls he has no way of reaching.
            bool visible = Collision.CanHitLine(
                Projectile.position, Projectile.width, Projectile.height,
                npc.position, npc.width, npc.height
            );

            if (!visible)
                continue;

            closestDistance = distance;
            closest = npc;
        }

        return closest;
    }

    private void FaceToward(Vector2 position)
    {
        float delta = position.X - Projectile.Center.X;

        // Deadzone, so something standing on top of him does not flip him every frame.
        if (Math.Abs(delta) > 8f)
            facing = Math.Sign(delta);
    }

    private void Move(Vector2? destination)
    {
        Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + GRAVITY, MAX_FALL);

        if (destination is not { } goal)
        {
            Projectile.velocity.X *= GROUND_FRICTION;
            ResolveTerrain();

            return;
        }

        float delta = goal.X - Projectile.Center.X;

        // Arrived: he stands! Jumping is a walking move, so nothing below applies either. Without
        // that, Shroomson idling against a wall reads as blocked and hops on the spot forever.
        if (Math.Abs(delta) <= ARRIVAL_SLACK)
        {
            Projectile.velocity.X *= GROUND_FRICTION;
            ResolveTerrain();

            return;
        }

        int direction = Math.Sign(delta);

        // He breaks into a run once he is properly behind, and drops back to a walk as he arrives.
        float speed = Math.Abs(delta) > CATCHUP_DISTANCE ? WALK_SPEED * CATCHUP_SPEED : WALK_SPEED;

        Projectile.velocity.X = MathHelper.Clamp(
            Projectile.velocity.X + direction * WALK_ACCELERATION,
            -speed,
            speed
        );

        facing = direction;

        // Blocked by a wall, or the destination is a storey up and close enough to be worth the
        // hop. Anything the step-up could have handled never gets this far.
        bool climbing = goal.Y < Projectile.Center.Y - JUMP_REACH && Math.Abs(delta) < JUMP_APPROACH;

        if (grounded && (blocked || climbing))
            Projectile.velocity.Y = -JUMP_SPEED;

        ResolveTerrain();
    }

    /// <summary>Resolves the walk against terrain, leaving velocity as the move the engine may make this tick.</summary>
    private void ResolveTerrain()
    {
        DecayStepOffset();

        Vector2 position = Projectile.position;
        Vector2 velocity = Projectile.velocity;

        // Lifts the leading foot onto low ledges, so a single raised tile is a step and not a wall.
        Collision.StepUp(ref position, ref velocity, Projectile.width, Projectile.height, ref stepSpeed, ref Projectile.gfxOffY);

        Projectile.position = position;
        Projectile.velocity = velocity;

        Vector2 resolved = Collision.TileCollision(Projectile.position, Projectile.velocity, Projectile.width, Projectile.height);

        // Stopped dead against something he was walking into, and gravity stopped against a floor.
        blocked = Projectile.velocity.X != 0f && resolved.X == 0f;
        grounded = Projectile.velocity.Y > 0f && resolved.Y == 0f;

        Projectile.velocity = resolved;
    }

    /// <summary>Eases the step-up gfxOffY back to zero; nothing decays it for projectiles.</summary>
    private void DecayStepOffset()
    {
        if (Projectile.gfxOffY > 0f)
            Projectile.gfxOffY = Math.Max(Projectile.gfxOffY - stepSpeed, 0f);
        else if (Projectile.gfxOffY < 0f)
            Projectile.gfxOffY = Math.Min(Projectile.gfxOffY + stepSpeed, 0f);
    }

    private void Animate()
    {
        Projectile.spriteDirection = facing;

        Projectile.frame = State switch
        {
            ShroomsonState.PrepAttack => WindFrame(),
            ShroomsonState.Toss => StateTimer < RELEASE_TICK ? FRAME_HOLD : FRAME_RELEASE,
            ShroomsonState.SonShroom => FRAME_HOLD,

            // The squish is doing all the work through the dig; the pose just stays out of its way.
            ShroomsonState.Dive or ShroomsonState.Burrow or ShroomsonState.Surface => FRAME_IDLE,

            // Feet off the ground, which is the pose the walk cycle would have picked anyway.
            ShroomsonState.Ascend => FRAME_AIRBORNE,

            _ => WalkFrame(),
        };
    }

    private int WindFrame()
    {
        // The cloud gathers in the cocked hand, then moves to the presenting pose for the flash, so the mushroom appears where it grew.
        if (FlashProgress > 0f)
            return FRAME_HOLD;

        return GrowProgress < 0.45f ? FRAME_WIND_START : FRAME_WIND_PEAK;
    }

    private int WalkFrame()
    {
        if (!grounded)
        {
            walkFrameTimer = 0;

            return FRAME_AIRBORNE;
        }

        float speed = Math.Abs(Projectile.velocity.X);

        if (speed < 0.4f)
        {
            walkFrameTimer = 0;

            return FRAME_IDLE;
        }

        // Cadence comes off distance covered rather than off a fixed interval, so a Shroomson
        // shuffling the last few pixels into place does not sprint on the spot.
        int ticks = Math.Clamp((int)(WALK_FRAME_PIXELS / speed), 4, 12);

        if (++walkFrameTimer < ticks)
            return Math.Clamp(Projectile.frame, FRAME_WALK_FIRST, FRAME_WALK_LAST);

        walkFrameTimer = 0;

        int next = Projectile.frame + 1;

        return next is < FRAME_WALK_FIRST or > FRAME_WALK_LAST ? FRAME_WALK_FIRST : next;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        if (State == ShroomsonState.Burrow)
            return false;

        Texture2D texture = TextureAssets.Projectile[Type].Value;

        int frameHeight = texture.Height / Main.projFrames[Type];
        var source = new Rectangle(0, Projectile.frame * frameHeight, texture.Width, frameHeight);

        var origin = new Vector2(
            texture.Width / 2f - BODY_NUDGE_X * Projectile.spriteDirection,
            frameHeight - 2 // so his lil feet touch the ground visually
        );

        SpriteEffects effects = Projectile.spriteDirection == -1
            ? SpriteEffects.FlipHorizontally
            : SpriteEffects.None;

        Vector2 position = Projectile.Bottom - Main.screenPosition + new Vector2(0f, Projectile.gfxOffY);

        Main.EntitySpriteDraw(
            texture,
            position,
            source,
            lightColor,
            Projectile.rotation,
            origin,
            squish * Projectile.scale,
            effects
        );

        return false;
    }

    public override void PostDraw(Color lightColor)
    {
        float flash = FlashProgress;

        if (!HoldingMushroom && flash <= 0f)
            return;

        Vector2 position = MushroomAnchor - Main.screenPosition;

        if (HoldingMushroom)
        {
            if (mushroomTexture is not { Value: { } mushroom })
                return;

            Main.EntitySpriteDraw(mushroom, position, null, lightColor, 0f, mushroom.Size() / 2f, Projectile.scale, SpriteEffects.None, 0);

            return;
        }

        if (maskTexture is not { Value: { } mask })
            return;

        // The silhouette arriving a beat before the mushroom does: it swells out of the knot of
        // spores, holds white for a frame, and is replaced by the real thing.
        float scale = MathHelper.Lerp(0.45f, 1.05f, flash) * Projectile.scale;

        Main.EntitySpriteDraw(mask, position, null, Color.White * flash, 0f, mask.Size() / 2f, scale, SpriteEffects.None, 0);
    }
}

public class ShroomsonMushroom : ModProjectile
{
    public const float GRAVITY = 0.28f;

    private const float MAX_FALL = 12f;

    private const float SPIN = 0.2f;

    private const int TRAIL_INTERVAL = 5;

    public override string Texture => AssetReferences.Content.Fungore.Mushroom.KEY;

    public override void SetStaticDefaults() 
        => ProjectileID.Sets.MinionShot[Type] = true;
    

    public override void SetDefaults()
    {
        Projectile.DamageType = DamageClass.Summon;
        Projectile.width = Projectile.height = 16;

        Projectile.friendly = Projectile.tileCollide = true;

        Projectile.penetrate = 1;
        Projectile.aiStyle = -1;
        Projectile.timeLeft = 300;
    }

    public override void OnSpawn(IEntitySource source) 
        => Projectile.rotation = Main.rand.NextFloat(MathHelper.TwoPi);
    

    public override void AI()
    {
        Projectile.velocity.Y = Math.Min(Projectile.velocity.Y + GRAVITY, MAX_FALL);

        Projectile.rotation += SPIN * (Projectile.velocity.X < 0f ? -1f : 1f);

        if (Main.dedServ || Projectile.timeLeft % TRAIL_INTERVAL != 0)
            return;

        SporeField.Trail(Projectile.Center, Projectile.velocity * -0.1f, Main.rand.NextFloat(5f, 9f));
    }

    public override void OnHitNPC(NPC target, NPC.HitInfo hit, int damageDone)
    {
        SoundEngine.PlaySound(Assets.SFX.Sporeclot_Splash.Asset with { Volume = 0.7f, Pitch = 0.45f }, Projectile.Center);
        SporeField.Splat(target, Projectile.Center, Projectile.velocity, anchorTicks: 24);
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        SoundEngine.PlaySound(Assets.SFX.Sporeclot_TileImpact.Asset with { Volume = 0.6f, Pitch = 0.5f }, Projectile.Center);
        SporeField.TileSplat(Projectile.Center, oldVelocity);

        return true;
    }
}