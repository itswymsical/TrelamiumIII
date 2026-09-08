using System.IO;
using Terraria.Audio;
using TrelamiumIII.Core.NPCs.Combat;

namespace TrelamiumIII.Core.NPCs.Actors;

/// <summary>
/// Keeps a <see cref="TerrarianNPC"/>'s backing player animating.
///
/// <para>
/// The renderer draws whatever that player says it is - but the player never runs
/// <see cref="Player.Update"/>, so nothing writes the fields the renderer reads:
/// <see cref="Player.itemLocation"/>, <see cref="Player.itemRotation"/>, the composite
/// arms, and <see cref="Player.lastVisualizedSelectedItem"/>, which is where
/// <c>PlayerDrawSet</c> takes the held weapon from. Untouched, the result is a
/// mannequin holding nothing.
/// </para>
///
/// <para>
/// This runs the visual slice of <see cref="Player.ItemCheck"/> and stops there. The
/// owner-only half - tile placement, mining, ammo, mana, mounts, cursor aim - has no
/// meaning for a puppet. Anything that <em>hurts</em> lives in <see cref="PuppetCombat"/>
/// and is only attached when an NPC is meant to fight; a shopkeeper pays none of it.
/// </para>
///
/// <para>Call <see cref="Update"/> once per tick, after the NPC's position is final.</para>
/// </summary>
public sealed class PlayerPuppet
{
    private readonly NPC npc;

    /// <summary>World position the current use is aimed at. Null means "straight ahead".</summary>
    private Vector2? aimTarget;

    /// <summary>Set on the tick a use begins, so Update can fire it at the right moment.</summary>
    private bool useJustStarted;

    /// <summary>
    /// True while the running animation exists only to keep the weapon on screen. It
    /// poses the arm but must never fire, swing, or read as a use.
    /// </summary>
    private bool presenting;

    public PlayerPuppet(NPC npc, Player player)
    {
        this.npc = npc;
        Player = player;
    }

    /// <summary>The NPC this puppet belongs to.</summary>
    public NPC NPC => npc;

    /// <summary>The backing player. Appearance and equipment state lives here.</summary>
    public Player Player { get; }

    /// <summary>
    /// Attached to give this NPC working attacks. Null - the default - means every
    /// swing is animation only.
    /// </summary>
    public PuppetCombat Combat { get; private set; }

    /// <summary>Gives this puppet working attacks, and returns them for configuration.</summary>
    public PuppetCombat ArmWith(PuppetAllegiance allegiance)
    {
        Combat ??= new PuppetCombat(this);
        Combat.Allegiance = allegiance;
        return Combat;
    }

    /// <summary>Takes away the ability to hurt anything. Animation continues.</summary>
    public void Disarm() => Combat = null;

    /// <summary>The item in the puppet's hand.</summary>
    public Item HeldItem => Player.inventory[Player.selectedItem];

    /// <summary>
    /// True while a real use animation is playing. A weapon merely being presented by
    /// <see cref="PresentHeldItem"/> does not count, so a combat behaviour can still
    /// start a swing on an actor that stands around visibly armed.
    /// </summary>
    public bool IsUsingItem => Player.itemAnimation > 0 && !presenting;

    /// <summary>Progress through the current use, 0 at the start and 1 at the end.</summary>
    public float UseProgress => !IsUsingItem || Player.itemAnimationMax <= 0
        ? 0f
        : 1f - Player.itemAnimation / (float)Player.itemAnimationMax;

    /// <summary>
    /// Keep the held weapon on screen while idle.
    /// <para>
    /// Players don't visibly carry a sword until they swing it, and the vanilla
    /// HeldItem layer enforces that: an item with no hold style only draws while
    /// <see cref="Player.itemAnimation"/> runs. Set this for an actor that should read
    /// as armed at a glance and the puppet pins a one-frame animation.
    /// </para>
    /// </summary>
    public bool PresentHeldItem { get; set; }

    /// <summary>
    /// Draw at full brightness regardless of world lighting.
    /// <para>
    /// Kept here because <see cref="Player.PlayerFrame"/> clears
    /// <see cref="Player.socialIgnoreLight"/> on every call and only restores it for
    /// players that are not display dolls - which every puppet is.
    /// </para>
    /// </summary>
    public bool IgnoreLighting { get; set; }

