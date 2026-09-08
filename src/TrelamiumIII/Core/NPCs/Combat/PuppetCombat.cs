using System.Collections.Generic;
using Microsoft.Xna.Framework.Graphics;
using Terraria.DataStructures;
using TrelamiumIII.Core.NPCs.Actors;

namespace TrelamiumIII.Core.NPCs.Combat;

/// <summary>Who a puppet's swings and projectiles may hurt.</summary>
public enum PuppetAllegiance
{
    /// <summary>Fights alongside players. Hits hostile NPCs, ignores players.</summary>
    Ally,

    /// <summary>Fights players. Hits players, ignores other NPCs.</summary>
    Enemy,
}

/// <summary>
/// Makes a <see cref="PlayerPuppet"/>'s attacks real.
///
/// <para>
/// Optional by design. Most world NPCs talk, hand out work and stand somewhere; none
/// of that needs a melee hitbox or an ammo pool, so none of it is in the framework.
/// Attach this with <see cref="PlayerPuppet.ArmWith"/> when an NPC is meant to fight,
/// and the puppet starts routing its use animations through here.
/// </para>
///
/// <para>
/// Damage authority follows vanilla's split: hits on NPCs resolve on the server, hits
/// on a player resolve on that player's own client.
/// </para>
/// </summary>
public sealed class PuppetCombat
{
    /// <summary>Vanilla's dedicated ammo slots on <see cref="Player.inventory"/>.</summary>
    private const int FirstAmmoSlot = 54;
    private const int LastAmmoSlot = 57;

    private readonly PlayerPuppet puppet;

    /// <summary>Targets already hit by the current swing.</summary>
    private readonly HashSet<int> swingHits = [];

    internal PuppetCombat(PlayerPuppet puppet)
    {
        this.puppet = puppet;
    }

    private Player Player => puppet.Player;

    private NPC NPC => puppet.NPC;

    /// <summary>Which side this NPC fights for.</summary>
    public PuppetAllegiance Allegiance { get; set; } = PuppetAllegiance.Ally;

    /// <summary>Scales the damage of everything swung or shot.</summary>
    public float DamageMultiplier { get; set; } = 1f;

    /// <summary>Scales knockback dealt.</summary>
    public float KnockbackMultiplier { get; set; } = 1f;

    /// <summary>
    /// Set false to keep swings cosmetic while still spawning dust and playing sounds.
    /// Useful for sparring and idle flourishes.
    /// </summary>
    public bool DealsDamage { get; set; } = true;

    /// <summary>
    /// Ammo is picked but never spent. On by default - an NPC has no way to restock,
    /// and a guard that runs dry after forty arrows is a bug report waiting to happen.
    /// </summary>
    public bool InfiniteAmmo { get; set; } = true;

    /// <summary>Stock a basic round when the ammo slots are empty.</summary>
    public bool AutoSupplyAmmo { get; set; } = true;

    /// <summary>
    /// Raised the tick an item goes off. The hook for attacks a plain
    /// <see cref="Item.shoot"/> can't express - spears, flails, anything channelled.
    /// </summary>
    public Action<PuppetCombat, Item> OnUse { get; set; }

    /// <summary>The last melee swing rectangle built, for the debug overlay.</summary>
    internal Rectangle? LastSwingHitbox { get; private set; }

    public PuppetCombat WithDamage(float multiplier) { DamageMultiplier = multiplier; return this; }
    public PuppetCombat WithKnockback(float multiplier) { KnockbackMultiplier = multiplier; return this; }
    public PuppetCombat Harmless() { DealsDamage = false; return this; }

    /// <summary>
    /// Loads an ammo slot. Needed for bows, guns and launchers, whose projectile and
    /// stats come from the ammo rather than the weapon.
    /// </summary>
    public PuppetCombat SetAmmo(int itemType, int stack = 999)
    {
        Item slot = Player.inventory[FirstAmmoSlot];
        slot.SetDefaults(itemType);
        slot.stack = Math.Max(1, stack);
        return this;
    }

    /// <summary>Empties every ammo slot.</summary>
    public void ClearAmmo()
    {
        for (int i = FirstAmmoSlot; i <= LastAmmoSlot; i++)
            Player.inventory[i].TurnToAir();
    }

    /// <summary>Called by the puppet when a swing ends. Resets per-swing bookkeeping.</summary>
    internal void EndSwing()
    {
        swingHits.Clear();
        LastSwingHitbox = null;
    }

    // Firing

    /// <summary>Called by the puppet on the tick the item goes off.</summary>
    internal void OnUseStarted(Item item)
    {
        OnUse?.Invoke(this, item);

        if (item.shoot <= ProjectileID.None || Main.netMode == NetmodeID.MultiplayerClient || !DealsDamage)
            return;

        // Spears, flails and yoyos replace the item sprite with a projectile that
        // positions itself from Main.player[owner] - it would hang off the local
        // player's hand, not the NPC's. Those need a bespoke attack through OnUse;
        // the animation still plays either way.
        if (item.noUseGraphic)
            return;

        Shoot(item);
    }

