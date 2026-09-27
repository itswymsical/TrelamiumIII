using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.Items.Rusted
{
    [LegacyName("Rusted Nugget")]
    public class RustedNugget : ModItem, IAlmanacDefined
    {
        AlmanacEntry IAlmanacDefined.AlmanacEntry => new()
        {
            AlmanacPotency = 2,
            Temperaments = Temperament.Acidic,
            Principle = PrincipleType.Bane | PrincipleType.Martial,
        };

        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 100;
        }

        public override void SetDefaults()
        {
            Item.width = Item.height = 18;
            Item.maxStack = Item.CommonMaxStack;
            Item.rare = ItemRarityID.Gray;
            Item.value = Item.sellPrice(Item.buyPrice(copper: 0));
            Item.DefaultToPlaceableTile(ModContent.TileType<RustedIronOre>());
        }
    }
}
