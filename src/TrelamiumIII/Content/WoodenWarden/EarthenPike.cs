using Terraria.Audio;
using Terraria.GameContent;
using TrelamiumIII.Core;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.UI;
using static TrelamiumIII.Utilities.EaseFunction;

namespace TrelamiumIII.Content.WoodenWarden;

/// <summary>
///     The spear a <see cref="WardenArm" /> materialises and throws. Flies straight, embeds in terrain, then fades out
///     through its white mask. <see cref="DrawHeld" /> draws it in the hand before the throw; there is no aim telegraph.
/// </summary>
public sealed class EarthenPike : ModProjectile
{
    /// <summary>Tip at (0, 0), butt at (77, 91): the direction the unrotated sprite points.</summary>
    public static readonly float SpriteAngle = MathF.Atan2(-91f, -77f);

    /// <summary>Shaft knot the hand closes on, in sprite pixels.</summary>
    public static readonly Vector2 Grip = new(55f, 66f);

    /// <summary>Middle of the head, where the hitbox is centred, in sprite pixels.</summary>
    public static readonly Vector2 Head = new(20f, 24f);

    public const float SPEED = 21f;

    private const int EMBED_TICKS = 40;
    private const int FADE_TICKS = 18;

    /// <summary>How far past the contact point the head sinks, in pixels.</summary>
    private const float EMBED_DEPTH = 10f;

    private const int GLOW_PADDING = 6;
    private const int GLOW_BLUR = 3;

    /// <summary>Form progress at which the white silhouette peaks, before it fades back to the texture.</summary>
    private const float WHITE_RISE = 0.2f;

    private static readonly SoundStyle EmbedSound = SoundID.Dig with { Volume = 1.1f, Pitch = -0.4f };

    private static Texture2D? mask;
    private static Texture2D? glowMask;

    public override string Texture => AssetReferences.Content.WoodenWarden.EarthenPikeProj.KEY;

    /// <summary>0 while flying; counts up from 1 once embedded.</summary>
    private ref float EmbedTimer => ref Projectile.ai[0];

    private bool Embedded => EmbedTimer > 0f;

    public override void SetStaticDefaults()
    {
        ProjectileID.Sets.TrailCacheLength[Type] = 6;
        ProjectileID.Sets.TrailingMode[Type] = 2;
    }

    public override void SetDefaults()
    {
        Projectile.width = 22;
        Projectile.height = 22;

        Projectile.aiStyle = -1;

        Projectile.hostile = true;
        Projectile.friendly = false;

        Projectile.tileCollide = true;
        Projectile.ignoreWater = true;

        Projectile.penetrate = -1;
        Projectile.timeLeft = 360;
    }

    /// <summary>Server-side. Spawns the pike where the held one's head was, so the hand-off is seamless.</summary>
    public static void Throw(NPC source, Vector2 grip, float aim, int damage)
    {
        if (Main.netMode == NetmodeID.MultiplayerClient)
            return;

        Vector2 head = grip + (Head - Grip).RotatedBy(aim - SpriteAngle);

        Projectile.NewProjectile(
            source.GetSource_FromAI(),
            head,
            aim.ToRotationVector2() * SPEED,
            ModContent.ProjectileType<EarthenPike>(),
            damage,
            5f,
            Main.myPlayer
        );
    }

    public override void AI()
    {
        if (Embedded)
        {
            Projectile.velocity = Vector2.Zero;

            if (++EmbedTimer >= EMBED_TICKS + FADE_TICKS)
                Projectile.Kill();

            return;
        }

        Projectile.rotation = Projectile.velocity.ToRotation() - SpriteAngle;

        if (Main.dedServ)
            return;

        Lighting.AddLight(Projectile.Center, TileClusterShards.EdgeColor.ToVector3() * 0.3f);

        if (Main.rand.NextBool(2))
        {
            Dust dust = Dust.NewDustPerfect(Projectile.Center, DustID.GreenTorch, -Projectile.velocity * 0.1f, Scale: 1.1f);

            dust.noGravity = true;
        }
    }

    public override bool OnTileCollide(Vector2 oldVelocity)
    {
        if (Embedded)
            return false;

        EmbedTimer = 1f;

        Projectile.rotation = oldVelocity.ToRotation() - SpriteAngle;
        Projectile.position += oldVelocity.SafeNormalize(Vector2.Zero) * EMBED_DEPTH;
        Projectile.velocity = Vector2.Zero;
        Projectile.tileCollide = false;

        if (Main.dedServ)
            return false;

        SoundEngine.PlaySound(EmbedSound, Projectile.Center);

        if (Main.LocalPlayer.DistanceSQ(Projectile.Center) < 900f * 900f)
            CameraSystem.shake = Math.Max(CameraSystem.shake, 5);

        for (int i = 0; i < 10; i++)
            Dust.NewDust(Projectile.position, Projectile.width, Projectile.height, DustID.Dirt, -oldVelocity.X * 0.15f, -3f, Scale: 1.2f);

        return false;
    }

    public override bool CanHitPlayer(Player target) => !Embedded;

