using TrelamiumIII.Core;
using TrelamiumIII.Core.Alchemy;
using TrelamiumIII.Core.Liquids;

namespace TrelamiumIII.Content.Liquids;

public class LimewaterBucket : BucketItem
{
    public override CustomLiquid Liquid => AlkalineLiquids.Instance;
    public override InternalLiquid LiquidType => Liquid.Get(AlkalineLiquids.Instance.Limewater.Index);

    protected override BucketTemplate Template => BucketTemplate.Standard;
    protected override LiquidPalette Palette => new(new Color(101, 101, 124), new Color(175, 177, 188), new Color(227, 227, 227));
    protected override LiquidSurface Surface => null!;
}

public class LyeBucket : BucketItem
{
    public override CustomLiquid Liquid => AlkalineLiquids.Instance;
    public override InternalLiquid LiquidType => Liquid.Get(AlkalineLiquids.Instance.Lye.Index);

    protected override BucketTemplate Template => BucketTemplate.Standard;
    protected override LiquidPalette Palette => new(new Color(95, 80, 112), new Color(140, 120, 165), new Color(209, 194, 218));
    protected override LiquidSurface Surface => null!;
}

public class SpiritOfHartshornBucket : BucketItem
{
    public override CustomLiquid Liquid => AlkalineLiquids.Instance;
    public override InternalLiquid LiquidType => Liquid.Get(AlkalineLiquids.Instance.SpiritOfHartshorn.Index);

    protected override BucketTemplate Template => BucketTemplate.Standard;
    protected override LiquidPalette Palette => new(new Color(34, 81, 68), new Color(45, 147, 147), new Color(87, 176, 179));
    protected override LiquidSurface Surface => null!;
}