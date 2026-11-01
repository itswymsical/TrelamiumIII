using ReLogic.Content;

namespace TrelamiumIII.Core.Tiles.Conditions.Types;

/// <summary>
///     Frost the world grows by itself: rime on the snow biome, ice blown onto the surface by a
///     blizzard. Slippery underfoot and nothing more; <see cref="CryonicResidueCondition" /> is the
///     chemical kind.
/// </summary>
/// <remarks>Laid by the rules in <c>EnvironmentRules</c>, never synced.</remarks>
public sealed class FrostCondition : TileCondition
{
    /// <summary>One tick in this many, per tile, looks for heat that would melt it.</summary>
    private const int HEAT_PROBE_MASK = 15;

    private static readonly Vector3 EdgeColor = new(0.82f, 0.95f, 1f);

    public override float Duration => 25f * 60f;

    public override int MaxTiles => 2500;

    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.Canvas;

    public override Asset<Effect>? Shader => Assets.Shaders.TileFrostMask.Asset;

    public override int FadeInTicks => 40;

    public override int FadeOutTicks => 45;

    public override void Load() => FrostFilm.Load();

    public override void Unload() => FrostFilm.Unload();

    public override TileCanvasSample EncodeCanvas(in TileConditionInstance instance) => FrostFilm.Encode(in instance);

    public override void ApplyShaderParameters(Effect effect)
        => FrostFilm.Apply(effect, EdgeColor, threshold: 0.24f, bodyAlpha: 0.6f);

    public override void UpdateTile(ref TileConditionInstance instance)
    {
        if (((instance.Age + instance.X * 5 + instance.Y * 11) & HEAT_PROBE_MASK) != 0)
            return;

        if (TileConditionSystem.Has<BurnedCondition>(instance.X, instance.Y) || TileConditionSystem.Has<MagmaCondition>(instance.X, instance.Y))
            instance.Life = 0f;
    }

    public override void OnPlayerContact(ref TileConditionInstance instance, Player player)
    {
        if (!FirstContact(player))
            return;

        FrostFilm.Slide(player, ref player.velocity, player.oldVelocity, instance.Strength);

        player.slippy = true;
        player.slippy2 = true;
    }

    public override void OnNPCContact(ref TileConditionInstance instance, NPC npc)
    {
        if (npc.noTileCollide || npc.noGravity || !FirstContact(npc))
            return;

        FrostFilm.Slide(npc, ref npc.velocity, npc.oldVelocity, instance.Strength);
    }

    public override void OnLiquidContact(ref TileConditionInstance instance, int liquidType, int amount)
    {
        if (liquidType == LiquidID.Lava)
            instance.Life = 0f;
    }
}

/// <summary>What every kind of frost shares: its canvas encoding, its shader parameters and how it slides things.</summary>
internal static class FrostFilm
{
    private const float SLIP_RECOVERY = 0.72f;

    private const float SLIDE_THRESHOLD = 0.35f;

    /// <summary>Ticks for the crystal veins to grow in after a tile first freezes.</summary>
    private const float CRYSTAL_GROWTH_TICKS = 90f;

    private static Asset<Texture2D>? cellTexture;

    private static Asset<Texture2D>? veinTexture;

    public static void Load()
    {
        if (Main.dedServ)
            return;

        cellTexture = Assets.Textures.Noise.VoronoiNoise.Asset;
        veinTexture = Assets.Textures.Noise.VeinVoronoi.Asset;
    }

    public static void Unload()
    {
        cellTexture = null;
        veinTexture = null;
    }

    /// <summary>Coverage, and crystal growth counted from the first freeze.</summary>
    public static TileCanvasSample Encode(in TileConditionInstance instance)
        => new(instance.Strength, new Vector3(MathHelper.Clamp(instance.TotalAge / CRYSTAL_GROWTH_TICKS, 0f, 1f), 0f, 0f));

    /// <param name="edgeColor">The freezing front and the veins; the rime is this halfway to white.</param>
    public static void Apply(Effect effect, Vector3 edgeColor, float threshold, float bodyAlpha = 0.72f)
    {
        if (cellTexture is { Value: { } cells })
            effect.Parameters["uCellTex"]?.SetValue(cells);

        if (veinTexture is { Value: { } veins })
            effect.Parameters["uVeinTex"]?.SetValue(veins);

        effect.Parameters["SurfaceLip"]?.SetValue(2f);

        effect.Parameters["Threshold"]?.SetValue(threshold);
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
        effect.Parameters["VeinColor"]?.SetValue(edgeColor);
        effect.Parameters["EdgeColor"]?.SetValue(edgeColor);
        effect.Parameters["RimeColor"]?.SetValue(Vector3.Lerp(edgeColor, Vector3.One, 0.5f));
        effect.Parameters["BodyAlpha"]?.SetValue(bodyAlpha);
        effect.Parameters["BodyAlphaFloor"]?.SetValue(0.45f);
        effect.Parameters["EdgeAlpha"]?.SetValue(0.9f);
        effect.Parameters["RimeAlpha"]?.SetValue(0.85f);

        effect.Parameters["SparkleDensity"]?.SetValue(0.035f);
        effect.Parameters["SparkleSpeed"]?.SetValue(2.2f);
        effect.Parameters["SparkleColor"]?.SetValue(new Vector3(0.9f, 0.97f, 1f));

        effect.Parameters["AmbientLight"]?.SetValue(Vector3.Zero);
    }

    /// <summary>
    ///     Hands back the horizontal momentum the entity's own update just scrubbed off.
    /// </summary>
    public static void Slide(Entity entity, ref Vector2 velocity, Vector2 oldVelocity, float strength)
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