    public override bool PreDraw(ref Color lightColor)
    {
        Texture2D texture = TextureAssets.Projectile[Type].Value;
        SpriteBatch spriteBatch = Main.spriteBatch;

        Color light = TileCluster.LightAt(Projectile.Center);

        if (!Embedded)
            DrawTrail(spriteBatch, texture, light);

        float fade = Embedded ? Math.Clamp((EmbedTimer - EMBED_TICKS) / FADE_TICKS, 0f, 1f) : 0f;
        Vector2 position = Projectile.Center - Main.screenPosition;

        spriteBatch.Draw(texture, position, null, light * (1f - fade), Projectile.rotation, Head, 1f, SpriteEffects.None, 0f);

        // Leaves the way it arrived: flashes to its silhouette, and the silhouette fades with it.
        if (fade > 0f)
        {
            float white = (1f - fade) * Math.Min(fade * 3f, 1f);

            spriteBatch.Draw(Masks().Mask, position, null, Color.White * white, Projectile.rotation, Head, 1f, SpriteEffects.None, 0f);
        }

        return false;
    }

    private void DrawTrail(SpriteBatch spriteBatch, Texture2D texture, Color light)
    {
        var anchor = new Vector2(Projectile.width, Projectile.height) * 0.5f;

        for (int i = Projectile.oldPos.Length - 1; i >= 1; i--)
        {
            if (Projectile.oldPos[i] == Vector2.Zero)
                continue;

            float fade = 1f - i / (float)Projectile.oldPos.Length;

            spriteBatch.Draw(
                texture,
                Projectile.oldPos[i] + anchor - Main.screenPosition,
                null,
                light * (fade * fade * 0.35f),
                Projectile.rotation,
                Head,
                1f,
                SpriteEffects.None,
                0f
            );
        }
    }

    /// <summary>
    ///     Draws the pike held at <paramref name="grip" />, pointing along <paramref name="aim" />. <paramref name="form" />
    ///     0..1 materialises it from a white silhouette, <paramref name="charge" /> 0..1 adds a pulsing glow, and
    ///     <paramref name="flash" /> 0..1 lays the silhouette back over it.
    /// </summary>
    public static void DrawHeld(SpriteBatch spriteBatch, Vector2 grip, float aim, float form, float charge, float flash, Color light)
    {
        if (form <= 0f)
            return;

        Texture2D texture = TextureAssets.Projectile[ModContent.ProjectileType<EarthenPike>()].Value;
        (Texture2D white, Texture2D glow) = Masks();

        float rotation = aim - SpriteAngle;
        float scale = MathHelper.Lerp(0.7f, 1f, EaseCubicOut.Ease(form));
        Vector2 position = grip - Main.screenPosition;

        float pulse = 0.55f + 0.45f * MathF.Sin(Main.GlobalTimeWrappedHourly * 18f);
        float glowStrength = charge * pulse + form * (1f - form) * 1.6f;

        if (glowStrength > 0f)
            spriteBatch.Draw(glow, position, null, Additive(glowStrength), rotation, Grip + new Vector2(GLOW_PADDING), scale, SpriteEffects.None, 0f);

        float body = MathHelper.SmoothStep(0f, 1f, Math.Clamp((form - 0.3f) / 0.6f, 0f, 1f));

        spriteBatch.Draw(texture, position, null, light * body, rotation, Grip, scale, SpriteEffects.None, 0f);

        float silhouette = Math.Max(WhiteAt(form), flash);

        if (silhouette > 0f)
            spriteBatch.Draw(white, position, null, Color.White * silhouette, rotation, Grip, scale, SpriteEffects.None, 0f);
    }

    /// <summary>Rises to full white by <see cref="WHITE_RISE" />, then fades back to the texture by the end of the form.</summary>
    private static float WhiteAt(float form)
    {
        if (form < WHITE_RISE)
            return form / WHITE_RISE;

        return EaseQuadIn.Ease(Math.Clamp(1f - (form - WHITE_RISE) / (0.95f - WHITE_RISE), 0f, 1f));
    }

    private static Color Additive(float opacity) => (TileClusterShards.EdgeColor * opacity) with { A = 0 };

    /// <summary>White silhouette and padded blurred glow of the pike, built on first use. Main thread only.</summary>
    private static (Texture2D Mask, Texture2D Glow) Masks()
    {
        if (mask is null || glowMask is null)
        {
            Texture2D texture = TextureAssets.Projectile[ModContent.ProjectileType<EarthenPike>()].Value;

            mask = SpriteMask.Build(texture);
            glowMask = SpriteMask.Build(texture, GLOW_PADDING, GLOW_BLUR);
        }

        return (mask, glowMask);
    }

    public override void Unload()
    {
        Texture2D? oldMask = mask;
        Texture2D? oldGlow = glowMask;

        mask = glowMask = null;

        if (oldMask is null && oldGlow is null)
            return;

        Main.QueueMainThreadAction(() =>
        {
            oldMask?.Dispose();
            oldGlow?.Dispose();
        });
    }
}
