using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.Items.ContortionistSet;

public class TwistedFungus : ModItem, IAlmanacDefined
{
    AlmanacEntry IAlmanacDefined.AlmanacEntry => new()
    {
        AlmanacPotency = 4,
        Temperaments = Temperament.Volatile,
        Principle = PrincipleType.Bane,
        Rarity = ItemRarityID.Blue,
    };

    public override void SetDefaults()
    {
        Item.rare = ItemRarityID.Blue;
        Item.width = Item.height = 20;
        Item.maxStack = Item.CommonMaxStack;
        Item.value = Item.sellPrice(copper: 4);
    }
}
