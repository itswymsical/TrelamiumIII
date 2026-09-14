using ReLogic.Graphics;
using SubworldLibrary;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.WorldBuilding;
using TrelamiumIII.Common.Configs;
using TrelamiumIII.Core.Dialogue.Projected;

namespace TrelamiumIII.Common.Subworlds;

/// <summary>
///     Draws a <see cref="SubworldRealm" />'s loading screen. When the realm is being left rather than entered,
///     it shows the main world's name and vanilla loading tips instead of the realm's own content.
/// </summary>
internal sealed class RealmLoadingScreen(SubworldRealm realm)
{
    private const string TipLabelKey = "Mods.TrelamiumIII.Subworlds.LoadingScreen.TipLabel";
    private const string VanillaTipPrefix = "LoadingTips_Default.";

    /// <summary>A gap between draws longer than this starts a new session.</summary>
    private const double SessionGapSeconds = 0.5;

    private const float FadeInSeconds = 0.8f;
    private const float TipSeconds = 8f;
    private const float TipFadeSeconds = 0.5f;
    private const float BackgroundZoom = 0.06f;
    private const float BackgroundZoomSeconds = 40f;

    /// <summary>Layout lengths below are pixels at this screen height, scaled to the actual one.</summary>
    private const float ReferenceHeight = 1080f;

    private const float Margin = 56f;
    private const float ProgressReserve = 64f;

    private const float TitleScale = 1.35f;
    private const float TitleOutline = 3f;
    private const float TitleSlide = 80f;
    private const float TitleYaw = -0.28f;
    private const float TitlePitch = 0.08f;
    private const float TitleSway = 0.015f;

    private const float TipScale = 1f;
    private const float TipWrapFraction = 0.45f;
    private const int TipMaxLines = 4;

    private const int IconFrames = 19;
    private const float IconFrameSeconds = 0.04f;

    private double lastDrawSeconds = double.NegativeInfinity;
    private float elapsed;
    private bool leaving;
    private LocalizedText[] tips = [];
    private int firstTip;

    private static Texture2D Pixel => TextureAssets.MagicPixel.Value;

    public void Draw(SpriteBatch spriteBatch, GameTime gameTime)
    {
        double now = gameTime.TotalGameTime.TotalSeconds;

        if (now - lastDrawSeconds > SessionGapSeconds)
            BeginSession();

        lastDrawSeconds = now;
        elapsed += MathF.Min((float)gameTime.ElapsedGameTime.TotalSeconds, 0.1f);

        bool debug = DebugConfig.Instance.DebugMode;
        float fade = Math.Clamp(elapsed / FadeInSeconds, 0f, 1f);
        float eased = 1f - MathF.Pow(1f - fade, 3f);
        float ui = Math.Clamp(Main.screenHeight / ReferenceHeight, 0.6f, 2f);
        float bottom = Main.screenHeight - (Margin + (debug ? ProgressReserve : 0f)) * ui;

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, Matrix.Identity);

        DrawBackground(spriteBatch, fade);
        DrawShade(spriteBatch, fade);
        DrawTip(spriteBatch, fade, ui, bottom);
        DrawLoadingIcon(spriteBatch, fade, ui, bottom);

        if (debug)
            DrawGenerationProgress(spriteBatch, fade, ui);

        spriteBatch.End();

