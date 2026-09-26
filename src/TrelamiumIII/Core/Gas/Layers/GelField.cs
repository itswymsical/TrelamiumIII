using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Tiles.Conditions;
using TrelamiumIII.Core.Tiles.Conditions.Types;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Gas.Layers;

public class GelField : ChemicalGasField
{
    private const int SLOW_TICKS = 45;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Solute;

    public override int ChemicalDamage => 1;

    public override int AfflictionDuration => 90;

    public override float AfflictionRate => 30f;

    public override int ReactionChance => 0;

    public override int ReactionDamage => 0;

    /// <summary>Thick liquid, pools on the ground and sits under anything airborne.</summary>
    public override int LoadOrder => 20;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    protected override int GasType => GasTypeID.Liquid;

    protected override float DefaultDensity => 0.95f;

    protected override float DefaultViscosity => 0.72f;

    protected override float GravityScale => 0.18f;

    protected override float BuoyancyForce => 0.02f;

    protected override float Drag => 0.986f;

    protected override float LifeDecay => 0.9f;

    protected override float EnclosedLifePenalty => 0.6f;

    protected override bool UsesTileField => true;

    protected override float TileFieldThreshold => 0.45f;

    protected override int TileFieldInterval => 3;

    private Asset<Texture2D>? noiseTexture;

    public override void Load()
    {
        base.Load();

        if (!Main.dedServ)
            noiseTexture = Assets.Textures.Noise.LiquidNoise.Asset;
    }

    public override void Unload()
    {
        base.Unload();

        noiseTexture = null;
    }

    protected override void ApplyShaderParameters(Effect effect)
    {

        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        effect.Parameters["WorldLight"]?.SetValue(new Vector3(0.25f));


        effect.Parameters["Threshold"]?.SetValue(0.13f);
        effect.Parameters["NoiseScale"]?.SetValue(1.1f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.14f);
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0.006f, 0.022f));

        // Barely moves, so the drag is slight -- enough that the mass reads as churning rather than as a pattern it is sliding under.
        effect.Parameters["FlowStrength"]?.SetValue(0.08f);

        effect.Parameters["Bands"]?.SetValue(6f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.80f);

        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.05f, 0.15f, 0.34f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.17f, 0.42f, 0.80f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.47f, 0.74f, 1.00f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.76f, 0.92f, 1.00f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.90f);
    }

    protected override void ApplyLighting(ref FieldNode node)
        => Lighting.AddLight(node.Position, 0.04f * node.Alpha, 0.09f * node.Alpha, 0.16f * node.Alpha);

    protected override void OnTileField(int tileX, int tileY, Tile tile, float density, int owner, ChemicalPayload? payload)
    {
        float intensity = MathHelper.Clamp(density / Math.Max(TileFieldThreshold, 0.01f) * 0.5f, 0.3f, 1f);

        TileConditionSystem.Apply<SlimedCondition>(tileX, tileY, intensity, owner: owner, sync: false);
    }

    protected override void OnNPCContact(ref FieldNode node, NPC npc)
    {
        base.OnNPCContact(ref node, npc);

        npc.AddBuff(BuffID.Slow, SLOW_TICKS);

        npc.velocity.X *= 0.97f;
    }

    protected override void OnPlayerContact(ref FieldNode node, Player player) { }
}