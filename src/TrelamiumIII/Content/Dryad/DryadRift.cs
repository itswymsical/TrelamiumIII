using ReLogic.Content;
using Terraria.DataStructures;
using TrelamiumIII.Core.Graphics.Particles;

namespace TrelamiumIII.Content.Dryad;

/// <summary>
///     The slit of light the Dryad steps out of. Client-side; <see cref="DryadArrivalSequence" /> pushes its
///     state every tick and the slit sprite and shader are the ones the Scar of Ingress uses.
/// </summary>
internal sealed class DryadRift : ModSystem
{
    private const string SlitPath = $"{nameof(TrelamiumIII)}/Content/Ingress/WardenAltarTile_Slit";

    /// <summary>Sprite scale. The sprite is 96x200, so 0.9 stands the slit about eleven tiles tall.</summary>
    private const float SCALE = 0.9f;

    private const float SPRITE_HEIGHT = 200f;

    /// <summary>Slit width at full opening as a multiple of the sprite's, and the extra fraction of it a surge adds.</summary>
    private const float WIDTH_FULL = 1.5f;

    private const float SURGE_WIDENING = 0.7f;

    /// <summary>Lean from vertical baked into the sprite. Stretching it sideways leans it further, so the draw counter-rotates.</summary>
    private const float TILT = 20f * MathHelper.Pi / 180f;

    private static readonly Color SapColor = new(176, 255, 198);

    private static readonly Color PaleColor = new(226, 255, 232);

    private static Asset<Texture2D>? slit;

    private static Vector2 floor;
    private static float open;
    private static float surge;
    private static float emitCarry;

    private static float HalfHeight => SPRITE_HEIGHT * 0.5f * SCALE;

    /// <summary>Centre of the slit, standing on <see cref="floor" />.</summary>
    public static Vector2 Center => CenterAt(floor);

    /// <summary>Centre of a slit standing on <paramref name="floorPoint" />, in world pixels.</summary>
    public static Vector2 CenterAt(Vector2 floorPoint) => floorPoint - new Vector2(0f, HalfHeight + 4f);

    /// <summary>
    ///     Called once per tick. <paramref name="openAmount" /> is 0 (shut) to 1 (fully open);
    ///     <paramref name="surgeAmount" /> is a momentary flare that also widens the slit.
    /// </summary>
    public static void Set(Vector2 floorPoint, float openAmount, float surgeAmount)
    {
        floor = floorPoint;
        open = MathHelper.Clamp(openAmount, 0f, 1f);
        surge = MathF.Max(surgeAmount, 0f);

        if (Main.dedServ || open <= 0f)
            return;

        Illuminate();
        EmitMotes();
    }

    public static void Clear()
    {
        open = 0f;
        surge = 0f;
        emitCarry = 0f;
    }

    /// <summary>Throws motes outward from the slit.</summary>
    public static void Burst(int count)
    {
        if (Main.dedServ)
            return;

        for (int i = 0; i < count; i++)
        {
            Vector2 direction = Main.rand.NextVector2Unit();
            Vector2 at = Center + direction * Main.rand.NextFloat(0f, HalfHeight * 0.5f);

            ParticleSystem.Spawn<RiftMoteParticle>(
                at,
                direction * Main.rand.NextFloat(1.2f, 3.4f) + new Vector2(0f, -0.4f),
                timeLeft: Main.rand.Next(45, 90),
                scale: Main.rand.NextFloat(0.14f, 0.32f),
                rotation: Main.rand.NextFloat(MathHelper.TwoPi),
                rotationVelocity: Main.rand.NextFloat(-0.04f, 0.04f)
            );
        }
    }

    public override void Load()
    {
        if (!Main.dedServ)
            slit = ModContent.Request<Texture2D>(SlitPath);
    }

    public override void Unload() => slit = null;

    public override void OnWorldUnload() => Clear();

    public override void PostDrawTiles()
    {
        if (Main.dedServ || open <= 0f || slit is not { IsLoaded: true })
            return;

        SpriteBatch batch = Main.spriteBatch;
        Texture2D texture = slit.Value;
        Vector2 center = Center - Main.screenPosition;

        // Grows along its axis first, then widens. Starts long enough to read as a seam.
        float length = MathHelper.Lerp(0.22f, 1f, Smooth(MathF.Min(open * 1.8f, 1f)));
        float width = MathF.Pow(open, 3f) * WIDTH_FULL * (1f + surge * SURGE_WIDENING);
        float pulse = 0.88f + 0.12f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.6f);
        float strength = open * pulse + surge * 1.4f;

        Vector2 scale = new Vector2(MathF.Max(width, 0.08f), length) * SCALE;
        float rotation = TILT - MathF.Atan(scale.X / scale.Y * MathF.Tan(TILT));

