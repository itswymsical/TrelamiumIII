using Terraria.Localization;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Rarities;
using TrelamiumIII.Core.Rarities.Flairs;

namespace TrelamiumIII.Content.Rarities;

public abstract class BaseMyconidRarity : FlavorRarity
{
    public override Color RarityColor => new(220, 190, 120);

    protected virtual LocalizedText? ItemFlavorText => null;

    protected override RarityStyle BuildStyle() => new()
    {
        Color = RarityColor,
        Glow = new Color(255, 240, 200),
        Flavor = ItemFlavorText,
        Flairs =
        [
            new GhostNameFlair
        {
            Copies = 6,
            Radius = 1.8f,
            Intensity = 0.22f,
            Spin = 0.3f,
        },

        new GradientNameFlair
        {
            From = new Color(190, 155, 90),
            To = new Color(255, 245, 210),
            Speed = 0.45f,
        },

        new DriftFlair
        {
            OnSprite = false,
            TexturePath = "Terraria/Images/Item_" + ItemID.Mushroom,
            Count = 4,
            Scale = 0.25f,
            Blend = BlendState.AlphaBlend,
            DriftSpeed = 9f,
            FallSpeed = 5f,
            SwayAmplitude = 6f,
            SpriteOrbit = 0.3f,
            Color = Color.White,
        }
    ],
    };
}

public sealed class MyconidRarity : BaseMyconidRarity { }

public sealed class GirthquakeRarity : BaseMyconidRarity
{
    protected override LocalizedText ItemFlavorText =>
        Language.GetOrRegister("Mods.TrelamiumIII.Rarities.Girthquake", () => "\"The bigger the girth, the bigger the boom.\"");
}

public sealed class SporeclotRarity : BaseMyconidRarity
{
    protected override LocalizedText ItemFlavorText =>
        Language.GetOrRegister("Mods.TrelamiumIII.Rarities.Sporeclot", () => "\"Splorch!\"");
}