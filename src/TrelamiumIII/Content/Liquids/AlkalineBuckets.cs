using TrelamiumIII.Core.Liquids;
using TrelamiumIII.Core;

namespace TrelamiumIII.Content.Liquids;

public class LimewaterBucket : BucketItem
{
    public override InternalLiquid Liquid => AlkalineLiquids.Instance;
    public override InternalLiquidType LiquidSet => Liquid.Get(AlkalineLiquids.Instance.Limewater.Index);

    public override string Texture => AssetReferences.Content.Liquids.z_templateBucket.KEY;
}

public class LyeBucket : BucketItem
{
    public override InternalLiquid Liquid => AlkalineLiquids.Instance;
    public override InternalLiquidType LiquidSet => Liquid.Get(AlkalineLiquids.Instance.Lye.Index);

    public override string Texture => AssetReferences.Content.Liquids.z_templateBucket.KEY;
}

public class SpiritOfHartshornBucket : BucketItem
{
    public override InternalLiquid Liquid => AlkalineLiquids.Instance;
    public override InternalLiquidType LiquidSet => Liquid.Get(AlkalineLiquids.Instance.SpiritOfHartshorn.Index);

    public override string Texture => AssetReferences.Content.Liquids.z_templateBucket.KEY;
}