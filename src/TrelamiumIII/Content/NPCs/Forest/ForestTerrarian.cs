using TrelamiumIII.Core.NPCs.Trading;
using System.Collections.Generic;
using TrelamiumIII.Core.NPCs;
using TrelamiumIII.Content.Alchemy.Solutions;

namespace TrelamiumIII.Content.NPCs.Forest;

// USA BABYYYYYYYY!
public class ForestTerrarian : Villager
{
    protected override Color[] HairPalette =>
    [
        new(60,  50,  40),  // dark brown
        new(25,  25,  25),  // black
        new(220, 180, 90),  // blonde
        new(255, 88,  52),  // ginger
        new(150, 90,  60),  // auburn
    ];

    protected override Color[] EyePalette =>
    [
        new(80,  162, 224), // blue
        new(153, 229, 80),  // green
        new(120, 80,  60),  // brown
        new(60,  50,  40),  // dark brown
    ];

    protected override Color[] SkinPallete =>
    [
        new Color(255, 125, 90),
        new Color(171, 115, 99),
        new Color(188, 105, 82),
        new Color(241, 196, 169),
        new Color(228, 186, 161),
    ];

    protected override int[] MaleHairPool =>
    [
        1, 19, 24, 54, 13, 114, 115, 156, 
    ];

    protected override int[] FemaleHairPool =>
    [
        6, 89, 90, 101, 110, 78,
    ];


    protected override string[] MaleNames =>
    [
        "Jason", "Alex", "Riley",
        "Bob", "Dan", "Everrett", "Skyler",
        "Emerson", "Finley", "Hayden", "Rowan", "Elliot",
    ];

    protected override string[] FemaleNames =>
    [
        "Olivia", "Alex", "Taylor", "Lacey", "Morgan", "Amelia", "Evelen",
        "Harper", "Addison", "Clara", "Avery", "Grace", "Clementine",
        "Paisley", "Quinn", "Stephani", "Gwendolyn", "Ava", "Charolette",
    ];


    private static readonly string[] ForestNeutralChat =
    [
        "Hey, you new around here?",
        "Nice day out. Feels like something’s gonna happen though.",
        "You ever just wander and forget what you were doing?",
        "I swear the woods shift when you’re not looking.",
        "You got supplies? Always pays to be ready.",
        "I’ve been trying to map this place out. Not going great.",
        "You hear that? ...nah, probably nothing.",
        "I like it out here. Quiet, but not too quiet.",
        "You run into anything weird lately?",
        "Careful where you step. Ground’s not always what it looks like.",
        "I trade fair. Long as you do.",
        "You ever feel like you’re being watched out here?",
        "I don’t mind the wild. It’s people I’m not sure about.",
        "You look like you can handle yourself.",
    ];

    private static readonly string[] ForestMaleChat =
    [
        "Man, I could use a better axe.",
        "I had something figured out earlier... totally lost it.",
        "You ever try building out here? Nothing stays put.",
        "I keep telling myself I’ll settle down somewhere. Hasn’t happened yet.",
        "If you find anything useful, I’m open to trades.",
        "I don’t trust those glowing mushrooms. Not one bit.",
        "You look like you’ve seen some stuff.",
        "I had a camp set up. Something tore through it overnight.",
    ];

    private static readonly string[] ForestFemaleChat =
    [
        "I like the forest. It feels... alive, you know?",
        "You ever notice how fast things grow out here?",
        "I’ve been collecting herbs. Some of them don’t feel right.",
        "If you see anything unusual, let me know.",
        "I try to keep things organized. The world doesn’t make that easy.",
        "There’s patterns out here. You just have to notice them.",
        "I don’t scare easy, but this place tests that sometimes.",
        "You seem alright. That’s rare.",
    ];

    public override string GetChat()
    {
        timeAlive = 0; // reset their despawn timer if interacted with

        var pool = new List<string>();

        pool.AddRange(ForestNeutralChat);

        if (Male)
            pool.AddRange(ForestMaleChat);
        else
            pool.AddRange(ForestFemaleChat);

        return pool[Main.rand.Next(pool.Count)];
    }
    protected override void SetAppearanceDefaults()
    {
        base.SetAppearanceDefaults();

        if (Main.rand.NextBool(32))
            SetHeadSlot(ItemID.CopperHelmet);
        else if (Main.rand.NextBool(32))
            SetHeadSlot(ItemID.TinHelmet);
        else if (Main.rand.NextBool(32))
            SetHeadSlot(ItemID.IronHelmet);
        else if (Main.rand.NextBool(32))
            SetHeadSlot(ItemID.LeadHelmet);
        else if (Main.rand.NextBool(32))
            SetHeadSlot(ItemID.GoldHelmet);
        else if (Main.rand.NextBool(32))
            SetHeadSlot(ItemID.PlatinumHelmet);
        
    }

    public override float SpawnChance(NPCSpawnInfo spawnInfo)
    {
        return spawnInfo.Player.ZoneOverworldHeight && Main.dayTime && spawnInfo.Player.ZoneForest ? 0.125f : 0f;
    }

    protected override List<Action> SetWorldInventory() =>
    [
        () => AddInvItem(ItemID.Wood, Main.rand.Next(10, 20)),
        () => AddInvItem(ItemID.StoneBlock, Main.rand.Next(10, 15)),
        () => AddInvItem(ItemID.CopperOre, Main.rand.Next(2, 9)),
        () => AddInvItem(ItemID.TinOre, Main.rand.Next(3, 9)),
    ];

    protected override List<StockEntry> GetStockPool() =>
    [
        new(() => AddInvItem(ItemID.TinBroadsword),
        Trade().For(ItemID.Wood, 22).Grants(ItemID.TinBroadsword)),

        new(() => AddInvItem(ItemID.IronBar, 5),
            Trade().For(ItemID.StoneBlock, 5).Grants(ItemID.IronBar)),

        new(() => AddInvItem(ItemID.HerbBag, 3),
        Trade().For(ItemID.Rope, 30).Grants(ItemID.HerbBag)),

        new(() => AddInvItem(ItemID.LifeCrystal, 2),
            Trade().For(ItemID.Blinkroot, 2).Grants(ItemID.LifeCrystal).WithUses(1)),

        new(() => AddInvItem(ItemID.MiningPotion, Main.rand.Next(3, 4)),
            Trade().For(ItemID.CopperBar, 5).Grants(ItemID.MiningPotion)),

        new(() => AddInvItem(ItemID.LesserHealingPotion, Main.rand.Next(6, 10)),
            Trade().ForCoins(Item.buyPrice(silver: 50)).Grants(ItemID.LesserHealingPotion, 2)),

        new(() => AddInvItem(ItemID.SilverWatch),
            Trade().For(ItemID.IronOre, 20).Grants(ItemID.SilverWatch)),

        new(() => AddInvItem(ItemID.Silk, 10),
            Trade().ForCoins(Item.buyPrice(copper: 80))
                            .Grants(ItemID.Silk)),

        new(() => AddInvItem(SolventID.SpecularTonic, 5),
            Trade().For(ItemID.Bass, 3)
                            .Grants(SolventID.SpecularTonic)),

        new(() => AddInvItem(SolventID.FortunateTonic, 5),
            Trade().For(ItemID.Topaz, 2)
                            .Grants(SolventID.FortunateTonic)),

        new(() => AddInvItem(SolventID.VitalTonic, 5),
            Trade().For(ItemID.Sunflower, 3)
                            .Grants(SolventID.VitalTonic)),
    ];
}
