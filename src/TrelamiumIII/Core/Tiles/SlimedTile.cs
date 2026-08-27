namespace TrelamiumIII.Core.Tiles;

public class SlimeOverlayedTile : OverlayedTile
{
    public override Texture2D? BlockTexture { get; set; }
    public override Texture2D? PlatformTexture { get; set; }

    public override void Load()
    {
        BlockTexture = AssetReferences.Core.Tiles.SlimeOverlay.Asset.Value;
        PlatformTexture = AssetReferences.Core.Tiles.SlimeOverlay_Platform.Asset.Value;
    }
}
//  OverlayedTileManager.AddOverlayedTile<SlimeOverlayedTile>(tileX, tileY);