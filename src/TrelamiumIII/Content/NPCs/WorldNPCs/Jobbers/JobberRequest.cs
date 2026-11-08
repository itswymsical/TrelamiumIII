using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.ModLoader.IO;
using Terraria.Utilities;
using TrelamiumIII.Content.Alchemy.Reagents;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Jobbers;

public enum JobberRequestKind : byte
{
    MaterialItem,
    ResourceItems,
    Gem,
    Block,
    Accessory
}

/// <summary>What a <see cref="Jobber"/> wants gathered, and what it pays in copper.</summary>
public readonly record struct JobberRequest(JobberRequestKind Kind, int ItemType, int Count, int Payment)
{
    private static readonly (int Coin, int Value)[] Coins =
    [
        (ItemID.PlatinumCoin, 1_000_000),
        (ItemID.GoldCoin, 10_000),
        (ItemID.SilverCoin, 100),
        (ItemID.CopperCoin, 1),
    ];

    public string ItemName => Lang.GetItemNameValue(ItemType);

    /// <summary><see cref="Payment"/> split into coin stacks, largest first.</summary>
    public List<JobReward> Rewards()
    {
        List<JobReward> rewards = [];
        int remaining = Payment;

        foreach ((int coin, int value) in Coins)
        {
            if (remaining < value)
                continue;

            rewards.Add(new JobReward(coin, remaining / value));
            remaining %= value;
        }

        return rewards;
    }

    public void Save(TagCompound tag)
    {
        tag["reqKind"] = (byte)Kind;
        tag["reqItem"] = ItemType;
        tag["reqCount"] = Count;
        tag["reqPay"] = Payment;
    }

    public static JobberRequest? Load(TagCompound tag)
    {
        if (!tag.TryGet("reqItem", out int itemType) || itemType <= ItemID.None)
            return null;

        return new JobberRequest(
            (JobberRequestKind)tag.GetByte("reqKind"), itemType, Math.Max(1, tag.GetInt("reqCount")), tag.GetInt("reqPay"));
    }

    public static void Write(BinaryWriter writer, JobberRequest? request)
    {
        writer.Write(request.HasValue);

        if (request is not { } value)
            return;

        writer.Write((byte)value.Kind);
        writer.Write(value.ItemType);
        writer.Write(value.Count);
        writer.Write(value.Payment);
    }

    public static JobberRequest? Read(BinaryReader reader)
    {
        if (!reader.ReadBoolean())
            return null;

        return new JobberRequest((JobberRequestKind)reader.ReadByte(), reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
    }
}

/// <summary>
/// The pools <see cref="Jobber"/> requests are rolled from. Payment is <see cref="BasePayment"/> plus a per-item
/// rate, in copper. All values are provisional.
/// </summary>
public static class JobberRequests
{
    public const int BasePayment = 1000;
    /// <summary>
    /// Defines an entry in the jobber request pool.
    /// </summary>
    /// <param name="itemIDs">The type of item to request.</param>
    /// <param name="min">The minimum quantity to request.</param>
    /// <param name="max">The maximum quantity to request.</param>
    /// <param name="value">coin value per item</param>
    /// <param name="Condition">A function that determines if the entry is available.</param>
    private readonly record struct Entry(int min, int max, int value, Func<bool>? Condition = null, params int[] itemIDs)
    {
        public bool IsAvailable => Condition?.Invoke() ?? true;
    }


    private static readonly Func<bool> Hardmode = () => Main.hardMode;

    private static readonly (JobberRequestKind Kind, double Weight)[] KindWeights =
    [
        (JobberRequestKind.MaterialItem, 30),
        (JobberRequestKind.ResourceItems, 25),
        (JobberRequestKind.Block, 20),
        (JobberRequestKind.Gem, 17),
        (JobberRequestKind.Accessory, 8),
    ];

    private static readonly Dictionary<JobberRequestKind, Entry[]> Pools = new()
    {
        [JobberRequestKind.MaterialItem] =
        [
            new(itemIDs: ItemID.CopperOre, min: 20, max: 35, value: 30),
            new(itemIDs: ItemID.TinOre, min: 20, max: 35, value: 30),
            new(itemIDs: ItemID.IronOre, min: 15, max: 30, value: 50),
            new(itemIDs: ItemID.LeadOre, min: 15, max: 30, value: 50),
            new(itemIDs: ItemID.SilverOre, min: 12, max: 25, value: 60),
            new(itemIDs: ItemID.TungstenOre, min: 12, max: 25, value: 70),
            new(itemIDs: ItemID.GoldOre, min: 10, max: 20, value: 90),
            new(itemIDs: ItemID.PlatinumOre, min: 10, max: 20, value: 95),

            new(itemIDs: SoluteID.Sulfur, min: 6, max: 20, value: 80),
            new(itemIDs: SoluteID.Cinnabar, min: 6, max: 20, value: 100),
            new(itemIDs: SoluteID.PureSalt, min: 7, max: 14, value: 50),
            new(itemIDs: SoluteID.Potash, min: 4, max: 10, value: 5),
            new(itemIDs: SoluteID.Niter, min: 6, max: 13, value: 20),

            new(min: 10, max: 20, value: 105, () => NPC.downedBoss1, itemIDs: ItemID.DemoniteOre),
            new(min: 10, max: 20, value: 105, () => NPC.downedBoss1, itemIDs: ItemID.CrimtaneOre),

            new(min: 10, max: 20, value: 250, Hardmode, itemIDs: ItemID.CobaltOre),
            new(min: 10, max: 20, value: 250, Hardmode, itemIDs: ItemID.PalladiumOre),
            new(min: 8, max: 16, value: 320, Hardmode, itemIDs: ItemID.MythrilOre),
            new(min: 8, max: 16, value: 320, Hardmode, itemIDs: ItemID.OrichalcumOre),
            new(min: 6, max: 12, value: 400, Hardmode, itemIDs: ItemID.AdamantiteOre),
            new(min: 6, max: 12, value: 420, Hardmode, itemIDs: ItemID.TitaniumOre),
        ],

        [JobberRequestKind.ResourceItems] =
        [
            new(min: 40, max: 80, value: 10, itemIDs: [ItemID.Wood]),
            new(itemIDs: ItemID.ClayBlock, min: 30, max: 60, value: 6),

            new(itemIDs: ItemID.SlushBlock, min: 30, max: 60, value: 2),
            new(itemIDs: ItemID.SiltBlock, min: 65, max: 110, value: 2),

            new(itemIDs: ItemID.Rope, min: 45, max: 95, value: 8),
            new(itemIDs: ItemID.VineRope, min: 45, max: 110, value: 3),
            new(itemIDs: [ItemID.Seashell, ItemID.Starfish, ItemID.Coral], min: 3, max: 12, value: 55),

            new(min: 25, max: 50, value: 20, () => !WorldGen.crimson, itemIDs: ItemID.EbonstoneBlock),
            new(min: 25, max: 50, value: 20, () => WorldGen.crimson, itemIDs: ItemID.CrimstoneBlock),
            new(min: 25, max: 50, value: 100, Hardmode, itemIDs: ItemID.PearlstoneBlock),
        ],

        [JobberRequestKind.Gem] =
        [
            new(itemIDs: ItemID.Amethyst, min: 4, max: 8, value: 1300),
            new(itemIDs: ItemID.Topaz, min: 4, max: 8, value: 1500),
            new(itemIDs: ItemID.Sapphire, min: 3, max: 6, value: 1800),
            new(itemIDs: ItemID.Emerald, min: 3, max: 6, value: 1900),
            new(itemIDs: ItemID.Ruby, min: 2, max: 5, value: 2200),
            new(itemIDs: ItemID.Amber, min: 2, max: 5, value: 2400),
            new(itemIDs: ItemID.Diamond, min: 2, max: 4, value: 3000),
            new(min: 9, max: 18, value: 5000, Hardmode, itemIDs: ItemID.CrystalShard),
        ],

        [JobberRequestKind.Block] =
        [
            new(itemIDs: ItemID.StoneBlock, min: 50, max: 100, value: 10),
            new(itemIDs: ItemID.SandBlock, min: 40, max: 80, value: 5),
            new(itemIDs: ItemID.MudBlock, min: 40, max: 80, value: 5),
            new(itemIDs: ItemID.SnowBlock, min: 40, max: 80, value: 5),
            new(itemIDs: ItemID.ClayBlock, min: 30, max: 60, value: 3),
            new(itemIDs: ItemID.IceBlock, min: 30, max: 60, value: 3),
            new(itemIDs: ItemID.Marble, min: 30, max: 60, value: 4),
            new(itemIDs: ItemID.Granite, min: 30, max: 60, value: 4),
            new(itemIDs: ItemID.SiltBlock, min: 20, max: 40, value: 5),
            new(itemIDs: ItemID.Glass, min: 20, max: 40, value: 5),
            new(itemIDs: ItemID.Obsidian, min: 15, max: 30, value: 10),
        ],

        // Totals land on round coin amounts once BasePayment is added.
        [JobberRequestKind.Accessory] =
        [
            new(itemIDs: ItemID.Aglet, min: 1, max: 1, value: 9_500),
            new(itemIDs: ItemID.Shackle, min: 1, max: 1, value: 9_500),
            new(itemIDs: ItemID.ClimbingClaws, min: 1, max: 1, value: 9_500),
            new(itemIDs: ItemID.Radar, min: 1, max: 1, value: 9_500),
            new(itemIDs: ItemID.BandofRegeneration, min: 1, max: 1, value: 14_500),
            new(itemIDs: ItemID.HermesBoots, min: 1, max: 1, value: 22_500),
            new(itemIDs: ItemID.FlurryBoots, min: 1, max: 1, value: 22_500),
            new(itemIDs: ItemID.SailfishBoots, min: 1, max: 1, value: 22_500),
            new(itemIDs: ItemID.CloudinaBottle, min: 1, max: 1, value: 22_500),
            new(itemIDs: ItemID.AnkletoftheWind, min: 1, max: 1, value: 33_500),
            new(itemIDs: ItemID.LuckyHorseshoe, min: 1, max: 1, value: 45_500),
        ],
    };

    public static JobberRequest Roll()
    {
        var kinds = new WeightedRandom<JobberRequestKind>(Main.rand);

        foreach ((JobberRequestKind kind, double weight) in KindWeights)
        {
            if (Pools[kind].Any(entry => entry.IsAvailable))
                kinds.Add(kind, weight);
        }

        JobberRequestKind picked = kinds.Get();
        Entry chosen = Main.rand.Next(Pools[picked].Where(entry => entry.IsAvailable).ToArray());
        int count = Main.rand.Next(chosen.min, chosen.max + 1);

        foreach (int itemID in chosen.itemIDs)
        {
            if (Main.rand.NextBool(chosen.itemIDs.Length))
                return new JobberRequest(picked, itemID, count, BasePayment + chosen.value * count);
        }

        return new JobberRequest(picked, chosen.itemIDs[0], count, BasePayment + chosen.value * count);
    }
}