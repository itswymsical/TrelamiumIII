using System.Collections.Generic;
using System.Linq;
using Terraria.GameContent;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Core.UI.Jobs.Indicators;

// TODO: Localization
public class JobIndicator : ScreenIndicator
{
    private readonly Job job;
    private readonly List<Job> jobs; // todo: job list
    private readonly Texture2D iconTexture;

    private const int PANEL_WIDTH = 220;
    private const int PADDING = 10;
    private const int BUTTON_HEIGHT = 28;

    private static readonly Color HeaderColor = new Color(255, 215, 0);
    private static readonly Color TextColor = Color.White;
    private static readonly Color ShadowColor = new Color(1, 1, 1, 170);
    private static readonly Color ButtonHoverColor = new Color(200, 200, 50);
    private static readonly Color ButtonIdleColor = Color.Yellow;

    private bool isPanelOpen = false;
    private Rectangle panelRect;
    private Rectangle confirmButtonRect;

    public override AnimationType AnimationStyle => AnimationType.Wag;

    public JobIndicator(Vector2 worldPosition, Job job, AnimationType? animationType = null)
         : base(worldPosition, 48, 48, animationType)
    {
        this.job = job;

        iconTexture = ModContent.Request<Texture2D>($"{Assets.Textures.UI.Jobs.JobAvailable.KEY}", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;

        if (iconTexture != null)
        {
            Width = iconTexture.Width;
            Height = iconTexture.Height;
        }

        OnDrawWorld = Draw;
        OnClick += HandleClick;
    }

    public static JobIndicator CreateForNPC(NPC npc, Job job, AnimationType? animationType = null)
    {
        var indicator = new JobIndicator(npc.Top, job, animationType);
        indicator.TrackEntity(npc, new Vector2(0, -40));
        return indicator;
    }

    protected override void PostUpdate()
    {
        // Check if job should be permanently removed
        if (job != null && ShouldHideIndicator())
        {
            IsActive = false;
            return;
        }

        // Temporarily hide during dialogue
        if (DialogueManager.Instance.IsAnyActive())
        {
            if (!IsHidden)
                Hide();
        }
        else
        {
            if (IsHidden)
                Unhide();
        }
    }

    /// <summary>
    /// Determines if the indicator should be hidden based on job state.
    /// Single player only - hides when the job is no longer available to start.
    /// </summary>
    private bool ShouldHideIndicator()
    {
        var jobPlayer = Main.LocalPlayer.GetModPlayer<JobPlayer>();
        var localJob = jobPlayer.GetOrCreateJob(job.ID);
        return localJob?.Progress != JobProgress.Inactive || localJob?.Status != JobStatus.Unlocked;
    }

    private void Draw(SpriteBatch spriteBatch, Vector2 screenPos, float opacity)
    {

        var scale = GetAnimationScale();
        var glowColor = IsHovering ? Color.White : Color.White * 0.8f;
        var rotation = GetAnimationRotation();
        spriteBatch.Draw(
            iconTexture,
            screenPos,
            null,
            glowColor * opacity,
            rotation,
            new Vector2(iconTexture.Width / 2, iconTexture.Height / 2),
            scale,
            SpriteEffects.None,
            0f
        );

        if (IsHovering)
        {
            DrawPanel(spriteBatch, opacity * GetHoverOpacity());
        }
    }

    private void DrawPanel(SpriteBatch spriteBatch, float opacity)
    {
        if (job == null)
            return;

        var screenPos = GetScreenPosition();
        var rewardLayout = CalculateRewardLayout(job);

        var lineCount = 3;
        if (job.TaskList.Count > 0)
        {
            lineCount += job.TaskList[0].Task.Count;
        }

        var rewardSectionHeight = 0;
        if (rewardLayout.HasRewards)
        {
            rewardSectionHeight = 20 + (rewardLayout.SlotSize + 5) * rewardLayout.Rows + 10;
        }

        var panelHeight = 20 + lineCount * 20 + PADDING * 2 + rewardSectionHeight + 40;

        // Position panel to the right of the icon
        var panelX = screenPos.X + Width / 2 + 10;
        var panelY = screenPos.Y - panelHeight / 2;

        // Adjust if panel would go off screen
        if (panelX + PANEL_WIDTH > Main.screenWidth)
        {
            panelX = screenPos.X - Width / 2 - PANEL_WIDTH - 10;
        }

        if (panelY + panelHeight > Main.screenHeight)
        {
            panelY = Main.screenHeight - panelHeight - 10;
        }

        if (panelY < 10)
        {
            panelY = 10;
        }

        var panelRect = new Rectangle(
       (int)panelX,
       (int)panelY,
       PANEL_WIDTH,
       panelHeight
   );

        var textY = panelRect.Y + PADDING;

        // Job name
        Utils.DrawBorderStringFourWay(
            spriteBatch,
            FontAssets.MouseText.Value,
            job.Name,
            panelRect.X + PADDING,
            textY,
            new Color(255, 215, 0) * opacity,
            new Color(1, 1, 1, (int)(170 * opacity)),
            Vector2.Zero,
            1f
        );

        textY += 25;

        // Employer name
        var employerName = "Unknown";
        if (job.ProviderNPC > 0)
        {
            employerName = Lang.GetNPCNameValue(job.ProviderNPC);
        }

        Utils.DrawBorderStringFourWay(
            spriteBatch,
            FontAssets.MouseText.Value,
            $"From: {employerName}",
            panelRect.X + PADDING,
            textY,
            Color.White * opacity,
            Color.Black * opacity,
            Vector2.Zero,
            0.9f
        );
        textY += 24;

        // Job description with effects
        DrawWithEffects(spriteBatch, job.Description, panelRect.X + PADDING, textY, opacity);
        textY += 28;

        // Draw rewards with dynamic layout
        if (rewardLayout.HasRewards)
        {
            Utils.DrawBorderStringFourWay(
                spriteBatch,
                FontAssets.MouseText.Value,
                "Rewards:",
                panelRect.X + PADDING,
                textY,
                Color.White * opacity,
                Color.Black * opacity,
                Vector2.Zero,
                0.75f
            );
            textY += 20;

            DrawDynamicRewards(spriteBatch, job, rewardLayout, panelRect.X + PADDING, textY, opacity);
        }

        var clickText = "Click to accept";
        var clickColor = Color.Yellow;

        Utils.DrawBorderStringFourWay(
            spriteBatch,
            FontAssets.MouseText.Value,
            clickText,
            panelRect.X + 4,
            panelRect.Y + panelHeight - 30,
            clickColor * opacity,
            new Color(1, 1, 1, (int)(170 * opacity)),
            default,
            0.8f
        );
    }

    private void DrawWithEffects(SpriteBatch spriteBatch, string rawDescription, int x, int y, float opacity, Color? baseColor = null, Color? glowColor = null)
    {
        var (plainText, effects) = JobTextParser.ParseDescription(rawDescription);
        var font = FontAssets.MouseText.Value;
        var scale = 1f;

        // Set defaults for colors
        var defaultBase = baseColor ?? Color.White * opacity;
        var defaultGlow = glowColor ?? Color.Gold * opacity;

        float currentX = x;
        float currentY = y;

        for (int i = 0; i < plainText.Length; i++)
        {
            var c = plainText[i].ToString();
            var charSize = font.MeasureString(c) * scale;

            Vector2 offset = Vector2.Zero;
            Color color = defaultBase;

            // Apply effects if present
            if (effects.TryGetValue(i, out var effect))
            {
                var time = (float)Main.timeForVisualEffects;

                offset = effect.Type switch
                {
                    TextEffectType.Shake => new Vector2(
                        Main.rand.NextFloat(-effect.Intensity, effect.Intensity),
                        Main.rand.NextFloat(-effect.Intensity, effect.Intensity)
                    ),
                    TextEffectType.Sine => new Vector2(
                        0,
                        (float)Math.Sin(time * 0.1f + i * 0.5f) * effect.Intensity
                    ),
                    _ => Vector2.Zero
                };

                color = effect.Type switch
                {
                    TextEffectType.Glow => Color.Lerp(
                        defaultBase,
                        effect.GlowColor ?? defaultGlow, // Use effect's color if specified
                        (float)Math.Sin(time * 0.1f + i * 0.3f) * 0.5f + 0.5f
                    ) * effect.Intensity,
                    _ => defaultBase
                };
            }

            Utils.DrawBorderStringFourWay(
                spriteBatch,
                font,
                c,
                currentX + offset.X,
                currentY + offset.Y,
                color,
                Color.Black * opacity,
                Vector2.Zero,
                scale
            );

            currentX += charSize.X;
        }
    }
    
    private struct RewardLayout
    {
        public bool HasRewards;
        public int TotalRewards;
        public int Columns;
        public int Rows;
        public int SlotSize;
        public float ItemScale;
    }

    private RewardLayout CalculateRewardLayout(Job job)
    {
        var layout = new RewardLayout();

        layout.TotalRewards = job.Rewards.Count(r => r.IsValid);

        layout.HasRewards = layout.TotalRewards > 0;

        if (!layout.HasRewards)
            return layout;

        if (layout.TotalRewards <= 3)
        {
            layout.Columns = layout.TotalRewards;
            layout.Rows = 1;
            layout.SlotSize = 32;
            layout.ItemScale = 1.0f;
        }
        else if (layout.TotalRewards <= 6)
        {
            layout.Columns = Math.Min(3, layout.TotalRewards);
            layout.Rows = (int)Math.Ceiling(layout.TotalRewards / 3.0);
            layout.SlotSize = 28;
            layout.ItemScale = 0.85f;
        }
        else if (layout.TotalRewards <= 12)
        {
            layout.Columns = Math.Min(4, layout.TotalRewards);
            layout.Rows = (int)Math.Ceiling(layout.TotalRewards / 4.0);
            layout.SlotSize = 24;
            layout.ItemScale = 0.7f;
        }
        else
        {
            layout.Columns = 5;
            layout.Rows = (int)Math.Ceiling(layout.TotalRewards / 5.0);
            layout.SlotSize = 20;
            layout.ItemScale = 0.6f;
        }

        return layout;
    }

    private void DrawDynamicRewards(SpriteBatch spriteBatch, Job job, RewardLayout layout, int startX, int startY, float opacity)
    {
        int currentSlot = 0;
        int slotSpacing = layout.SlotSize + 2;

        foreach (var reward in job.Rewards)
        {
            var item = reward.GetItem(); // Resolves cross-mod items
            if (item == null || item.type <= ItemID.None)
                continue;

            int row = currentSlot / layout.Columns;
            int col = currentSlot % layout.Columns;

            var slotX = startX + col * slotSpacing;
            var slotY = startY + row * slotSpacing;

            // Draw inventory slot background
            var slotRect = new Rectangle(slotX, slotY, layout.SlotSize, layout.SlotSize);
            spriteBatch.Draw(
                TextureAssets.InventoryBack.Value,
                slotRect,
                null,
                Color.White * opacity * 0.8f,
                0f,
                Vector2.Zero,
                SpriteEffects.None,
                0f
            );

            // Center the item in the slot
            var itemTexture = TextureAssets.Item[item.type].Value;
            var itemSize = new Vector2(itemTexture.Width, itemTexture.Height) * layout.ItemScale;
            var itemPos = new Vector2(
                slotX + (layout.SlotSize - itemSize.X) / 2,
                slotY + (layout.SlotSize - itemSize.Y) / 2
            );

            spriteBatch.Draw(
                itemTexture,
                itemPos,
                null,
                Color.White * opacity,
                0f,
                Vector2.Zero,
                layout.ItemScale,
                SpriteEffects.None,
                0f
            );

            // Draw stack count if > 1
            if (item.stack > 1)
            {
                var stackScale = Math.Max(0.5f, layout.ItemScale * 0.8f);
                Utils.DrawBorderStringFourWay(
                    spriteBatch,
                    FontAssets.ItemStack.Value,
                    item.stack.ToString(),
                    slotX + layout.SlotSize - 12,
                    slotY + layout.SlotSize - 12,
                    Color.White * opacity,
                    Color.Black * opacity,
                    Vector2.Zero,
                    stackScale
                );
            }

            currentSlot++;
        }
    }

    private void HandleClick()
    {
        var jobPlayer = Main.LocalPlayer.GetModPlayer<JobPlayer>();

        try
        {
            jobPlayer.StartJob(job.ID);
            IsActive = false;
        }
        catch (Exception ex)
        {
            TrelamiumIII.Instance.Logger.Error(
                $"Error starting job {job.Name}: {ex.Message}");
        }
    }
}