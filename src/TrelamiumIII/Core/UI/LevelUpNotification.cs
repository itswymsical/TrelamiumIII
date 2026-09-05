using ReLogic.Content;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.UI;
using TrelamiumIII.Core.ExperientialSystem;

namespace TrelamiumIII.Core.UI;

public class LevelUpNotification(ExperientialPlayer player) : IInGameNotification
{
    private const float DISPLAY_TIME = 5.5f * 60f;
    private const float ANIMATION_TIME = 60f;
    private const float FADEOUT_TIME = 30f;

    public bool ShouldBeRemoved => timeLeft <= 0;
    private float timeLeft = DISPLAY_TIME;

    private float AnimationProgress => Math.Clamp((DISPLAY_TIME - timeLeft) / ANIMATION_TIME, 0f, 1f);

    private float FadeoutProgress
    {
        get
        {
            if (timeLeft > FADEOUT_TIME) return 1f;
            return timeLeft / FADEOUT_TIME;
        }
    }

    // Unified scale for all elements - starts small, hits normal size
    private float UnifiedScale => MathHelper.SmoothStep(0.15f, 1f, AnimationProgress);

    private float ElementOpacity => FadeoutProgress; // Simple fadeout at the end

    private Color TextColor
    {
        get
        {
            var targetColor = new Color(255, 252, 114);
            var whiteColor = Color.White;
            // Start white, transition to gold during animation
            var currentColor = Color.Lerp(whiteColor, targetColor, AnimationProgress);
            return currentColor * ElementOpacity;
        }
    }

    private float SunburstRotation => timeLeft * 0.005f;

    public void DrawInGame(SpriteBatch spriteBatch, Vector2 bottomAnchorPosition)
    {
        if (timeLeft <= 0) return;

        var fadeoutOffset = MathHelper.Lerp(30f, 0f, FadeoutProgress);
        Vector2 screenCenter = new(Main.screenWidth / 2f, Main.screenHeight / 3f - fadeoutOffset);

        var lvlText = $"Threshold {player.Threshold}";
        var skillpointText = $"— The meter now holds {player.Capacity:N0} —";

        var headerSize = FontAssets.DeathText.Value.MeasureString(lvlText);
        var titleSize = FontAssets.DeathText.Value.MeasureString(skillpointText);

        var headerScale = UnifiedScale;
        var titleScale = UnifiedScale * 0.45f;

        var headerPos = screenCenter - new Vector2(0, headerSize.Y * headerScale + 10);
        var titlePos = screenCenter + new Vector2(0, titleSize.Y * titleScale * 0.5f);

        var accentTexture = ModContent.Request<Texture2D>($"TrelamiumIII/Assets/Textures/UI/Jobs/Accent").Value;
        Vector2 accentOrigin = new(accentTexture.Width / 2f, accentTexture.Height / 2f);
        Vector2 accentPosition = headerPos + new Vector2(0, headerSize.Y / 2 * headerScale + 8);

        spriteBatch.Draw(
            accentTexture,
            accentPosition,
            null,
            TextColor * 0.8f,
            0f,
            accentOrigin,
            UnifiedScale * 1.15f,
            SpriteEffects.None,
            0f
        );

        Utils.DrawBorderStringFourWay(
            spriteBatch,
            FontAssets.DeathText.Value,
            lvlText,
            headerPos.X,
            headerPos.Y,
            TextColor,
            Color.Black * ElementOpacity,
            headerSize / 2f,
            headerScale
        );

        Utils.DrawBorderStringFourWay(
            spriteBatch,
            FontAssets.DeathText.Value,
            skillpointText,
            titlePos.X,
            titlePos.Y,
            TextColor,
            Color.Black * ElementOpacity,
            titleSize / 2f,
            titleScale
        );

        spriteBatch.End();

        var effect = ModContent.Request<Effect>("TrelamiumIII/Assets/Shaders/SunburstShader", AssetRequestMode.ImmediateLoad).Value;
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp,
                          DepthStencilState.None, Main.Rasterizer, effect, Main.UIScaleMatrix);

        if (effect != null)
        {
            effect.Parameters["uTime"]?.SetValue(SunburstRotation);
            effect.Parameters["uIntensity"]?.SetValue(TextColor.R / 255f * 0.2f);
            effect.Parameters["uColor"]?.SetValue(new Vector3(1f, 0.65f, 0.4f));


            Vector2 shaderCenter = new(
                screenCenter.X / Main.screenWidth * 2f - 1f,
                screenCenter.Y / Main.screenHeight * 2f - 1.1f
            );

            effect.Parameters["uCenter"]?.SetValue(shaderCenter);
            effect.Parameters["uScale"]?.SetValue(UnifiedScale * 0.7f);
            effect.Parameters["uRayCount"]?.SetValue(18f);
        }

        var noiseTexture = Assets.Textures.Noise.Perlin.Asset.Value;
        spriteBatch.Draw(noiseTexture, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
        spriteBatch.End();

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                          DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
    }
    public void Update()
    {
        if (timeLeft == DISPLAY_TIME)
            SoundEngine.PlaySound(Assets.SFX.JobComplete.Asset);

        timeLeft--;
        timeLeft = Math.Max(0, timeLeft);
    }

    public void PushAnchor(ref Vector2 positionAnchorBottom) { }
}