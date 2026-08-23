using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Content.Alchemy.Weapons;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.Gas.Layers;

public class IceMistLayer : ChemicalGasLayer
{
    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;
    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;
    protected override int GasType => GasTypeID.Gas;

    protected override float DefaultDensity => 0.35f;
    protected override float DefaultViscosity => 0.25f;
    protected override float EnclosedLifePenalty => 1.5f;
    protected override float GravityScale => 0.04f;
    protected override float BuoyancyForce => 0.1f;
    protected override float Drag => 0.936f;
    protected override float LifeDecay => 0.75f;
    protected override BlendState CompositeBlendState => BlendState.AlphaBlend;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Cryonic;
    public override float ChemicalDamage => 0.3f;
    public override int AfflictionDuration => 60;
    public override float AfflictionRate => 30f;
    public override float ReactionChance => 0.02f;
    public override float ReactionDamage => 3f;


    private Asset<Texture2D> noiseTex;

    public override void Load()
    {
        base.Load();
        noiseTex = Assets.Textures.Noise.LiquidNoise.Asset;
    }

    protected override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["Time"]?.SetValue((float)Main.timeForVisualEffects / 60f);
        effect.Parameters["uNoiseTex"]?.SetValue(noiseTex.Value);

        int stride = Math.Max(1, gasNodes.Count / 16);

        Vector3 light = Vector3.Zero;
        int samples = 0;

        for (int i = 0; i < gasNodes.Count; i += stride)
        {
            light += Lighting.GetSubLight(gasNodes[i].Position);
            samples++;
        }

        light /= samples;

        effect.Parameters["WorldLight"]?.SetValue(light);

        // Field target is screen / PixelScale(2): one field texel = one 2px vanilla pixel.
        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(
            2f / Main.screenWidth, 2f / Main.screenHeight));
        effect.Parameters["WorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);
        effect.Parameters["Aspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

        // Airy mist: softer erosion than the spores, slow lateral roll.
        effect.Parameters["Threshold"]?.SetValue(0.14f);
        effect.Parameters["NoiseScale"]?.SetValue(1.4f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.18f);
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0.05f, 0.015f));

        effect.Parameters["Bands"]?.SetValue(4f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.8f);

        // Frost palette: steel-blue shadow through pale frost to a near-white core,
        // with a bright icy rim (bright outlines read better than dark ink).
        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.36f, 0.52f, 0.68f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.62f, 0.80f, 0.92f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.88f, 0.97f, 1.0f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.85f, 0.96f, 1.0f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.85f);
    }

    protected override void OnNPCContact(ref GasNode node, NPC npc)
    {
        // Apply chemistry first, then layer the visual frostburn tick on top.
        base.OnNPCContact(ref node, npc);
        if (!npc.friendly)
            npc.AddBuff(BuffID.Frostburn, 30);
    }

    protected override void OnPlayerContact(ref GasNode node, Player player) { }
    protected override void OnTileContact(ref GasNode node, int tileX, int tileY, Tile tile) { }

    protected override void OnLiquidContact(ref GasNode node, int tileX, int tileY, Tile tile)
    {
        if (HyperboreanFlaskProjectile.shattered)
        {
            LiquidUtils.ConvertLiquidIntoTile(ref node, tileX, tileY, tile,
                TileID.BreakableIce, LiquidID.Water, amountLeft: 200);
        }
    }
}