using System.Collections.Generic;
using Terraria.Audio;
using Terraria.DataStructures;

namespace TrelamiumIII.Core.NPCs.Actors;

/// <summary>Who a puppet's swings and projectiles are allowed to hurt.</summary>
public enum PuppetAllegiance
{
    /// <summary>Attacks land on nothing. Animation only - use for shopkeepers and cutscenes.</summary>
    Harmless,

    /// <summary>Fights alongside players. Hits hostile NPCs, ignores players.</summary>
    Ally,

    /// <summary>Fights players. Hits players, ignores other NPCs.</summary>
    Enemy,
}

/// <summary>
/// Drives the vanilla item-use pipeline for an entity that is not a player.
///
/// <para>
/// A <see cref="WorldNPC"/> renders through <see cref="Main.PlayerRenderer"/>, so
/// its pose is whatever the backing <see cref="Player"/> says it is. But that
/// player never runs <see cref="Player.Update"/>, which means none of the fields
/// the renderer reads - <see cref="Player.itemLocation"/>,
/// <see cref="Player.itemRotation"/>, the composite arms,
/// <see cref="Player.lastVisualizedSelectedItem"/> - are ever written. The result
/// is a mannequin holding nothing in a T-pose arm.
/// </para>
///
/// <para>
/// This class runs the slice of <see cref="Player.ItemCheck"/> that produces those
/// fields, and nothing else. It deliberately skips the owner-only half of ItemCheck
/// (tile placement, mining, ammo consumption, mana, buffs, mounts, PvP), because a
/// puppet has no inventory to spend and no cursor to aim with. What it needs
/// instead - an aim vector and an allegiance - it takes from whoever drives it.
/// </para>
///
/// <para>
/// Call <see cref="Update"/> exactly once per tick, after the NPC's position for
/// that tick is final. Everything downstream (PlayerFrame, the HeldItem draw layer)
/// reads what Update leaves behind.
/// </para>
/// </summary>
public sealed class PlayerPuppet
{
    /// <summary>Vanilla's dedicated ammo slots on <see cref="Player.inventory"/>.</summary>
    private const int FirstAmmoSlot = 54;
    private const int LastAmmoSlot = 57;

    private readonly NPC npc;

    /// <summary>Targets already hit by the current swing. Cleared when a new animation starts.</summary>
    private readonly HashSet<int> swingHits = [];

    /// <summary>World position the current use is aimed at. Null means "straight ahead".</summary>
    private Vector2? aimTarget;

    /// <summary>Set on the tick a use begins so <see cref="Update"/> can fire it at the right moment.</summary>
    private bool useJustStarted;

    /// <summary>
    /// True while the running animation exists only to keep the weapon on screen.
    /// It poses the arm but must never fire, swing, or read as a use to a module.
    /// </summary>
    private bool presenting;

    public PlayerPuppet(NPC npc, Player player)
    {
        this.npc = npc;
        Player = player;
    }

    /// <summary>The backing player. All appearance and equipment state lives here.</summary>
    public Player Player { get; }

    /// <summary>Who this puppet's attacks may hurt. Defaults to <see cref="PuppetAllegiance.Harmless"/>.</summary>
    public PuppetAllegiance Allegiance { get; set; } = PuppetAllegiance.Harmless;

    /// <summary>Scales the damage of everything the puppet swings or shoots.</summary>
    public float DamageMultiplier { get; set; } = 1f;

    /// <summary>Scales knockback dealt by melee swings.</summary>
    public float KnockbackMultiplier { get; set; } = 1f;

    /// <summary>
    /// Ammo is picked but never spent. On by default - an NPC has no way to restock,
    /// and a guard that runs dry after forty arrows is a bug report waiting to happen.
    /// </summary>
    public bool InfiniteAmmo { get; set; } = true;

