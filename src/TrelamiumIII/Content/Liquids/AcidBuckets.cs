using TrelamiumIII.Core.Liquids;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Alchemy;

namespace TrelamiumIII.Content.Liquids;

public class AcidBucket : BucketItem
{
    public override CustomLiquid Liquid => AcidicLiquids.Instance;
    public override InternalLiquid LiquidType => Liquid.Get(AcidicLiquids.Instance.Acid.Index);

    protected override BucketTemplate Template => BucketTemplate.Honey;

    protected override LiquidPalette Palette => new(new Color(0, 128, 0), new Color(157, 226, 29), new Color(217, 244, 121));
    protected override LiquidSurface Surface => null!;
}

public class AquaFortisBucket : BucketItem
{
    public override CustomLiquid Liquid => AcidicLiquids.Instance;
    public override InternalLiquid LiquidType => Liquid.Get(AcidicLiquids.Instance.AquaFortis.Index);

    protected override BucketTemplate Template => BucketTemplate.Lava;
    protected override LiquidPalette Palette => new(new Color(96, 74, 9), new Color(128, 102, 0), new Color(203, 184, 95));
    protected override LiquidSurface Surface => null!;

}

public class AquaRegiaBucket : BucketItem
{
    public override CustomLiquid Liquid => AcidicLiquids.Instance;
    public override InternalLiquid LiquidType => Liquid.Get(AcidicLiquids.Instance.AquaRegia.Index);

    protected override BucketTemplate Template => BucketTemplate.Lava;
    protected override LiquidPalette Palette => new(new Color(111, 46, 21), new Color(208, 132, 38), new Color(225, 135, 36));
    protected override LiquidSurface Surface => null!;
}

public class BlueVitriolBucket : BucketItem
{
    public override CustomLiquid Liquid => AcidicLiquids.Instance;
    public override InternalLiquid LiquidType => Liquid.Get(AcidicLiquids.Instance.BlueVitriol.Index);

    protected override BucketTemplate Template => BucketTemplate.Lava;
    protected override LiquidPalette Palette => new(new Color(21, 34, 70), new Color(31, 135, 169), LiquidType.Tint);
    protected override LiquidSurface Surface => null!;
}

public class GreenVitriolBucket : BucketItem
{
    public override CustomLiquid Liquid => AcidicLiquids.Instance;
    public override InternalLiquid LiquidType => Liquid.Get(AcidicLiquids.Instance.GreenVitriol.Index);

    protected override BucketTemplate Template => BucketTemplate.Lava;
    protected override LiquidPalette Palette => new(new Color(0, 60, 0), new Color(19, 159, 132), LiquidType.Tint);
    protected override LiquidSurface Surface => null!;
}

public class SpiritOfSaltBucket : BucketItem
{
    public override CustomLiquid Liquid => AcidicLiquids.Instance;
    public override InternalLiquid LiquidType => Liquid.Get(AcidicLiquids.Instance.SpiritOfSalt.Index);

    protected override BucketTemplate Template => BucketTemplate.Standard;
    protected override LiquidPalette Palette => new(new Color(175, 167, 90), new Color(220, 206, 141), LiquidType.Tint);
    protected override LiquidSurface Surface => null!;
}

public class OilOfVitriolBucket : BucketItem
{
    public override CustomLiquid Liquid => AcidicLiquids.Instance;
    public override InternalLiquid LiquidType => Liquid.Get(AcidicLiquids.Instance.OilOfVitriol.Index);

    protected override BucketTemplate Template => BucketTemplate.Lava;
    protected override LiquidPalette Palette => new(new Color(87, 75, 38), new Color(209, 193, 120), new Color(224, 215, 184));
    protected override LiquidSurface Surface => null!;
}