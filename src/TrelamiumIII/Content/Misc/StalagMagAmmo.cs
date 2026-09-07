using System.Collections.Generic;

namespace TrelamiumIII.Content.Misc;

/// <summary>
///     Which family a round belongs to, which is the same thing as saying how it is loaded and what
///     it does when it lands.
/// </summary>
public enum StalagMagFamily
{
    /// <summary>A raw block off the wall. Shatters into shards of its own stone.</summary>
    Block,

    /// <summary>A knapped spike. Sticks into whatever it lands in.</summary>
    Stalagmite,

    /// <summary>The heavy shell. Loaded on the alternate trigger and on nothing else.</summary>
    Boulder,
}

/// <summary>
///     One round out of the StalagMag, as numbers.
/// </summary>
/// <remarks>
///     The weapon has no damage of its own worth speaking of -- it is a tube that throws whatever is
///     shovelled into it, so the ammunition is where the whole of the progression lives. Stone is
///     what you have; Titanstone is what you graduate to. Keeping that as a table of values rather
///     than as a chain of <c>if (ammo == ...)</c> in the firing path is what makes adding the next
///     stone a one-line change.
/// </remarks>
public readonly record struct StalagMagShot
{
    /// <summary>How the round is loaded and what it does on impact.</summary>
    public StalagMagFamily Family { get; init; }

    /// <summary>Tile the round is cut from. What the debris is drawn out of.</summary>
    public ushort TileType { get; init; }

    /// <summary>
    ///     Projectile fired, or 0 to take the family's default.
    /// </summary>
    /// <remarks>
    ///     Left at zero for everything vanilla, because a mod projectile's id is not a compile-time
    ///     constant and this table has to be readable from a static initialiser. Only the stalagmite
    ///     items -- which are mod content themselves, and are asked for their profile at runtime --
    ///     fill it in. See <see cref="StalagMagAmmo.ResolveProjectile" />.
    /// </remarks>
    public int ProjectileType { get; init; }

    public int Damage { get; init; }

    public float Knockback { get; init; }

    public float ShootSpeed { get; init; }

    /// <summary>Crit chance before the holder's own ranged crit is added on top.</summary>
    public int Crit { get; init; }

    /// <summary>Debuff applied on hit, or 0 for none.</summary>
    public int Buff { get; init; }

    /// <summary>Ticks the debuff lasts.</summary>
    public int BuffTime { get; init; }
}

/// <summary>
///     Ammunition that is not a vanilla block, and so cannot live in the table.
/// </summary>
/// <remarks>
///     Implemented by the ammunition item itself rather than registered into a dictionary at load,
///     which keeps a round's numbers next to the item they belong to and sidesteps the load-order
///     question of when a mod item's id becomes knowable.
/// </remarks>
public interface IStalagMagAmmo
{
    StalagMagShot Shot { get; }
}

