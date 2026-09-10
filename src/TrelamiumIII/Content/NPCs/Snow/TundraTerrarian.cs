using System.Collections.Generic;
using TrelamiumIII.Content.Alchemy.Solutions;
using TrelamiumIII.Core.NPCs;

namespace TrelamiumIII.Content.NPCs.Snow;

public class TundraTerrarian : Villager
{
    protected override Color[] HairPalette =>
    [
        new(60,  50,  40),  // dark brown
        new(40,  30,  25),  // black
        new(150, 90,  60),  // auburn
        new(180, 200, 220), // silver
    ];

    protected override Color[] EyePalette =>
    [
        new(80,  162, 224), // blue
        new(120, 80,  60),  // brown
        new(60,  50,  40),  // dark brown
    ];
    protected override Color[] SkinPallete =>
    [
        new Color(255, 125, 90),
        new Color(240, 192, 164),
        new Color(241, 196, 169),
        new Color(228, 186, 161),
    ];

    protected override int[] MaleHairPool =>
    [
        1, 57, 51, 76, 135, 136, 137, 147, 157, 148
    ];

    protected override int[] FemaleHairPool =>
    [
        7, 55, 88, 84, 88, 89, 97, 135, 148, 164
    ];

    protected override string[] MaleNames =>
    [
        "Soren", "Stellan", "Axel",
        "Leif", "Anton", "Erik", "Henrik",
        "Oskar", "Levin", "Mathias", "Anselm", "Espen",
    ];

    protected override string[] FemaleNames =>
    [
        "Malin", "Linnea", "Lena", "Ida", "Greta", "Annika", "Liv",
        "Amalia", "Elin", "Lina", "Helene", "Alida", "Lidia",
        "Frieda", "Silja", "Neele",
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
        "Cold today. Colder than yesterday.",
        "Fire's more valuable than gold where I’m from.",
        "You carry heat on you, or are you just hoping?",
        "I trade fair. No tricks.",
    ];

    private static readonly string[] FemaleChat =
    [
        "Cold today. Colder than yesterday.",
        "Keep your hands warm. You’ll need them.",
        "You look like you’ve got somewhere to be. That’s good.",
        "We can trade, if you’re serious about it.",
    ];

    private static readonly string[] NeutralChat =
    [
        "You learn to keep moving out here.",
        "Snow hides things. Be careful.",
        "Don’t waste heat. That includes talking.",
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
        return spawnInfo.Player.ZoneOverworldHeight && Main.dayTime && spawnInfo.Player.ZoneSnow && 
            !spawnInfo.Player.ZoneCorrupt && !spawnInfo.Player.ZoneCrimson && !spawnInfo.Player.ZoneHallow
            ? 0.15f : 0f;
    }

    protected override void SetAppearanceDefaults()
    {
        base.SetAppearanceDefaults();

        SetBodySlot(ItemID.EskimoCoat);
        SetLegSlot(ItemID.EskimoPants);

        if (Main.rand.NextBool(6))
            SetHeadSlot(ItemID.EskimoHood);
    }
    protected override List<Action> SetWorldInventory() =>
    [
        () => AddInvItem(ItemID.Torch, Main.rand.Next(10, 20)),
        () => AddInvItem(ItemID.Wood, Main.rand.Next(20, 50)),
        () => AddInvItem(ItemID.BorealWood, Main.rand.Next(20, 50)),
        () => AddInvItem(ItemID.SnowBlock, Main.rand.Next(30, 70)),
    ];

    protected override List<StockEntry> GetStockPool() =>
    [
        new(() => AddInvItem(ItemID.TungstenShortsword),
            Trade().For(ItemID.Campfire).Grants(ItemID.TungstenShortsword)),

        new(() => AddInvItem(ItemID.BorealWood, 250),
            Trade().For(ItemID.Wood, 10).Grants(ItemID.BorealWood, 10)),

        new(() => AddInvItem(ItemID.ManaCrystal, 2),
            Trade().For(ItemID.Moonglow, 2).Grants(ItemID.ManaCrystal).WithUses(1)),

        new(() => AddInvItem(ItemID.WarmthPotion, Main.rand.Next(1, 2)),
            Trade().For(ItemID.Torch, 14).Grants(ItemID.WarmthPotion)),

        new(() => AddInvItem(ItemID.WandofFrosting),
            Trade().ForCoins(Item.buyPrice(silver: 50))
                .Grants(ItemID.WandofFrosting)),

        new(() => AddInvItem(ItemID.SilverAxe),
            Trade().For(ItemID.CopperOre, 20).Grants(ItemID.SilverAxe)),

        new(() => AddInvItem(ItemID.Extractinator),
            Trade().ForCoins(Item.buyPrice(gold: 1, silver: 50))
                            .Grants(ItemID.Extractinator)),

        new(() => AddInvItem(Solution.SpecularTonic, 11),
            Trade().For(ItemID.FrostMinnow)
                            .Grants(Solution.SpecularTonic, 1)),

        new(() => AddInvItem(Solution.FortunateTonic, 5),
            Trade().For(ItemID.AtlanticCod, 2)
                            .Grants(Solution.FortunateTonic, 1)),

        new(() => AddInvItem(Solution.VitalTonic, 5),
            Trade().For(ItemID.Salmon)
                            .Grants(Solution.VitalTonic, 1)),
    ];
}
