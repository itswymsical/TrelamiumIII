using ReLogic.Content;

namespace TrelamiumIII.Core.Tiles.Conditions.Types;

/// <summary>
///     Terrain that water has been on: darkened, glistening, and hostile to fire.
/// </summary>
/// <remarks>
///     <para>
///         This one is an overlay rather than a field, because a wet surface is genuinely a per-tile
///         thing. There is no silhouette to fuse and no region to erode -- the mark is exactly the
///         tile, and the interesting variation is within it, which is where a shader on the sprite
///         batch does more than a screen-space pass would. It is the cheap end of the three modes and
///         the right one here.
///     </para>
///     <para>
///         It also seeds itself, which is the other thing this demonstrates: a condition does not
///         have to wait to be applied by content. Anyone who has been in water tracks it for a while
///         afterwards.
///     </para>
/// </remarks>
public sealed class SoakedCondition : TileCondition
{
    private static readonly Color WetTint = new(14, 26, 48);

    /// <summary>Ticks a player keeps dripping after climbing out.</summary>
    private const int DRIP_TICKS = 300;

    private const int DRIP_INTERVAL = 5;

    public override float Duration => 20f * 60f;

    public override int MaxTiles => 1500;

    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.Overlay;

    public override Asset<Effect>? Shader => Assets.Shaders.TileWetSheen.Asset;

    private Asset<Texture2D>? noiseTexture;

    /// <summary>How much longer each player is still wet enough to leave a trail.</summary>
    /// <remarks>
    ///     Terraria has a flag for being in water and none for having just got out, which is the
    ///     state that actually leaves marks on the floor.
    /// </remarks>
    private readonly int[] dripTicks = new int[Main.maxPlayers];

    public override void Load()
    {
        if (Main.dedServ)
            return;

        noiseTexture = Assets.Textures.Noise.Milky_3.Asset;
    }

    public override void Unload() => noiseTexture = null;

    /// <summary>Water lands on anything solid, wet or burnt, and the burn loses.</summary>
    public override void OnApplied(ref TileConditionInstance instance)
        => TileConditionSystem.Remove<BurnedCondition>(instance.X, instance.Y);

    /// <summary>
    ///     Tracks who is dripping and lays a trail under them.
    /// </summary>
    /// <remarks>
    ///     Applied without a packet. Every client already simulates every player, wet flag included,
    ///     so each one works out the same trail from state it has -- and syncing would mean a message
    ///     every few ticks per swimmer for the whole time they drip.
    /// </remarks>
    public override void UpdateGlobal()
    {
        for (int i = 0; i < Main.maxPlayers; i++)
        {
            Player player = Main.player[i];

            if (!player.active || player.dead)
            {
                dripTicks[i] = 0;
                continue;
            }

            if (player.wet)
                dripTicks[i] = DRIP_TICKS;
            else if (dripTicks[i] > 0)
                dripTicks[i]--;

            if (dripTicks[i] <= 0 || player.velocity.Y != 0f || Main.GameUpdateCount % DRIP_INTERVAL != 0)
                continue;

            int x = (int)(player.Center.X / 16f);
            int y = (int)((player.position.Y + player.height + 4f) / 16f);

            // Thinner the longer they have been out of the water, so a trail dries up as it goes.
            TileConditionSystem.Apply<SoakedCondition>(x, y, 0.35f + 0.55f * (dripTicks[i] / (float)DRIP_TICKS), sync: false);
        }
    }

    public override void UpdateTile(ref TileConditionInstance instance)
    {
        if (Main.dedServ || !OnScreen(in instance) || !Main.rand.NextBool(900))
            return;

        // Only from a tile with air under it -- a drip needs somewhere to fall.
        if (Framing.GetTileSafely(instance.X, instance.Y + 1).HasTile)
            return;

        Dust.NewDustPerfect(
            instance.WorldPosition + new Vector2(Main.rand.NextFloat(16f), 16f),
            DustID.Water,
            new Vector2(0f, 0.6f),
            100,
            default,
            Main.rand.NextFloat(0.5f, 0.9f)
        );
    }

    public override void DrawTile(SpriteBatch spriteBatch, in TileConditionInstance instance, Vector2 drawPosition)
        => DrawSilhouette(in instance, WetTint * (0.62f * instance.Strength));

    public override void ApplyShaderParameters(Effect effect)
    {
        effect.Parameters["Time"]?.SetValue((float)Main.timeForVisualEffects / 60f);

        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        // In texture space, so the sample runs over the tile sheet rather than the world. Adjacent
        // tiles on different frames therefore catch the light differently, which is the point.
        effect.Parameters["NoiseScale"]?.SetValue(4.5f);
        effect.Parameters["NoiseDrift"]?.SetValue(new Vector2(0.014f, 0.024f));

        effect.Parameters["SheenColor"]?.SetValue(new Vector3(0.58f, 0.76f, 1.00f));
        effect.Parameters["SheenStrength"]?.SetValue(0.85f);

        // Sharp: a few tight glints read as a wet surface, a broad wash reads as blue paint.
        effect.Parameters["SheenSharpness"]?.SetValue(7f);
    }

    /// <summary>Standing on wet ground puts a player out.</summary>
    public override void OnPlayerContact(ref TileConditionInstance instance, Player player)
    {
        if (instance.Strength < 0.25f)
            return;

        player.ClearBuff(BuffID.OnFire);
        player.ClearBuff(BuffID.OnFire3);
    }

    /// <inheritdoc cref="OnPlayerContact" />
    public override void OnNPCContact(ref TileConditionInstance instance, NPC npc)
    {
        if (instance.Strength < 0.25f)
            return;

        for (int i = 0; i < NPC.maxBuffs; i++)
        {
            if (npc.buffType[i] is BuffID.OnFire or BuffID.OnFire3)
                npc.DelBuff(i);
        }
    }

    /// <summary>Standing water keeps it topped up; lava boils it off outright.</summary>
    public override void OnLiquidContact(ref TileConditionInstance instance, int liquidType, int amount)
    {
        if (liquidType == LiquidID.Lava)
        {
            instance.Life = 0f;
            return;
        }

        instance.Life = instance.MaxLife;
    }

    /// <summary>Soaks everything solid within <paramref name="radius" /> pixels.</summary>
    /// <inheritdoc cref="SlimedCondition.Splatter" path="/remarks" />
    public static void Douse(Vector2 center, float radius, float intensity = 1f)
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

                TileConditionSystem.Apply<SoakedCondition>(x, y, intensity * MathHelper.Lerp(0.5f, 1f, falloff), sync: false);
            }
        }
    }
}
