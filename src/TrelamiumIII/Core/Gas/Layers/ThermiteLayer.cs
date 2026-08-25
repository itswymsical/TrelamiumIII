using ReLogic.Content;
using Terraria.Audio;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Content.Alchemy.Weapons;
using TrelamiumIII.Content.Rusted;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.Gas.Layers;

public class ThermiteLayer : ChemicalGasLayer
{
    protected override Asset<Effect> Shader => Assets.Shaders.LiquidFbm.Asset;
    protected override string TexturePath => Assets.Textures.VFX.BloomFlare.KEY;
    protected override int GasType => GasTypeID.Liquid;
    protected override float DefaultDensity => 1.0f;
    protected override float DefaultViscosity => 0.2f;
    protected override float EnclosedLifePenalty => 0.5f;
    protected override float GravityScale => 0.33f;
    protected override float BuoyancyForce => 0.5f;
    protected override float Drag => 0.9956f;
 
    protected override float LifeDecay => 0.35f;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Thermal;
    public override float ChemicalDamage => 1f;
    public override int AfflictionDuration => 90;
    public override float AfflictionRate => 30f;
    public override float ReactionChance => 0.02f;
    public override float ReactionDamage => 1.25f;

    private Asset<Texture2D> noiseTex;

    public override void Load()
    {
        base.Load();

        noiseTex = Assets.Textures.Noise.Melt_03.Asset;
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

        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(
            2f / Main.screenWidth, 2f / Main.screenHeight));
        effect.Parameters["WorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);
        effect.Parameters["Aspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

        effect.Parameters["Threshold"]?.SetValue(0.16f);
        effect.Parameters["NoiseScale"]?.SetValue(1.05f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.22f);
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0f, -0.08f));
        effect.Parameters["NoiseStretch"]?.SetValue(new Vector2(1f, 0.45f));

        effect.Parameters["Bands"]?.SetValue(3f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.94f);

        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.72f, 0.38f, 0.01f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.75f, 0.22f, 0.01f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.94f, 0.21f, 0f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.95f, 0.15f, 0f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.8f);
        effect.Parameters["HighlightColor"]?.SetValue(new Vector3(1f, 0.77f, 0.22f));
        effect.Parameters["HighlightAlpha"]?.SetValue(0.9f);
    }

    protected override void ApplyLighting(ref GasNode node)
    {
        Dust.NewDust(node.Position / 16, 16, 16, DustID.Lava);
        Lighting.AddLight(node.Position, new Vector3(1f, 0.77f, 0.22f));
    }

    protected override void BindAdditionalTextures(GraphicsDevice device)
    {
        base.BindAdditionalTextures(device);

        if (noiseTex?.IsLoaded == true)
            device.Textures[1] = noiseTex.Value;
    }

    protected override void OnNPCContact(ref GasNode node, NPC npc)
    {
        // Apply chemistry first, then layer the visual bleeding tick on top.
        base.OnNPCContact(ref node, npc);
        if (!npc.friendly)
            npc.AddBuff(BuffID.OnFire, 120);
    }

    protected override void OnLiquidContact(ref GasNode node, int tileX, int tileY, Tile tile)
    {
        node.Life *= 0.99f;
        if (ThermiteFlaskProjectile.shattered)
        {
            LiquidUtils.ConvertLiquidIntoTile(ref node, tileX, tileY, tile,
                TileID.Obsidian, LiquidID.Water, amountLeft: 50);
        }
        if (ThermiteFlaskProjectile.shattered)
        {
            LiquidUtils.ConvertLiquidIntoTile(ref node, tileX, tileY, tile,
               (ushort)ModContent.TileType<RustedIronOre>(), LiquidID.Lava, amountLeft: 50);
        }
        if (ThermiteFlaskProjectile.shattered)
        {
            LiquidUtils.ConvertLiquidIntoTile(ref node, tileX, tileY, tile,
               TileID.CrispyHoneyBlock, LiquidID.Honey, amountLeft: 50);
        }
    }

    protected override void OnTileContact(ref GasNode node, int tileX, int tileY, Tile tile)
    {
        node.Viscosity *= 1.01f;
        node.Velocity *= 0.89f;
    }
    public override void OnChemicalReaction(in ChemicalReactionEvent e)
    {
        base.OnChemicalReaction(e);

        Vector2 impactDir = e.Target.oldVelocity.SafeNormalize(-Vector2.UnitY);
        Vector2 splashDir = Vector2.Lerp(impactDir, -Vector2.UnitY, 0.5f)
                                  .SafeNormalize(-Vector2.UnitY);

        SoundEngine.PlaySound(SoundID.Item74);

        int count = Main.rand.Next(6, 8);
        for (int i = 0; i < count; i++)
        {
            Vector2 vel = splashDir.RotatedByRandom(MathHelper.PiOver4)
                        * Main.rand.NextFloat(4f, 7f);

            this.SpawnNode(e.Target.Center, vel, radius: 16.5f, life: 200);
        }
    }
}