using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Tiles.Conditions;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Gas.Layers;

public class BloodField : ChemicalGasField
{
    /// <summary>Liquid; over gel so a splash on a puddle still reads.</summary>
    public override int LoadOrder => 30;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;
    protected override string TexturePath => Assets.Textures.VFX.Bloom.KEY;
    protected override int GasType => GasTypeID.Liquid;

    protected override float DefaultDensity => 1.2f;
    protected override float DefaultViscosity => 0.67f;
    protected override float EnclosedLifePenalty => 1.5f;
    protected override float GravityScale => 0.09f;
    protected override float BuoyancyForce => 0.5f;
    protected override float Drag => 0.9956f;
    protected override float LifeDecay => 0.85f;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Necrotic;
    public override int ChemicalDamage => 1;
    public override int AfflictionDuration => 90;
    public override float AfflictionRate => 30f;
    public override int ReactionChance => 2;
    public override int ReactionDamage => 4;

    private static readonly ClingBehaviour Splatter = new SplashCling(6, 8, 16.5f, 140f, SoundID.Item74, shake: 4);

    public override ClingBehaviour? ClingBehaviour => Splatter;

    /// <summary>
    ///     Leaves nothing on the terrain it lands on.
    /// </summary>
    protected override ChemicalReactionTileCondition? Residue => null;

    private Asset<Texture2D> noiseTex;

    public override void Load()
    {
        base.Load();
        noiseTex = Assets.Textures.Noise.LiquidNoise.Asset;
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["uNoiseTex"]?.SetValue(noiseTex.Value);

        Vector3 light = new Vector3(0.18f);

        effect.Parameters["WorldLight"]?.SetValue(light);


        effect.Parameters["Threshold"]?.SetValue(0.12f);
        effect.Parameters["NoiseScale"]?.SetValue(0.75f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.04f);
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0f, -0.08f));

        effect.Parameters["FlowStrength"]?.SetValue(0.08f);
        effect.Parameters["NoiseStretch"]?.SetValue(new Vector2(1f, 0.45f));

        effect.Parameters["BodyAlphaFloor"]?.SetValue(0.8f);

        effect.Parameters["Bands"]?.SetValue(8f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.94f);

        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.72f, 0.08f, 0.08f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.55f, 0.07f, 0.03f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.34f, 0.05f, 0.02f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.76f, 0.01f, 0.02f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.8f);
        effect.Parameters["HighlightColor"]?.SetValue(new Vector3(0.95f, 0.30f, 0.25f));
        effect.Parameters["HighlightAlpha"]?.SetValue(0.9f);
    }

    protected override void OnNPCContact(ref FieldNode node, NPC npc)
    {
        base.OnNPCContact(ref node, npc);

        npc.AddBuff(BuffID.BloodButcherer, 30);
    }
}