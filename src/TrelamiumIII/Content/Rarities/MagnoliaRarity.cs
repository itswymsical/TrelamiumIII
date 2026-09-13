using Terraria.Localization;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Rarities;
using TrelamiumIII.Core.Rarities.Flairs;

namespace TrelamiumIII.Content.Rarities;

public class MagnoliaRarity : FlavorRarity
{
    public override Color RarityColor => Color.White;

    protected override RarityStyle BuildStyle() => new()
    {
        Color = RarityColor,
        Glow = new Color(255, 240, 200),
        Flairs =
        [
            new GhostNameFlair
        {
            Copies = 6,
            Radius = 2.8f,
            Intensity = 0.22f,
            Spin = 0.3f,
        },

        new DriftFlair
        {
            OnSprite = false,
            TexturePath = Assets.Textures.MagnoliaLeaf.KEY,
            Count = 6,
            Scale = 1f,
            Blend = BlendState.AlphaBlend,
            DriftSpeed = 9f,
            FallSpeed = 5f,
            SwayAmplitude = 6f,
            FrameCount = 8,
            TicksPerFrame = 4,
            SpriteOrbit = 0.3f,
            Color = Color.White,
        }
    ],
    };
}