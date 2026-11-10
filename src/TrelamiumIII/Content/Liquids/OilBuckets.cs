using TrelamiumIII.Core;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

public class CrudeOilBucketItem : BucketItem
{
    public override InternalLiquid Liquid => OilLiquids.Instance;
    public override InternalLiquidType LiquidSet => Liquid.Get(OilLiquids.Instance.Crude.Index);
}

public class SunflowerOilBucket : BucketItem
{
    public override InternalLiquid Liquid => OilLiquids.Instance;
    public override InternalLiquidType LiquidSet => Liquid.Get(OilLiquids.Instance.Sunflower.Index);
}

public class PitchBucket : BucketItem
{
    public override InternalLiquid Liquid => OilLiquids.Instance;
    public override InternalLiquidType LiquidSet => Liquid.Get(OilLiquids.Instance.Pitch.Index);
    public override string Texture => AssetReferences.Content.Liquids.CrudeOilBucketItem.KEY;
}