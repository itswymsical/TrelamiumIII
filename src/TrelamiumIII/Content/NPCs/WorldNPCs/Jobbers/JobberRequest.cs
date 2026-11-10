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
    /// <param name="itemID">The type of item to request.</param>
    /// <param name="min">The minimum quantity to request.</param>
    /// <param name="max">The maximum quantity to request.</param>
    /// <param name="value">coin value per item</param>
    /// <param name="Condition">A function that determines if the entry is available.</param>
    private readonly record struct Entry(int itemID, int min, int max, int value, Func<bool>? Condition = null)
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
            new(itemID: ItemID.CopperOre, min: 20, max: 35, value: 30),
            new(itemID: ItemID.TinOre, min: 20, max: 35, value: 30),
            new(itemID: ItemID.IronOre, min: 15, max: 30, value: 50),
            new(itemID: ItemID.LeadOre, min: 15, max: 30, value: 50),
            new(itemID: ItemID.SilverOre, min: 12, max: 25, value: 60),
            new(itemID: ItemID.TungstenOre, min: 12, max: 25, value: 70),
            new(itemID: ItemID.GoldOre, min: 10, max: 20, value: 90),
            new(itemID: ItemID.PlatinumOre, min: 10, max: 20, value: 95),

            new(itemID: SoluteID.Sulfur, min: 6, max: 20, value: 80),
            new(itemID: SoluteID.Cinnabar, min: 6, max: 20, value: 100),
            new(itemID: SoluteID.PureSalt, min: 7, max: 14, value: 50),
            new(itemID: SoluteID.Potash, min: 4, max: 10, value: 5),
            new(itemID: SoluteID.Niter, min: 6, max: 13, value: 20),

            new(itemID: ItemID.DemoniteOre, min: 10, max: 20, value: 105, () => NPC.downedBoss1),
            new(itemID: ItemID.CrimtaneOre, min: 10, max: 20, value: 105, () => NPC.downedBoss1),

            new(itemID: ItemID.CobaltOre, min: 10, max: 20, value: 250, Hardmode),
            new(itemID: ItemID.PalladiumOre, min: 10, max: 20, value: 250, Hardmode),
            new(itemID: ItemID.MythrilOre, min: 8, max: 16, value: 320, Hardmode),
            new(itemID: ItemID.OrichalcumOre, min: 8, max: 16, value: 320, Hardmode),
            new(itemID: ItemID.AdamantiteOre, min: 6, max: 12, value: 400, Hardmode),
            new(itemID: ItemID.TitaniumOre, min: 6, max: 12, value: 420, Hardmode),
        ],

        [JobberRequestKind.ResourceItems] =
        [
            new(itemID: ItemID.Wood, min: 40, max: 80, value: 10),
            new(itemID: ItemID.ClayBlock, min: 30, max: 60, value: 6),

            new(itemID: ItemID.SlushBlock, min: 30, max: 60, value: 2),
            new(itemID: ItemID.SiltBlock, min: 65, max: 110, value: 2),

            new(itemID: ItemID.Rope, min: 45, max: 95, value: 8),
            new(itemID: ItemID.VineRope, min: 45, max: 110, value: 3),
            new(itemID: ItemID.Seashell, min: 3, max: 12, value: 55),

            new(itemID: ItemID.EbonstoneBlock, min: 25, max: 50, value: 20, () => !WorldGen.crimson),
            new(itemID: ItemID.CrimstoneBlock, min: 25, max: 50, value: 20, () => WorldGen.crimson),
            new(itemID: ItemID.PearlstoneBlock, min: 25, max: 50, value: 100, Hardmode),
        ],

        [JobberRequestKind.Gem] =
        [
            new(itemID: ItemID.Amethyst, min: 4, max: 8, value: 1300),
            new(itemID: ItemID.Topaz, min: 4, max: 8, value: 1500),
            new(itemID: ItemID.Sapphire, min: 3, max: 6, value: 1800),
            new(itemID: ItemID.Emerald, min: 3, max: 6, value: 1900),
            new(itemID: ItemID.Ruby, min: 2, max: 5, value: 2200),
            new(itemID: ItemID.Amber, min: 2, max: 5, value: 2400),
            new(itemID: ItemID.Diamond, min: 2, max: 4, value: 3000),
            new(itemID: ItemID.CrystalShard, min: 9, max: 18, value: 5000, Hardmode),
        ],

        [JobberRequestKind.Block] =
        [
            new(itemID: ItemID.StoneBlock, min: 50, max: 100, value: 10),
            new(itemID: ItemID.SandBlock, min: 40, max: 80, value: 5),
            new(itemID: ItemID.MudBlock, min: 40, max: 80, value: 5),
            new(itemID: ItemID.SnowBlock, min: 40, max: 80, value: 5),
            new(itemID: ItemID.ClayBlock, min: 30, max: 60, value: 3),
            new(itemID: ItemID.IceBlock, min: 30, max: 60, value: 3),
            new(itemID: ItemID.Marble, min: 30, max: 60, value: 4),
            new(itemID: ItemID.Granite, min: 30, max: 60, value: 4),
            new(itemID: ItemID.SiltBlock, min: 20, max: 40, value: 5),
            new(itemID: ItemID.Glass, min: 20, max: 40, value: 5),
            new(itemID: ItemID.Obsidian, min: 15, max: 30, value: 10),
        ],

        // Totals land on round coin amounts once BasePayment is added.
        [JobberRequestKind.Accessory] =
        [
            new(itemID: ItemID.Aglet, min: 1, max: 1, value: 9_500),
            new(itemID: ItemID.Shackle, min: 1, max: 1, value: 9_500),
            new(itemID: ItemID.ClimbingClaws, min: 1, max: 1, value: 9_500),
            new(itemID: ItemID.Radar, min: 1, max: 1, value: 9_500),
            new(itemID: ItemID.BandofRegeneration, min: 1, max: 1, value: 14_500),
            new(itemID: ItemID.HermesBoots, min: 1, max: 1, value: 22_500),
            new(itemID: ItemID.FlurryBoots, min: 1, max: 1, value: 22_500),
            new(itemID: ItemID.SailfishBoots, min: 1, max: 1, value: 22_500),
            new(itemID: ItemID.CloudinaBottle, min: 1, max: 1, value: 22_500),
            new(itemID: ItemID.AnkletoftheWind, min: 1, max: 1, value: 33_500),
            new(itemID: ItemID.LuckyHorseshoe, min: 1, max: 1, value: 45_500),
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

        return new JobberRequest(picked, chosen.itemID, count, BasePayment + chosen.value * count);
    }
}