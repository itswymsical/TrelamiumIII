namespace TrelamiumIII.Content.Items.Quest;

// Placeholder job items (docs/act-one.md). No sprites yet: PlaceholderTextures draws them until a PNG named after the
// class is added next to this file.

/// <summary>Carried from Silvian to Argie. Held, it shows the way to her (<see cref="LetterScent" />).</summary>
public class SilviansLetter : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 22;
        Item.height = 18;
        Item.maxStack = 1;
        Item.rare = ItemRarityID.Quest;
    }

    public override void HoldItem(Player player) => LetterScent.Emit(player);
}

/// <summary>Argie's survey samples, from the glowing mushroom biome.</summary>
public class SporeSample : ModItem
{
    public override void SetDefaults()
    {
        Item.width = Item.height = 18;
        Item.maxStack = Item.CommonMaxStack;
        Item.value = Item.sellPrice(copper: 50);
        Item.rare = ItemRarityID.Blue;
    }
}

/// <summary>The Merchant's bounty on the Eye of Cthulhu.</summary>
public class BountyNotice : ModItem
{
    public override void SetDefaults()
    {
        Item.width = 24;
        Item.height = 28;
        Item.maxStack = 1;
        Item.rare = ItemRarityID.Quest;
    }
}