/// <summary>
///     Everything the StalagMag will eat, and how to find it in a player's inventory.
/// </summary>
public static class StalagMagAmmo
{
    /// <summary>
    ///     The block rounds, in the order <see cref="StoneBlockParticle" /> lists its variants.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Progression runs on the stone rather than on the gun. Everything reachable before a
    ///         mechanical boss tops out at 13 so that Pearlstone -- which is free the moment the
    ///         world turns -- is a step up rather than a sidegrade, and Titanstone sits well clear
    ///         of all of it. Hellstone pays for the trip to the Underworld in burn rather than in
    ///         raw damage, which keeps the ordering honest.
    ///     </para>
    ///     <para>
    ///         One array rather than a dictionary because the position in it is also the particle
    ///         variant: the debris that comes off a Crimstone round has to be Crimstone-coloured,
    ///         and deriving the index from the same table that holds the numbers means the two can
    ///         never drift apart.
    ///     </para>
    /// </remarks>
    private static readonly (int Item, StalagMagShot Shot)[] blocks =
    [
        (ItemID.StoneBlock, new StalagMagShot
        {
            Family = StalagMagFamily.Block,
            TileType = TileID.Stone,
            Damage = 10,
            Knockback = 15f,
            ShootSpeed = 25f,
            Crit = 4,
            Buff = BuffID.BrokenArmor,
            BuffTime = 60,
        }),
        (ItemID.MudstoneBlock, new StalagMagShot
        {
            Family = StalagMagFamily.Block,
            TileType = TileID.Mudstone,
            Damage = 11,
            Knockback = 17f,
            ShootSpeed = 23f,
            Crit = 4,
            Buff = BuffID.Slow,
            BuffTime = 90,
        }),
        (ItemID.Sandstone, new StalagMagShot
        {
            Family = StalagMagFamily.Block,
            TileType = TileID.Sandstone,
            Damage = 12,
            Knockback = 13f,
            ShootSpeed = 26f,
            Crit = 4,
            Buff = BuffID.Confused,
            BuffTime = 60,
        }),
        (ItemID.EbonstoneBlock, new StalagMagShot
        {
            Family = StalagMagFamily.Block,
            TileType = TileID.Ebonstone,
            Damage = 13,
            Knockback = 16f,
            ShootSpeed = 25f,
            Crit = 4,
            Buff = BuffID.Poisoned,
            BuffTime = 180,
        }),
        (ItemID.CrimstoneBlock, new StalagMagShot
        {
            Family = StalagMagFamily.Block,
            TileType = TileID.Crimstone,
            Damage = 13,
            Knockback = 16f,
            ShootSpeed = 25f,
            Crit = 4,
            Buff = BuffID.Bleeding,
            BuffTime = 300,
        }),
        (ItemID.Hellstone, new StalagMagShot
        {
            Family = StalagMagFamily.Block,
            TileType = TileID.Hellstone,
            Damage = 13,
            Knockback = 15f,
            ShootSpeed = 24f,
            Crit = 4,
            Buff = BuffID.OnFire,
            BuffTime = 180,
        }),
        (ItemID.PearlstoneBlock, new StalagMagShot
        {
            Family = StalagMagFamily.Block,
            TileType = TileID.Pearlstone,
            Damage = 14,
            Knockback = 16f,
            ShootSpeed = 25f,
            Crit = 4,
            Buff = BuffID.BrokenArmor,
            BuffTime = 180,
        }),
        (ItemID.TitanstoneBlock, new StalagMagShot
        {
            Family = StalagMagFamily.Block,
            TileType = TileID.Titanstone,
            Damage = 18,
            Knockback = 18f,
            ShootSpeed = 22f,
            Crit = 4,
            Buff = BuffID.BrokenArmor,
            BuffTime = 240,
        }),
    ];

    /// <summary>
    ///     The alternate trigger's round: slow to reload, brutal when it lands.
    /// </summary>
    /// <remarks>
    ///     Not in <see cref="blocks" /> because it is not one -- it never shatters into shards, it
    ///     is never picked by the primary trigger, and it has no particle variant. Its numbers are
    ///     the ones that make the alternate fire worth the ammunition: forty-five percent crit on
    ///     top of the holder's own, against three rounds every three seconds.
    /// </remarks>
    private static readonly StalagMagShot boulder = new()
    {
        Family = StalagMagFamily.Boulder,
        TileType = TileID.Stone,
        Damage = 40,
        Knockback = 20f,
        ShootSpeed = 18f,
        Crit = 45,
        Buff = BuffID.BrokenArmor,
        BuffTime = 300,
    };

    private static readonly Dictionary<int, int> blockIndex = BuildIndex();

    /// <summary>One texture per block round, for <see cref="StoneBlockParticle" />'s variants.</summary>
    public static string[] BlockTextures { get; } = BuildTextures();

    /// <summary>Every block round the weapon accepts, for recipes and tooltips.</summary>
    public static IEnumerable<int> BlockItems
    {
        get
        {
            foreach ((int item, _) in blocks)
                yield return item;
        }
    }

    /// <summary>The profile for one ammunition item, if the weapon takes it at all.</summary>
    public static bool TryGet(int itemType, out StalagMagShot shot)
    {
        if (blockIndex.TryGetValue(itemType, out int index))
        {
            shot = blocks[index].Shot;

            return true;
        }

        if (itemType == ItemID.Boulder)
        {
            shot = boulder;

            return true;
        }

        if (ItemLoader.GetItem(itemType) is IStalagMagAmmo ammo)
        {
            shot = ammo.Shot;

            return true;
        }

        shot = default;

        return false;
    }

