using Terraria.Localization;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Rarities;
using TrelamiumIII.Core.Rarities.Flairs;
using TrelamiumIII.Core.Rarities.Text;

namespace TrelamiumIII.Content.Rarities;

public class MagnoliaRarity : FlavorRarity
{
    public override Color RarityColor => Color.White;

    protected override RarityStyle BuildStyle() => new()
    {
        Text = new TextStyle { Outline = new TextOutline { Color = RarityColor, Directions = 2, Spread = 0.5f }, BodyBlend = BlendState.Additive },
        Color = RarityColor,
        Glow = new Color(255, 240, 200) * 0.5f,
        Flairs =
        [
        new GhostNameFlair
        {
            Radius = 2.5f,
            Intensity = 0.28f,
        },

        new DriftFlair
        {
            OnSprite = false,
            TexturePath = Assets.Textures.MagnoliaLeaf.KEY,
            Count = 6,
            Scale = 0.85f,
            Blend = BlendState.Additive,
            DriftSpeed = 7f,
            FallSpeed = 4f,
            SwayAmplitude = 9f,
            FrameCount = 8,
            TicksPerFrame = 8,
            Color = Color.White * 0.5f,
        }
    ],
    };
}