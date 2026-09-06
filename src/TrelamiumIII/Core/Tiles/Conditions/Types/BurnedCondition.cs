using ReLogic.Content;

namespace TrelamiumIII.Core.Tiles.Conditions.Types;

/// <summary>
///     Charred terrain, rendered as a coverage mask rather than an overlay texture.
/// </summary>
/// <remarks>
///     <para>
///         The mask is the point. Every burnt tile stamps its own silhouette into a field, and the
///         shader erodes the whole field at once with noise locked to the world. A run of burnt
///         blocks therefore chars as one patch, with holes and ember edges that fall wherever the
///         noise puts them, instead of eight copies of a 16x16 texture with a visible seam every
///         tile. Slopes and half blocks come out right for free, because the stamp is the tile's own
///         sprite.
///     </para>
///     <para>
///         Decay comes out of the same step at no cost. Intensity scales the coverage that goes into
///         the field, so a fading burn slides below the shader's threshold in the thinnest places
///         first and recedes into scattered specks, rather than uniformly dimming.
///     </para>
/// </remarks>
public sealed class BurnedCondition : TileCondition
{
    private const float QUENCH_PER_PROBE = 16f;

    public override float Duration => 13f * 60f;

    public override int MaxTiles => 1200;

    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.Field;

    /// <summary>
    ///     Premultiplied alpha, which is the one blend that leaves the field's alpha channel linear
    ///     in the stamp's own alpha. The shader reads coverage from there, so the reading has to mean
    ///     what the stamp meant; additive would square it and no two tiles overlap anyway.
    /// </summary>
    public override BlendState FieldBlend => BlendState.AlphaBlend;

    public override Asset<Effect>? Shader => Assets.Shaders.TileConditionMask.Asset;

    private Asset<Texture2D>? noiseTexture;

    public override void Load()
    {
        if (Main.dedServ)
            return;

        noiseTexture = Assets.Textures.Noise.Turbulence_05.Asset;
    }

    public override void Unload() => noiseTexture = null;

    /// <summary>Fire cannot take on a tile that is still wet; it spends itself boiling the water off.</summary>
    public override bool CanApplyTo(int i, int j)
    {
        if (!base.CanApplyTo(i, j))
            return false;

        if (!TileConditionSystem.Has<SoakedCondition>(i, j))
            return true;

        TileConditionSystem.Remove<SoakedCondition>(i, j);

        if (!Main.dedServ)
            Steam(new Vector2(i * 16f + 8f, j * 16f + 8f), 2);

        return false;
    }

    public override void UpdateTile(ref TileConditionInstance instance)
    {
        if (Main.dedServ || !OnScreen(in instance))
            return;

        float strength = instance.Strength;

        Lighting.AddLight(instance.WorldCenter, 0.30f * strength, 0.11f * strength, 0.02f * strength);

        if (!Main.rand.NextBool((int)MathHelper.Lerp(340f, 90f, strength)))
            return;

        Dust ember = Dust.NewDustPerfect(
            instance.WorldPosition + new Vector2(Main.rand.NextFloat(16f), Main.rand.NextFloat(16f)),
            DustID.Torch,
            new Vector2(Main.rand.NextFloat(-0.25f, 0.25f), Main.rand.NextFloat(-1.1f, -0.4f)),
            0,
            default,
            Main.rand.NextFloat(0.7f, 1.3f) * strength
        );

        ember.noGravity = true;
    }

    public override void DrawTile(SpriteBatch spriteBatch, in TileConditionInstance instance, Vector2 drawPosition)
        => DrawSilhouette(in instance, Color.White * instance.Strength);

    public override void ApplyShaderParameters(Effect effect)
    {
        ApplyFieldFrameParameters(effect);

        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        // Floored low, unlike the gas layers: soot in an unlit cave should stay unlit. The embers
        // do not read this at all, so the mark never disappears entirely.
        effect.Parameters["WorldLight"]?.SetValue(Vector3.Max(AverageScreenLight(), new Vector3(0.12f)));

        // Threshold and strength are tuned as a pair. Erosion of roughly twice the threshold means
        // a fully covered tile still dips under the cut wherever the noise troughs, which is what
        // punches holes through the middle of a burn instead of only nibbling its outline.
        effect.Parameters["Threshold"]?.SetValue(0.55f);
        effect.Parameters["NoiseStrength"]?.SetValue(0.95f);
        effect.Parameters["NoiseScale"]?.SetValue(6f);

        // Slow, and mostly upward: char does not move, but the heat coming off it should.
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0.008f, -0.02f));

        effect.Parameters["EdgeWidth"]?.SetValue(0.22f);

        effect.Parameters["BodyColor"]?.SetValue(new Vector3(0.06f, 0.05f, 0.05f));
        effect.Parameters["EdgeColor"]?.SetValue(new Vector3(1.00f, 0.42f, 0.10f));
        effect.Parameters["BodyAlpha"]?.SetValue(0.84f);
        effect.Parameters["EdgeAlpha"]?.SetValue(0.95f);
    }

    public override void OnPlayerContact(ref TileConditionInstance instance, Player player)
    {
        if (instance.Strength < 0.3f)
            return;

        player.AddBuff(BuffID.OnFire, 90);
    }

    public override void OnNPCContact(ref TileConditionInstance instance, NPC npc)
    {
        if (npc.friendly || instance.Strength < 0.3f)
            return;

        npc.AddBuff(BuffID.OnFire, 120);
    }

    /// <summary>Water puts it out and leaves the tile soaked; lava keeps it going indefinitely.</summary>
    public override void OnLiquidContact(ref TileConditionInstance instance, int liquidType, int amount)
    {
        if (liquidType == LiquidID.Lava)
        {
            instance.Life = instance.MaxLife;
            return;
        }

        float share = amount / 255f;

        instance.Life -= QUENCH_PER_PROBE * share;

        if (!Main.dedServ)
            Steam(instance.WorldCenter, 1);

        if (instance.Life <= 0f)
            TileConditionSystem.Apply<SoakedCondition>(instance.X, instance.Y, share, sync: false);
    }

    private static void Steam(Vector2 position, int count)
    {
        for (int i = 0; i < count; i++)
        {
            Dust steam = Dust.NewDustPerfect(
                position + Main.rand.NextVector2Circular(7f, 7f),
                DustID.Cloud,
                new Vector2(Main.rand.NextFloat(-0.3f, 0.3f), Main.rand.NextFloat(-1.4f, -0.6f)),
                120,
                Color.White,
                Main.rand.NextFloat(0.7f, 1.2f)
            );

            steam.noGravity = true;
        }
    }

    /// <summary>
    ///     Chars everything solid within <paramref name="radius" /> pixels, thinning toward the edge.
    /// </summary>
    /// <inheritdoc cref="SlimedCondition.Splatter" path="/remarks" />
    public static void Scorch(Vector2 center, float radius, float intensity = 1f)
    {
        int reach = (int)MathF.Ceiling(radius / 16f);
        int centerX = (int)(center.X / 16f);
        int centerY = (int)(center.Y / 16f);

        for (int x = centerX - reach; x <= centerX + reach; x++)
        {
            for (int y = centerY - reach; y <= centerY + reach; y++)
            {
                float distance = Vector2.Distance(new Vector2(x * 16f + 8f, y * 16f + 8f), center);

                if (distance > radius)
                    continue;

                float falloff = 1f - distance / MathF.Max(radius, 1f);

                TileConditionSystem.Apply<BurnedCondition>(x, y, intensity * MathHelper.Lerp(0.4f, 1f, falloff), sync: false);
            }
        }
    }
}