    /// <summary>
    ///     The profile for one ammunition item, falling back to plain stone.
    /// </summary>
    /// <remarks>
    ///     For the read side, where a round in flight is asking what it was loaded from. A shot that
    ///     has lost track of its ammunition -- an old projectile out of a save, a spawn that never
    ///     got told -- should look like the commonest round rather than like nothing at all.
    /// </remarks>
    public static StalagMagShot GetOrDefault(int itemType)
        => TryGet(itemType, out StalagMagShot shot) ? shot : blocks[0].Shot;

    /// <summary>Whether a vanilla item needs the ammunition stats bolted onto it.</summary>
    public static bool IsVanillaAmmo(int itemType)
        => blockIndex.ContainsKey(itemType) || itemType == ItemID.Boulder;

    /// <summary>Which <see cref="StoneBlockParticle" /> variant a block round's debris wears.</summary>
    public static int BlockVariant(int itemType)
        => blockIndex.TryGetValue(itemType, out int index) ? index : 0;

    /// <summary>
    ///     What a round actually spawns.
    /// </summary>
    /// <remarks>
    ///     The families that are entirely vanilla-fed share one projectile each and resolve it here,
    ///     at a point where mod content is loaded; anything carrying its own
    ///     <see cref="StalagMagShot.ProjectileType" /> has already answered the question itself.
    /// </remarks>
    public static int ResolveProjectile(in StalagMagShot shot)
    {
        if (shot.ProjectileType > 0)
            return shot.ProjectileType;

        return shot.Family == StalagMagFamily.Boulder
            ? ModContent.ProjectileType<BoulderShotProj>()
            : ModContent.ProjectileType<StoneBlockProj>();
    }

    /// <summary>
    ///     The stack the next round comes out of, ammunition slots first.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Hand-rolled rather than deferred to <see cref="Player.ChooseAmmo" /> because the
    ///         choice depends on which trigger is being pulled, and vanilla's selection has no
    ///         notion of that -- it would happily load a boulder into the primary fire. Searching
    ///         the ammunition slots before the pack keeps the vanilla habit intact, so a player who
    ///         wants Titanstone spent before their Stone puts it in an ammunition slot and it is.
    ///     </para>
    ///     <para>
    ///         Side-effect free, because the firing cycle asks it on ticks where nothing is fired to
    ///         find out whether a committed burst can be finished.
    ///     </para>
    /// </remarks>
    public static Item? Choose(Player player, bool boulder)
    {
        for (int i = 54; i < 58; i++)
            if (Matches(player.inventory[i], boulder))
                return player.inventory[i];

        for (int i = 0; i < 54; i++)
            if (Matches(player.inventory[i], boulder))
                return player.inventory[i];

        return null;
    }

    private static bool Matches(Item item, bool boulder)
        => item.stack > 0
           && TryGet(item.type, out StalagMagShot shot)
           && (shot.Family == StalagMagFamily.Boulder) == boulder;

    private static Dictionary<int, int> BuildIndex()
    {
        var index = new Dictionary<int, int>(blocks.Length);

        for (int i = 0; i < blocks.Length; i++)
            index[blocks[i].Item] = i;

        return index;
    }

    private static string[] BuildTextures()
    {
        var textures = new string[blocks.Length];

        for (int i = 0; i < blocks.Length; i++)
            textures[i] = $"Images/Item_{blocks[i].Item}";

        return textures;
    }
}

/// <summary>
///     Turns the blocks the StalagMag eats into things a player can put in an ammunition slot.
/// </summary>
/// <remarks>
///     <c>notAmmo</c> is what keeps them out of the way the rest of the time: a stack of stone is a
///     building material first and a magazine second, and without it every block picked up would be
///     shovelled into the ammunition slots. The damage figure is set from the same table the weapon
///     fires off, so the number on a block's tooltip is the number it actually hits for.
/// </remarks>
public sealed class StalagMagAmmoItem : GlobalItem
{
    public override bool AppliesToEntity(Item entity, bool lateInstantiation)
        => StalagMagAmmo.IsVanillaAmmo(entity.type);

    public override void SetDefaults(Item entity)
    {
        base.SetDefaults(entity);

        if (!StalagMagAmmo.TryGet(entity.type, out StalagMagShot shot))
            return;

        entity.DamageType = DamageClass.Ranged;
        entity.damage = shot.Damage;
        entity.knockBack = shot.Knockback;
        entity.shootSpeed = shot.ShootSpeed;
        entity.crit = shot.Crit;
        entity.noMelee = true;
        entity.notAmmo = true;
        entity.ammo = entity.type;
    }
}
