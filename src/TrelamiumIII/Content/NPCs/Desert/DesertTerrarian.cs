using TrelamiumIII.Content.Items.GemPendants;
using TrelamiumIII.Core.NPCs;
using System.Collections.Generic;
using TrelamiumIII.Content.Alchemy.Solutions;

namespace TrelamiumIII.Content.NPCs.Desert;

public class DesertTerrarian : Villager
{
    protected override Color[] HairPalette =>
    [
        new(102, 57, 49), // brown 
        new(60,  50,  40),  // dark brown
        new(40,  30,  25),  // black
        new(70, 40,  48),  // brownish
    ];

    protected override Color[] EyePalette =>
    [
        new(120, 80,  60),  // brown
        new(60,  50,  40),  // dark brown
    ];
    protected override Color[] SkinPallete =>
    [
        new Color(200, 116, 92),
        new Color(171, 115, 99),
        new Color(188, 105, 82),
        new Color(198, 123, 71),
    ];

    protected override int[] MaleHairPool => [.. HairDatabase.Tags.Beards, 98, 99, 100, 157];

    protected override int[] FemaleHairPool =>
    [
        55, 83, 84, 88, 89, 95, 135, 164, 101, 102, 103
    ];

    protected override string[] MaleNames =>
    [
        "Khalid", "Omar", "Yusuf", "Hassan", "Karim",
        "Rashid", "Samir", "Nadir", "Tariq", "Zayd",
        "Amir", "Faris", "Hakim", "Ibrahim", "Malik"
    ];

    protected override string[] FemaleNames =>
    [
        "Amina", "Layla", "Zahra", "Nadia", "Samira",
        "Yasmin", "Farah", "Noor", "Salma", "Leila",
        "Iman", "Rania", "Dalia", "Soraya", "Mariam"
    ];

    public override List<string> SetNPCNameList()
    {
        if (Male)
        {
            return [.. MaleNames];
        }

        return [.. FemaleNames];
    }

    private static readonly string[] MaleChat =
    [
        "The heat’s not so bad once you stop fighting it.",
        "You travel far? The sands don’t let go easy.",
        "Water’s worth more than gold out here. Remember that.",
        "Careful where you step. The dunes shift when they feel like it.",
        "Trade’s simple. You bring something useful, I’ll do the same.",
        "The sun teaches patience. Or it burns it out of you.",
        "I’ve seen storms swallow whole camps. No trace left.",
        "You move fast. That won’t last long in this heat.",
        "Gold’s heavy. Water isn’t. Choose what you carry.",
        "I don’t ask questions. I just trade.",
        "You’ve got good timing. Storm passed not long ago.",
        "I’ve walked these sands longer than I care to count.",
        "You seem capable. That’ll take you far.",
        "I’ve got what you need -- if you’ve got what I need.",
        "Storms don’t warn you twice.",
    ];

    private static readonly string[] FemaleChat =
    [
        "Sit a moment. No one wins against the desert in a hurry.",
        "Sand gets everywhere. You learn to live with it.",
        "You hear that wind? Means the weather’s turning.",
        "You’ll want shade soon. Trust me.",
        "Not many come this way without a reason.",
        "The nights are colder than you’d think.",
        "You ever watch the dunes at sunset? Worth it.",
        "Some things buried out here should stay buried.",
        "Careful with fire. The dry air makes it eager.",
        "You carry yourself well. Not your first harsh place, I take it.",
        "Even the quiet out here has weight to it.",
        "Don’t waste your strength. The desert will take the rest.",
        "You planning to stay, or just passing through?",
        "Keep your head covered. Small things matter out here.",
        "You look like you’ve got a good head on your shoulders.",
    ];

    private static readonly string[] NeutralChat =
    [
        "You look lost. Or maybe just new.",
        "You’ve been walking a while, haven’t you?",
        "The desert doesn’t care who you are.",
        "Shade and water. That’s all that matters out here.",
        "You get used to the silence after a while.",
        "The wind carries more than just sand.",
        "Not everything out here wants to be found.",
        "Take your time. The desert isn’t going anywhere.",
        "You learn quickly, or you don’t last.",
        "It’s a long way to anywhere from here.",
    ];

    public override string GetChat()
    {
        timeAlive = 0; // reset their despawn timer if interacted with

        var pool = new List<string>();

        pool.AddRange(NeutralChat);

        if (Male)
            pool.AddRange(MaleChat);
        else
            pool.AddRange(FemaleChat);

        return pool[Main.rand.Next(pool.Count)];
    }

