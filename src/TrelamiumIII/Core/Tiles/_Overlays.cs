using ReLogic.Content;
using TrelamiumIII.Core.Gas;

namespace TrelamiumIII.Core.Tiles;

public class SlimeOverlay : OverlayedTile
{
    public override Texture2D? BlockTexture => Assets.Textures.SlimeOverlay.Asset.Value;

    public override Texture2D? PlatformTexture => Assets.Textures.SlimeOverlay_Platform.Asset.Value;

    public override BlendState BlendState => BlendState.Additive;
}

public class BurnOverlay : OverlayedTile
{
    public override Texture2D? BlockTexture => Assets.Textures.BurnOverlay.Asset.Value;

    public override Texture2D? PlatformTexture => Assets.Textures.BurnOverlay_Platform.Asset.Value;

    public override BlendState BlendState => BlendState.AlphaBlend;
}