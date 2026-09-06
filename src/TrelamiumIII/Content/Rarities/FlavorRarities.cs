using Terraria.Localization;
using TrelamiumIII.Core.Graphics.Metaballs;
using TrelamiumIII.Core.Graphics.Metaballs.Layers;
using TrelamiumIII.Core.Rarities;
using TrelamiumIII.Core.Rarities.Flairs;

namespace TrelamiumIII.Content.Rarities;

/// <summary>
///     Something pulled out of a fire and never quite allowed to go out.
/// </summary>
/// <remarks>
///     The reference example for a rarity that is a piece of characterisation rather than a power
///     tier: flame boiling off the tooltip, a name that reads as heated metal, and a flavour line.
/// </remarks>
public sealed class EmberRarity : FlavorRarity
{
    public override Color RarityColor => new(255, 150, 60);

    protected override RarityStyle BuildStyle() => new()
    {
        Color = RarityColor,
        Glow = new Color(255, 220, 130),
        Flavor = Language.GetOrRegister("Mods.TrelamiumIII.Rarities.Ember", () => "It has not finished burning."),

        // Back to front: flame, then the halo over it, then the name on top of both.
        Flairs =
        [
            new MetaballFlair
            {
                Look = GasLook.Flame,
                SpawnRate = 2f,
                Radius = 8f,
                Life = 26f,
                Growth = 0.18f,
                Buoyancy = 0.09f,
            },

            new GhostNameFlair
            {
                Radius = 2.5f,
                Intensity = 0.28f,
            },

            new GradientNameFlair
            {
                From = new Color(255, 120, 40),
                To = new Color(255, 235, 165),
                WaveAmplitude = 1f,
                WaveSpeed = 5f,
            },
        ],
    };
}

/// <summary>
///     Fungal, damp, and quietly releasing something into the air.
/// </summary>
/// <remarks>
///     Shows the world half of <see cref="MetaballFlair" />: dropped on the ground this keeps
///     smoking, by feeding the same <see cref="SporeLayer" /> the Fungore fight uses rather than
///     standing up a second cloud system of its own.
/// </remarks>
public sealed class SporebornRarity : FlavorRarity
{
    public override Color RarityColor => new(150, 195, 95);

    protected override RarityStyle BuildStyle() => new()
    {
        Color = RarityColor,
        Glow = new Color(205, 235, 150),
        Flavor = Language.GetOrRegister("Mods.TrelamiumIII.Rarities.Sporeborn", () => "Still shedding."),

        Flairs =
        [
            new MetaballFlair
            {
                Look = GasLook.Spore,
                SpawnRate = 1.2f,
                Radius = 10f,
                Life = 40f,
                Buoyancy = 0.045f,

                WorldLayer = static () => MetaballSystem.GetLayer<SporeLayer>(),
                WorldSpawnRate = 0.35f,
            },

            new GradientNameFlair
            {
                From = new Color(120, 170, 75),
                To = new Color(215, 240, 165),
                Speed = 0.2f,
            },
        ],
    };
}

/// <summary>
///     Old, deliberate, and polished by more hands than yours.
/// </summary>
/// <remarks>
///     The no-metaball case: a shader on the sprite and drifting motes, which is most of what a
///     rarity needs and costs no render target at all.
/// </remarks>
public sealed class RelicRarity : FlavorRarity
{
    public override Color RarityColor => new(220, 190, 120);

    protected override RarityStyle BuildStyle() => new()
    {
        Color = RarityColor,
        Glow = new Color(255, 240, 200),
        Flavor = Language.GetOrRegister("Mods.TrelamiumIII.Rarities.Relic", () => "Older than the hands that made it."),

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
                TexturePath = Assets.Textures.VFX.PixelStar.KEY,
                Count = 3,
                Scale = 0.6f,
                DriftSpeed = 9f,
                FallSpeed = 5f,
                SwayAmplitude = 6f,
                SpriteOrbit = 0.9f,
            },

            new SpriteShaderFlair
            {
                Shader = static () => Assets.Shaders.ShineShader.Asset,
                TimeScale = 0.8f,
            },
        ],
    };
}