    public override float SpawnChance(NPCSpawnInfo spawnInfo)
    {
        return spawnInfo.Player.ZoneOverworldHeight && Main.dayTime && spawnInfo.Player.ZoneDesert && 
            !spawnInfo.Player.ZoneCorrupt && !spawnInfo.Player.ZoneCrimson && !spawnInfo.Player.ZoneHallow
            ? 0.15f : 0f;
    }

    protected override void SetAppearanceDefaults()
    {
        base.SetAppearanceDefaults();

        SetBodySlot(ItemID.RoninShirt);
        SetLegSlot(ItemID.RoninPants);
    }

    protected override List<Action>SetWorldInventory() =>
    [
        () => AddInvItem(ItemID.Amethyst, Main.rand.Next(3, 8)),
        () => AddInvItem(ItemID.Topaz, Main.rand.Next(3, 8)),
        () => AddInvItem(ItemID.Emerald, Main.rand.Next(2, 6)),
        () => AddInvItem(ItemID.Ruby, Main.rand.Next(1, 4)),
        () => AddInvItem(ItemID.Diamond, Main.rand.Next(1, 2)),

        () => AddInvItem(ItemID.DesertTorch, Main.rand.Next(10, 25)),
        () => AddInvItem(ItemID.BottledWater, Main.rand.Next(5, 12)),
        () => AddInvItem(ItemID.Cactus, Main.rand.Next(20, 50)),

        () => AddInvItem(ItemID.HealingPotion, Main.rand.Next(3, 7)),
        () => AddInvItem(ItemID.SwiftnessPotion, Main.rand.Next(2, 5)),

        () => AddInvItem(ModContent.ItemType<TopazPendantItem>(), 2),
        () => AddInvItem(ModContent.ItemType<SapphirePendantItem>(), 2),
        () => AddInvItem(ModContent.ItemType<RubyPendantItem>(), 2),
        () => AddInvItem(ModContent.ItemType<AmberPendantItem>(), 2),
        () => AddInvItem(ItemID.SilverCoin, Main.rand.Next(30, 90)),
    ];

    protected override List<StockEntry> GetStockPool() =>
    [
        new(() => AddInvItem(ItemID.Amethyst, Main.rand.Next(3, 8)),
            Trade().For(ItemID.TinOre, 2).Grants(ItemID.Amethyst)),

        new(() => AddInvItem(ItemID.Topaz, Main.rand.Next(3, 8)),
            Trade().For(ItemID.LeadOre, 2).Grants(ItemID.Topaz)),

        new(() => AddInvItem(ItemID.Emerald, Main.rand.Next(2, 6)),
            Trade().For(ItemID.IronOre).Grants(ItemID.Emerald, 2)),

        new(() => AddInvItem(ItemID.Ruby, Main.rand.Next(3, 6)),
            Trade().For(ItemID.GoldOre).Grants(ItemID.Ruby)),

        new(() => AddInvItem(ItemID.Diamond, Main.rand.Next(1, 2)),
            Trade().For(ItemID.PlatinumOre).Grants(ItemID.Diamond)),

        new(() => AddInvItem(ModContent.ItemType<TopazPendantItem>(), 2),
            Trade().For(ItemID.TinBar, 5).Grants(ModContent.ItemType<TopazPendantItem>())),

        new(() => AddInvItem(ModContent.ItemType<SapphirePendantItem>()),
            Trade().ForCoins(Item.buyPrice(gold: 1))
                            .Grants(ModContent.ItemType<SapphirePendantItem>())),

        new(() => AddInvItem(ModContent.ItemType<AmberPendantItem>()),
            Trade().ForCoins(Item.buyPrice(gold: 2))
                            .Grants(ModContent.ItemType<AmberPendantItem>())),

        new(() => AddInvItem(SolventID.BriskTonic, 5),
            Trade().For(ItemID.Sapphire, 3)
                            .Grants(SolventID.BriskTonic)),

        new(() => AddInvItem(SolventID.HarmoniousTonic, 5),
            Trade().For(ItemID.CopperOre, 8)
                            .Grants(SolventID.HarmoniousTonic)),

        new(() => AddInvItem(SolventID.BaneTonic, 12),
            Trade().For(ItemID.EbonsandBlock, 20)
                            .Grants(SolventID.BaneTonic, 2)),

        new(() => AddInvItem(SolventID.BaneTonic, 12),
            Trade().For(ItemID.CrimsandBlock, 20)
                            .Grants(SolventID.BaneTonic, 2)),
    ];
}