    // Equipment and use

    /// <summary>Puts an item in the hand without animating it. 0 empties it.</summary>
    public void Hold(int itemType)
    {
        Item slot = HeldItem;

        if (slot.type == itemType)
            return;

        slot.SetDefaults(itemType);
        Player.lastVisualizedSelectedItem = slot.Clone();

        // Swapping mid-swing would draw the new sprite on the old arc.
        CancelUse();
    }

    /// <summary>
    /// Starts the held item's use animation, aimed at <paramref name="aimAt"/> if given.
    /// False when the hand is empty or an animation is already running.
    /// </summary>
    public bool BeginUse(Vector2? aimAt = null)
    {
        Item item = HeldItem;

        if (item.IsAir || item.useStyle == ItemUseStyleID.None || IsUsingItem)
            return false;

        aimTarget = aimAt;
        presenting = false;

        // Face the target before the use style bakes direction into the pose.
        if (aimAt.HasValue)
            FaceToward(aimAt.Value);

        // The draw layer reads this, not the slot: freeze the weapon being swung.
        Player.lastVisualizedSelectedItem = item.Clone();

        // Vanilla only sets the shot rotation when the projectile leaves, a tick into
        // the animation. We know the aim now, so the first frame is posed correctly
        // rather than pointing wherever the last shot did.
        PuppetCombat.ApplyShootRotation(Player, item, AimDirection());

        Player.ApplyItemAnimation(item);
        Player.ApplyItemTime(item, 1f, callUseItem: false);
        useJustStarted = true;

        if (item.UseSound.HasValue && !ItemID.Sets.SkipsInitialUseSound[item.type])
            SoundEngine.PlaySound(item.UseSound, npc.Center);

        // Pushes the swing to other clients through TerrarianNPC's extra AI.
        npc.netUpdate = true;
        return true;
    }

    /// <summary>Equips <paramref name="itemType"/> and immediately uses it.</summary>
    public bool BeginUse(int itemType, Vector2? aimAt = null)
    {
        Hold(itemType);
        return BeginUse(aimAt);
    }

    /// <summary>Cuts any running animation short.</summary>
    public void CancelUse()
    {
        Player.itemAnimation = 0;
        Player.itemAnimationMax = 0;
        Player.itemTime = 0;
        Player.itemTimeMax = 0;
        aimTarget = null;
        presenting = false;
        Combat?.EndSwing();
    }

    // Per-tick pipeline

    /// <summary>Copies the NPC's physical state onto the backing player.</summary>
    public void SyncTransform()
    {
        Player.position = npc.position;
        Player.velocity = npc.velocity;
        Player.width = npc.width;
        Player.height = npc.height;
        Player.direction = npc.direction;
        Player.gravDir = 1f;
        Player.gfxOffY = npc.gfxOffY;
        Player.wet = npc.wet;
        Player.lavaWet = npc.lavaWet;
        Player.honeyWet = npc.honeyWet;
        Player.whoAmI = Main.maxPlayers;
    }

