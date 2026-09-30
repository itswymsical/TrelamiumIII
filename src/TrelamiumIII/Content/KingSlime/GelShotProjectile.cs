using Terraria.Audio;
using Terraria.GameContent;
using TrelamiumIII.Common.ChemicalClass;
using TrelamiumIII.Core.Gas.Layers;
using TrelamiumIII.Core.Graphics.Fields;
using TrelamiumIII.Core.Tiles.Conditions.Types;

namespace TrelamiumIII.Content.KingSlime;

/// <summary>
///     A blob of gel in flight. Mostly a hitbox: what the player sees is the <see cref="GelField" /> trail it lays every tick.
///     Subclasses decide whom it hurts and whose gel it is.
/// </summary>
public abstract class GelShotProjectile : ModProjectile
{
    /// <summary>Downward acceleration, pixels per tick squared.</summary>
    public const float GRAVITY = 0.16f;

    /// <summary>Share of horizontal velocity kept each tick.</summary>
    public const float DRAG = 0.997f;

    /// <summary>Pixels of terrain coated when the blob bursts.</summary>
    private const float SPLATTER_RADIUS = 28f;

    /// <summary>Nodes thrown out by the burst.</summary>
    private const int BURST_NODES = 9;

    /// <summary>Owner stamped on the nodes and on the terrain coat.</summary>
    protected virtual int NodeOwner => Projectile.owner;

    /// <summary>Chemistry the trail and burst carry, or null.</summary>
    protected virtual ChemicalPayload? Payload => ChemicalHandler.PayloadOf(Projectile);

    /// <summary>Live nodes in the gel layer past which this shot stops adding more.</summary>
    protected virtual int NodeCeiling => int.MaxValue;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 5;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = Projectile.height = 12;
        Projectile.penetrate = 1;
        Projectile.timeLeft = 240;
        Projectile.tileCollide = true;
        Projectile.aiStyle = -1;
        Projectile.scale = 1f;
    }

    public override void AI()
    {
        Projectile.velocity.Y += GRAVITY;
        Projectile.velocity.X *= DRAG;

        Projectile.rotation += Projectile.velocity.X * 0.04f;

        Trail();
    }

    /// <summary>Lays gel from a point behind the blob, thinning as it travels.</summary>
    private void Trail()
    {
        if (FieldSystem.GetLayer<GelField>() is not { } layer || layer.NodeCount >= NodeCeiling)
            return;

        Vector2 behind = Projectile.Center - Projectile.velocity * 0.4f;
        Vector2 drift = Projectile.velocity * 0.12f + Main.rand.NextVector2Circular(0.6f, 0.6f);

        layer.SpawnNode(
            behind,
            drift,
            radius: Main.rand.NextFloat(6f, 9f),
            life: Main.rand.NextFloat(16f, 26f),
            owner: NodeOwner,
            payload: Payload
        );
    }

    /// <summary>Bursts against terrain, splashing along the collision normal so floors spread and walls run.</summary>
    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        var normal = new Vector2(
            Projectile.velocity.X != oldVelocity.X ? -MathF.Sign(oldVelocity.X) : 0f,
            Projectile.velocity.Y != oldVelocity.Y ? -MathF.Sign(oldVelocity.Y) : 0f
        );

        Burst(normal.SafeNormalize(-Vector2.UnitY), coatTerrain: true);

        Projectile.Kill();

        return false;
    }

    public override void OnKill(int timeLeft)
        => SoundEngine.PlaySound(SoundID.NPCHit1 with { Volume = 0.5f, Pitch = 0.5f, MaxInstances = 4 }, Projectile.Center);

    /// <summary>Splashes off whatever the shot struck, without coating terrain.</summary>
    protected void BurstOnHit() => Burst(-Projectile.velocity.SafeNormalize(-Vector2.UnitY), coatTerrain: false);

    /// <summary>Throws gel outward and, on a terrain hit, coats what is under it.</summary>
    private void Burst(Vector2 outward, bool coatTerrain)
    {
        if (coatTerrain)
            SlimedCondition.Splatter(Projectile.Center, SPLATTER_RADIUS, intensity: 0.9f, owner: NodeOwner);

        if (FieldSystem.GetLayer<GelField>() is not { } layer)
            return;

        ChemicalPayload? payload = Payload;

        float speed = payload?.Stats.BurstSpeedMultiplier ?? 1f;
        int count = (int)MathF.Round(BURST_NODES * (payload?.Stats.BurstCountMultiplier ?? 1f));

        count = Math.Min(count, NodeCeiling - layer.NodeCount);

        for (int i = 0; i < count; i++)
        {
            // Biased along the surface rather than straight out of it: a splash spreads across what
            // it landed on, and a cone centred on the normal would have it standing up off the wall.
            Vector2 direction = outward.RotatedByRandom(MathHelper.PiOver2 * 0.9f);

            layer.SpawnNode(
                Projectile.Center + direction * 4f,
                direction * Main.rand.NextFloat(1.5f, 4.5f) * speed,
                radius: Main.rand.NextFloat(7f, 12f),
                life: Main.rand.NextFloat(52f, 66f),
                owner: NodeOwner,
                payload: payload
            );
        }
    }

    /// <summary>
    ///     Launch velocity, rightward, that puts a shot back at its launch height <paramref name="distance" /> pixels away
    ///     when fired <paramref name="elevation" /> radians above horizontal.
    /// </summary>
    public static Vector2 LobVelocity(float distance, float elevation)
    {
        float sin = MathF.Sin(elevation);
        float cos = MathF.Cos(elevation);

        distance = MathF.Max(distance, 1f);

        // Drag-free range as the first guess, then corrected against the drag the shot actually flies with.
        float speed = MathF.Sqrt(GRAVITY * distance / MathF.Sin(2f * elevation));

        for (int i = 0; i < 6; i++)
        {
            float flight = 2f * speed * sin / GRAVITY;
            float reach = speed * cos * (1f - MathF.Pow(DRAG, flight)) / (1f - DRAG);

            // Reach grows with the square of speed, so correcting by the full miss overshoots and oscillates.
            speed *= MathF.Sqrt(distance / reach);
        }

        return new Vector2(cos, -sin) * speed;
    }

    public override bool PreDraw(ref Color lightColor)
    {
        Main.instance.LoadProjectile(Projectile.type);

        Texture2D texture = TextureAssets.Projectile[Projectile.type].Value;
        Vector2 origin = texture.Size() / 2f;

        for (int i = 0; i < Projectile.oldPos.Length; i++)
        {
            if (Projectile.oldPos[i] == Vector2.Zero)
                continue;

            float fade = (Projectile.oldPos.Length - i) / (float)Projectile.oldPos.Length;
            Vector2 position = Projectile.oldPos[i] + Projectile.Size / 2f - Main.screenPosition;

            Main.EntitySpriteDraw(
                texture, position, null, Projectile.GetAlpha(lightColor) * fade * 0.6f,
                Projectile.oldRot[i], origin, Projectile.scale * fade, SpriteEffects.None, 0
            );
        }

        Main.EntitySpriteDraw(
            texture, Projectile.Center - Main.screenPosition, null, Projectile.GetAlpha(lightColor),
            Projectile.rotation, origin, Projectile.scale, SpriteEffects.None, 0
        );

        return false;
    }
}
