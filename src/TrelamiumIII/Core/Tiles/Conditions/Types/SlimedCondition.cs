using System.Collections.Generic;
using ReLogic.Content;

namespace TrelamiumIII.Core.Tiles.Conditions.Types;

/// <summary>
///     A coat of slime over the terrain.
/// </summary>
public sealed class SlimedCondition : TileCondition
{
    private const float PLAYER_DRAG = 0.93f;

    private const float NPC_DRAG = 0.90f;

    private const float PROJECTILE_SCOUR = 2.5f;

    private static readonly TileFilmLook Look = new()
    {
        Threshold = 0.22f,
        NoiseScale = 0.06f,
        NoiseStrength = 0.14f,
        NoiseDrift = new Vector2(0.006f, 0.022f),
        Bands = 4f,
        ShadowColor = new Vector3(0.05f, 0.15f, 0.34f),
        BodyColor = new Vector3(0.17f, 0.42f, 0.80f),
        CoreColor = new Vector3(0.47f, 0.74f, 1.00f),
        BodyAlpha = 0.80f,
        OutlineColor = new Vector3(0.76f, 0.92f, 1.00f),
        OutlineAlpha = 0.90f,
        HighlightColor = new Vector3(0.88f, 0.97f, 1.00f),
        HighlightAlpha = 0.85f,
        EmissiveEdge = 0.25f,
        AmbientLight = new Vector3(0.25f),
        SurfaceLip = 2f,
    };

    public override float Duration => 9f * 60f;

    public override int MaxTiles => 900;

    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.Canvas;

    public override Asset<Effect>? Shader => Assets.Shaders.TileConditionMask.Asset;

    public override int FadeInTicks => 6;

    public override int FadeOutTicks => 14;

    private Asset<Texture2D>? noiseTexture;

    private readonly HashSet<Entity> draggedThisTick = [];

    public override void Load()
    {
        if (Main.dedServ)
            return;

        noiseTexture = Assets.Textures.Noise.LiquidNoise.Asset;
    }

    public override void Unload()
    {
        noiseTexture = null;
        draggedThisTick.Clear();
    }

    public override void UpdateGlobal() => draggedThisTick.Clear();

    public override void UpdateTile(ref TileConditionInstance instance)
    {
        if (Main.dedServ || !OnScreen(in instance))
            return;

        float strength = instance.Strength;

        Lighting.AddLight(instance.WorldCenter, 0.03f * strength, 0.07f * strength, 0.12f * strength);
    }

    public override TileCanvasSample EncodeCanvas(in TileConditionInstance instance)
    {
        // Per-tile phase, so the coat swells unevenly instead of pulsing in lockstep.
        float phase = instance.X * 0.7f + instance.Y * 1.3f;
        float time = (float)Main.timeForVisualEffects / 60f;

        return new TileCanvasSample(instance.Strength * (0.92f + 0.08f * MathF.Sin(time * 1.4f + phase)));
    }

    public override void ApplyShaderParameters(Effect effect)
    {
        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        Look.Apply(effect);
    }

    public override void OnPlayerContact(ref TileConditionInstance instance, Player player)
    {
        if (instance.IsOwnedBy(player) || !draggedThisTick.Add(player))
            return;

        player.velocity.X *= MathHelper.Lerp(1f, PLAYER_DRAG, instance.Strength);

        if (!Main.dedServ && Main.rand.NextBool(14))
            SlimeDust(player.Bottom, -Vector2.UnitY);
    }

    public override void OnNPCContact(ref TileConditionInstance instance, NPC npc)
    {
        if (npc.noTileCollide || !draggedThisTick.Add(npc))
            return;

        npc.velocity.X *= MathHelper.Lerp(1f, NPC_DRAG, instance.Strength);
    }

    public override void OnProjectileContact(ref TileConditionInstance instance, Projectile projectile)
    {
        if (projectile.damage <= 0)
            return;

        instance.Life -= PROJECTILE_SCOUR;

        if (!Main.dedServ && Main.rand.NextBool(3))
            SlimeDust(instance.WorldCenter, -projectile.velocity);
    }

    public override void OnLiquidContact(ref TileConditionInstance instance, int liquidType, int amount)
    {
        float share = amount / 255f;

        if (liquidType == LiquidID.Lava)
        {
            instance.Life = 0f;
            TileConditionSystem.Apply<BurnedCondition>(instance.X, instance.Y, 0.9f, sync: false);
            //Projectile.NewProjectile(new EntitySource_Misc("Chemical Reaction"),
            //instance.WorldPosition, Vector2.Zero, ProjectileID.InfernoHostileBlast, 10, 4);
            return;
        }

        instance.Life -= 6f * share;

    }

    private static void SlimeDust(Vector2 position, Vector2 direction)
    {
        Dust dust = Dust.NewDustPerfect(
            position + Main.rand.NextVector2Circular(6f, 6f),
            DustID.t_Slime,
            direction.SafeNormalize(-Vector2.UnitY) * Main.rand.NextFloat(0.4f, 1.2f),
            150,
            new Color(86, 162, 255, 100),
            Main.rand.NextFloat(0.6f, 1.1f)
        );

        dust.noGravity = true;
    }

    /// <summary>
    ///     Splatters slime over everything solid within <paramref name="radius" /> pixels, thinning
    ///     toward the edge of the splash.
    /// </summary>
    public static void Splatter(Vector2 center, float radius, float intensity = 1f, int owner = TileConditionInstance.NoOwner)
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

                TileConditionSystem.Apply<SlimedCondition>(x, y, intensity * MathHelper.Lerp(0.45f, 1f, falloff), owner: owner, sync: false);
            }
        }
    }
}
