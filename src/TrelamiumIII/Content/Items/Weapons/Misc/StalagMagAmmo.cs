using System.Collections.Generic;
using TrelamiumIII.Content.Items.Weapons.Misc;

namespace TrelamiumIII.Content.Items.Weapons.Misc;

/// <summary>A round's family, which decides how it loads and what it does on impact.</summary>
public enum StalagMagFamily
{
    /// <summary>A raw block off the wall. Shatters into shards of its own stone.</summary>
    Block,

    /// <summary>A knapped spike. Sticks into whatever it lands in.</summary>
    Stalagmite,

    /// <summary>The heavy shell. Loaded on the alternate trigger and on nothing else.</summary>
    Boulder,
}

/// <summary>One StalagMag round as data; progression lives in the ammo, so a new stone is one table entry.</summary>
public readonly record struct StalagMagShot
{
    public StalagMagFamily Family { get; init; }

    /// <summary>Tile the round is cut from. What the debris is drawn out of.</summary>
    public ushort TileType { get; init; }

    /// <summary>
    ///     Projectile fired, or 0 for the family default (mod ids aren't compile-time constants). See <see cref="StalagMagAmmo.ResolveProjectile" />.
    /// </summary>
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

/// <summary>Non-vanilla ammunition, implemented by the item itself so its numbers live beside it.</summary>
public interface IStalagMagAmmo
{
    StalagMagShot Shot { get; }
}

/// <summary>Everything the StalagMag accepts, and how to find it in an inventory.</summary>
public static class StalagMagAmmo
{
    /// <summary>
    ///     Block rounds in <see cref="StoneBlockParticle" /> variant order (the index is the debris variant). Pre-mech stones cap at
    ///     13 damage so Pearlstone is a step up; Titanstone sits well clear; Hellstone pays in burn.
    /// </summary>
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

    /// <summary>The alternate trigger's round: never shatters, has no particle variant, and hits hard (45% extra crit).</summary>
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

    /// <summary>The profile for an item, falling back to plain stone for rounds that lost track of their ammo.</summary>
    public static StalagMagShot GetOrDefault(int itemType)
        => TryGet(itemType, out StalagMagShot shot) ? shot : blocks[0].Shot;

    /// <summary>Whether a vanilla item needs the ammunition stats bolted onto it.</summary>
    public static bool IsVanillaAmmo(int itemType)
        => blockIndex.ContainsKey(itemType) || itemType == ItemID.Boulder;

    /// <summary>Which <see cref="StoneBlockParticle" /> variant a block round's debris wears.</summary>
    public static int BlockVariant(int itemType)
        => blockIndex.TryGetValue(itemType, out int index) ? index : 0;

    /// <summary>What a round spawns: its own <see cref="StalagMagShot.ProjectileType" />, or its family's shared projectile.</summary>
    public static int ResolveProjectile(in StalagMagShot shot)
    {
        if (shot.ProjectileType > 0)
            return shot.ProjectileType;

        return shot.Family == StalagMagFamily.Boulder
            ? ModContent.ProjectileType<BoulderShotProj>()
            : ModContent.ProjectileType<StoneBlockProj>();
    }

    /// <summary>
    ///     The stack the next round comes from, ammo slots first. Hand-rolled because <see cref="Player.ChooseAmmo" /> doesn't know
    ///     the trigger. Side-effect free: the firing cycle also asks it on ticks where nothing fires.
    /// </summary>
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
///     Makes StalagMag blocks ammo while <c>notAmmo</c> keeps them out of ammo slots; tooltip damage comes from the table.
/// </summary>
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
