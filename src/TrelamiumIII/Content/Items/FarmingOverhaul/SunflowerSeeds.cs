using System.Collections.Generic;
using Terraria.Audio;
using Terraria.GameContent;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Common.DamageMeter;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.Items.FarmingOverhaul;

public class SunflowerSeeds : ModItem, IAlmanacDefined
{
    AlmanacEntry IAlmanacDefined.AlmanacEntry => new()
    {
        AlmanacPotency = 3,
        Temperaments = Temperament.Acidic,
        Principle = PrincipleType.Brisk,
        Rarity = ItemRarityID.White,
        Hidden = false,
    };

    public override void SetStaticDefaults()
    {
        base.SetStaticDefaults();
        Item.ResearchUnlockCount = 100;
    }

    public override void SetDefaults()
    {
        //Item.DefaultToPlaceableTile(ModContent.TileType<GrowingSunflower>());
        Item.value = Item.sellPrice(copper: 15);
    }
}