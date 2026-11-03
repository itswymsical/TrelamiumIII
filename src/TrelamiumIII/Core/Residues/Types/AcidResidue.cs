using ReLogic.Content;
using TrelamiumIII.Content.Buffs;
using TrelamiumIII.Core.Residues.Reactions;

namespace TrelamiumIII.Core.Residues.Types;

/// <summary>
///     Acid eating into the terrain: a bubbling film that pits what it sits on, breaks through anything an explosion could
///     break, and drips onto whatever is underneath. What it can't break it only scars.
/// </summary>
/// <remarks>
///     <para>
///         <see cref="ResidueInstance.Level" /> is how far the tile has been eaten, 0..1. It climbs while the film is
///         wet, at a rate set by intensity, so the middle of a splash breaks through while its thin rim dries first and
///         only leaves a scar. <see cref="ResidueInstance.Stage" /> records that the tile has had its chance to break.
///     </para>
///     <para>
///         Water doesn't wash it off, in keeping with the acid liquids, which dissolve water. It keeps the film wet.
///     </para>
/// </remarks>
public sealed class AcidResidue : Residue
{
    private const float QUENCH_PER_PROBE = 16f;

    /// <summary>Ticks for a full-strength film to eat through a tile.</summary>
    private const float EATING_TICKS = 5f * 60f;

    /// <summary>Share of its life left when the film has dried to a scar: it stops eating and stops hurting.</summary>
    private const float DRY_FRACTION = 0.3f;

    /// <summary>Share of its life left when the scar starts to fade.</summary>
    private const float SCAR_FADE_FRACTION = 0.2f;

    /// <summary>Strength kept by what drips through a broken tile onto the one below.</summary>
    private const float DRIP_FALLOFF = 0.82f;

    /// <summary>Below this strength a drip has nothing left to eat with, so a splash bores a shaft rather than a hole through the world.</summary>
    private const float WEAKEST_DRIP = 0.55f;

    /// <summary>Film below this doesn't hurt what touches it.</summary>
    private const float HARMFUL_FILM = 0.15f;

    /// <summary>The furthest it eats into a body, as a share of a tile eaten through.</summary>
    private const float BODY_MOST_EATEN = 0.3f;

    private const int BODY_BUBBLE_ODDS = 12;

    private const byte STAGE_EATING = 0;

    private const byte STAGE_STOOD_UP_TO_IT = 1;

    public override float Duration => 14f * 60f;

    public override int MaxTiles => 1200;

    public override ResidueRenderMode RenderMode => ResidueRenderMode.Canvas;

    public override Asset<Effect>? Shader => Assets.Shaders.TileAcidMask.Asset;

    public override int FadeInTicks => 6;

    public override int FadeOutTicks => 30;

    private Asset<Texture2D>? flowTexture;

