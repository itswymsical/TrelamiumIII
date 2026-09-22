using ReLogic.Content;
using SubworldLibrary;
using Terraria.Audio;
using TrelamiumIII.Common.Subworlds;
using TrelamiumIII.Common.Subworlds.Realms;
using TrelamiumIII.Common.Systems;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Content.Ingress;

/// <summary>
/// The Scar of Ingress: a diagonal slit of white light filled with drifting FBM (<c>TextFbm.fx</c> over the slit sprite).
/// In Ingress it opens above the Altar of Ingress once the Wooden Warden is defeated and leads to Reverie; in Reverie it
/// stands by the front desk and leads back to the altar. Client-side; drawn after tiles so it sits in front of the altar.
/// </summary>
public sealed class IngressScar : ModSystem
{
    private const float RevealTicks = 150f;

    /// <summary> Slit tilt from vertical, matching the sprite; the top end leans right. </summary>
    private const float Tilt = 20f * MathHelper.Pi / 180f;

    /// <summary> Half the slit's length along its axis, in world pixels. </summary>
    private const float HalfLength = 92f;

    private const float HoverHalfWidth = 16f;
    private const float InteractRange = 180f;
    private const int AltarTimeoutTicks = 10;

    /// <summary> Seconds a departure focused on the scar takes to pull it fully open. </summary>
    private const float OpenSeconds = 1.2f;

    /// <summary> From the altar's top centre to the scar's centre, so the lower tip hangs a tile above the stone. </summary>
    private static readonly Vector2 AltarOffset = new(31f, -102f);

    private static Asset<Texture2D>? slit;

    private static Vector2 altarTop;
    private static uint altarSeen = uint.MaxValue;

    /// <summary> 0 while the Warden stands, rising to 1 after its defeat; always 1 in Reverie. </summary>
    public static float Reveal { get; private set; }

    public static float Opacity => Reveal * Reveal * (3f - 2f * Reveal);

    private static bool InReverie => SubworldSystem.IsActive<ReverieRealm>();

    /// <summary> World position of the scar's centre; null in Ingress while the altar is off screen, and in other worlds. </summary>
    public static Vector2? Center
    {
        get
        {
            if (InReverie)
                return ReverieRealm.ScarCenter;

            return SubworldSystem.IsActive<IngressRealm>() && Main.GameUpdateCount - altarSeen <= AltarTimeoutTicks ? altarTop + AltarOffset : null;
        }
    }

    /// <summary> 0 to 1 while an entry cutscene focused on the scar pulls it open. </summary>
    private static float OpenBoost =>
        RealmTransition.FocusWorld is { } focus && Center is { } at && Vector2.DistanceSquared(focus, at) < 4f
            ? EaseFunction.EaseQuadOut.Ease(MathF.Min(RealmTransition.DepartureSeconds / OpenSeconds, 1f))
            : 0f;

    private static Vector2 Axis => new(MathF.Sin(Tilt), -MathF.Cos(Tilt));

    public override void Load()
    {
        if (!Main.dedServ)
            slit = ModContent.Request<Texture2D>($"{nameof(TrelamiumIII)}/Content/Ingress/WardenAltarTile_Slit");
    }

    public override void Unload() => slit = null;

    public override void OnWorldLoad()
    {
        Reveal = InReverie || DownedBossSystem.DownedWoodenWarden ? 1f : 0f;
        altarSeen = uint.MaxValue;
    }

    /// <summary> From the altar's draw: its top centre, in world pixels. </summary>
    internal static void MarkAltar(Vector2 topCenter)
    {
        altarTop = topCenter;
        altarSeen = Main.GameUpdateCount;
    }

    /// <summary> Through the scar to the other side: Reverie from Ingress, or back to the altar from Reverie. </summary>
    public static bool Enter()
    {
        Vector2? focus = Center;

        if (!InReverie)
            return RealmTransition.Enter<ReverieRealm>(focus);

        IngressRealm.ArriveAtAltar = true;

        if (RealmTransition.Enter<IngressRealm>(focus))
            return true;

        IngressRealm.ArriveAtAltar = false;

        return false;
    }

    public override void PostUpdateEverything()
    {
        if (Main.dedServ)
            return;

        float previous = Reveal;
        Reveal = InReverie || DownedBossSystem.DownedWoodenWarden ? MathF.Min(Reveal + 1f / RevealTicks, 1f) : 0f;

        if (Center is not { } at)
            return;

        if (previous == 0f && Reveal > 0f)
            SoundEngine.PlaySound(Assets.SFX.RiftOpenAlt.Asset with { Volume = 0.6f, Pitch = 0.5f }, at);

        if (Opacity <= 0f)
            return;

        float brightness = Opacity * (1f + OpenBoost * 2f);

        for (int k = -2; k <= 2; k++)
            Lighting.AddLight(at + Axis * (HalfLength * 0.4f * k), new Vector3(0.9f, 0.95f, 1f) * brightness);

        if (!Main.gamePaused && Main.rand.NextFloat() < 0.25f + (1f - Reveal) * 0.6f)
        {
            Vector2 along = at + Axis * Main.rand.NextFloat(-HalfLength, HalfLength) * Opacity;
            Vector2 drift = new Vector2(Axis.Y, -Axis.X) * Main.rand.NextFloat(-0.6f, 0.6f) - Vector2.UnitY * Main.rand.NextFloat(0.2f, 0.9f);
            Dust dust = Dust.NewDustPerfect(along, DustID.TintableDustLighted, drift, 0, Color.White, Main.rand.NextFloat(0.6f, 1.1f));
            dust.noGravity = true;
        }
    }

