using TrelamiumIII.Common.ChemicalClass;
using ReLogic.Content;
using TrelamiumIII.Content.EyeOfCthulhu;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Graphics.Fields.Layers;
using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Core.Gas.Layers;

public sealed class StarflaskField : GasField
{
    /// <summary>Topmost: plasma is the brightest thing on screen and owns the stack.</summary>
    public override int LoadOrder => 95;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    /// <summary>Neutral: ink hangs rather than rising or sinking. See the class remarks.</summary>
    protected override float DefaultDensity => 0.5f;

    /// <summary>Thick. The cloud should hold a shape and loom, not disperse like a puff.</summary>
    protected override float DefaultViscosity => 0.72f;

    protected override float BuoyancyForce => 0.02f;

    protected override float GravityScale => 0.02f;

    /// <summary>Heavier drag than the other layers, so a node settles into a slow billow.</summary>
    protected override float Drag => 0.965f;

    protected override float LifeDecay => 1f;

    protected override float EnclosedLifePenalty => 1.2f;

    protected override int GasType => GasTypeID.Plasma;

    protected override GasLook Look { get; } = new()
    {
        Noise = static () => Assets.Textures.Noise.Turbulence_08.Asset,
        WorldLight = new Vector3(0.72f),
        Threshold = 0.165f,
        NoiseScale = 1.05f,
        NoiseStrength = 0.20f,
        Drift = new Vector2(-0.014f, -0.022f),
        FlowStrength = 0.20f,
        Bands = 3f,
        BodyAlpha = 0.94f,
        Outline = Vector3.One,
        OutlineAlpha = 0.90f,
    };

    private Vector3 GetRGB(float speed = 2.0f, float sat = 1.0f, float bright = 1.0f)
    {
        float time = (float)Main.timeForVisualEffects / 60f;
        float t = time * speed;

        float r = (MathF.Sin(t) + 1f) * 0.5f;
        float g = (MathF.Sin(t + 2.0944f) + 1f) * 0.5f;
        float b = (MathF.Sin(t + 4.1888f) + 1f) * 0.5f;

        Vector3 rawRainbow = new Vector3(r, g, b);

        Vector3 color = Vector3.Lerp(Vector3.One, rawRainbow, sat);

        return color * bright;
    }

    /// <summary>Cycles the ramp through the hue wheel.</summary>
    protected override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["ShadowColor"]?.SetValue(GetRGB(speed: 2, sat: 0.4f, bright: 0.6f));
        effect.Parameters["BodyColor"]?.SetValue(GetRGB(speed: 4f));
        effect.Parameters["CoreColor"]?.SetValue(GetRGB(speed: 4f));
    }

    protected override void ApplyLighting(ref FieldNode node)
    {
        Lighting.AddLight(node.Position, TorchID.Rainbow);
    }

    /// <summary>Emitter hits count as chemical damage; the layer itself carries no afflictions.</summary>
    protected override DamageClass EmitterDamageClass => ModContent.GetInstance<ChemicalDamageClass>();

    /// <summary>Each emitted node throws off a star dust.</summary>
    protected override void Emit(ref GasEmitter emitter)
    {
        base.Emit(ref emitter);

        if (Main.dedServ)
            return;

        Vector2 drift = emitter.Velocity * emitter.Inheritance;

        Dust.NewDust(emitter.Position, 8, 8, DustID.YellowStarDust, drift.X / 2f, drift.Y, Scale: 0.65f);
    }

    protected override void UpdateNode(ref FieldNode node, int index)
    {
        base.UpdateNode(ref node, index);
        if (Main.rand.NextFloat() < 0.025f)
        {
            Vector2 offset = Main.rand.NextVector2Circular(14f, 14f);

            ParticleSystem.Spawn<InkSparkParticle>(
                node.Position,
                node.Velocity * 0.4f + Main.rand.NextVector2Circular(0.7f, 0.7f),
                timeLeft: (int)node.Life / 3,
                scale: Main.rand.NextFloat(0.5f, 0.75f),
                rotation: Main.rand.NextFloat(MathHelper.TwoPi),
                rotationVelocity: Main.rand.NextFloat(-0.05f, 0.05f)
            );
        }
    }
}