    /// <summary>
    /// Stock an ammo weapon automatically when its ammo slots are empty, using a
    /// basic round for the ammo class. Turn off to require explicit
    /// <see cref="SetAmmo"/> calls.
    /// </summary>
    public bool AutoSupplyAmmo { get; set; } = true;

    /// <summary>The last melee swing rectangle built this tick, for debug drawing.</summary>
    internal Rectangle? LastSwingHitbox { get; private set; }

    /// <summary>
    /// Set false to keep the swing purely cosmetic while still spawning dust and
    /// playing sounds. Useful for sparring matches and idle flourishes.
    /// </summary>
    public bool DealsDamage { get; set; } = true;

    /// <summary>
    /// Draw at full brightness regardless of world lighting.
    /// <para>
    /// Kept here rather than on the player because <see cref="Player.PlayerFrame"/>
    /// clears <see cref="Player.socialIgnoreLight"/> every call, and only restores it
    /// for players that are not display dolls - which every puppet is. Update
    /// re-applies it afterwards so the flag actually sticks.
    /// </para>
    /// </summary>
    public bool IgnoreLighting { get; set; }

    /// <summary>
    /// Keep the held weapon on screen while idle.
    /// <para>
    /// Players don't visibly carry a sword until they swing it, and the vanilla
    /// HeldItem layer enforces that: an item with no hold style only draws while
    /// <see cref="Player.itemAnimation"/> is running. Set this when an actor should
    /// read as armed at a glance - a guard on watch, a duelist waiting - and the
    /// puppet pins a one-frame animation so the weapon stays in hand.
    /// </para>
    /// </summary>
    public bool PresentHeldItem { get; set; }

    /// <summary>The item in the puppet's hand.</summary>
    public Item HeldItem => Player.inventory[Player.selectedItem];

    /// <summary>
    /// True while a real use animation is playing. A weapon merely being presented
    /// by <see cref="PresentHeldItem"/> does not count, so a combat module can still
    /// start a swing on an actor that stands around visibly armed.
    /// </summary>
    public bool IsUsingItem => Player.itemAnimation > 0 && !presenting;

    /// <summary>Normalised progress through the current use animation, 0 at the start and 1 at the end.</summary>
    public float UseProgress => !IsUsingItem || Player.itemAnimationMax <= 0
        ? 0f
        : 1f - Player.itemAnimation / (float)Player.itemAnimationMax;

    /// <summary>
    /// Raised on the tick the item actually "goes off" - the frame vanilla would
    /// have consumed ammo and spawned the projectile. Use this for bespoke attacks
    /// that a plain <see cref="Item.shoot"/> can't express.
    /// </summary>
    public Action<PlayerPuppet, Item> OnUse;

    // Equipment

    /// <summary>
    /// Puts an item in the puppet's hand without animating it. Pass 0 to empty the hand.
    /// </summary>
    public void Hold(int itemType)
    {
        Item slot = HeldItem;

        if (slot.type == itemType)
            return;

        slot.SetDefaults(itemType);
        Player.lastVisualizedSelectedItem = slot.Clone();

        // A change of weapon mid-swing would draw the old sprite on the new arc.
        Player.itemAnimation = 0;
        Player.itemAnimationMax = 0;
        Player.itemTime = 0;
        Player.itemTimeMax = 0;
        swingHits.Clear();
    }

