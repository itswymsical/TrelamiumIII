using System.ComponentModel;
using Terraria.ModLoader.Config;

namespace TrelamiumIII.Common.Configs;

public enum GraphicsPreset
{
    Toaster,
    Low,
    Standard,
    HighEnd,
    NASA,
    Custom
}

public class VFXConfig : ModConfig
{
    public override ConfigScope Mode => ConfigScope.ClientSide;

    [Header("QuickSettings")]
    [DefaultValue(GraphicsPreset.Standard)]
    public GraphicsPreset Preset { get; set; } = GraphicsPreset.Standard;

    [Header("ParticleCount")]
    [DefaultValue(256)]
    [Range(0, 2048)]
    [Increment(16)]
    public int ParticleCount { get; set; } = 256;

    [Header("MetaballCount")]
    [DefaultValue(1f)]
    [Range(0.25f, 4f)]
    [Increment(0.25f)]
    public float MetaballCountMultiplier { get; set; } = 1f;

    public override void OnChanged()
    {
        // 1. Check if the active sliders currently match any of our predefined presets
        GraphicsPreset currentPresetFromValues = GetPresetFromValues(ParticleCount, MetaballCountMultiplier);

        // 2. If the user changed the Preset dropdown selection specifically
        // (meaning the preset dropdown doesn't match what the sliders say), apply the new preset's values.
        if (Preset != GraphicsPreset.Custom && Preset != currentPresetFromValues)
        {
            ApplyPresetValues(Preset);
            return;
        }

        // 3. Otherwise, update the dropdown to accurately reflect the slider positions
        Preset = currentPresetFromValues;
    }

    private void ApplyPresetValues(GraphicsPreset preset)
    {
        switch (preset)
        {
            case GraphicsPreset.Toaster:
                ParticleCount = 64;
                MetaballCountMultiplier = 0.25f;
                break;
            case GraphicsPreset.Low:
                ParticleCount = 128;
                MetaballCountMultiplier = 0.5f;
                break;
            case GraphicsPreset.Standard:
                ParticleCount = 256;
                MetaballCountMultiplier = 1.0f;
                break;
            case GraphicsPreset.HighEnd:
                ParticleCount = 1024;
                MetaballCountMultiplier = 2.0f;
                break;
            case GraphicsPreset.NASA:
                ParticleCount = 2048;
                MetaballCountMultiplier = 4.0f;
                break;
        }
    }

    private static GraphicsPreset GetPresetFromValues(int particles, float metaballs)
    {
        if (particles == 64 && metaballs == 0.25f) return GraphicsPreset.Toaster;
        if (particles == 128 && metaballs == 0.5f) return GraphicsPreset.Low;
        if (particles == 256 && metaballs == 1.0f) return GraphicsPreset.Standard;
        if (particles == 1024 && metaballs == 2.0f) return GraphicsPreset.HighEnd;
        if (particles == 2048 && metaballs == 4.0f) return GraphicsPreset.NASA;

        return GraphicsPreset.Custom;
    }
}