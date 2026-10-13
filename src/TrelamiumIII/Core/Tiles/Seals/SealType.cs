using ReLogic.Content;

namespace TrelamiumIII.Core.Tiles.Seals;

/// <summary>
///     A kind of magick laid on tiles: what it forbids, and how it answers when struck. Each type draws on its own canvas
///     through <see cref="Shader" />, showing only where <see cref="SealField" /> has woken it.
/// </summary>
public abstract class SealType : ModType
{
    /// <summary>The byte stored in <see cref="SealData" />. Never 0, which means unsealed.</summary>
    public byte Index { get; internal set; }

    /// <summary>Blocks and walls here cannot be mined, hammered, exploded or replaced.</summary>
    public virtual bool PreventsBreaking => true;

    /// <summary>Nothing can be placed here, and players standing here cannot build.</summary>
    public virtual bool PreventsBuilding => true;

    public virtual Vector3 PrimaryColor => new(0.3f, 0.8f, 1f);

    public virtual Vector3 SecondaryColor => new(0.6f, 0.4f, 1f);

    public virtual Vector3 CoreColor => new(0.9f, 0.97f, 1f);

    public virtual float Opacity => 0.9f;

    public virtual float NoiseScale => 0.35f;

    public virtual Vector2 NoiseDrift => new(0.04f, -0.06f);

    public virtual Asset<Effect>? Shader => Assets.Shaders.SealField.Asset;

    public virtual Asset<Texture2D> NoiseTexture => Assets.Textures.Noise.Turbulence_05.Asset;

    protected sealed override void Register()
    {
        ModTypeLookup<SealType>.Register(this);
        SealSystem.Register(this);
    }

    public sealed override void SetupContent() => SetStaticDefaults();

    /// <summary>Sets the look parameters, after the canvas has set the frame ones and the field its impulses.</summary>
    public virtual void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["PrimaryColor"]?.SetValue(PrimaryColor);
        effect.Parameters["SecondaryColor"]?.SetValue(SecondaryColor);
        effect.Parameters["CoreColor"]?.SetValue(CoreColor);
        effect.Parameters["Opacity"]?.SetValue(Opacity);
        effect.Parameters["NoiseScale"]?.SetValue(NoiseScale);
        effect.Parameters["NoiseDrift"]?.SetValue(NoiseDrift);
        effect.Parameters["NoisePush"]?.SetValue(10f);
        effect.Parameters["RippleWavelength"]?.SetValue(14f);
        effect.Parameters["Bands"]?.SetValue(6f);
        effect.Parameters["uNoiseTex"]?.SetValue(NoiseTexture.Value);
    }
}