    /// <summary>Advances one tick of item state: timers, pose and held-item visuals.</summary>
    public void Update()
    {
        SyncTransform();
        AdvanceTimers();

        Item held = HeldItem;

        // A one-frame animation is the only lever vanilla gives us for "hold this
        // where people can see it"; the draw layer keys off itemAnimation, not the
        // inventory slot. Flagged so it poses the arm without counting as a use -
        // otherwise a guard at ease would swing its hitbox into everything nearby.
        presenting = false;

        if (PresentHeldItem && Player.itemAnimation <= 0 && !held.IsAir && held.holdStyle == 0)
        {
            Player.itemAnimation = 1;
            Player.itemAnimationMax = 1;
            presenting = true;
        }

        // Hold and BeginUse keep this current; this only catches direct slot writes.
        if (Player.itemAnimation <= 0 && Player.lastVisualizedSelectedItem.type != held.type)
            Player.lastVisualizedSelectedItem = held.Clone();

        Item visual = Player.lastVisualizedSelectedItem;

        // The style methods re-enable these when they want them.
        Player.compositeFrontArm.enabled = false;
        Player.compositeBackArm.enabled = false;

        Rectangle drawHitbox = Item.GetDrawHitbox(visual.type, Player);

        if (Player.itemAnimation > 0)
        {
            Player.itemWidth = Main.dedServ ? visual.width : drawHitbox.Width;
            Player.itemHeight = Main.dedServ ? visual.height : drawHitbox.Height;
        }

        ItemLoader.HoldItem(held, Player);

        if (Player.itemAnimation > 0)
            Player.ItemCheck_ApplyUseStyle(Player.HeightOffsetHitboxCenter, visual, drawHitbox);
        else
            PlayerInternals.ApplyHoldStyle?.Invoke(Player, Player.HeightOffsetHitboxCenter, visual, drawHitbox);

        // Both style methods bail out on a headless server, leaving itemLocation
        // stale. Melee hitboxes are built from it, so give the server a plausible hand
        // position - the same thing vanilla does for hold styles.
        if (Main.dedServ)
        {
            Player.itemLocation.X = Player.position.X + Player.width * 0.5f + 20f * Player.direction;
            Player.itemLocation.Y = Player.position.Y + 24f;
        }
        else
        {
            PlayerInternals.EmitHeldItemLight?.Invoke(Player, held);
        }

        if (Player.itemAnimation > 0 && !presenting && Combat != null)
        {
            if (useJustStarted)
                Combat.OnUseStarted(held);

            Combat.OnUseTick(held, drawHitbox);
        }

        useJustStarted = false;

        // Body, leg and head frames, including the use-style poses.
        Player.PlayerFrame();

        Player.socialIgnoreLight = IgnoreLighting;
    }

    private void AdvanceTimers()
    {
        if (Player.itemAnimation > 0)
            Player.itemAnimation--;

        if (Player.itemTime > 0)
            Player.itemTime--;

        if (Player.itemAnimation < 0)
            Player.itemAnimation = 0;

        if (Player.itemTime < 0)
            Player.itemTime = 0;

        if (Player.itemAnimation != 0)
            return;

        aimTarget = null;
        Combat?.EndSwing();
    }

    // Aiming

    /// <summary>Unit vector the current use is aimed along.</summary>
    public Vector2 AimDirection()
    {
        if (!aimTarget.HasValue)
            return new Vector2(npc.direction, 0f);

        Vector2 delta = aimTarget.Value - npc.Center;
        return delta == Vector2.Zero ? new Vector2(npc.direction, 0f) : Vector2.Normalize(delta);
    }

    /// <summary>Points the NPC and the puppet at a world position.</summary>
    public void FaceToward(Vector2 worldPosition)
    {
        int direction = worldPosition.X < npc.Center.X ? -1 : 1;
        npc.direction = direction;
        npc.spriteDirection = direction;
        Player.direction = direction;
    }

    // Networking

    /// <summary>
    /// Writes the state a remote client needs to replay the swing.
    /// <para>
    /// Only the animation is synced, not the behaviour that started it. Behaviours
    /// tick on every side, so a client may reach the same swing on its own; letting
    /// the authoritative timings overwrite it keeps the two from drifting.
    /// </para>
    /// </summary>
    public void WriteNetState(BinaryWriter writer)
    {
        writer.Write((short)HeldItem.type);
        writer.Write((short)Player.itemAnimation);
        writer.Write((short)Player.itemAnimationMax);
        writer.Write((short)Player.itemTime);
        writer.Write((short)Player.itemTimeMax);

        bool aimed = aimTarget.HasValue;
        writer.Write(aimed);

        if (!aimed)
            return;

        writer.Write(aimTarget.Value.X);
        writer.Write(aimTarget.Value.Y);
    }

    /// <summary>Applies state written by <see cref="WriteNetState"/>.</summary>
    public void ReadNetState(BinaryReader reader)
    {
        int type = reader.ReadInt16();
        int animation = reader.ReadInt16();
        int animationMax = reader.ReadInt16();
        int time = reader.ReadInt16();
        int timeMax = reader.ReadInt16();
        bool aimed = reader.ReadBoolean();

        aimTarget = aimed ? new Vector2(reader.ReadSingle(), reader.ReadSingle()) : null;

        if (HeldItem.type != type)
            Hold(type);

        Player.itemAnimation = animation;
        Player.itemAnimationMax = animationMax;
        Player.itemTime = time;
        Player.itemTimeMax = timeMax;

        if (animation > 0)
            Player.lastVisualizedSelectedItem = HeldItem.Clone();
    }
}
