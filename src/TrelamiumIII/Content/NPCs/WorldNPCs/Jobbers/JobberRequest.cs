using System.Collections.Generic;
using System.IO;
using System.Linq;
using Terraria.ModLoader.IO;
using Terraria.Utilities;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Content.NPCs.WorldNPCs.Jobbers;

public enum JobberRequestKind : byte
{
    Ore,
    Wood,
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
    public const int BasePayment = 900;

    private readonly record struct Entry(int ItemType, int Min, int Max, int PayEach, Func<bool>? Condition = null)
    {
        public bool IsAvailable => Condition?.Invoke() ?? true;
    }

    private static readonly Func<bool> Hardmode = () => Main.hardMode;

    private static readonly (JobberRequestKind Kind, double Weight)[] KindWeights =
    [
        (JobberRequestKind.Ore, 30),
        (JobberRequestKind.Wood, 25),
        (JobberRequestKind.Block, 20),
        (JobberRequestKind.Gem, 17),
        (JobberRequestKind.Accessory, 8),
    ];

    private static readonly Dictionary<JobberRequestKind, Entry[]> Pools = new()
    {
        [JobberRequestKind.Ore] =
        [
            new(ItemID.CopperOre, 20, 35, 30),
            new(ItemID.TinOre, 20, 35, 30),
            new(ItemID.IronOre, 15, 30, 50),
            new(ItemID.LeadOre, 15, 30, 50),
            new(ItemID.SilverOre, 12, 25, 60),
            new(ItemID.TungstenOre, 12, 25, 70),
            new(ItemID.GoldOre, 10, 20, 90),
            new(ItemID.PlatinumOre, 10, 20, 95),
            new(ItemID.DemoniteOre, 10, 20, 105, () => NPC.downedBoss1),
            new(ItemID.CrimtaneOre, 10, 20, 105, () => NPC.downedBoss1),
            new(ItemID.CobaltOre, 10, 20, 250, Hardmode),
            new(ItemID.PalladiumOre, 10, 20, 250, Hardmode),
            new(ItemID.MythrilOre, 8, 16, 320, Hardmode),
            new(ItemID.OrichalcumOre, 8, 16, 320, Hardmode),
            new(ItemID.AdamantiteOre, 6, 12, 400, Hardmode),
            new(ItemID.TitaniumOre, 6, 12, 420, Hardmode),
        ],

        [JobberRequestKind.Wood] =
        [
            new(ItemID.Wood, 40, 80, 3),
            new(ItemID.BorealWood, 30, 60, 4),
            new(ItemID.RichMahogany, 30, 60, 4),
            new(ItemID.PalmWood, 30, 60, 4),
            new(ItemID.AshWood, 25, 50, 6),
            new(ItemID.Ebonwood, 25, 50, 6, () => !WorldGen.crimson),
            new(ItemID.Shadewood, 25, 50, 6, () => WorldGen.crimson),
            new(ItemID.Pearlwood, 25, 50, 12, Hardmode),
        ],

        [JobberRequestKind.Gem] =
        [
            new(ItemID.Amethyst, 4, 8, 1300),
            new(ItemID.Topaz, 4, 8, 1500),
            new(ItemID.Sapphire, 3, 6, 1800),
            new(ItemID.Emerald, 3, 6, 1900),
            new(ItemID.Ruby, 2, 5, 2200),
            new(ItemID.Amber, 2, 5, 2400),
            new(ItemID.Diamond, 2, 4, 3000),
        ],

        [JobberRequestKind.Block] =
        [
            new(ItemID.StoneBlock, 50, 100, 2),
            new(ItemID.SandBlock, 40, 80, 2),
            new(ItemID.MudBlock, 40, 80, 2),
            new(ItemID.SnowBlock, 40, 80, 2),
            new(ItemID.ClayBlock, 30, 60, 3),
            new(ItemID.IceBlock, 30, 60, 3),
            new(ItemID.Marble, 30, 60, 4),
            new(ItemID.Granite, 30, 60, 4),
            new(ItemID.SiltBlock, 20, 40, 5),
            new(ItemID.Glass, 20, 40, 5),
            new(ItemID.Obsidian, 15, 30, 10),
        ],

        // Totals land on round coin amounts once BasePayment is added.
        [JobberRequestKind.Accessory] =
        [
            new(ItemID.Aglet, 1, 1, 9_500),
            new(ItemID.Shackle, 1, 1, 9_500),
            new(ItemID.ClimbingClaws, 1, 1, 9_500),
            new(ItemID.Radar, 1, 1, 9_500),
            new(ItemID.BandofRegeneration, 1, 1, 14_500),
            new(ItemID.HermesBoots, 1, 1, 22_500),
            new(ItemID.FlurryBoots, 1, 1, 22_500),
            new(ItemID.SailfishBoots, 1, 1, 22_500),
            new(ItemID.CloudinaBottle, 1, 1, 22_500),
            new(ItemID.AnkletoftheWind, 1, 1, 33_500),
            new(ItemID.LuckyHorseshoe, 1, 1, 45_500),
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
        int count = Main.rand.Next(chosen.Min, chosen.Max + 1);

        return new JobberRequest(picked, chosen.ItemType, count, BasePayment + chosen.PayEach * count);
    }
}
