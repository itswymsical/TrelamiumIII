using ReLogic.Content;
using System.Linq;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.UI;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Core.UI.Jobs;

// TODO: Localization
public class JobCompleteNotification(Job Job) : IInGameNotification
{
    private const float DISPLAY_TIME = 5.5f * 60f;
    private const float ANIMATION_TIME = 60f;
    private const float FADEOUT_TIME = 30f;

    public bool ShouldBeRemoved => timeLeft <= 0;
    private float timeLeft = DISPLAY_TIME;
    private readonly Job completedJob = Job;

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

        var header = $"Job Complete!";
        var title = $"{completedJob.Name}";

        var headerSize = FontAssets.DeathText.Value.MeasureString(header);
        var titleSize = FontAssets.DeathText.Value.MeasureString(title);

        var headerScale = UnifiedScale;
        var titleScale = UnifiedScale * 0.65f;

        var headerPos = screenCenter - new Vector2(0, headerSize.Y * headerScale + 10);
        var titlePos = screenCenter + new Vector2(0, titleSize.Y * titleScale * 0.5f);

        var accentTexture = ModContent.Request<Texture2D>($"{Assets.Textures.UI.Jobs.Accent.KEY}").Value;
        Vector2 accentOrigin = new(accentTexture.Width / 2f, accentTexture.Height / 2f);
        Vector2 accentPosition = headerPos + new Vector2(0, (headerSize.Y / 2 * headerScale) + 8);

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
            header,
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
            title,
            titlePos.X,
            titlePos.Y,
            TextColor,
            Color.Black * ElementOpacity,
            titleSize / 2f,
            titleScale
        );

        DrawRewardItems(spriteBatch, screenCenter, titlePos.Y + (titleSize.Y * titleScale) / 2f + 20);

        spriteBatch.End();

        var effect = ModContent.Request<Effect>($"{Assets.Shaders.SunburstShader.KEY}", AssetRequestMode.ImmediateLoad).Value;
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.Additive, SamplerState.PointClamp,
                          DepthStencilState.None, Main.Rasterizer, effect, Main.UIScaleMatrix);

        if (effect != null)
        {
            effect.Parameters["uTime"]?.SetValue(SunburstRotation);
            effect.Parameters["uIntensity"]?.SetValue((TextColor.R / 255f) * 0.2f);
            effect.Parameters["uColor"]?.SetValue(new Vector3(1f, 0.65f, 0.4f));


            Vector2 shaderCenter = new(
                (screenCenter.X / Main.screenWidth) * 2f - 1f,
                (screenCenter.Y / Main.screenHeight) * 2f - 1.1f
            );

            effect.Parameters["uCenter"]?.SetValue(shaderCenter);
            effect.Parameters["uScale"]?.SetValue(UnifiedScale * 0.7f);
            effect.Parameters["uRayCount"]?.SetValue(18f);
        }

        var noiseTexture = ModContent.Request<Texture2D>($"{Assets.Textures.Noise.Perlin.KEY}").Value;
        spriteBatch.Draw(noiseTexture, new Rectangle(0, 0, Main.screenWidth, Main.screenHeight), Color.White);
        spriteBatch.End();

        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.LinearClamp,
                          DepthStencilState.None, Main.Rasterizer, null, Main.UIScaleMatrix);
    }

    private void DrawRewardItems(SpriteBatch spriteBatch, Vector2 screenCenter, float yPosition)
    {
        var rewards = completedJob.Rewards;
        if (rewards == null || rewards.Count == 0) return;

        // Filter to only valid rewards
        var validRewards = rewards.Where(r => r.IsValid).Select(r => r.GetItem()).ToList();
        if (validRewards.Count == 0) return;

        const float ITEM_SPACING = 10f;
        const float ITEM_SIZE = 16f;
        float totalWidth = (validRewards.Count * ITEM_SIZE) + ((validRewards.Count - 1) * ITEM_SPACING);

        Vector2 currentPos = new(screenCenter.X - totalWidth / 2f, yPosition);

        foreach (var item in validRewards)
        {
            Main.instance.LoadItem(item.type);
            var itemTexture = TextureAssets.Item[item.type].Value;
            Rectangle? sourceRect = null;

            if (Main.itemAnimations[item.type] != null)
            {
                Main.itemAnimations[item.type].Update();
                sourceRect = Main.itemAnimations[item.type].GetFrame(itemTexture);
            }

            float scale = (ITEM_SIZE / Math.Max(itemTexture.Width, itemTexture.Height) * 1.25f) * UnifiedScale;

            Vector2 origin = sourceRect.HasValue
                ? new Vector2(sourceRect.Value.Width / 2f, sourceRect.Value.Height / 2f)
                : new Vector2(itemTexture.Width / 2f, itemTexture.Height / 2f);

            var itemColor = Color.Lerp(Color.White, Color.White, AnimationProgress) * ElementOpacity;

            spriteBatch.Draw(
                itemTexture,
                currentPos + new Vector2(ITEM_SIZE / 2f),
                sourceRect,
                itemColor,
                0f,
                origin,
                scale,
                SpriteEffects.None,
                0f
            );

            if (item.stack > 1)
            {
                var stackText = item.stack.ToString();
                var stackScale = 0.8f * UnifiedScale;
                var stackSize = FontAssets.ItemStack.Value.MeasureString(stackText) * stackScale;
                var stackPos = currentPos + new Vector2(ITEM_SIZE - stackSize.X / 2f, ITEM_SIZE - stackSize.Y / 2f);

                Utils.DrawBorderStringFourWay(
                    spriteBatch,
                    FontAssets.ItemStack.Value,
                    stackText,
                    stackPos.X,
                    stackPos.Y,
                    TextColor,
                    Color.Black * ElementOpacity,
                    Vector2.Zero,
                    stackScale
                );
            }

            currentPos.X += ITEM_SIZE + ITEM_SPACING;
        }
    }

    public void Update()
    {
        if (timeLeft == DISPLAY_TIME)
            SoundEngine.PlaySound(new SoundStyle($"{Assets.SFX.JobComplete.KEY}"));

        timeLeft--;
        timeLeft = Math.Max(0, timeLeft);
    }

    public void PushAnchor(ref Vector2 positionAnchorBottom) { }
}