using TrelamiumIII.Core.Items.Components;
using TrelamiumIII.Core.Tiles;

namespace TrelamiumIII.Content.Ingress;

public class BramblewoodTorchTile : FurnitureActor
{
    public override void SetStaticDefaults()
    {
        FurnitureType = FurnitureType.Torch;
        DustID = Terraria.ID.DustID.RichMahogany;
        TorchColor = new(0.2f, 0.26f, 0.8f);
        base.SetStaticDefaults();
    }
}
public class BramblewoodTorch : FurnitureItem
{
    protected override FurnitureType FurnitureType => FurnitureType.Torch;
    protected override int TileType => ModContent.TileType<BramblewoodTorchTile>();
    protected override int MaterialType => ModContent.ItemType<Bramblewood>();
    protected override bool IsTorch => true;
    protected override int TorchDustType => 0;
    protected override Vector3 TorchColor => new(0.24f, 0.26f, 0.8f);
}