    /// <summary>
    /// Fires along the same path a player's shot takes.
    ///
    /// <para>
    /// A trimmed <see cref="Player.ItemCheck_Shoot"/>: same ammo pick, speed scaling,
    /// muzzle origin, and the same hook order - <c>CanShoot</c>,
    /// <c>ModifyShootStats</c>, <c>Shoot</c> - with a mod's own <c>Shoot</c> returning
    /// false meaning it spawned things itself. Going through the hooks is what makes
    /// modded weapons behave; a bare <c>NewProjectile</c> skips every one of them.
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
        Vector2 velocity = puppet.AimDirection() * speed;

        CombinedHooks.ModifyShootStats(Player, item, ref origin, ref velocity, ref type, ref damage, ref knockback);
        ApplyShootRotation(Player, item, velocity);

        var source = new EntitySource_ItemUse_WithAmmo(Player, item, ammoItemId);

        // Everything spawned inside this scope is credited to the NPC, including
        // projectiles a modded Shoot hook creates itself.
        PuppetProjectile.FiringNPC = NPC.whoAmI;
        PuppetProjectile.FiringAllegiance = Allegiance;

        try
        {
            if (CombinedHooks.Shoot(Player, item, source, origin, velocity, type, damage, knockback))
                Projectile.NewProjectile(source, origin, velocity, type, damage, knockback, Main.myPlayer);
        }
        finally
        {
            PuppetProjectile.FiringNPC = -1;
        }
    }

    /// <summary>
    /// Points the held item along the shot.
    ///
    /// <para>
    /// Only the styles whose pose vanilla derives from
    /// <see cref="Player.itemRotation"/> need this - for those,
    /// <c>ItemCheck_ApplyUseStyle</c> <em>reads</em> the rotation rather than computing
    /// one, and <c>ItemCheck_Shoot</c> is what normally sets it from the mouse. Skip it
    /// and every staff and gun points straight ahead regardless of the target.
    /// </para>
    /// </summary>
    internal static void ApplyShootRotation(Player player, Item item, Vector2 velocity)
    {
        if (item.useStyle != ItemUseStyleID.Shoot && item.useStyle != ItemUseStyleID.RaiseLamp)
            return;

        if (velocity == Vector2.Zero)
            return;

        player.itemRotation = (float)Math.Atan2(
            velocity.Y * player.direction,
            velocity.X * player.direction) - player.fullRotation;
    }

    /// <summary>Runs the weapon's ammo selection against the puppet's own slots.</summary>
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
            if (Player.inventory[i].ammo == item.useAmmo && Player.inventory[i].stack > 0)
                return true;
        }

        return false;
    }

    /// <summary>
    /// Loads a basic round so an ammo weapon works the moment it is equipped. A lookup
    /// rather than a switch: AmmoID's members aren't compile-time constants, since mods
    /// extend the set at load time.
    /// </summary>
    private void SupplyDefaultAmmo(Item item)
    {
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

    // Melee

    /// <summary>Called by the puppet every tick a use animation is running.</summary>
    internal void OnUseTick(Item item, Rectangle drawHitbox)
    {
        if (item.damage <= 0 || item.noMelee || PlayerInternals.GetMeleeHitbox == null)
            return;

        PlayerInternals.GetMeleeHitbox(Player, item, drawHitbox, out bool dontAttack, out Rectangle swing);
        if (dontAttack)
            return;

        if (!Main.dedServ && PlayerInternals.EmitUseVisuals != null)
            swing = PlayerInternals.EmitUseVisuals(Player, item, swing);

        CombinedHooks.MeleeEffects(Player, item, swing);

        // Recorded even for a harmless swing: "dealt no damage" and "had no hitbox"
        // look identical in game without it.
        LastSwingHitbox = swing;

        if (!DealsDamage)
            return;

        // A multiplayer client never resolves hits against NPCs.
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
            if (target.whoAmI == NPC.whoAmI || target.friendly || target.dontTakeDamage)
                continue;

            if (target.immortal || target.CountsAsACritter || !target.CanBeChasedBy())
                continue;

            // One hit per target per swing, but any frame of the arc may be the one.
            if (!swing.Intersects(target.Hitbox) || !swingHits.Add(target.whoAmI))
                continue;

            target.SimpleStrikeNPC(
                damage,
                target.Center.X < NPC.Center.X ? -1 : 1,
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

            target.Hurt(PlayerDeathReason.ByNPC(NPC.whoAmI), damage, target.Center.X < NPC.Center.X ? -1 : 1);
        }
    }

    private int ScaleDamage(int baseDamage) => Math.Max(1, (int)(baseDamage * DamageMultiplier));

    /// <summary>Draws the swing box when the overlay is on. Called from the actor's draw.</summary>
    internal void DrawDebug(SpriteBatch spriteBatch)
    {
        if (PuppetDebug.ShowHitboxes && LastSwingHitbox is { } swing)
            PuppetDebug.DrawHitbox(spriteBatch, swing, Color.Red);
    }
}