    /// <summary>
    /// Starts the held item's use animation, aimed at <paramref name="aimAt"/> if given.
    /// Returns false when the hand is empty or an animation is already running.
    /// </summary>
    public bool BeginUse(Vector2? aimAt = null)
    {
        Item item = HeldItem;

        if (item.IsAir || item.useStyle == ItemUseStyleID.None || IsUsingItem)
            return false;

        aimTarget = aimAt;
        presenting = false;
        swingHits.Clear();

        // Face the target before the use style bakes direction into the pose.
        if (aimAt.HasValue)
            FaceToward(aimAt.Value);

        // Freeze the weapon being swung: the draw layer reads this, not the slot.
        Player.lastVisualizedSelectedItem = item.Clone();

        // Vanilla only sets the shot rotation when the projectile leaves, one tick
        // into the animation. We know the aim now, so the very first frame is posed
        // correctly instead of pointing wherever the last shot did.
        ApplyShootRotation(item, AimDirection());

        Player.ApplyItemAnimation(item);
        Player.ApplyItemTime(item, 1f, callUseItem: false);
        useJustStarted = true;

        // Pushes the swing to other clients through WorldNPC's extra AI.
        npc.netUpdate = true;

        if (item.UseSound.HasValue && !ItemID.Sets.SkipsInitialUseSound[item.type])
            SoundEngine.PlaySound(item.UseSound, npc.Center);

        return true;
    }

    /// <summary>Equips <paramref name="itemType"/> and immediately swings it.</summary>
    public bool BeginUse(int itemType, Vector2? aimAt = null)
    {
        Hold(itemType);
        return BeginUse(aimAt);
    }

    /// <summary>
    /// Loads an ammo slot. Needed for bows, guns and launchers, whose projectile and
    /// stats come from the ammo rather than the weapon.
    /// </summary>
    public void SetAmmo(int itemType, int stack = 999)
    {
        Item slot = Player.inventory[FirstAmmoSlot];
        slot.SetDefaults(itemType);
        slot.stack = Math.Max(1, stack);
    }

    /// <summary>Empties every ammo slot.</summary>
    public void ClearAmmo()
    {
        for (int i = FirstAmmoSlot; i <= LastAmmoSlot; i++)
            Player.inventory[i].TurnToAir();
    }

    /// <summary>Cuts any running animation short and forgets the aim target.</summary>
    public void CancelUse()
    {
        Player.itemAnimation = 0;
        Player.itemAnimationMax = 0;
        Player.itemTime = 0;
        Player.itemTimeMax = 0;
        aimTarget = null;
        presenting = false;
        swingHits.Clear();
    }

    // Per-tick pipeline

    /// <summary>
    /// Copies the NPC's physical state onto the backing player. Anything the
    /// renderer or the item pipeline derives from position or facing reads these.
    /// </summary>
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

