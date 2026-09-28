using ReLogic.Content;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Gas;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Fields;

namespace TrelamiumIII.Core.Tiles.Conditions.Types;

// Palette, affliction rate, reaction odds, contact debuff and light all come from ElementProfile via
// the base, so each residue below only states what makes it distinct: which layer it breathes, how it
// moves underfoot, how long it lasts.

public sealed class ThermalResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Thermal;

    public override float Duration => 9f * 60f;

    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.None;

    protected override Vector3 EmittedLight => Vector3.Zero;

    public override void OnApplied(ref TileConditionInstance instance)
        => BurnedCondition.Ignite(instance.X, instance.Y, BurnProfile.Thermal, instance.Intensity, instance.Owner);
}

public sealed class CryonicResidueCondition : ChemicalReactionTileCondition
{
    private const float SLIP_RECOVERY = 0.72f;

    private const float SLIDE_THRESHOLD = 0.35f;

    /// <summary>Ticks for the crystal veins to grow in after a tile first freezes.</summary>
    private const float CRYSTAL_GROWTH_TICKS = 90f;

    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Cryonic;

    public override Asset<Effect>? Shader => Assets.Shaders.TileFrostMask.Asset;

    public override int FadeInTicks => 16;

    public override int FadeOutTicks => 24;

    protected override GasField? LingerLayer => FieldSystem.GetLayer<IceMistField>();

    private Asset<Texture2D>? cellTexture;

    private Asset<Texture2D>? veinTexture;

    public override void Load()
    {
        base.Load();

        if (Main.dedServ)
            return;

        cellTexture = Assets.Textures.Noise.VoronoiNoise.Asset;
        veinTexture = Assets.Textures.Noise.VeinVoronoi.Asset;
    }

    public override void Unload()
    {
        base.Unload();

        cellTexture = null;
        veinTexture = null;
    }

    public override TileCanvasSample EncodeCanvas(in TileConditionInstance instance)
        => new(instance.Strength, new Vector3(MathHelper.Clamp(instance.TotalAge / CRYSTAL_GROWTH_TICKS, 0f, 1f), 0f, 0f));

    public override void ApplyShaderParameters(Effect effect)
    {
        if (cellTexture is { Value: { } cells })
            effect.Parameters["uCellTex"]?.SetValue(cells);

        if (veinTexture is { Value: { } veins })
            effect.Parameters["uVeinTex"]?.SetValue(veins);

        effect.Parameters["SurfaceLip"]?.SetValue(2f);

        effect.Parameters["Threshold"]?.SetValue(Threshold);
        effect.Parameters["CellScale"]?.SetValue(0.25f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.3f);
        effect.Parameters["DepthRange"]?.SetValue(0.5f);
        effect.Parameters["EdgeWidth"]?.SetValue(0.08f);

        effect.Parameters["VeinScale"]?.SetValue(0.2f);
        effect.Parameters["VeinCut"]?.SetValue(0.35f);
        effect.Parameters["VeinAlpha"]?.SetValue(0.85f);

        // Bluer than the element palette: that is tuned for mist, and reads grey laid thin over dirt.
        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.42f, 0.66f, 0.92f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.66f, 0.84f, 1.00f));
        effect.Parameters["VeinColor"]?.SetValue(EdgeColor);
        effect.Parameters["EdgeColor"]?.SetValue(EdgeColor);
        effect.Parameters["RimeColor"]?.SetValue(Vector3.Lerp(EdgeColor, Vector3.One, 0.5f));
        effect.Parameters["BodyAlpha"]?.SetValue(0.72f);
        effect.Parameters["BodyAlphaFloor"]?.SetValue(0.45f);
        effect.Parameters["EdgeAlpha"]?.SetValue(0.9f);
        effect.Parameters["RimeAlpha"]?.SetValue(0.85f);

        effect.Parameters["SparkleDensity"]?.SetValue(0.035f);
        effect.Parameters["SparkleSpeed"]?.SetValue(2.2f);
        effect.Parameters["SparkleColor"]?.SetValue(new Vector3(0.9f, 0.97f, 1f));

        effect.Parameters["AmbientLight"]?.SetValue(new Vector3(0.22f));
    }

    protected override float LingerRadius => 6f;

    protected override int LingerInterval => 24;

    protected override float LingerRise => 0.2f;

    public override void OnPlayerContact(ref TileConditionInstance instance, Player player)
    {
        Slide(player, ref player.velocity, player.oldVelocity, instance.Strength);

        // slippy is what vanilla sets from an icy tile, and the collision code reads it on the next
        // pass
        player.slippy = true;
        player.slippy2 = true;
    }

    public override void OnNPCContact(ref TileConditionInstance instance, NPC npc)
    {
        base.OnNPCContact(ref instance, npc);

        if (!npc.noTileCollide && !npc.noGravity)
            Slide(npc, ref npc.velocity, npc.oldVelocity, instance.Strength);
    }

    /// <summary>
    ///     Hands back the horizontal momentum the entity's own update just scrubbed off.
    /// </summary>
    private static void Slide(Entity entity, ref Vector2 velocity, Vector2 oldVelocity, float strength)
    {
        if (velocity.Y != 0f || MathF.Abs(oldVelocity.X) < SLIDE_THRESHOLD)
            return;

        velocity.X = MathHelper.Lerp(velocity.X, oldVelocity.X, SLIP_RECOVERY * MathHelper.Clamp(strength, 0f, 1f));

        if (Main.dedServ || !Main.rand.NextBool(20))
            return;

        Dust dust = Dust.NewDustPerfect(
            entity.Bottom + new Vector2(Main.rand.NextFloat(-6f, 6f), -2f),
            DustID.SnowflakeIce,
            new Vector2(-MathF.Sign(velocity.X) * Main.rand.NextFloat(0.4f, 1.1f), -Main.rand.NextFloat(0.2f, 0.6f)),
            120,
            default,
            Main.rand.NextFloat(0.5f, 0.9f)
        );

        dust.noGravity = true;
    }
}

public sealed class CausticResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Caustic;

    /// <inheritdoc cref="NecroticResidueCondition.RenderMode" />
    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.None;

    protected override GasField? LingerLayer => FieldSystem.GetLayer<PhytoncideSporeField>();
}

public sealed class NecroticResidueCondition : ChemicalReactionTileCondition
{
    public override ChemicalDamageType ChemicalType => ChemicalDamageType.Necrotic;

    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.None;

    protected override int ContactBuff => BuffID.Bleeding;

    protected override GasField? LingerLayer => FieldSystem.GetLayer<VilesporeField>();
}
