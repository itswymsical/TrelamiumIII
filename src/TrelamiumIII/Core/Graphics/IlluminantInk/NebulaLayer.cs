using Terraria.GameContent;

namespace TrelamiumIII.Core.Graphics.IlluminantInk;

/// <summary>
///     The ground of the scene: one full-screen pass of <c>InkNebula.fx</c>.
/// </summary>
/// <remarks>
///     Not an <see cref="IInkLayer" />. The layer stack describes things drawn <em>onto</em> the
///     scene under a shared blend mode; this establishes what the scene is, opaquely, before any
///     of that runs, and it carries no simulation state of its own -- everything it draws is a
///     pure function of <see cref="InkFrame.Time" />.
/// </remarks>
internal sealed class NebulaLayer
{
    private WrapperShaderData<Assets.Shaders.InkNebula.Parameters>? shader;

    public void Reset()
    {
        // Dropped rather than reused so a resolution change or a mod reload can't leave a
        // stale Effect bound to a dead device.
        shader = null;
    }

    public void Draw(SpriteBatch spriteBatch, in InkFrame frame)
    {
        shader ??= Assets.Shaders.InkNebula.CreateP0();

        var parameters = shader.Parameters;

        parameters.uTime = frame.Time;
        parameters.uAspect = frame.Aspect;
        parameters.uCenter = frame.SingularityUv;

        parameters.uScale = InkStyle.NebulaScale;
        parameters.uPivot = InkStyle.NebulaPivot;
        parameters.uExpand = InkStyle.NebulaExpand;
        parameters.uWarp = InkStyle.NebulaWarp;
        parameters.uSwirl = InkStyle.NebulaSwirl;
        parameters.uSwirlCore = InkStyle.NebulaSwirlCore;
        parameters.uSpin = InkStyle.NebulaSpin;
        parameters.uDensity = InkStyle.NebulaDensity;
        parameters.uContrast = InkStyle.NebulaContrast;
        parameters.uBodyLevel = InkStyle.NebulaBodyLevel;

        parameters.uSheen = InkStyle.NebulaSheen;
        parameters.uRelief = InkStyle.NebulaRelief;
        parameters.uReliefEps = InkStyle.NebulaReliefEps;
        parameters.uGloss = InkStyle.NebulaGloss;
        parameters.uRim = InkStyle.NebulaRim;
        parameters.uRimPower = InkStyle.NebulaRimPower;
        parameters.uLightDir = InkStyle.NebulaLight;

        parameters.uWeb = InkStyle.NebulaWeb;
        parameters.uWebPower = InkStyle.NebulaWebPower;
        parameters.uReach = InkStyle.NebulaReach;
        parameters.uHalo = InkStyle.NebulaHalo;

        parameters.uHorizon = InkStyle.Horizon;
        parameters.uRing = InkStyle.RingBrightness;
        parameters.uRingWidth = InkStyle.RingWidth;
        parameters.uDisc = InkStyle.DiscBrightness;
        parameters.uDiscRadius = InkStyle.DiscRadius;
        parameters.uDiscWidth = InkStyle.DiscWidth;
        parameters.uDiscFlatten = InkStyle.DiscFlatten;
        parameters.uBeam = InkStyle.DiscBeaming;

        parameters.uVignette = InkStyle.NebulaVignette;
        parameters.uOpacity = frame.Fade;
        parameters.uInkLow = InkStyle.InkLow;
        parameters.uInkHigh = InkStyle.InkHigh;

        // Both fields tile, so they are bound through the shader's own wrap samplers rather
        // than through s0 -- SpriteBatch owns s0 and its sampler state, and it would clamp
        // these into a smear at the edges.
        parameters.uNoiseTex = Assets.Textures.Noise.FbmNoiseBright.Asset.Value;
        parameters.NoiseSampler = new HlslSampler2D
        {
            Texture = Assets.Textures.Noise.FbmNoiseBright.Asset.Value,
            Sampler = SamplerState.LinearWrap,
        };

        parameters.uCellTex = Assets.Textures.Noise.VoronoiNoise.Asset.Value;
        parameters.CellSampler = new HlslSampler2D
        {
            Texture = Assets.Textures.Noise.VoronoiNoise.Asset.Value,
            Sampler = SamplerState.LinearWrap,
        };

        // Immediate, because the pass has to be bound between Begin and the draw that uses
        // it; Deferred would flush after End with whatever shader happened to be current.
        spriteBatch.Begin(
            SpriteSortMode.Immediate,
            BlendState.Opaque,
            SamplerState.LinearClamp,
            DepthStencilState.None,
            Main.Rasterizer
        );

        shader.Apply();

        // MagicPixel is white and opaque, so the shader's mask read is a no-op and the field
        // covers the frame. Swapping in one of the Masks textures here confines the ink to a
        // shape without touching the shader.
        spriteBatch.Draw(
            TextureAssets.MagicPixel.Value,
            new Rectangle(0, 0, frame.Width, frame.Height),
            Color.White
        );

        spriteBatch.End();
    }
}
