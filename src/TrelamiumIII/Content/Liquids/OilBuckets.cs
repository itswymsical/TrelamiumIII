using TrelamiumIII.Core;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

public class CrudeOilBucketItem : BucketItem
{
    public override CustomLiquid Liquid => OilLiquids.Instance;
    public override InternalLiquid LiquidType => Liquid.Get(OilLiquids.Instance.Crude.Index);
    protected override BucketTemplate Template => BucketTemplate.Honey;
    protected override LiquidPalette Palette => new(new Color(14, 14, 14), new Color(40, 35, 54), new Color(43, 47, 66));
    protected override LiquidSurface Surface => null!;
}

public class SunflowerOilBucket : BucketItem
{
    public override CustomLiquid Liquid => OilLiquids.Instance;
    public override InternalLiquid LiquidType => Liquid.Get(OilLiquids.Instance.Sunflower.Index);
    protected override BucketTemplate Template => BucketTemplate.Lava;
    protected override LiquidPalette Palette => new(new Color(96, 80, 2), new Color(228, 197, 16), new Color(255, 255, 191));
    protected override LiquidSurface Surface => null!;
}

public class PitchBucket : BucketItem
{
    public override CustomLiquid Liquid => OilLiquids.Instance;
    public override InternalLiquid LiquidType => Liquid.Get(OilLiquids.Instance.Pitch.Index);
    public override string Texture => AssetReferences.Content.Liquids.CrudeOilBucketItem.KEY;
    protected override BucketTemplate Template => BucketTemplate.Honey;
    protected override LiquidPalette Palette => new(new Color(34, 34, 32), new Color(33, 36, 48), new Color(60, 36, 72));
    protected override LiquidSurface Surface => null!;
}