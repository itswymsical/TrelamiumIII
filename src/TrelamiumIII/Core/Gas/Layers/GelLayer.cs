using ReLogic.Content;
using TrelamiumIII.Core.Tiles.Conditions;
using TrelamiumIII.Core.Tiles.Conditions.Types;

namespace TrelamiumIII.Core.Gas.Layers;

/// <summary>
///     Airborne gel: the blobs a slime weapon puts in the air, before they find something to stick
///     to.
/// </summary>
/// <remarks>
///     <para>
///         This is the layer half of a two-part effect, and the split is what makes the weapon
///         work. Gel in flight is a metaball field -- it sags, pools, merges with the blob next to
///         it, and is gone in a second or two. Gel that has landed is a
///         <see cref="SlimedCondition" /> on the tile, which is durable, mechanical, and knows how
///         to be walked through. The handoff between the two is <see cref="OnTileField" />: the
///         field decides where there is genuinely enough gel to matter, and hands those tiles to
///         the condition.
///     </para>
///     <para>
///         Deliberately not a <c>ChemicalGasLayer</c>. Gel is not chemistry -- it afflicts nothing,
///         rolls no reactions, and belongs to no damage type. Everything it does it does by being
///         in the way, which is the plain <see cref="GasLayer" /> contract and nothing more.
///     </para>
///     <para>
///         The palette is copied from <see cref="SlimedCondition" /> on purpose, down to the
///         threshold. Both draw through the same shader, so matching them means the blob that hits
///         a wall and the coat that appears on it are visibly the same substance rather than two
///         effects that happen to fire at the same moment.
///     </para>
/// </remarks>
public class GelLayer : GasLayer
{
    /// <summary>Ticks of <see cref="BuffID.Slow" /> put on anything wading through the airborne gel.</summary>
    private const int SLOW_TICKS = 45;

    protected override Asset<Effect> Shader => Assets.Shaders.GasFbm.Asset;

    protected override string TexturePath => Assets.Textures.VFX.RadialGradient.KEY;

    protected override int GasType => GasTypeID.Liquid;

    /// <summary>Heavy and sluggish: gel arcs and lands, it does not billow.</summary>
    protected override float DefaultDensity => 0.95f;

    protected override float DefaultViscosity => 0.72f;

    protected override float GravityScale => 0.18f;

    protected override float BuoyancyForce => 0.02f;

    protected override float Drag => 0.986f;

    protected override float LifeDecay => 0.9f;

    /// <summary>
    ///     Low, because the gel is supposed to spend its life on the floor rather than be punished
    ///     for landing on it.
    /// </summary>
    /// <remarks>
    ///     The liquid path in the base already bleeds life on floor contact, and the default
    ///     penalty on top of that had blobs evaporating on the tile they were meant to be coating.
    /// </remarks>
    protected override float EnclosedLifePenalty => 0.6f;

    /// <summary>Gel sticks where it lands.</summary>
    protected override bool UsesTileField => true;

    /// <summary>
    ///     Well under a node's worth, so a single blob glancing a wall still leaves a mark.
    /// </summary>
    /// <remarks>
    ///     The opposite tuning to thermite's, and for the opposite reason. Thermite converting
    ///     terrain has to be sure; gel is a coating, and a coating that only appears where the
    ///     spray was thickest reads as the weapon missing.
    /// </remarks>
    protected override float TileFieldThreshold => 0.45f;

    /// <summary>Rebuilt more often than the default, so a coat appears as the blob lands.</summary>
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
        effect.Parameters["Time"]?.SetValue((float)Main.timeForVisualEffects / 60f);

        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        // Ambient floor; the real light rides in the field per node.
        effect.Parameters["WorldLight"]?.SetValue(new Vector3(0.25f));

        effect.Parameters["FieldTexel"]?.SetValue(new Vector2(2f / Main.screenWidth, 2f / Main.screenHeight));
        effect.Parameters["WorldOffset"]?.SetValue(Main.screenPosition / Main.screenHeight);
        effect.Parameters["Aspect"]?.SetValue(Main.screenWidth / (float)Main.screenHeight);

        // Low threshold and light erosion: gel fuses into sheets at the first opportunity and holds
        // a taut edge rather than a ragged one.
        effect.Parameters["Threshold"]?.SetValue(0.13f);
        effect.Parameters["NoiseScale"]?.SetValue(1.1f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.14f);
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0.006f, 0.022f));

        effect.Parameters["Bands"]?.SetValue(4f);
        effect.Parameters["BodyAlpha"]?.SetValue(0.80f);

        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.05f, 0.15f, 0.34f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.17f, 0.42f, 0.80f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.47f, 0.74f, 1.00f));
        effect.Parameters["OutlineColor"]?.SetValue(new Vector3(0.76f, 0.92f, 1.00f));
        effect.Parameters["OutlineAlpha"]?.SetValue(0.90f);
    }

    protected override void ApplyLighting(ref GasNode node)
        => Lighting.AddLight(node.Position, 0.04f * node.Alpha, 0.09f * node.Alpha, 0.16f * node.Alpha);

    /// <summary>
    ///     Hands the tile to <see cref="SlimedCondition" />, carrying the coverage and the owner.
    /// </summary>
    /// <remarks>
    ///     Coverage becomes intensity, so a blob that lands square leaves a thick coat and one that
    ///     clips a corner leaves a smear. Not synced: every client ran the same node physics, so
    ///     each works out the same coat for itself.
    /// </remarks>
    protected override void OnTileField(int tileX, int tileY, Tile tile, float density, int owner)
    {
        float intensity = MathHelper.Clamp(density / Math.Max(TileFieldThreshold, 0.01f) * 0.5f, 0.3f, 1f);

        TileConditionSystem.Apply<SlimedCondition>(tileX, tileY, intensity, owner: owner, sync: false);
    }

    /// <summary>Wading through the cloud itself is sticky, on top of whatever it lands on.</summary>
    protected override void OnNPCContact(ref GasNode node, NPC npc)
    {
        npc.AddBuff(BuffID.Slow, SLOW_TICKS);

        npc.velocity.X *= 0.97f;
    }

    /// <summary>
    ///     Nothing at all, and that is the intended behaviour.
    /// </summary>
    /// <remarks>
    ///     A gel weapon paints the ground directly in front of its user; anything this did to a
    ///     player would land on the person firing it more often than on the target.
    /// </remarks>
    protected override void OnPlayerContact(ref GasNode node, Player player) { }
}
