using System.Collections.Generic;
using ReLogic.Content;
using Terraria.DataStructures;
using TrelamiumIII.Core.Graphics;

namespace TrelamiumIII.Core.Tiles.Conditions.Types;

/// <summary>
///     A coat of slime, rendered as a metaball field so a splattered wall reads as one sheet of goo
///     rather than a grid of stickers.
/// </summary>
/// <remarks>
///     Each tile stamps a single soft blob wider than the tile it sits on. Summed additively and cut
///     at a low threshold, neighbouring blobs cross that threshold in the gap between them and fuse;
///     an isolated tile stays a lone droplet. That is the whole reason this is a field rather than an
///     overlay, and it is what a per-tile texture could never do -- the sheet bulges over the seams
///     and sags past the edges of the marked area.
/// </remarks>
public sealed class SlimedCondition : TileCondition
{
    /// <summary>Wider than a tile, so a blob reaches its neighbours and the two merge.</summary>
    private const float BLOB_DIAMETER = 36f;

    private const float PLAYER_DRAG = 0.93f;

    private const float NPC_DRAG = 0.90f;

    /// <summary>Life a damaging projectile scrubs off the tile it passes through.</summary>
    private const float PROJECTILE_SCOUR = 2.5f;

    public override float Duration => 9f * 60f;

    public override int MaxTiles => 900;

    public override TileConditionRenderMode RenderMode => TileConditionRenderMode.Field;

    /// <summary>Additive: blobs have to sum for the threshold to fuse them.</summary>
    public override BlendState FieldBlend => BlendState.Additive;

    public override Asset<Effect>? Shader => Assets.Shaders.GasFbm.Asset;

    private Asset<Texture2D>? blobTexture;

    private Asset<Texture2D>? noiseTexture;

    /// <summary>
    ///     Entities already dragged this tick.
    /// </summary>
    /// <remarks>
    ///     Contact fires once per marked tile touched, and a player standing across a slimed floor
    ///     touches two or three of them. Multiplying velocity once per tile would make the drag scale
    ///     with how the tiles happen to line up under the hitbox, which is both invisible to the
    ///     player and wrong -- wading through slime should cost the same wherever they stand.
    /// </remarks>
    private readonly HashSet<Entity> draggedThisTick = [];

    public override void Load()
    {
        if (Main.dedServ)
            return;

        blobTexture = Assets.Textures.VFX.RadialGradient.Asset;
        noiseTexture = Assets.Textures.Noise.LiquidNoise.Asset;
    }

    public override void Unload()
    {
        blobTexture = null;
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

    public override void DrawTile(SpriteBatch spriteBatch, in TileConditionInstance instance, Vector2 drawPosition)
    {
        if (blobTexture is not { Value: { } texture })
            return;

        // Per-tile phase, so the sheet undulates instead of pulsing in lockstep.
        float phase = instance.X * 0.7f + instance.Y * 1.3f;
        float time = (float)Main.timeForVisualEffects / 60f;
        var wobble = new Vector2(MathF.Sin(time * 1.4f + phase) * 1.6f, MathF.Cos(time * 1.1f + phase) * 1.2f);

        // Coverage in red, coverage-weighted brightness in green -- the same field encoding the gas
        // layers stamp, because this draws through the same shader. Slime coating a lit wall and
        // slime down a dark shaft should not be the same colour.
        //
        // Overdriven past white on purpose: the shader thresholds the summed field, and a blob that
        // only just reaches 1.0 at its centre never fuses with its neighbours.
        Color color = FieldStamp.Lit(instance.WorldCenter, (0.55f + 0.45f * instance.Strength) * 1.25f);

        spriteBatch.Draw(
            texture,
            drawPosition + new Vector2(8f) + wobble,
            null,
            color,
            0f,
            texture.Size() / 2f,
            BLOB_DIAMETER / texture.Width,
            SpriteEffects.None,
            0f
        );
    }

    public override void ApplyShaderParameters(Effect effect)
    {
        ApplyFieldFrameParameters(effect);

        if (noiseTexture is { Value: { } noise })
            effect.Parameters["uNoiseTex"]?.SetValue(noise);

        effect.Parameters["WorldLight"]?.SetValue(new Vector3(0.25f));

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

    /// <summary>Anything with damage on it blasts slime off the tile it hits.</summary>
    public override void OnProjectileContact(ref TileConditionInstance instance, Projectile projectile)
    {
        if (projectile.damage <= 0)
            return;

        instance.Life -= PROJECTILE_SCOUR;

        if (!Main.dedServ && Main.rand.NextBool(3))
            SlimeDust(instance.WorldCenter, -projectile.velocity);
    }

    /// <summary>Water rinses it off and leaves the tile wet; lava cooks it into a scorch.</summary>
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

        // Probed one tick in eight, so this is really about 5 life a second at full submersion.
        instance.Life -= 6f * share;

        if (instance.Life <= 0f)
            TileConditionSystem.Apply<SoakedCondition>(instance.X, instance.Y, share, sync: false);
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
    /// <remarks>
    ///     Applied without a packet: every client runs the same collision that led here, so each one
    ///     works the splash out for itself. Syncing it instead would put one message on the wire per
    ///     tile of every splash, to say something the receiver already knows.
    /// </remarks>
    /// <param name="owner">
    ///     The player who put it there, so the sheet knows whose boots to let through.
    /// </param>
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
