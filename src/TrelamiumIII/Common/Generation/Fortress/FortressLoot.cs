using Terraria.Utilities;
using TrelamiumIII.Content.Ingress;
using TrelamiumIII.Core.Structures;

namespace TrelamiumIII.Common.Generation.Fortress;

/// <summary> Placeholder contents for generated room chests until the fortress has its own rewards. </summary>
public static class FortressLoot
{
    private static LootRule[]? rules;

    public static void Fill(Chest chest, UnifiedRandom random)
    {
        int slot = 0;

        foreach (LootRule rule in rules ??= CreateRules())
        {
            if (!rule.Roll(chest, ref slot, random))
                return;
        }
    }

    private static LootRule[] CreateRules() =>
    [
        new LootRule
        {
            Mode = LootMode.Pick,
            Count = 2,
            Entries =
            [
                Entry(ItemID.RegenerationPotion, 1, 2),
                Entry(ItemID.IronskinPotion, 1, 2),
                Entry(ItemID.SwiftnessPotion, 1, 2),
                Entry(ItemID.SpelunkerPotion, 1, 2),
                Entry(ItemID.ShinePotion, 1, 2),
                Entry(ItemID.NightOwlPotion, 1, 2)
            ]
        },
        new LootRule
        {
            Mode = LootMode.All,
            Entries =
            [
                Entry(ItemID.LesserHealingPotion, 2, 5),
                Entry(ModContent.ItemType<BramblewoodTorch>(), 6, 14),
                Entry(ModContent.ItemType<Bramblewood>(), 10, 25)
            ]
        },
        new LootRule
        {
            Mode = LootMode.Pick,
            Entries = [Entry(ItemID.SilverCoin, 30, 90, weight: 3), Entry(ItemID.GoldCoin, 1, 2)]
        }
    ];

    private static LootEntry Entry(int type, int minStack, int maxStack, int weight = 1) => new(new Item(type), minStack, maxStack, weight);
}