        Vector2 anchor = new((Margin - TitleSlide * (1f - eased)) * ui, Margin * ui);
        float yaw = TitleYaw + TitleSway * MathF.Sin(elapsed * 0.8f);

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp, DepthStencilState.None, RasterizerState.CullNone, null, ProjectedTransform(anchor, yaw, TitlePitch));
        DrawTitle(spriteBatch, fade, eased, ui);
        spriteBatch.End();
    }

    private void BeginSession()
    {
        elapsed = 0f;
        leaving = SubworldSystem.Current != realm;

        string prefix = leaving ? VanillaTipPrefix : realm.GetLocalizationKey("Tips") + ".";
        tips = Language.FindAll((key, _) => key.StartsWith(prefix, StringComparison.Ordinal));

        // Not Main.rand: the generation thread reseeds it per pass, and drawing would shift the world seed.
        firstTip = tips.Length > 0 ? Random.Shared.Next(tips.Length) : 0;
    }

    /// <summary>
    ///     Rotates local space about <paramref name="anchor" />, then applies <see cref="PanelCamera" />'s perspective
    ///     about the screen centre. FNA's sprite shader keeps the w row, so the GPU does the divide.
    /// </summary>
    private static Matrix ProjectedTransform(Vector2 anchor, float yaw, float pitch)
    {
        float distance = PanelCamera.Distance;
        Vector2 centre = new Vector2(Main.screenWidth, Main.screenHeight) * 0.5f;

        Matrix perspective = new(
            1f, 0f, 0f, 0f,
            0f, 1f, 0f, 0f,
            centre.X / distance, centre.Y / distance, 0f, 1f / distance,
            0f, 0f, 0f, 1f);

        return Matrix.CreateRotationX(pitch)
            * Matrix.CreateRotationY(yaw)
            * Matrix.CreateTranslation(anchor.X, anchor.Y, 0f)
            * perspective;
    }

    private void DrawBackground(SpriteBatch spriteBatch, float fade)
    {
        Texture2D? texture = leaving ? null : realm.LoadingBackground;

        if (texture is null)
            return;

        Vector2 screen = new(Main.screenWidth, Main.screenHeight);
        float cover = MathF.Max(screen.X / texture.Width, screen.Y / texture.Height);
        float zoom = 1f + BackgroundZoom * MathF.Min(elapsed / BackgroundZoomSeconds, 1f);

        spriteBatch.Draw(texture, screen * 0.5f, null, Color.White * fade, 0f, texture.Size() * 0.5f, cover * zoom, SpriteEffects.None, 0f);
    }

    /// <summary>Darkens the top and bottom of the screen behind the title and tips.</summary>
    private static void DrawShade(SpriteBatch spriteBatch, float fade)
    {
        float height = Main.screenHeight;

        DrawGradient(spriteBatch, 0f, height * 0.35f, 0.75f * fade, 0f);
        DrawGradient(spriteBatch, height * 0.6f, height, 0f, 0.85f * fade);
    }

    private static void DrawGradient(SpriteBatch spriteBatch, float top, float bottom, float fromAlpha, float toAlpha)
    {
        const int bands = 48;
        float step = (bottom - top) / bands;

        for (int i = 0; i < bands; i++)
        {
            int y = (int)(top + i * step);
            int next = (int)(top + (i + 1) * step);
            float alpha = MathHelper.Lerp(fromAlpha, toAlpha, (i + 0.5f) / bands);

            spriteBatch.Draw(Pixel, new Rectangle(0, y, Main.screenWidth, next - y), Color.Black * alpha);
        }
    }

    /// <summary>Drawn in local space at the origin; <see cref="ProjectedTransform" /> places it.</summary>
    private void DrawTitle(SpriteBatch spriteBatch, float fade, float eased, float ui)
    {
        DynamicSpriteFont font = leaving ? FontAssets.DeathText.Value : realm.TitleFont;
        string title = leaving ? UpdateSubworldSystem.MainWorldName : realm.DisplayName.Value;

        if (string.IsNullOrEmpty(title))
            return;

        float scale = TitleScale * ui;
        float outline = TitleOutline * ui;
        Vector2 size = font.MeasureString(title) * scale;
        Color shadow = Color.Black * fade;

        for (int i = 0; i < 8; i++)
        {
            Vector2 offset = (MathHelper.PiOver4 * i).ToRotationVector2() * outline;
            spriteBatch.DrawString(font, title, offset, shadow, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        spriteBatch.DrawString(font, title, Vector2.Zero, Color.White * fade, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

        Rectangle underline = new(0, (int)size.Y, (int)((size.X + 24f * ui) * eased), (int)MathF.Max(6f * ui, 2f));
        Rectangle underlineShadow = underline;
        underlineShadow.Offset((int)outline, (int)outline);

        spriteBatch.Draw(Pixel, underlineShadow, shadow);
        spriteBatch.Draw(Pixel, underline, realm.LoadingAccent * fade);
    }

    private void DrawTip(SpriteBatch spriteBatch, float fade, float ui, float bottom)
    {
        if (tips.Length == 0)
            return;

        float cycle = elapsed / TipSeconds;
        int index = (firstTip + (int)cycle) % tips.Length;
        float phase = (cycle - MathF.Floor(cycle)) * TipSeconds;
        float alpha = tips.Length == 1
            ? fade
            : fade * Math.Clamp(MathF.Min(phase, TipSeconds - phase) / TipFadeSeconds, 0f, 1f);

        DynamicSpriteFont font = leaving ? FontAssets.MouseText.Value : realm.TipFont;
        float scale = TipScale * ui;
        float lineHeight = font.LineSpacing * scale;
        float left = Margin * ui;

        string[] lines = Utils.WordwrapString(tips[index].Value, font, (int)(Main.screenWidth * TipWrapFraction / scale), TipMaxLines, out _);
        int count = lines.Length;

        while (count > 0 && string.IsNullOrEmpty(lines[count - 1]))
            count--;

        float top = bottom - count * lineHeight;
        Color accent = realm.LoadingAccent * alpha;
        Color border = Color.Black * alpha;

        spriteBatch.Draw(Pixel, new Rectangle((int)(left - 14f * ui), (int)(top - lineHeight), (int)MathF.Max(4f * ui, 2f), (int)((count + 1) * lineHeight)), accent);
        Utils.DrawBorderStringFourWay(spriteBatch, font, Language.GetTextValue(TipLabelKey), left, top - lineHeight, accent, border, Vector2.Zero, scale);

        for (int i = 0; i < count; i++)
            Utils.DrawBorderStringFourWay(spriteBatch, font, lines[i], left, top + i * lineHeight, Color.White * alpha, border, Vector2.Zero, scale);
    }

    private void DrawLoadingIcon(SpriteBatch spriteBatch, float fade, float ui, float bottom)
    {
        Texture2D texture = TextureAssets.LoadingSunflower.Value;
        int frameHeight = texture.Height / IconFrames;
        int frame = (int)(elapsed / IconFrameSeconds) % IconFrames;

        Rectangle source = new(0, frame * frameHeight, texture.Width, frameHeight);
        Vector2 position = new Vector2(Main.screenWidth - Margin * ui, bottom) - new Vector2(texture.Width, frameHeight) * ui;

        spriteBatch.Draw(texture, position, source, Color.White * fade, 0f, Vector2.Zero, ui, SpriteEffects.None, 0f);
    }

    /// <summary>Debug only. Total progress fills the bar, the current pass fills the strip beneath it.</summary>
    private void DrawGenerationProgress(SpriteBatch spriteBatch, float fade, float ui)
    {
        // Read once: the generation thread sets it to null after the last pass.
        GenerationProgress? progress = WorldGenerator.CurrentGenerationProgress;

        float left = Margin * ui;
        float width = Main.screenWidth - left * 2f;
        float height = 8f * ui;
        float top = Main.screenHeight - Margin * 0.5f * ui - height;
        string status = Main.statusText;

        spriteBatch.Draw(Pixel, new Rectangle((int)left, (int)top, (int)width, (int)height), Color.Black * 0.6f * fade);

        if (progress is not null)
        {
            float total = (float)Math.Clamp(progress.TotalProgress, 0.0, 1.0);
            float pass = (float)Math.Clamp(progress.Value, 0.0, 1.0);

            spriteBatch.Draw(Pixel, new Rectangle((int)left, (int)top, (int)(width * total), (int)height), realm.LoadingAccent * fade);
            spriteBatch.Draw(Pixel, new Rectangle((int)left, (int)(top + height), (int)(width * pass), (int)MathF.Max(2f * ui, 1f)), Color.White * 0.7f * fade);

            status = $"{progress.Message}  {total * 100f:0}%";
        }

        if (string.IsNullOrEmpty(status))
            return;

        DynamicSpriteFont font = FontAssets.MouseText.Value;
        float scale = 0.8f * ui;

        Utils.DrawBorderStringFourWay(spriteBatch, font, status, left, top - font.LineSpacing * scale - 4f * ui, Color.White * fade, Color.Black * fade, Vector2.Zero, scale);
    }
}
