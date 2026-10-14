using Terraria.GameContent;

namespace TrelamiumIII.Content.Ingress.HighlandsBiome;

public class BirchLeafFX : ModGore
{
    public override void SetStaticDefaults()
    {
        ChildSafety.SafeGore[Type] = true;
        GoreID.Sets.SpecialAI[Type] = 3; 
        GoreID.Sets.PaintedFallingLeaf[Type] = true;
    }
    public override bool Update(Gore gore)
    {
        return base.Update(gore);
    }
}
