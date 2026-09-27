using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.Items.Alchemy.Reagents.Misc;

[LegacyName("OxidizedCopper")]

public class OxidizedCopperItem : ModItem, IAlmanacDefined
{
    AlmanacEntry IAlmanacDefined.AlmanacEntry => new()
    {
        AlmanacPotency = 2,
        Temperaments = Temperament.Acidic | Temperament.Catalytic,
        Principle = PrincipleType.Harmony,
    };

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        Item.ResearchUnlockCount = 100;
    }

    public override void SetDefaults()
    {
        base.SetDefaults();

        Item.width = Item.height = 26;
        Item.rare = ItemRarityID.Gray;
        Item.value = Item.sellPrice(copper: 0);

        Item.maxStack = Item.CommonMaxStack;
    }
}
