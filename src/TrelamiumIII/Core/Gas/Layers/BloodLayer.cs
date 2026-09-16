using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Tiles.Conditions;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using Terraria.Audio;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Gas.Layers;

public class BloodLayer : ChemicalGasField
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
    public override float ChemicalDamage => 0.4f;
    public override int AfflictionDuration => 90;
    public override float AfflictionRate => 30f;
    public override float ReactionChance => 0.02f;
    public override float ReactionDamage => 4f;

    /// <summary>
    ///     Leaves nothing on the terrain it lands on.
    /// </summary>
    /// <remarks>
    ///     <para>
    ///         Blood used to pool into a residue that went on steaming the layer back out, which put
    ///         it in a loop with itself: the puddle exhaled blood, the blood pooled, and the pooling
    ///         refreshed the puddle. A splash is a moment, not a place the fight then has to be had
    ///         in, and a burst that permanently redecorates the floor is not what the flask is for.
    ///     </para>
    ///     <para>
    ///         Null rather than a silent condition, because null is the answer the base actually
    ///         reads: it switches off <see cref="GasField.UsesTileField" />, so the layer stops
    ///         paying for the accumulation pass that would work out where to mark.
    ///     </para>
    /// </remarks>
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

        Vector3 light = new Vector3(0.18f); // ambient floor; the real light rides in the field per node

        effect.Parameters["WorldLight"]?.SetValue(light);


        effect.Parameters["Threshold"]?.SetValue(0.12f);
        effect.Parameters["NoiseScale"]?.SetValue(0.75f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.04f);
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0f, -0.08f));

        // Barely moves, so the drag is slight -- enough that the mass reads as churning rather than as a pattern it is sliding under.
        effect.Parameters["FlowStrength"]?.SetValue(0.08f);
        effect.Parameters["NoiseStretch"]?.SetValue(new Vector2(1f, 0.45f));

        // LiquidFbm kept its thinnest cells slightly more opaque than GasFbm did; preserved here
        // now that both materials share one shader.
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
        // Apply chemistry first, then layer the visual bleeding tick on top.
        base.OnNPCContact(ref node, npc);

        npc.AddBuff(BuffID.BloodButcherer, 30);
    }

    public override void OnChemicalReaction(in ChemicalReactionEvent e)
    {
        base.OnChemicalReaction(e);

        Vector2 impactDir = e.Target.oldVelocity.SafeNormalize(-Vector2.UnitY);
        Vector2 splashDir = Vector2.Lerp(impactDir, -Vector2.UnitY, 0.5f)
                                  .SafeNormalize(-Vector2.UnitY);

        CameraSystem.shake = 4;
        SoundEngine.PlaySound(SoundID.Item74);

        int count = Main.rand.Next(6, 8);
        for (int i = 0; i < count; i++)
        {
            Vector2 vel = splashDir.RotatedByRandom(MathHelper.PiOver4)
                        * Main.rand.NextFloat(4f, 7f);

            this.SpawnNode(e.Target.Center, vel, radius: 16.5f, life: 140);
        }
    }
}