        DrawLight(batch, center, length, MathF.Min(width, 1f), strength);
        DrawBody(batch, texture, center, scale, rotation, strength);
    }

    /// <summary>A wide soft halo, the light column down to the ground, the bright core inside it, and the pool it lands in.</summary>
    private static void DrawLight(SpriteBatch batch, Vector2 center, float length, float width, float strength)
    {
        Texture2D gradient = Assets.Textures.VFX.RadialGradient.Asset.Value;
        Texture2D halo = Assets.Textures.VFX.Glow.Asset.Value;
        Vector2 gradientOrigin = gradient.Size() * 0.5f;
        Vector2 pool = floor - Main.screenPosition;

        batch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

        // Glow carries its falloff in alpha, RadialGradient in RGB; both read correctly under additive blending.
        batch.Draw(halo, center, null, SapColor * (strength * 0.3f), 0f, halo.Size() * 0.5f,
            new Vector2(360f, HalfHeight * 3.4f) / halo.Width * new Vector2(0.55f + width * 0.45f, length), SpriteEffects.None, 0f);

        batch.Draw(gradient, center, null, SapColor * (strength * 0.6f), 0f, gradientOrigin,
            new Vector2(150f, HalfHeight * 3.2f) / gradient.Width * new Vector2(0.5f + width, length), SpriteEffects.None, 0f);

        batch.Draw(gradient, center, null, PaleColor * (strength * 0.8f), 0f, gradientOrigin,
            new Vector2(48f, HalfHeight * 2.3f) / gradient.Width * new Vector2(0.4f + width, length), SpriteEffects.None, 0f);

        batch.Draw(gradient, pool, null, SapColor * (strength * 0.9f), 0f, gradientOrigin,
            new Vector2(280f, 56f) / gradient.Width * new Vector2(MathF.Min(open * 1.4f, 1f), 1f), SpriteEffects.None, 0f);

        batch.End();
    }

    /// <summary>The slit's alpha is the coverage <c>TextFbm.fx</c> fills with pale green FBM.</summary>
    private static void DrawBody(SpriteBatch batch, Texture2D texture, Vector2 position, Vector2 scale, float rotation, float strength)
    {
        Effect effect = Assets.Shaders.TextFbm.Asset.Value;
        EffectParameterCollection parameters = effect.Parameters;

        parameters["uNoiseTex"]?.SetValue(Assets.Textures.Noise.Milky_5.Asset.Value);
        parameters["uImageSize0"]?.SetValue(texture.Size());
        parameters["uSourceRect"]?.SetValue(new Vector4(0f, 0f, texture.Width, texture.Height));
        parameters["uGlyphPosition"]?.SetValue(Vector2.Zero);
        parameters["uTime"]?.SetValue(Main.GlobalTimeWrappedHourly);
        parameters["uNoiseScale"]?.SetValue(0.016f);
        parameters["uDrift"]?.SetValue(new Vector2(0.03f, 0.12f));
        parameters["uWarp"]?.SetValue(0.6f);
        parameters["uContrast"]?.SetValue(1.8f);
        parameters["uBias"]?.SetValue(-0.18f);
        parameters["uBands"]?.SetValue(4f);
        parameters["uShadowColor"]?.SetValue(new Vector3(0.52f, 0.86f, 0.62f));
        parameters["uBodyColor"]?.SetValue(new Vector3(0.84f, 1f, 0.88f));
        parameters["uHighlightColor"]?.SetValue(Vector3.One);

        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, effect, Main.GameViewMatrix.TransformationMatrix);

        // Zero alpha adds under premultiplied AlphaBlend: a halo of the same noise without another pass.
        var halo = new Color(255, 255, 255, 0);
        Vector2 origin = texture.Size() * 0.5f;

        for (int k = 0; k < 8; k++)
        {
            Vector2 ring = (k * MathHelper.PiOver4).ToRotationVector2() * 2f;
            batch.Draw(texture, position + ring, null, halo * (0.3f * strength), rotation, origin, scale, SpriteEffects.None, 0f);
        }

        batch.Draw(texture, position, null, Color.White * MathHelper.Clamp(open * 3f, 0f, 1f), rotation, origin, scale, SpriteEffects.None, 0f);
        batch.End();

        parameters["uNoiseTex"]?.SetValue((Texture2D?)null);
    }

    private static void Illuminate()
    {
        float brightness = open * (1f + surge * 1.5f);

        for (int k = -2; k <= 2; k++)
            Lighting.AddLight(Center + new Vector2(0f, HalfHeight * 0.4f * k), new Vector3(0.62f, 1f, 0.74f) * brightness);

        Lighting.AddLight(floor - new Vector2(0f, 12f), new Vector3(0.5f, 0.9f, 0.6f) * brightness);
    }

    private static void EmitMotes()
    {
        emitCarry += open * 0.9f + surge * 4f;

        while (emitCarry >= 1f)
        {
            emitCarry -= 1f;

            Vector2 at = Center + new Vector2(Main.rand.NextFloat(-6f, 6f), Main.rand.NextFloat(-0.5f, 0.5f) * HalfHeight * 2f * open);

            ParticleSystem.Spawn<RiftMoteParticle>(
                at,
                new Vector2(Main.rand.NextFloat(-0.5f, 0.5f), Main.rand.NextFloat(-1.1f, -0.2f)),
                timeLeft: Main.rand.Next(50, 100),
                scale: Main.rand.NextFloat(0.10f, 0.26f),
                rotation: Main.rand.NextFloat(MathHelper.TwoPi),
                rotationVelocity: Main.rand.NextFloat(-0.03f, 0.03f)
            );
        }

        if (open > 0.5f && Main.rand.NextBool(14))
        {
            Vector2 at = Center + new Vector2(Main.rand.NextFloat(-14f, 14f), Main.rand.NextFloat(-HalfHeight, HalfHeight * 0.4f));

            Gore.NewGore(
                new EntitySource_Misc(nameof(DryadRift)),
                at,
                new Vector2(Main.rand.NextFloat(-1f, 1f), Main.rand.NextFloat(-1.2f, -0.2f)),
                GoreID.TreeLeaf_Normal,
                Main.rand.NextFloat(0.7f, 1f)
            );
        }
    }

    private static float Smooth(float x) => x * x * (3f - 2f * x);
}