    public override void PostUpdatePlayers()
    {
        if (Main.dedServ || Reveal < 1f || Center is not { } at || Main.LocalPlayer.mouseInterface || Main.mapFullscreen || !Hovered(at, Main.MouseWorld))
            return;

        if (Vector2.Distance(Main.LocalPlayer.Center, at) > InteractRange + HalfLength)
            return;

        string text = InReverie
            ? TrelamiumIII.Instance.GetLocalization("Scar.Return").Value
            : ModContent.GetInstance<WardenAltarTile>().GetLocalization("Enter").Value;

        WardenAltarTile.ShowCursorText(text);

        if (Main.mouseRight && Main.mouseRightRelease)
        {
            Main.mouseRightRelease = false;
            Enter();
        }
    }

    private static bool Hovered(Vector2 at, Vector2 point)
    {
        Vector2 offset = point - at;
        float along = Vector2.Dot(offset, Axis);
        float across = Vector2.Dot(offset, new Vector2(-Axis.Y, Axis.X));

        return MathF.Abs(along) <= HalfLength && MathF.Abs(across) <= HoverHalfWidth;
    }

    public override void PostDrawTiles()
    {
        float opacity = Opacity;

        if (Main.dedServ || opacity <= 0f || Center is not { } at || slit is not { IsLoaded: true })
            return;

        SpriteBatch batch = Main.spriteBatch;
        Texture2D texture = slit.Value;
        Vector2 position = at - Main.screenPosition;
        Vector2 origin = texture.Size() * 0.5f;
        float boost = OpenBoost;

        // Grows along its axis first, then widens; a departure focused on it pulls it wider still.
        float length = MathHelper.Lerp(0.05f, 1f, opacity);
        float width = MathF.Pow(opacity, 3f) * (1f + boost * 2.5f);
        float surge = MathF.Sin(MathHelper.Pi * Reveal);
        float pulse = 0.85f + 0.15f * MathF.Sin(Main.GlobalTimeWrappedHourly * 2.4f);
        float glowStrength = opacity * pulse + surge * 1.2f + boost * 2f;

        DrawGlow(batch, position, length, width, glowStrength);
        DrawBody(batch, texture, position, origin, new Vector2(MathF.Max(width, 0.08f), length), opacity, glowStrength);
    }

    private static void DrawGlow(SpriteBatch batch, Vector2 position, float length, float width, float strength)
    {
        Texture2D glow = Assets.Textures.VFX.RadialGradient.Asset.Value;
        Vector2 origin = glow.Size() * 0.5f;

        batch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.LinearClamp, DepthStencilState.None, Main.Rasterizer, null, Main.GameViewMatrix.TransformationMatrix);

        batch.Draw(glow, position, null, new Color(200, 225, 255) * (strength * 0.35f), Tilt, origin,
            new Vector2(80f, HalfLength * 3.2f) / glow.Width * new Vector2(0.4f + width, length), SpriteEffects.None, 0f);

        batch.Draw(glow, position, null, Color.White * (strength * 0.45f), Tilt, origin,
            new Vector2(30f, HalfLength * 2.3f) / glow.Width * new Vector2(0.3f + width, length), SpriteEffects.None, 0f);

        batch.End();
    }

    /// <summary> The slit's alpha is the coverage <c>TextFbm.fx</c> fills with pale blue-white FBM. </summary>
    private static void DrawBody(SpriteBatch batch, Texture2D texture, Vector2 position, Vector2 origin, Vector2 scale, float opacity, float glowStrength)
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
        parameters["uShadowColor"]?.SetValue(new Vector3(0.62f, 0.72f, 1f));
        parameters["uBodyColor"]?.SetValue(new Vector3(0.9f, 0.94f, 1f));
        parameters["uHighlightColor"]?.SetValue(Vector3.One);

        batch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, DepthStencilState.None, Main.Rasterizer, effect, Main.GameViewMatrix.TransformationMatrix);

        // Zero alpha adds under premultiplied AlphaBlend: a halo of the same noise without another pass.
        var halo = new Color(255, 255, 255, 0);

        for (int k = 0; k < 8; k++)
        {
            Vector2 ring = (k * MathHelper.PiOver4).ToRotationVector2() * 2f;
            batch.Draw(texture, position + ring, null, halo * (0.3f * glowStrength), 0f, origin, scale, SpriteEffects.None, 0f);
        }

        batch.Draw(texture, position, null, Color.White * opacity, 0f, origin, scale, SpriteEffects.None, 0f);
        batch.End();

        parameters["uNoiseTex"]?.SetValue((Texture2D?)null);
    }
}
