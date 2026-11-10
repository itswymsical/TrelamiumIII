using TrelamiumIII.Core.Liquids;
using TrelamiumIII.Core;

namespace TrelamiumIII.Content.Liquids;

public class AcidBucket : BucketItem
{
    public override InternalLiquid Liquid => AcidicLiquids.Instance;
    public override InternalLiquidType LiquidSet => Liquid.Get(AcidicLiquids.Instance.Acid.Index);

    public override string Texture => AssetReferences.Content.Liquids.z_templateBucket.KEY;
}

public class AquaFortisBucket : BucketItem
{
    public override InternalLiquid Liquid => AcidicLiquids.Instance;
    public override InternalLiquidType LiquidSet => Liquid.Get(AcidicLiquids.Instance.AquaFortis.Index);

    public override string Texture => AssetReferences.Content.Liquids.z_templateBucket.KEY;
}

public class AquaRegiaBucket : BucketItem
{
    public override InternalLiquid Liquid => AcidicLiquids.Instance;
    public override InternalLiquidType LiquidSet => Liquid.Get(AcidicLiquids.Instance.AquaRegia.Index);

    public override string Texture => AssetReferences.Content.Liquids.z_templateBucket.KEY;
}

public class BlueVitriolBucket : BucketItem
{
    public override InternalLiquid Liquid => AcidicLiquids.Instance;
    public override InternalLiquidType LiquidSet => Liquid.Get(AcidicLiquids.Instance.BlueVitriol.Index);

    public override string Texture => AssetReferences.Content.Liquids.z_templateBucket.KEY;
}

public class GreenVitriolBucket : BucketItem
{
    public override InternalLiquid Liquid => AcidicLiquids.Instance;
    public override InternalLiquidType LiquidSet => Liquid.Get(AcidicLiquids.Instance.GreenVitriol.Index);

    public override string Texture => AssetReferences.Content.Liquids.z_templateBucket.KEY;
}

public class SpiritOfSaltBucket : BucketItem
{
    public override InternalLiquid Liquid => AcidicLiquids.Instance;
    public override InternalLiquidType LiquidSet => Liquid.Get(AcidicLiquids.Instance.SpiritOfSalt.Index);

    public override string Texture => AssetReferences.Content.Liquids.z_templateBucket.KEY;
}

public class OilOfVitriolBucket : BucketItem
{
    public override InternalLiquid Liquid => AcidicLiquids.Instance;
    public override InternalLiquidType LiquidSet => Liquid.Get(AcidicLiquids.Instance.OilOfVitriol.Index);

    public override string Texture => AssetReferences.Content.Liquids.z_templateBucket.KEY;
}