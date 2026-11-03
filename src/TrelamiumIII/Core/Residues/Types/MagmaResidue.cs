using ReLogic.Content;
using TrelamiumIII.Core.Residues.Reactions;

namespace TrelamiumIII.Core.Residues.Types;

/// <summary>
///     Molten rock left where a lava slime lands or bursts. It glows and burns while hot, cools to a
///     dark crust over a few seconds, and water quenches it outright.
/// </summary>
/// <remarks>
///     <see cref="ResidueInstance.Level" /> is the heat, 0..1. The film is dyed per tile from
///     the crust colour up to the look's own body colour by that heat, which is how a patch can be
///     molten in the middle and set at the rim with one uniform palette.
/// </remarks>
public sealed class MagmaResidue : FilmResidue
{
    /// <summary>Ticks for a full-heat tile to cool to crust.</summary>
    private const float COOLING_TICKS = 6f * 60f;

    /// <summary>Heat below which it no longer burns what touches it.</summary>
    private const float BURNING_HEAT = 0.3f;

    private static readonly Vector3 CrustColor = new(0.2f, 0.12f, 0.09f);

    protected override FilmLook Look { get; } = new()
    {
        Threshold = 0.2f,
        NoiseScale = 0.08f,
        NoiseStrength = 0.2f,
        NoiseDrift = new Vector2(0.004f, 0.01f),
        Bands = 5f,
        ShadowColor = new Vector3(0.55f, 0.1f, 0.03f),
        BodyColor = new Vector3(1f, 0.42f, 0.08f),
        CoreColor = new Vector3(1f, 0.78f, 0.3f),
        BodyAlpha = 0.92f,
        OutlineColor = new Vector3(0.32f, 0.1f, 0.04f),
        OutlineAlpha = 0.95f,
        HighlightColor = new Vector3(1f, 0.9f, 0.55f),
        HighlightAlpha = 0.7f,
        EmissiveEdge = 0.6f,
        SurfaceLip = 2f,
        TintStrength = 1f,
        TintWash = 0.3f,
    };

    protected override Asset<Texture2D> NoiseTexture => Assets.Textures.Noise.Melt_02.Asset;

    public override float Duration => 12f * 60f;

    public override int MaxTiles => 600;

    public override int FadeInTicks => 4;

    public override int FadeOutTicks => 30;

    public override void OnApplied(ref ResidueInstance instance)
        => instance.Level = MathF.Max(instance.Level, instance.Intensity);

    public override ResidueElement ElementsOf(in ResidueInstance instance) => ResidueElement.Heat;

    /// <summary>Sets what touches it alight, rather than coating it.</summary>
    public override IResidue? BodyPickup(in ResidueInstance instance, out float amount)
    {
        amount = instance.Level * 0.12f;

        return instance.Level < BURNING_HEAT ? null : ResidueSystem.Get<BurnedResidue>();
    }

    public override float PotencyOf(in ResidueInstance instance) => instance.Level;

    /// <summary>Only the heat goes. The crust stays to wash away slowly, as it does under real water.</summary>
    public override void Diminish(ref ResidueInstance instance, float share) => instance.Level *= 1f - share;

    public override void UpdateTile(ref ResidueInstance instance)
    {
        instance.Level = MathF.Max(instance.Level - 1f / COOLING_TICKS, 0f);

        if (Main.dedServ || instance.Level <= 0f || !OnScreen(in instance))
            return;

        float heat = instance.Level * instance.Fade;

        Lighting.AddLight(instance.WorldCenter, 0.55f * heat, 0.22f * heat, 0.04f * heat);

        if (Main.rand.NextBool((int)MathHelper.Lerp(400f, 70f, heat)))
        {
            Dust ember = Dust.NewDustPerfect(
                instance.WorldPosition + new Vector2(Main.rand.NextFloat(16f), Main.rand.NextFloat(4f)),
                DustID.Torch,
                new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-1.6f, -0.5f)),
                0,
                default,
                Main.rand.NextFloat(0.8f, 1.3f)
            );

            ember.noGravity = true;
        }
    }

    public override TileCanvasSample EncodeCanvas(in ResidueInstance instance)
        => Encode(in instance, instance.Strength, Vector3.Lerp(CrustColor, Look.BodyColor, MathHelper.Clamp(instance.Level, 0f, 1f)));

    public override void OnPlayerContact(ref ResidueInstance instance, Player player)
    {
        if (instance.Level < BURNING_HEAT || player.fireWalk || player.lavaImmune || !FirstContact(player))
            return;

        player.AddBuff(BuffID.OnFire, (int)MathHelper.Lerp(60f, 180f, instance.Level));
    }

    public override void OnNPCContact(ref ResidueInstance instance, NPC npc)
    {
        if (instance.Level < BURNING_HEAT || npc.lavaImmune || npc.noTileCollide || !FirstContact(npc))
            return;

        ContactDebuff(npc, BuffID.OnFire, (int)MathHelper.Lerp(90f, 240f, instance.Level));
    }

    public override void OnLiquidContact(ref ResidueInstance instance, int liquidType, int amount)
    {
        if (liquidType == LiquidID.Lava)
        {
            instance.Life = instance.MaxLife;
            instance.Level = 1f;

            return;
        }

        // Anything else quenches it: the heat goes at once, the crust stays to wash away slowly.
        if (instance.Level > 0f && !Main.dedServ && OnScreen(in instance))
        {
            Dust steam = Dust.NewDustPerfect(
                instance.WorldCenter + Main.rand.NextVector2Circular(6f, 4f),
                DustID.Smoke,
                new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-1.5f, -0.7f)),
                120,
                Color.LightGray,
                Main.rand.NextFloat(0.9f, 1.4f)
            );

            steam.noGravity = true;
        }

        instance.Level = 0f;
        instance.Life -= 4f * (amount / 255f);
    }
}
