using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrelamiumIII.Content.Rusted
{
    [LegacyName("Rusted Nugget")]
    public class RustedNugget : ModItem
    {
        public override void SetStaticDefaults()
        {
            Item.ResearchUnlockCount = 100;
        }

        public override void SetDefaults()
        {
            Item.width = Item.height = 18;
            Item.maxStack = Item.CommonMaxStack;
            Item.rare = ItemRarityID.Gray;
            Item.sellPrice(Item.buyPrice(copper: 0));
        }
    }
}