    /// <summary>
    /// Advances one tick of item state: timers, pose, held-item visuals and,
    /// if the puppet is armed, the attack itself.
    /// </summary>
    public void Update()
    {
        SyncTransform();

        AdvanceTimers();

        Item held = HeldItem;

        // A one-frame animation is the only lever vanilla gives us for "hold this
        // where people can see it"; the draw layer keys off itemAnimation, not off
        // the inventory slot. Flagged as a presentation so it poses the arm without
        // ever counting as a use - otherwise a guard standing at ease would swing
        // its hitbox into everything nearby, every tick.
        presenting = false;

        if (PresentHeldItem && Player.itemAnimation <= 0 && !held.IsAir && held.holdStyle == 0)
        {
            Player.itemAnimation = 1;
            Player.itemAnimationMax = 1;
            presenting = true;
        }

        // Vanilla draws the item that was equipped when the swing started, not
        // whatever is in the slot now. PlayerDrawSet reads lastVisualizedSelectedItem
        // directly, so a puppet that never sets it renders no weapon at all - which
        // is exactly why hand-rolled item drawing was needed here before. Hold and
        // BeginUse keep it current; this only catches direct writes to the slot.
        if (Player.itemAnimation <= 0 && Player.lastVisualizedSelectedItem.type != held.type)
            Player.lastVisualizedSelectedItem = held.Clone();

        Item visual = Player.lastVisualizedSelectedItem;

        // ApplyUseStyle / ApplyHoldStyle re-enable these when they want them.
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

        // Both style methods bail out on a headless server, leaving itemLocation at
        // whatever it was. Melee hitboxes are built from it, so give the server a
        // plausible hand position rather than a stale one. Vanilla does the same
        // thing for hold styles.
        if (Main.dedServ)
        {
            Player.itemLocation.X = Player.position.X + Player.width * 0.5f + 20f * Player.direction;
            Player.itemLocation.Y = Player.position.Y + 24f;
        }
        else
        {
            PlayerInternals.EmitHeldItemLight?.Invoke(Player, held);
        }

        LastSwingHitbox = null;

        if (Player.itemAnimation > 0 && !presenting)
        {
            if (useJustStarted)
                FireUse(held);

            ApplyMelee(held, drawHitbox);
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

        if (Player.itemAnimation == 0)
        {
            aimTarget = null;
            swingHits.Clear();
        }
    }

    // Attacks

    private void FireUse(Item item)
    {
        OnUse?.Invoke(this, item);

        if (item.shoot <= ProjectileID.None || Main.netMode == NetmodeID.MultiplayerClient)
            return;

        if (Allegiance == PuppetAllegiance.Harmless || !DealsDamage)
            return;

        // Spears, flails and yoyos replace the item sprite with a projectile that
        // positions itself from Main.player[owner] - it would hang off the local
        // player's hand, not the NPC's. Those weapons need a bespoke attack through
        // OnUse; the animation still plays either way.
        if (item.noUseGraphic)
            return;

        Shoot(item);
    }

    /// <summary>
    /// Fires the held weapon along the same path a player's shot takes.
    ///
    /// <para>
    /// This is a trimmed <see cref="Player.ItemCheck_Shoot"/>: same ammo pick, same
    /// speed scaling, same muzzle origin, same hook order - <c>CanShoot</c>,
    /// <c>ModifyShootStats</c>, <c>Shoot</c> - and the same rule that a mod's own
    /// <c>Shoot</c> returning false means it spawned things itself. Going through the
    /// hooks is what makes modded weapons behave; the previous hand-rolled
    /// <c>NewProjectile</c> skipped every one of them, so anything with custom shoot
    /// behaviour fired a bare default projectile instead.
    /// </para>
    ///
    /// <para>
    /// Vanilla's version can't be called directly: it early-returns for any player
    /// that isn't <see cref="Main.myPlayer"/>, and it reads the cursor for aim.
    /// </para>
    /// </summary>
    private void Shoot(Item item)
    {
        if (!CombinedHooks.CanShoot(Player, item))
            return;

        int type = item.shoot;
        float speed = item.shootSpeed;
        int damage = ScaleDamage(Player.GetWeaponDamage(item));
        float knockback = Player.GetWeaponKnockback(item, item.knockBack) * KnockbackMultiplier;
        int ammoItemId = 0;

        if (item.melee && !ProjectileID.Sets.NoMeleeSpeedVelocityScaling[type])
            speed *= Player.GetTotalAttackSpeed(DamageClass.Melee);

        if (item.CountsAsClass(DamageClass.Throwing) && speed < 16f)
            speed = Math.Min(speed * Player.ThrownVelocity, 16f);

        if (item.useAmmo > 0 && !TryPickAmmo(item, ref type, ref speed, ref damage, ref knockback, out ammoItemId))
            return;

        if (speed <= 0f)
            speed = 1f;

        Vector2 origin = Player.RotatedRelativePoint(Player.MountedCenter);
        Vector2 velocity = AimDirection() * speed;

        CombinedHooks.ModifyShootStats(Player, item, ref origin, ref velocity, ref type, ref damage, ref knockback);

        // Vanilla sets itemRotation here, from the shot vector, for the styles whose
        // pose is derived from it. Skipping this is why staves and guns pointed
        // straight ahead no matter where the NPC was aiming - ApplyUseStyle reads
        // itemRotation for these styles rather than computing one.
        ApplyShootRotation(item, velocity);

        var source = new EntitySource_ItemUse_WithAmmo(Player, item, ammoItemId);

        // Everything spawned between here and the end of the scope is credited to
        // this NPC, including projectiles a modded Shoot hook creates itself.
        PuppetProjectile.FiringNPC = npc.whoAmI;
        PuppetProjectile.FiringAllegiance = Allegiance;

        try
        {
            if (CombinedHooks.Shoot(Player, item, source, origin, velocity, type, damage, knockback))
                Projectile.NewProjectile(source, origin, velocity, type, damage, knockback, Main.myPlayer);
        }
        finally
        {
            PuppetProjectile.FiringNPC = -1;
            PuppetProjectile.FiringAllegiance = PuppetAllegiance.Harmless;
        }
    }

    /// <summary>
    /// Runs the weapon's ammo selection against the puppet's own ammo slots.
    /// Returns false when there is nothing to fire.
    /// </summary>
    private bool TryPickAmmo(Item item, ref int type, ref float speed, ref int damage, ref float knockback, out int ammoItemId)
    {
        if (AutoSupplyAmmo && !HasAmmoFor(item))
            SupplyDefaultAmmo(item);

        if (!Player.PickAmmo(item, out int ammoType, out float ammoSpeed, out int ammoDamage, out float ammoKnockback, out ammoItemId, dontConsume: InfiniteAmmo))
            return false;

        type = ammoType;
        knockback = ammoKnockback * KnockbackMultiplier;

        // Guns keep the weapon's own stats; everything else takes the ammo's.
        if (!ItemID.Sets.gunProj[item.type])
        {
            speed = ammoSpeed;
            damage = ScaleDamage(ammoDamage);
        }

        return true;
    }

    private bool HasAmmoFor(Item item)
    {
        for (int i = 0; i < Player.inventory.Length; i++)
        {
            Item slot = Player.inventory[i];
            if (slot.ammo == item.useAmmo && slot.stack > 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Loads a basic round so an ammo weapon works the moment it is equipped.
    /// Anything more specific should be set explicitly with <see cref="SetAmmo"/>.
    /// </summary>
    private void SupplyDefaultAmmo(Item item)
    {
        // A lookup rather than a switch: AmmoID's members are not compile-time
        // constants in tModLoader, since mods extend the set at load time.
        int ammo = ItemID.None;

        if (item.useAmmo == AmmoID.Arrow) ammo = ItemID.WoodenArrow;
        else if (item.useAmmo == AmmoID.Bullet) ammo = ItemID.MusketBall;
        else if (item.useAmmo == AmmoID.Rocket) ammo = ItemID.RocketI;
        else if (item.useAmmo == AmmoID.Dart) ammo = ItemID.PoisonDart;
        else if (item.useAmmo == AmmoID.Gel) ammo = ItemID.Gel;
        else if (item.useAmmo == AmmoID.FallenStar) ammo = ItemID.FallenStar;
        else if (item.useAmmo == AmmoID.Sand) ammo = ItemID.SandBlock;
        else if (item.useAmmo == AmmoID.Snowball) ammo = ItemID.Snowball;
        else if (item.useAmmo == AmmoID.Coin) ammo = ItemID.CopperCoin;
        else if (item.useAmmo == AmmoID.Solution) ammo = ItemID.GreenSolution;

        if (ammo != ItemID.None)
            SetAmmo(ammo);
    }

    /// <summary>
    /// Points the held item along the shot. Only the styles whose pose vanilla
    /// derives from <see cref="Player.itemRotation"/> need this; the swing styles
    /// compute their own rotation from animation progress.
    /// </summary>
    private void ApplyShootRotation(Item item, Vector2 velocity)
    {
        if (item.useStyle != ItemUseStyleID.Shoot && item.useStyle != ItemUseStyleID.RaiseLamp)
            return;

        if (velocity == Vector2.Zero)
            return;

        Player.itemRotation = (float)Math.Atan2(
            velocity.Y * Player.direction,
            velocity.X * Player.direction) - Player.fullRotation;
    }

    private void ApplyMelee(Item item, Rectangle drawHitbox)
    {
        if (item.damage <= 0 || item.noMelee || PlayerInternals.GetMeleeHitbox == null)
            return;

        PlayerInternals.GetMeleeHitbox(Player, item, drawHitbox, out bool dontAttack, out Rectangle swing);
        if (dontAttack)
            return;

        if (!Main.dedServ && PlayerInternals.EmitUseVisuals != null)
            swing = PlayerInternals.EmitUseVisuals(Player, item, swing);

        CombinedHooks.MeleeEffects(Player, item, swing);

        // Recorded even for a harmless swing: seeing where the box went is the whole
        // point of the debug overlay, and "no damage" and "no hitbox" look identical
        // without it.
        LastSwingHitbox = swing;

        if (!DealsDamage || Allegiance == PuppetAllegiance.Harmless)
            return;

        // Damage to NPCs is the server's call; damage to a player is that player's
        // client's call, the same split vanilla uses. Either way a multiplayer client
        // is not allowed to resolve hits against NPCs.
        if (Allegiance == PuppetAllegiance.Ally && Main.netMode == NetmodeID.MultiplayerClient)
            return;

        int damage = ScaleDamage(Player.GetWeaponDamage(item));
        float knockback = Player.GetWeaponKnockback(item, item.knockBack) * KnockbackMultiplier;

        if (Allegiance == PuppetAllegiance.Ally)
            HitHostileNPCs(swing, damage, knockback);
        else
            HitPlayers(swing, damage);
    }

    private void HitHostileNPCs(Rectangle swing, int damage, float knockback)
    {
        foreach (NPC target in Main.ActiveNPCs)
        {
            if (target.whoAmI == npc.whoAmI || target.friendly || target.dontTakeDamage)
                continue;

            if (target.immortal || target.CountsAsACritter || !target.CanBeChasedBy())
                continue;

            // One hit per target per swing, but any frame of the arc may be the one that lands.
            if (!swing.Intersects(target.Hitbox) || !swingHits.Add(target.whoAmI))
                continue;

            int direction = target.Center.X < npc.Center.X ? -1 : 1;
            target.SimpleStrikeNPC(
                damage, direction,
                crit: false,
                knockBack: knockback,
                damageType: DamageClass.Melee,
                damageVariation: true,
                noPlayerInteraction: true);
        }
    }

    private void HitPlayers(Rectangle swing, int damage)
    {
        foreach (Player target in Main.ActivePlayers)
        {
            // Each client hurts only itself; Player.Hurt is not safe to call for a
            // remote player, and a dedicated server has no local player at all.
            if (target.whoAmI != Main.myPlayer || target.dead || target.immune)
                continue;

            if (!swing.Intersects(target.Hitbox) || !swingHits.Add(target.whoAmI))
                continue;

            int direction = target.Center.X < npc.Center.X ? -1 : 1;
            target.Hurt(PlayerDeathReason.ByNPC(npc.whoAmI), damage, direction);
        }
    }

    private int ScaleDamage(int baseDamage)
        => Math.Max(1, (int)(baseDamage * DamageMultiplier));

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
    ///
    /// <para>
    /// Only the animation is synced, not the module that started it. Modules tick on
    /// every side, so a client may reach the same swing on its own; letting the
    /// authoritative timings overwrite it keeps the two from drifting apart. Damage
    /// is never taken from this - see <see cref="ApplyMelee"/> for who resolves what.
    /// </para>
    /// </summary>
    public void WriteNetState(System.IO.BinaryWriter writer)
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
    public void ReadNetState(System.IO.BinaryReader reader)
    {
        int type = reader.ReadInt16();
        int animation = reader.ReadInt16();
        int animationMax = reader.ReadInt16();
        int time = reader.ReadInt16();
        int timeMax = reader.ReadInt16();
        bool aimed = reader.ReadBoolean();

        aimTarget = aimed
            ? new Vector2(reader.ReadSingle(), reader.ReadSingle())
            : null;

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
