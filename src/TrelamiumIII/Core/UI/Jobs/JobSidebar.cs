using System.Collections.Generic;
using System.Linq;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;
using TrelamiumIII.Core.Jobs;

namespace TrelamiumIII.Core.UI.Jobs;

// TODO: Localization
public class JobSidebar
{
    #region Constants
    private const float FADE_IN_SPEED = 0.015f;
    private const float FADE_OUT_SPEED = 0.015f;
    private const float HOVER_FADE_SPEED = 0.1f;

    private const string EMPTY_CHECKBOX = "☐";
    private const string CHECKED_CHECKBOX = "☑";

    private const int PANEL_WIDTH = 180;
    private const int PANEL_HEIGHT = 34;
    private const int TEXT_PADDING = 10;
    private const int JobS_PER_PAGE = 5;

    private const float PANEL_X_OFFSET = 3f;
    private const float ICON_Y_DIVISOR = 1.6f;
    private const float TITLE_Y_DIVISOR = (float)Math.PI;
    private const float ICON_SCALE = 0.67f; // doot doot doot
    private const int OBJECTIVE_Y_SPACING = 12;
    private const int AVAILABLE_Job_Y_SPACING = 22;
    private const int NAV_PADDING = 18;
    private const int INFO_DISPLAY_ROW_HEIGHT = 20;
    #endregion

    #region Fields
    private bool isFadingOut;
    private float fadeoutProgress;
    private float fadeInProgress;
    private float alpha;
    private float hoverFadeIn;

    private Job currentJob;
    private List<JobTask> activeTasks;
    private List<Job> activeJobs;
    private List<Job> availableJobs;

    private int currentJobIndex;
    private int currentAvailablePage;
    private bool showingAvailableJobs;
    private bool wasInventoryOpen;
    private bool isPinned;

    private readonly Dictionary<string, float> objFadeFinish = [];

    private bool isHoveringJob;
    private bool isHoveringToggleButton;
    private bool isHoveringPrevButton;
    private bool isHoveringNextButton;
    private bool isHoveringPinButton;

    private Vector2 iconPos;

    private Texture2D iconTexture;
    private Texture2D nextTexture;
    private Texture2D prevTexture;
    private Texture2D toggleTexture;
    private Texture2D pinTexture;
    #endregion

    #region Properties
    public bool ShouldBeRemoved => false;

    /// <summary>
    /// The area the panel took up when it last drew, in UI space, or null when it drew nothing.
    /// For anything that points at the tracker, such as <see cref="JobTrackerHint"/>.
    /// </summary>
    public Rectangle? Bounds { get; private set; }

    /// <summary>How faded in the panel is, 0..1.</summary>
    public float Visibility => alpha;
    #endregion

    #region Constructor
    public JobSidebar(Job Job)
    {
        currentJob = Job;
        LoadTextures();
        LoadJobs();
        UpdateActiveTasks();
        ResetFadeStates();
        SoundEngine.PlaySound(SoundID.MenuTick);
    }

