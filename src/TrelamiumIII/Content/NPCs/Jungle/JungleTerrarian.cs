using System.Collections.Generic;
using Terraria.ModLoader.Default.Patreon;
using TrelamiumIII.Core.NPCs;
using TrelamiumIII.Content.Alchemy.Solutions;

namespace TrelamiumIII.Content.NPCs.Jungle;

public class JungleTerrarian : Villager
{
    protected override Color[] HairPalette =>
    [        
        new(102, 57, 49), // brown 
        new(60,  50,  40),  // dark brown
        new(40,  30,  25),  // black
        new(50,  50,  45),  // less black (lol)
        new(70, 40,  48),  // brownish
    ];

    protected override Color[] EyePalette =>
    [
        new(80,  162, 224), // blue
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

    protected override int[] MaleHairPool =>
    [
        1, 57, 51, 76, 135, 136, 137, 147, 157, 148
    ];

    protected override int[] FemaleHairPool =>
    [
        6, 55, 88, 84, 88, 89, 97, 135, 164
    ];

    protected override string[] MaleNames =>
    [
        "Mareo", "Tavio", "Ralen", "Soren", "Kaelo",
        "Jaro", "Niven", "Elio", "Varo", "Zalen",
        "Amaro", "Lior", "Renzo", "Talen", "Kairo"
    ];

    protected override string[] FemaleNames =>
    [
        "Lina", "Mara", "Sela", "Nari", "Elara",
        "Vena", "Talia", "Rina", "Zara", "Mirel",
        "Anira", "Liora", "Senna", "Kaia", "Velin"
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
        "Careful where you step. The ground moves more than you think.",
        "You hear that? Means something noticed you first.",
        "It’s never really quiet out here.",
        "You get used to the humidity. Eventually.",
        "Don’t cut through the brush unless you have to.",
        "The vines look harmless. They aren’t always.",
        "I’ve seen people walk in circles for days out here.",
        "You move loud. The jungle remembers that.",
        "Rain’s coming. You can feel it before you see it.",
        "Watch the canopy. Not everything stays on the ground.",
        "You’ll want a blade. Plants don’t make paths for you.",
        "Spore clouds drift low this time of day.",
        "If it’s bright, it’s probably dangerous.",
        "You’re not the only one watching.",
        "Stay on the paths. The jungle doesn’t forgive wandering.",
    ];

    private static readonly string[] FemaleChat =
    [
        "Take it slow. The jungle punishes rushing.",
        "You feel that air shift? Storm’s close.",
        "Some of these plants only bloom when something’s wrong.",
        "Don’t trust clearings. They don’t happen naturally.",
        "You’ll learn which sounds matter.",
        "Keep your footing. Roots will trip you if you let them.",
        "There’s always water nearby. Finding it is the trick.",
        "The light plays games with your eyes out here.",
        "You ever notice how nothing really dies? It just… changes.",
        "Careful with spores. They linger longer than you think.",
        "You’re doing alright. Most don’t last this long.",
        "The jungle’s patient. You should be too.",
        "Some paths close behind you. Don’t rely on memory.",
        "You look like you’re learning. That’s good.",
        "Stay aware. That’s half of surviving here.",
    ];

    private static readonly string[] NeutralChat =
    [
        "You’re not from around here, are you?",
        "Something feels off today. Can’t place it.",
        "You get used to the weight of the air.",
        "It’s easy to get turned around out here.",
        "The jungle doesn’t like being watched back.",
        "You hear more when you stop moving.",
        "Not everything that grows here should.",
        "You’ll want to keep moving before dark.",
        "It’s thicker than usual today.",
        "If you feel like you’re being followed, you probably are.",
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
        return spawnInfo.Player.ZoneOverworldHeight && Main.dayTime && spawnInfo.Player.ZoneJungle && 
            !spawnInfo.Player.ZoneCorrupt && !spawnInfo.Player.ZoneCrimson && !spawnInfo.Player.ZoneHallow
            ? 0.15f : 0f;
    }

    protected override void SetAppearanceDefaults()
    {
        base.SetAppearanceDefaults();

        SetBodySlot(ModContent.ItemType<Tantamount_Body>());
        SetLegSlot(ModContent.ItemType<Tantamount_Legs>());

        if (Main.rand.NextBool(6))
            SetHeadSlot(ItemID.GarlandHat);
    }

    protected override List<Action> SetWorldInventory() =>
    [
        () => AddInvItem(ItemID.VineRope, Main.rand.Next(10, 21)),
        () => AddInvItem(ItemID.JungleSpores, Main.rand.Next(1, 3)),
        () => AddInvItem(ItemID.RichMahogany, Main.rand.Next(20, 51)),
        () => AddInvItem(ItemID.BambooBlock, Main.rand.Next(7, 32)),
    ];

    protected override List<StockEntry> GetStockPool() =>
    [
        new(() => AddInvItem(ItemID.BambooBlock, 250),
            Trade().For(ItemID.TinOre, 2).Grants(ItemID.BambooBlock, 12)),

        new(() => AddInvItem(ItemID.RichMahogany, 250),
            Trade().For(ItemID.LeadOre, 2).Grants(ItemID.RichMahogany, 12)),

        new(() => AddInvItem(ItemID.JungleSpores, 5),
            Trade().For(ItemID.Sunflower).Grants(ItemID.JungleSpores, 2)),

        new(() => AddInvItem(ItemID.BottledHoney, Main.rand.Next(3, 6)),
            Trade().For(ItemID.LesserHealingPotion).Grants(ItemID.BottledHoney)),

        new(() => AddInvItem(ItemID.NaturesGift),
            Trade().For(ItemID.HerbBag).Grants(ItemID.NaturesGift)),

        new(() => AddInvItem(ItemID.AnkletoftheWind),
            Trade().For(ItemID.GoldBar, 8).Grants(ItemID.AnkletoftheWind)),

        new(() => AddInvItem(ItemID.HerbBag, 8),
            Trade().ForCoins(Item.buyPrice(silver : 25)).Grants(ItemID.HerbBag)),

        new(() => AddInvItem(ItemID.JungleYoyo),
            Trade().ForCoins(Item.buyPrice(gold : 1, silver : 50)).Grants(ItemID.JungleYoyo)),

        new(() => AddInvItem(Solution.MartialTonic, 5),
            Trade().For(ItemID.Ruby, 2)
                            .Grants(Solution.MartialTonic)),

        new(() => AddInvItem(Solution.BaneTonic, 5),
            Trade().For(ItemID.Stinger, 3)
                            .Grants(Solution.BaneTonic)),

        new(() => AddInvItem(Solution.VitalTonic, 12),
            Trade().For(ItemID.JungleSpores, 8)
                            .Grants(Solution.VitalTonic, 2)),
    ];
}