    private Asset<Texture2D>? cellTexture;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        flowTexture = Assets.Textures.Noise.Melt_03.Asset;
        cellTexture = Assets.Textures.Noise.VoronoiNoise.Asset;
    }

    public override void Unload()
    {
        flowTexture = null;
        cellTexture = null;
    }

    public override ResidueElement ElementsOf(in ResidueInstance instance) => ResidueElement.Corrosive;

    public override void UpdateTile(ref ResidueInstance instance)
    {
        float film = Film(in instance);

        if (film <= 0f)
            return;

        instance.Level = MathF.Min(instance.Level + instance.Intensity / EATING_TICKS, 1f);

        if (instance.Level >= 1f && instance.Stage == STAGE_EATING)
            BreakThrough(ref instance);

        if (Main.dedServ || !OnScreen(in instance))
            return;

        Fizz(in instance, film);
    }

    /// <summary>The tile has been eaten through. If it breaks, the acid carries on into what's below it; if it stands up to it, it stays pitted until the acid dries.</summary>
    private static void BreakThrough(ref ResidueInstance instance)
    {
        instance.Stage = STAGE_STOOD_UP_TO_IT;

        if (!TryDestroyTile(ref instance))
            return;

        float drip = instance.Intensity * DRIP_FALLOFF;

        // Synced, unlike a splash: only the machine that broke the tile knows it happened on this tick.
        if (drip >= WEAKEST_DRIP)
            Corrode(instance.X, instance.Y + 1, drip, instance.Owner, sync: true);
    }

    private static void Fizz(in ResidueInstance instance, float film)
    {
        Lighting.AddLight(instance.WorldCenter, 0.1f * film, 0.26f * film, 0.03f * film);

        if (Main.rand.NextBool((int)MathHelper.Lerp(150f, 34f, film)))
        {
            Dust bubble = Dust.NewDustPerfect(
                instance.WorldPosition + new Vector2(Main.rand.NextFloat(16f), Main.rand.NextFloat(-2f, 6f)),
                DustID.BubbleBurst_Green,
                new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(-0.9f, -0.2f)),
                80,
                default,
                Main.rand.NextFloat(0.6f, 1f)
            );

            bubble.noGravity = true;
        }

        if (Main.rand.NextBool((int)MathHelper.Lerp(500f, 140f, instance.Level)))
            Fume(instance.WorldCenter + new Vector2(0f, -6f));

        // Underneath an overhang it runs off in drops, which fall the way a drop does.
        if (Main.rand.NextBool(120) && !WorldGen.SolidTile(instance.X, instance.Y + 1))
        {
            Dust.NewDustPerfect(
                new Vector2(instance.WorldPosition.X + Main.rand.NextFloat(16f), instance.WorldPosition.Y + 16f),
                DustID.t_Slime,
                new Vector2(0f, 0.5f),
                90,
                new Color(170, 255, 70),
                Main.rand.NextFloat(0.6f, 0.9f)
            );
        }
    }

    private static void Fume(Vector2 position)
    {
        Dust fume = Dust.NewDustPerfect(
            position + Main.rand.NextVector2Circular(7f, 3f),
            DustID.Smoke,
            new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(-1.1f, -0.4f)),
            170,
            new Color(170, 210, 90),
            Main.rand.NextFloat(0.7f, 1.2f)
        );

        fume.noGravity = true;
    }

    /// <summary>Eaten and film as amounts scaled by the mark's coverage, so neither one draws past the edge of it.</summary>
    public override TileCanvasSample EncodeCanvas(in ResidueInstance instance)
    {
        float coverage = instance.Intensity * MathHelper.Clamp(instance.Fade / SCAR_FADE_FRACTION, 0f, 1f);

        return new TileCanvasSample(coverage, new Vector3(coverage * instance.Level, instance.Intensity * Film(in instance), 0f));
    }

    /// <summary>How wet the film still is, 1 until it starts to dry and 0 once it's only a scar.</summary>
    private static float Film(in ResidueInstance instance)
        => MathHelper.Clamp((instance.Fade - DRY_FRACTION) / (1f - DRY_FRACTION) * 3f, 0f, 1f);

    public override void ApplyShaderParameters(Effect effect)
    {
        if (flowTexture is { Value: { } flow })
            effect.Parameters["uFlowTex"]?.SetValue(flow);

        if (cellTexture is { Value: { } cells })
            effect.Parameters["uCellTex"]?.SetValue(cells);

        effect.Parameters["SurfaceLip"]?.SetValue(2f);

        effect.Parameters["ScarColor"]?.SetValue(new Vector3(0.20f, 0.24f, 0.07f));
        effect.Parameters["ScarAlpha"]?.SetValue(0.62f);
        effect.Parameters["ScarThreshold"]?.SetValue(0.12f);
        effect.Parameters["ScarSoftness"]?.SetValue(0.5f);
        effect.Parameters["ScarBands"]?.SetValue(4f);

        effect.Parameters["PitScale"]?.SetValue(0.14f);
        effect.Parameters["PitDistort"]?.SetValue(0.05f);
        effect.Parameters["PitReach"]?.SetValue(0.7f);
        effect.Parameters["PitColor"]?.SetValue(new Vector3(0.025f, 0.05f, 0.02f));
        effect.Parameters["PitAlpha"]?.SetValue(0.96f);
        effect.Parameters["EatingEdge"]?.SetValue(0.035f);
        effect.Parameters["EatingColor"]?.SetValue(new Vector3(0.86f, 1f, 0.26f));
        effect.Parameters["EatingGlow"]?.SetValue(0.55f);

        effect.Parameters["FilmThreshold"]?.SetValue(0.2f);
        effect.Parameters["FilmDepth"]?.SetValue(0.55f);
        effect.Parameters["FlowScale"]?.SetValue(0.09f);
        effect.Parameters["FlowStrength"]?.SetValue(0.36f);
        effect.Parameters["FlowDrift"]?.SetValue(new Vector2(0.002f, -0.022f));
        effect.Parameters["FilmBands"]?.SetValue(4f);
        effect.Parameters["FilmAlpha"]?.SetValue(0.84f);

        effect.Parameters["ShadowColor"]?.SetValue(new Vector3(0.14f, 0.36f, 0.05f));
        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.38f, 0.74f, 0.08f));
        effect.Parameters["CoreColor"]?.SetValue(new Vector3(0.68f, 0.98f, 0.20f));
        effect.Parameters["SurfaceColor"]?.SetValue(new Vector3(0.90f, 1f, 0.48f));
        effect.Parameters["SurfaceAlpha"]?.SetValue(0.92f);

        effect.Parameters["BubbleScale"]?.SetValue(0.2f);
        effect.Parameters["BubbleSpeed"]?.SetValue(0.55f);
        effect.Parameters["BubbleSize"]?.SetValue(0.2f);
        effect.Parameters["BubbleColor"]?.SetValue(new Vector3(0.94f, 1f, 0.62f));
        effect.Parameters["BubbleAlpha"]?.SetValue(0.9f);

        effect.Parameters["DripLength"]?.SetValue(9f);
    }

    /// <summary>A wet film that slowly pits what it's on, stopping short of eating it through.</summary>
    public override TileCanvasSample EncodeBody(float rise, int age)
    {
        var instance = new ResidueInstance
        {
            Life = 1f,
            MaxLife = 1f,
            Intensity = MathHelper.Clamp(rise * 3f, 0f, 1f),
            Level = MathF.Min(age / EATING_TICKS, BODY_MOST_EATEN),
        };

        return EncodeCanvas(in instance);
    }

    public override void ApplyBodyParameters(Effect effect, float gravDir, float height)
    {
        ApplyShaderParameters(effect);

        float detail = BodyDetail(height);

        effect.Parameters["FlowScale"]?.SetValue(0.35f * detail);
        effect.Parameters["PitScale"]?.SetValue(0.28f * detail);
        effect.Parameters["BubbleScale"]?.SetValue(0.4f * detail);
        effect.Parameters["DripLength"]?.SetValue(5f);
    }

    public override void UpdateBody(Entity body, float fill)
    {
        if (Main.dedServ)
            return;

        Lighting.AddLight(body.Center, 0.1f * fill, 0.26f * fill, 0.03f * fill);

        if (!Main.rand.NextBool(BODY_BUBBLE_ODDS))
            return;

        Dust bubble = Dust.NewDustPerfect(
            body.Bottom - new Vector2(Main.rand.NextFloat(-body.width * 0.5f, body.width * 0.5f), Main.rand.NextFloat(body.height * fill)),
            DustID.BubbleBurst_Green,
            body.velocity * 0.4f + new Vector2(Main.rand.NextFloat(-0.2f, 0.2f), Main.rand.NextFloat(-0.9f, -0.2f)),
            80,
            default,
            Main.rand.NextFloat(0.6f, 1f));

        bubble.noGravity = true;
    }

    public override void OnPlayerContact(ref ResidueInstance instance, Player player)
    {
        float film = instance.Intensity * Film(in instance);

        if (film < HARMFUL_FILM || !FirstContact(player))
            return;

        player.AddBuff(ModContent.BuffType<AcidDebuff>(), (int)MathHelper.Lerp(60f, 180f, film));
    }

    public override void OnNPCContact(ref ResidueInstance instance, NPC npc)
    {
        float film = instance.Intensity * Film(in instance);

        if (film < HARMFUL_FILM || npc.friendly || npc.noTileCollide || !FirstContact(npc))
            return;

        npc.AddBuff(ModContent.BuffType<AcidDebuff>(), (int)MathHelper.Lerp(80f, 220f, film));
    }

    public override void OnLiquidContact(ref ResidueInstance instance, int liquidType, int amount)
    {
        if (liquidType == LiquidID.Water)
        {
            instance.Life = instance.MaxLife;

            return;
        }

        instance.Life -= QUENCH_PER_PROBE * (amount / 255f);

        if (!Main.dedServ && OnScreen(in instance))
            Fume(instance.WorldCenter);
    }

    /// <summary>Lays acid on one tile.</summary>
    public static bool Corrode(int i, int j, float intensity = 1f, int owner = ResidueInstance.NoOwner, bool sync = false)
        => ResidueSystem.Apply<AcidResidue>(i, j, intensity, owner: owner, sync: sync);

    /// <summary>Splashes acid over everything solid within <paramref name="radius" /> pixels, thinning toward the edge.</summary>
    public static void Splash(Vector2 center, float radius, float intensity = 1f, int owner = ResidueInstance.NoOwner)
        => ResidueSystem.ApplyCircle<AcidResidue>(center, radius, intensity, owner: owner);
}