    private void LoadTextures()
    {
        iconTexture = ModContent.Request<Texture2D>($"{Assets.Textures.UI.Jobs.JobAvailable.KEY}", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
        nextTexture = ModContent.Request<Texture2D>($"{Assets.Textures.UI.Jobs.Next.KEY}", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
        prevTexture = ModContent.Request<Texture2D>($"{Assets.Textures.UI.Jobs.Prev.KEY}", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
        toggleTexture = ModContent.Request<Texture2D>($"{Assets.Textures.UI.Jobs.CycleMenu.KEY}", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
        pinTexture = ModContent.Request<Texture2D>($"{Assets.Textures.UI.Jobs.Pin.KEY}", ReLogic.Content.AssetRequestMode.ImmediateLoad).Value;
    }

    private void ResetFadeStates()
    {
        fadeInProgress = 0f;
        fadeoutProgress = 0f;
        isFadingOut = false;
    }
    #endregion

    #region Job Management
    private void LoadJobs()
    {
        var JobPlayer = Main.LocalPlayer.GetModPlayer<JobPlayer>();

        activeJobs = [.. JobPlayer.ActiveJobs()];
        availableJobs = [.. JobPlayer.AvailableJobs()];

        HandleJobListToggling();

        if (!showingAvailableJobs)
            SetupActiveJobIndex();
        else
            SetupAvailableJobPage();
    }

    private void HandleJobListToggling()
    {
        bool shouldToggleToAvailable = showingAvailableJobs && availableJobs.Count == 0 && activeJobs.Count > 0;
        bool shouldToggleToActive = !showingAvailableJobs && activeJobs.Count == 0 && availableJobs.Count > 0;

        if (shouldToggleToAvailable || shouldToggleToActive)
            showingAvailableJobs = !showingAvailableJobs;
    }

    private void SetupActiveJobIndex()
    {
        if (activeJobs.Count == 0) return;

        if (currentJob == null || !activeJobs.Contains(currentJob))
        {
            currentJob = activeJobs[0];
            currentJobIndex = 0;
        }
        else
        {
            currentJobIndex = activeJobs.FindIndex(m => m.ID == currentJob.ID);
            if (currentJobIndex < 0 && activeJobs.Count > 0)
                currentJobIndex = 0;
        }
    }

    private void SetupAvailableJobPage()
    {
        int maxPages = CalculateMaxPages(availableJobs.Count);
        currentAvailablePage = Math.Clamp(currentAvailablePage, 0, maxPages - 1);

        if (availableJobs.Count > 0 && (currentJob == null || !availableJobs.Contains(currentJob)))
            currentJob = availableJobs[0];
    }

    private void UpdateActiveTasks()
    {
        activeTasks = new List<JobTask>();

        if (currentJob == null || currentJob.TaskList.Count == 0 || showingAvailableJobs)
            return;

        PopulateVisibleTasks();
    }

    /// <summary>
    /// Reads the stage the job says it is on. It does not advance it.
    /// </summary>
    private void PopulateVisibleTasks()
    {
        if (currentJob.CurrentSet is not { } currentSet)
            return;

        foreach (var task in currentSet.Task)
        {
            if (task.ShouldBeVisible(currentJob))
                activeTasks.Add(task);
        }
    }
    #endregion

    #region Navigation
    private void CycleToNextJob()
    {
        if (activeJobs.Count <= 1) return;

        currentJobIndex = (currentJobIndex + 1) % activeJobs.Count;
        currentJob = activeJobs[currentJobIndex];
        ResetHoverState();
        UpdateActiveTasks();
    }

    private void CycleToPreviousJob()
    {
        if (activeJobs.Count <= 1) return;

        currentJobIndex = (currentJobIndex - 1 + activeJobs.Count) % activeJobs.Count;
        currentJob = activeJobs[currentJobIndex];
        ResetHoverState();
        objFadeFinish.Clear();
        UpdateActiveTasks();
    }

    private void NavigateToNextPage()
    {
        int maxPages = CalculateMaxPages(availableJobs.Count);
        currentAvailablePage = (currentAvailablePage + 1) % maxPages;
    }

    private void NavigateToPreviousPage()
    {
        int maxPages = CalculateMaxPages(availableJobs.Count);
        currentAvailablePage = (currentAvailablePage - 1 + maxPages) % maxPages;
    }

    private int CalculateMaxPages(int itemCount) => Math.Max(1, (int)Math.Ceiling(itemCount / (float)JobS_PER_PAGE));

    private void ResetHoverState()
    {
        hoverFadeIn = 0f;
        isHoveringJob = false;
        isHoveringToggleButton = false;
        isHoveringPrevButton = false;
        isHoveringNextButton = false;
        isHoveringPinButton = false;
    }
    #endregion

    #region Drawing
    public void DrawInGame(SpriteBatch spriteBatch, Vector2 bottomAnchorPosition)
    {
        alpha = isFadingOut ? fadeInProgress - fadeoutProgress : fadeInProgress;
        alpha = MathHelper.Clamp(alpha, 0f, 1f);
        Bounds = null;
        DrawTaskList(spriteBatch, bottomAnchorPosition);
    }

    private void DrawTaskList(SpriteBatch spriteBatch, Vector2 bottomAnchorPosition)
    {
        var currentList = showingAvailableJobs ? availableJobs : activeJobs;

        if (!ShouldDrawPanel(currentList)) return;

        var layout = CalculateLayout(currentList);

        // From the pin, which hangs off the left of the panel, down to the last row.
        Bounds = new Rectangle(layout.PosX - 44, layout.PosY - 4, PANEL_WIDTH + 52, layout.ContentBottomY - layout.PosY + 8);

        DrawIcon(spriteBatch, layout);
        DrawTitle(spriteBatch, layout);
        DrawPinButton(spriteBatch, layout);
        DrawToggle(spriteBatch, layout);
        DrawNavigationArrows(spriteBatch, layout, currentList);
        DrawContent(spriteBatch, layout, currentList);
        DrawTooltips(spriteBatch);
    }

    private bool ShouldDrawPanel(List<Job> currentList)
    {
        if (iconTexture == null || currentList.Count == 0) return false;
        if (!showingAvailableJobs && (activeTasks == null || activeTasks.Count == 0)) return false;
        return true;
    }

    private PanelLayout CalculateLayout(List<Job> currentList)
    {
        int panelObjectCount = showingAvailableJobs
            ? CalculateVisibleJobs()
            : activeTasks.Count;

        int itemHeight = showingAvailableJobs
            ? AVAILABLE_Job_Y_SPACING
            : PANEL_HEIGHT - OBJECTIVE_Y_SPACING;

        int contentHeight = PANEL_HEIGHT + (itemHeight * panelObjectCount);
        int posX = Main.screenWidth - (int)(PANEL_WIDTH * PANEL_X_OFFSET);
        int posY = GetBaseYPosition();

        return new PanelLayout
        {
            PanelRect = new Rectangle(posX, posY, PANEL_WIDTH, contentHeight),
            PosX = posX,
            PosY = posY,
            ContentBottomY = posY + contentHeight
        };
    }

    private int CalculateVisibleJobs()
    {
        int startIndex = currentAvailablePage * JobS_PER_PAGE;
        return Math.Min(startIndex + JobS_PER_PAGE, availableJobs.Count) - startIndex;
    }

    private void DrawIcon(SpriteBatch spriteBatch, PanelLayout layout)
    {
        iconPos = new Vector2(layout.PosX, layout.PosY + PANEL_HEIGHT / ICON_Y_DIVISOR);

        spriteBatch.Draw(
            iconTexture,
            iconPos,
            null,
            Color.White * alpha,
            0f,
            new Vector2(iconTexture.Width / 2, iconTexture.Height / 2),
            ICON_SCALE,
            SpriteEffects.None,
            0f
        );
    }

    private void DrawTitle(SpriteBatch spriteBatch, PanelLayout layout)
    {
        string title = showingAvailableJobs ? "Available Jobs" : currentJob.Name;
        Color titleColor = showingAvailableJobs ? Color.LightGreen : Color.LightCyan;
        Vector2 titlePos = new Vector2(layout.PosX - 16, layout.PosY + PANEL_HEIGHT / TITLE_Y_DIVISOR);

        Utils.DrawBorderStringFourWay(
            spriteBatch,
            FontAssets.MouseText.Value,
            title,
            titlePos.X + 26,
            titlePos.Y + 3,
            titleColor * alpha,
            Color.Black * alpha,
            Vector2.Zero,
            0.9f
        );
    }

    private void DrawPinButton(SpriteBatch spriteBatch, PanelLayout layout)
    {
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, default, Main.Rasterizer);

        Vector2 pinPos = new Vector2(
            layout.PosX - 32,
            layout.PosY + PANEL_HEIGHT / TITLE_Y_DIVISOR + 10.5f
        );

        Rectangle pinRect = new Rectangle(
            (int)pinPos.X - pinTexture.Width / 2,
            (int)pinPos.Y - pinTexture.Height / 2,
            pinTexture.Width,
            pinTexture.Height
        );

        isHoveringPinButton = pinRect.Contains(Main.MouseScreen.ToPoint()) && PlayerInput.IgnoreMouseInterface == false;

        Color pinColor = isPinned ? Color.Gold : Color.White;
        if (isHoveringPinButton) pinColor = Color.LightGray;

        spriteBatch.Draw(
            pinTexture,
            pinPos,
            null,
            Color.White * alpha * (isHoveringPinButton ? 1f : 0.8f),
            isPinned ? MathHelper.ToRadians(-45f) : 0f,
            new Vector2(pinTexture.Width / 2, pinTexture.Height / 2),
            isHoveringPinButton ? 1.15f : 1f,
            SpriteEffects.FlipHorizontally,
            0f
        );

        if (isHoveringPinButton && Main.mouseLeft && Main.mouseLeftRelease && alpha > 0.9f)
        {
            Main.mouseLeftRelease = false;
            isPinned = !isPinned;
            SoundEngine.PlaySound(SoundID.MenuTick);
            isHoveringPinButton = false;
        }

        if (isHoveringPinButton)
        {
            Main.LocalPlayer.mouseInterface = true;
        }
        spriteBatch.End();
        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, default, default, default);
    }

    private void DrawToggle(SpriteBatch spriteBatch, PanelLayout layout)
    {
        bool hasOtherJobType = (showingAvailableJobs && activeJobs.Count > 0) ||
                                 (!showingAvailableJobs && availableJobs.Count > 0);

        if (!hasOtherJobType) return;

        string title = showingAvailableJobs ? "Available Jobs" : currentJob.Name;
        Vector2 titleSize = FontAssets.MouseText.Value.MeasureString(title) * 0.9f;

        Vector2 togglePos = new Vector2(
            layout.PosX + 26 + titleSize.X + 8,
            layout.PosY + PANEL_HEIGHT / TITLE_Y_DIVISOR + 10.5f
        );

        Rectangle toggleRect = new Rectangle(
            (int)togglePos.X - toggleTexture.Width / 2,
            (int)togglePos.Y - toggleTexture.Height / 2,
            toggleTexture.Width,
            toggleTexture.Height
        );

        isHoveringToggleButton = toggleRect.Contains(Main.MouseScreen.ToPoint()) && PlayerInput.IgnoreMouseInterface == false;

        spriteBatch.Draw(
            toggleTexture,
            togglePos,
            null,
            isHoveringToggleButton ? Color.White * alpha : Color.White * alpha * 0.8f,
            0f,
            new Vector2(toggleTexture.Width / 2, toggleTexture.Height / 2),
            isHoveringToggleButton ? 0.85f : 0.8f,
            SpriteEffects.None,
            0f
        );

        if (isHoveringToggleButton && Main.mouseLeft && Main.mouseLeftRelease && alpha > 0.9f)
        {
            Main.mouseLeftRelease = false;
            showingAvailableJobs = !showingAvailableJobs;
            LoadJobs();
            UpdateActiveTasks();
            SoundEngine.PlaySound(SoundID.MenuTick);

            isHoveringToggleButton = false;
            isHoveringPrevButton = false;
            isHoveringNextButton = false;
            isHoveringJob = false;
        }

        if (isHoveringToggleButton)
        {
            Main.LocalPlayer.mouseInterface = true;
        }
    }

    private bool ShouldProcessClick(bool isHovering)
    {
        return isHovering && Main.mouseLeft && Main.mouseLeftRelease && alpha > 0.9f;
    }

    private void DrawNavigationArrows(SpriteBatch spriteBatch, PanelLayout layout, List<Job> currentList)
    {
        bool shouldShowNavigation = showingAvailableJobs
            ? CalculateMaxPages(availableJobs.Count) > 1
            : activeJobs.Count > 1;

        if (!shouldShowNavigation) return;

        var (prevPos, nextPos) = CalculateNavigationPositions(layout);
        var prevRect = CreateButtonRect(prevPos, prevTexture);
        var nextRect = CreateButtonRect(nextPos, nextTexture);

        isHoveringPrevButton = IsHoveringButton(prevRect);
        isHoveringNextButton = IsHoveringButton(nextRect);

        DrawButton(spriteBatch, prevTexture, prevPos, isHoveringPrevButton);
        DrawButton(spriteBatch, nextTexture, nextPos, isHoveringNextButton);

        if (isHoveringPrevButton || isHoveringNextButton)
        {
            Main.LocalPlayer.mouseInterface = true;
        }

        HandleNavigationClicks();
        DrawCounter(spriteBatch, layout, currentList);
    }

    private (Vector2 prev, Vector2 next) CalculateNavigationPositions(PanelLayout layout)
    {
        int navY = layout.ContentBottomY + NAV_PADDING;
        Vector2 prevPos = new Vector2(
            layout.PosX + PANEL_WIDTH / 16f,
            navY
        );
        Vector2 nextPos = new Vector2(
            layout.PosX + PANEL_WIDTH / 6f,
            navY
        );

        return (prevPos, nextPos);
    }

    private void DrawButton(SpriteBatch spriteBatch, Texture2D texture, Vector2 position, bool isHovering)
    {
        float scale = isHovering ? 0.65f : 0.6f;
        Color color = isHovering ? Color.White * alpha : Color.White * alpha * 0.8f;

        spriteBatch.Draw(
            texture,
            position,
            null,
            color,
            0f,
            new Vector2(texture.Width / 2, texture.Height / 2),
            scale,
            SpriteEffects.None,
            0f
        );
    }

    private Rectangle CreateButtonRect(Vector2 position, Texture2D texture)
    {
        return new Rectangle(
            (int)position.X - texture.Width / 2,
            (int)position.Y - texture.Height / 2,
            texture.Width,
            texture.Height
        );
    }

    private bool IsHoveringButton(Rectangle buttonRect)
    {
        return buttonRect.Contains(Main.MouseScreen.ToPoint()) && PlayerInput.IgnoreMouseInterface == false;
    }

    private void DrawCounter(SpriteBatch spriteBatch, PanelLayout layout, List<Job> currentList)
    {
        string counterText = showingAvailableJobs
            ? $"Page {currentAvailablePage + 1}/{CalculateMaxPages(availableJobs.Count)}"
            : $"{currentJobIndex + 1}/{activeJobs.Count}";

        Vector2 counterPos = new Vector2(
            layout.PosX + (showingAvailableJobs ? PANEL_WIDTH / 2f : PANEL_WIDTH / 4f),
            layout.ContentBottomY + NAV_PADDING - 6
        );

        Vector2 origin = showingAvailableJobs ? Vector2.UnitX * 0.5f : Vector2.Zero;

        Utils.DrawBorderStringFourWay(
            spriteBatch,
            FontAssets.MouseText.Value,
            counterText,
            counterPos.X,
            counterPos.Y,
            Color.White * alpha,
            Color.Black * alpha,
            origin,
            0.7f
        );
    }

    private void DrawContent(SpriteBatch spriteBatch, PanelLayout layout, List<Job> currentList)
    {
        if (showingAvailableJobs)
            DrawAvailableJobs(spriteBatch, layout);
        else
            DrawActiveTasks(spriteBatch, layout);
    }

    private void DrawAvailableJobs(SpriteBatch spriteBatch, PanelLayout layout)
    {
        int startIndex = currentAvailablePage * JobS_PER_PAGE;
        int endIndex = Math.Min(startIndex + JobS_PER_PAGE, availableJobs.Count);
        int yOffset = PANEL_HEIGHT;

        for (int i = startIndex; i < endIndex; i++)
        {
            var Job = availableJobs[i];
            string employer = GetJobDisplayText(Job);
            Vector2 textPos = new Vector2(layout.PosX + TEXT_PADDING, layout.PosY + yOffset + PANEL_HEIGHT / TITLE_Y_DIVISOR);

            Utils.DrawBorderStringFourWay(
                spriteBatch,
                FontAssets.MouseText.Value,
                employer,
                textPos.X,
                textPos.Y,
                Color.LightGreen * alpha,
                Color.Black * alpha,
                Vector2.Zero,
                0.8f
            );

            yOffset += AVAILABLE_Job_Y_SPACING;
        }
    }

    private string GetJobDisplayText(Job Job)
    {
        if (Job.ProviderNPC <= 0) return "Unknown";

        return $"{Job.Name} - {Job.ProviderName}";
    }

    private void DrawActiveTasks(SpriteBatch spriteBatch, PanelLayout layout)
    {
        int yOffset = PANEL_HEIGHT;

        foreach (var task in activeTasks)
        {
            string taskText = FormatTaskText(task);
            Vector2 textPos = new Vector2(layout.PosX + TEXT_PADDING, layout.PosY + yOffset + PANEL_HEIGHT / TITLE_Y_DIVISOR);
            Color textColor = task.IsCompleted ? new Color(150, 255, 150) * alpha : Color.White * alpha;

            Utils.DrawBorderStringFourWay(
                spriteBatch,
                FontAssets.ItemStack.Value,
                taskText,
                textPos.X,
                textPos.Y,
                textColor,
                Color.Black * alpha,
                Vector2.Zero,
                0.85f
            );

            yOffset += PANEL_HEIGHT - OBJECTIVE_Y_SPACING;
        }
    }

    private string FormatTaskText(JobTask task)
    {
        string status = task.IsCompleted ? CHECKED_CHECKBOX : EMPTY_CHECKBOX;

        if (task.RequiredCount > 1)
            return $"{status} {task.Description} [{task.Count}/{task.RequiredCount}]";

        return $"{status} {task.Description}";
    }

    private void DrawTooltips(SpriteBatch spriteBatch)
    {
        if (alpha == 0) return;

        string hoverText = GetHoverText();
        if (!string.IsNullOrEmpty(hoverText))
            DrawHoverText(spriteBatch, hoverText);
    }

    private string GetHoverText()
    {
        if (isHoveringPinButton)
            return isPinned ? "Unpin Sidebar" : "Pin Sidebar";

        bool hasOtherJobType = (showingAvailableJobs && activeJobs.Count > 0) ||
                                 (!showingAvailableJobs && availableJobs.Count > 0);

        if (isHoveringToggleButton && hasOtherJobType)
            return showingAvailableJobs ? "See Active Jobs" : "See Available Jobs";

        if (isHoveringPrevButton)
            return showingAvailableJobs ? "Previous Page" : "Previous Job";

        if (isHoveringNextButton)
            return showingAvailableJobs ? "Next Page" : "Next Job";

        return string.Empty;
    }

    private void DrawHoverText(SpriteBatch spriteBatch, string text)
    {
        Vector2 mousePos = Main.MouseScreen;
        Vector2 textSize = FontAssets.MouseText.Value.MeasureString(text);

        Vector2 textPos = new Vector2(
            mousePos.X - textSize.X / 2,
            mousePos.Y - textSize.Y - 10
        );

        textPos.X = MathHelper.Clamp(textPos.X, 10, Main.screenWidth - textSize.X - 10);
        textPos.Y = MathHelper.Clamp(textPos.Y, 10, Main.screenHeight - textSize.Y - 10);

        Rectangle hoverBg = new Rectangle(
            (int)textPos.X - 16,
            (int)textPos.Y - 10,
            (int)textSize.X + 8,
            (int)textSize.Y + 8
        );

        Utils.DrawPanel(TextureAssets.InventoryBack.Value, 8, 0, spriteBatch, new Vector2(hoverBg.X, hoverBg.Y), hoverBg.Width, Color.White * 0.25f);

        Utils.DrawBorderStringFourWay(
            spriteBatch,
            FontAssets.MouseText.Value,
            text,
            textPos.X,
            textPos.Y,
            Color.White * alpha,
            Color.Black * alpha,
            Vector2.Zero,
            0.8f
        );
    }
    #endregion

    #region Input Handling
    private void HandleToggleClick()
    {
        if (!ShouldProcessClick(isHoveringToggleButton)) return;

        Main.mouseLeftRelease = false;
        Main.LocalPlayer.mouseInterface = true;

        showingAvailableJobs = !showingAvailableJobs;
        LoadJobs();
        UpdateActiveTasks();
        SoundEngine.PlaySound(SoundID.MenuTick);
        ResetHoverState();
    }

    private void HandleNavigationClicks()
    {
        if (ShouldProcessClick(isHoveringPrevButton))
        {
            Main.mouseLeftRelease = false;
            Main.LocalPlayer.mouseInterface = true;

            if (showingAvailableJobs) NavigateToPreviousPage();
            else CycleToPreviousJob();
            SoundEngine.PlaySound(SoundID.MenuTick);
        }
        else if (ShouldProcessClick(isHoveringNextButton))
        {
            Main.mouseLeftRelease = false;
            Main.LocalPlayer.mouseInterface = true;

            if (showingAvailableJobs) NavigateToNextPage();
            else CycleToNextJob();
            SoundEngine.PlaySound(SoundID.MenuTick);
        }
    }

    private void HandleInput()
    {
        if (alpha <= 0.9f || PlayerInput.IgnoreMouseInterface) return;

        HandleToggleClick();
        HandleNavigationClicks();
    }
    #endregion

    #region Update Logic
    public void Update()
    {
        bool isInventoryOpen = Main.playerInventory;
        bool shouldBeVisible = isInventoryOpen || isPinned;

        if (isInventoryOpen != wasInventoryOpen)
        {
            if (isPinned)
            {
                // Pinned sidebar stays fully visible through inventory toggles.
                // Refresh Job data without resetting fade state.
                LoadJobs();
                UpdateActiveTasks();
            }
            else if (isInventoryOpen)
            {
                isFadingOut = false;
                fadeInProgress = 0f;

                LoadJobs();
                UpdateActiveTasks();

                if ((showingAvailableJobs && availableJobs.Count > 0) ||
                    (!showingAvailableJobs && activeJobs.Count > 0))
                {
                    SoundEngine.PlaySound(SoundID.MenuTick);
                }
            }
            else
            {
                isFadingOut = true;
                fadeoutProgress = 0f;

                isHoveringJob = false;
                isHoveringToggleButton = false;
                isHoveringPrevButton = false;
                isHoveringNextButton = false;
                isHoveringPinButton = false;
            }

            wasInventoryOpen = isInventoryOpen;
        }

        if (shouldBeVisible && !isFadingOut)
        {
            if (isPinned)
            {
                fadeInProgress = 1.0f;
            }
            else
            {
                fadeInProgress += FADE_IN_SPEED;
                fadeInProgress = Math.Min(fadeInProgress, 1.0f);
            }

            LoadJobs();
            UpdateActiveTasks();
        }

        if (isFadingOut)
        {
            fadeoutProgress += FADE_OUT_SPEED;
            fadeoutProgress = Math.Min(fadeoutProgress, 1.0f);

            hoverFadeIn = 0f;
        }

        if (!isHoveringJob && hoverFadeIn > 0)
        {
            hoverFadeIn = MathHelper.Lerp(hoverFadeIn, 0f, HOVER_FADE_SPEED);
        }

        if (!showingAvailableJobs &&
            currentJob != null &&
            currentJob.CurrentTaskSet < currentJob.TaskList.Count &&
            currentJob.TaskList[currentJob.CurrentTaskSet].IsCompleted)
        {
            UpdateActiveTasks();
        }
    }

    private void HandleInventoryToggle()
    {
        bool isInventoryOpen = Main.playerInventory;

        if (isInventoryOpen == wasInventoryOpen) return;

        if (isInventoryOpen)
        {
            isFadingOut = false;
            LoadJobs();
            UpdateActiveTasks();

            if (HasJobsToShow())
                SoundEngine.PlaySound(SoundID.MenuTick);
        }
        else
        {
            isFadingOut = true;
            fadeoutProgress = 0f;
            ResetHoverState();
        }

        wasInventoryOpen = isInventoryOpen;
    }

    private bool HasJobsToShow()
    {
        return (showingAvailableJobs && availableJobs.Count > 0) ||
               (!showingAvailableJobs && activeJobs.Count > 0);
    }

    private void UpdateFadeStates()
    {
        bool isInventoryOpen = Main.playerInventory;

        if (isInventoryOpen)
        {
            isFadingOut = false;
            fadeInProgress = Math.Min(fadeInProgress + FADE_IN_SPEED, 1.0f);
            LoadJobs();
            UpdateActiveTasks();
        }

        if (isFadingOut)
        {
            fadeoutProgress = Math.Min(fadeoutProgress + FADE_OUT_SPEED, 1.0f);
            hoverFadeIn = 0f;
        }

        if (!isHoveringJob && hoverFadeIn > 0)
            hoverFadeIn = MathHelper.Lerp(hoverFadeIn, 0f, HOVER_FADE_SPEED);
    }

    private void UpdateTaskProgression()
    {
        if (showingAvailableJobs || currentJob == null) return;

        if (currentJob.CurrentTaskSet < currentJob.TaskList.Count &&
            currentJob.TaskList[currentJob.CurrentTaskSet].IsCompleted)
        {
            UpdateActiveTasks();
        }
    }

    public void StartFadeOut()
    {
        isFadingOut = true;
        fadeoutProgress = 0f;
    }

    public void PushAnchor(ref Vector2 positionAnchorBottom) { }
    #endregion

    #region Helper Structures
    /// <summary>
    /// Returns the Y position below the minimap and active info displays.
    /// Falls back to a reasonable default when the minimap is hidden.
    /// </summary>
    private int GetBaseYPosition()
    {
        int y;

        if (Main.mapStyle == 1)
        {
            // Minimap is active: anchor below it
            y = (int)(Main.miniMapY + Main.miniMapHeight) - 110;
        }
        else
        {
            // No minimap: use a fixed top-right anchor
            y = 90;
        }

        return y;
    }

    /// <summary>
    /// Measures the vertical space occupied by active info display accessories
    /// (depth meter, compass, DPS meter, etc.) rendered below the minimap.
    /// Checks each vanilla display's player flag and respects hideInfo toggles.
    /// </summary>
    private int GetInfoDisplayHeight()
    {
        Player p = Main.LocalPlayer;
        int activeCount = 0;

        // Each entry: (is the accessory equipped, InfoDisplay.Type index for hideInfo)
        (bool equipped, int type)[] displays =
        [
            (p.accWatch > 0,       InfoDisplay.Watches.Type),
            (p.accWeatherRadio,    InfoDisplay.WeatherRadio.Type),
            (p.accCalendar,        InfoDisplay.Sextant.Type),
            (p.accFishFinder,      InfoDisplay.FishFinder.Type),
            (p.accOreFinder,       InfoDisplay.MetalDetector.Type),
            (p.accCritterGuide,    InfoDisplay.LifeformAnalyzer.Type),
            (p.accThirdEye,        InfoDisplay.Radar.Type),
            (p.accJarOfSouls,      InfoDisplay.TallyCounter.Type),
            (p.accDreamCatcher,    InfoDisplay.DPSMeter.Type),
            (p.accStopwatch,       InfoDisplay.Stopwatch.Type),
            (p.accCompass > 0,     InfoDisplay.Compass.Type),
            (p.accDepthMeter > 0,  InfoDisplay.DepthMeter.Type),
        ];

        foreach (var (equipped, type) in displays)
        {
            if (equipped && type < p.hideInfo.Length && !p.hideInfo[type])
                activeCount++;
        }

        return activeCount * INFO_DISPLAY_ROW_HEIGHT;
    }

    private struct PanelLayout
    {
        public Rectangle PanelRect;
        public int PosX;
        public int PosY;
        public int ContentBottomY;
    }
    #endregion
}

public class JobSidebarManager : ModSystem
{
    public static JobSidebarManager Instance { get; set; }
    public JobSidebarManager() => Instance = this;

    private JobSidebar currentNotification;

    /// <summary>The tracker panel's area as it was last drawn, or null when it is not up.</summary>
    public Rectangle? TrackerBounds => currentNotification?.Bounds;

    /// <summary>How faded in the tracker panel is, 0..1.</summary>
    public float TrackerVisibility => currentNotification?.Visibility ?? 0f;

    public override void Unload()
    {
        if (!Main.dedServ)
        {
            Instance = null;
        }
        currentNotification = null;
    }

    public void SetNotification(JobSidebar notification)
    {
        currentNotification = notification;
    }

    /// <summary>
    /// Puts the sidebar up if it isn't already, seeded with <paramref name="headline"/>.
    /// </summary>
    /// <remarks>
    /// Idempotent, because the caller runs every tick. The panel re-reads the job list itself, so an
    /// existing one is already showing the right thing and rebuilding it would restart its fade and
    /// throw away which job the player had paged to.
    /// </remarks>
    public void EnsureNotification(Job headline)
    {
        currentNotification ??= new JobSidebar(headline);
    }

    public void ClearNotification()
    {
        currentNotification = null;
    }

    public override void PostUpdateEverything()
    {
        currentNotification?.Update();
    }

    public override void ModifyInterfaceLayers(List<GameInterfaceLayer> layers)
    {
        int mapIndex = layers.FindIndex(layer => layer.Name.Equals("Vanilla: Map / Minimap"));
        if (mapIndex != -1)
        {
            layers.Insert(mapIndex + 1, new LegacyGameInterfaceLayer(
                "TrelamiumIII: Job Notification",
                delegate {
                    currentNotification?.DrawInGame(Main.spriteBatch, Vector2.Zero);
                    return true;
                },
                InterfaceScaleType.UI)
            );
        }
    }
}