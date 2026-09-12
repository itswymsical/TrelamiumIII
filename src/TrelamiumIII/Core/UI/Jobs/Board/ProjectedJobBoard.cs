using System.Collections.Generic;
using ReLogic.Graphics;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;
using TrelamiumIII.Core.Components.NPCs;
using TrelamiumIII.Core.Dialogue;
using TrelamiumIII.Core.Dialogue.Projected;
using TrelamiumIII.Core.Events;
using TrelamiumIII.Core.Graphics.Cinematics.Camera;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.NPCs.Behaviors;
using TrelamiumIII.Core.UI.Projected;
using TrelamiumIII.Core.UI.World;

namespace TrelamiumIII.Core.UI.Jobs.Board;

/// <summary>
///     The job board as a plane standing in perspective over whoever is offering the work: the
///     NPC's jobs listed down the left, the highlighted one's brief and payout on the right, and a
///     two-step accept along the bottom.
/// </summary>
internal sealed class ProjectedJobBoard : WorldPanel, IProjectedPanel
{
    #region Tuning

    /// <summary>
    ///     Screen pixels per texel. Well under the dialogue's, which is authored for one or two
    ///     sentences at a time; this is the dial the board's legibility hangs off.
    /// </summary>
    private const float PIXEL = 1.15f;

    private const int SURFACE_W = 560;
    private const int SURFACE_H = 420;

    /// <summary>The content area inside the bubble, in texels.</summary>
    private const int CONTENT_W = 440;

    private const int CONTENT_H = 252;

    private const int LIST_W = 150;
    private const int COLUMN_GAP = 14;
    private const int ROW_PAD = 7;

    /// <summary>Vertical padding inside a list row and a button, above and below the text.</summary>
    private const int BOX_PAD_Y = 5;

    /// <summary>Gap under a header before whatever it labels.</summary>
    private const int HEADER_GAP = 6;

    private const int SLOT = 34;
    private const int MIN_SLOT = 22;
    private const int SLOT_GAP = 4;
    private const int SLOT_INSET = 3;

    private const int BUTTON_W = 96;
    private const int BUTTON_GAP = 8;

    private const int REWARDS_LIFT = 12;

    private const int OPEN_FRAMES = 16;

    /// <summary>Has to finish inside the base panel's own fade, or the panel is dropped mid-close.</summary>
    private const int CLOSE_FRAMES = 10;

    /// <summary>Screen pixels between the tail's point and the provider's head.</summary>
    private const float ANCHOR_GAP = 30f;

    /// <summary>How much the camera pulls in over the player's own zoom while the board is open.</summary>
    private const float ZOOM_FACTOR = 1.4f;

    private const float MAX_ZOOM = 2.4f;
    private const int ZOOM_FRAMES = 45;

    #endregion

    private enum Widget
    {
        None,
        Row,
        Primary,
        Secondary,
        Reward,
    }

    #region State

    private readonly NPC npc;
    private readonly List<Job> jobs;
    private readonly List<Item> rewardItems = [];

    private readonly ProjectedSurface surface = new(SURFACE_W, SURFACE_H, ProjectedInk.Pixelated);
    private readonly BubblePlacer placer;

    private Job? selected;
    private List<string> description = [];

    /// <summary>
    ///     Second stage of the accept. While true the buttons read Accept/Back over a prompt rather
    ///     than Take Job/Close, and only then does the primary button actually start the job.
    /// </summary>
    private bool confirming;

    private float presence;
    private QuadPlacement placement;

    /// <summary>Set once the panel has been dropped by the system, however that happened.</summary>
    private bool dropped;

    /// <summary>Set the moment the board starts to close; ends the provider's idle interrupt.</summary>
    private bool released;

    private bool zoomed;

    // What the cursor is on, found while drawing; and what it clicked, acted on next update.
    private Widget hovered = Widget.None;
    private int hoveredIndex = -1;
    private Widget clicked = Widget.None;
    private int clickedIndex = -1;

    // Layout, in texels on the face. Rebuilt every tick from the bubble geometry.
    private Rectangle content;
    private Rectangle primary;
    private Rectangle secondary;
    private Rectangle[] slots = [];
    private int detailX;
    private int detailW;
    private int rowsTop;
    private int rowHeight;
    private int visibleRows;
    private int descriptionTop;
    private int rewardsTop;
    private int maxDescriptionLines;

    #endregion

    public ProjectedJobBoard(NPC npc, IEnumerable<Job> jobs)
    {
        this.npc = npc;
        this.jobs = [.. jobs];

        placer = new BubblePlacer(PIXEL, Main.rand?.Next(1, 9999) ?? 1, 4f);

        WorldAnchor = npc.Top;

        surface.Initialize();
        ProjectedPanelSystem.Register(this);

        Select(this.jobs.Count > 0 ? this.jobs[0] : null);
        Place(snap: true);
    }

    /// <summary>Nothing is an element; the base panel's box is a zero-sized placeholder.</summary>
    protected override Vector2 PanelSize => Vector2.Zero;

    protected override void BuildPanel(UIElement body) { }

    protected override void DrawChrome(SpriteBatch spriteBatch, Rectangle bounds, float opacity) { }

    private static DynamicSpriteFont Body => ProjectedFonts.Body;

    private static DynamicSpriteFont Title => ProjectedFonts.Title;

    private static int LineH => ProjectedText.LineHeight(ProjectedFonts.Body);

    private static int DetailWidth => CONTENT_W - LIST_W - COLUMN_GAP;

    public bool ShouldBeRemoved => dropped || !IsActive;

    #region Lifecycle

    public override void OnOpened()
    {
        SoundEngine.PlaySound(SoundID.MenuOpen);

        HoldProvider();
        ZoomIn();
    }

    protected override void OnCloseRequested()
    {
        SoundEngine.PlaySound(SoundID.MenuClose);
        Release();
    }

    public override void OnClosed()
    {
        dropped = true;
        Release();
    }

    /// <summary>
    ///     Asks the provider to stand still and face the player for as long as the board is up.
    /// </summary>
    private void HoldProvider()
    {
        if (Main.dedServ || npc is not { active: true } || !npc.TryEnable(out NPCDirector director))
            return;

        director.Interrupt(
            npc,
            Behavior.Idle().FacingNearestPlayer().While(_ => !released && npc.active));
    }

    private void Release()
    {
        released = true;

        if (!zoomed)
            return;

        zoomed = false;

        // A conversation that opened meanwhile owns the camera now, and will put it back itself.
        if (!DialogueManager.Instance.IsZoomedIn)
            ZoomHandler.SetZoomAnimation(Main.GameZoomTarget, ZOOM_FRAMES);
    }

    /// <remarks>
    ///     Relative to the player's own zoom rather than a fixed level, so a player who already plays
    ///     zoomed in still sees the camera move. Restored to the same base rather than to 1, which is
    ///     what hands the zoom handler back to the game's own target when the animation ends.
    /// </remarks>
    private void ZoomIn()
    {
        if (DialogueManager.Instance.IsZoomedIn)
            return;

        float target = MathF.Min(MathF.Max(Main.GameZoomTarget, 1f) * ZOOM_FACTOR, MAX_ZOOM);

        ZoomHandler.SetZoomAnimation(target, ZOOM_FRAMES);
        zoomed = true;
    }

    public void Dispose() => surface.Dispose();

    #endregion

    #region Selection

    private void Select(Job? job)
    {
        selected = job;

        // Browsing away from a job the player was about to take cancels the confirmation, so the
        // Accept they click next can never apply to a job they are no longer looking at.
        confirming = false;

        rewardItems.Clear();

        if (job is null)
        {
            description = [];
            return;
        }

        (string plain, _) = JobTextParser.ParseDescription(job.Description ?? string.Empty);
        description = ProjectedText.Wrap(Body, plain, DetailWidth);

        foreach (JobReward reward in job.Rewards)
        {
            Item item = reward.GetItem();

            if (item is null || item.type <= ItemID.None)
                continue;

            // Requested now so the art is on disk by the time the face is first built.
            Main.instance.LoadItem(item.type);
            rewardItems.Add(item);
        }
    }

    private void OnPrimary()
    {
        if (selected is null)
            return;

        if (!confirming)
        {
            confirming = true;
            return;
        }

        Accept();
    }

    private void OnSecondary()
    {
        if (confirming)
        {
            confirming = false;
            return;
        }

        RequestClose();
    }

    private void Accept()
    {
        Job job = selected!;

        try
        {
            // The board knows which NPC was actually clicked, so the job is credited to that
            // instance rather than guessed at from its type.
            Main.LocalPlayer.GetModPlayer<JobPlayer>().StartJob(job.ID, npc);

            // The indicator reads its jobs from this cache; without the nudge it would keep
            // offering the job just taken until the next scheduled rebuild.
            EventNPC.InvalidateJobCache();
        }
        catch (Exception ex)
        {
            TrelamiumIII.Instance.Logger.Error($"Error starting job {job.Name}: {ex.Message}");
        }

        RequestClose();
    }

    #endregion

    #region Update

    public override void Update(GameTime gameTime)
    {
        presence = IsClosing
            ? MathF.Max(presence - 1f / CLOSE_FRAMES, 0f)
            : MathF.Min(presence + 1f / OPEN_FRAMES, 1f);

        // Nobody left to offer the work, or a conversation has started over the top of it: either
        // way the board has lost its reason to be open. Falls through to base.Update, which is
        // where the close fade runs.
        if (npc is not { active: true } || DialogueManager.Instance.IsAnyActive())
            RequestClose();

        // Before input: the mouse is tested against the panel's projected corners, and testing it
        // against last frame's would misread every click during the entrance.
        Place();
        ConsumeIntent();

        base.Update(gameTime);
    }

    private void Place(bool snap = false)
    {
        int nameWidth = (int)MathF.Ceiling(ProjectedText.Width(Title, npc.GivenOrTypeName)) + PanelStyle.NAME_PAD_X * 2;
        int nameHeight = ProjectedText.LineHeight(Title) + 4;

        placer.Place(new Point(CONTENT_W, CONTENT_H), nameWidth, nameHeight, Target(), ANCHOR_GAP, snap);

        Layout();

        placement = placer.Placement(presence, IsClosing);
    }

    /// <summary>The top of the provider's head in raw screen pixels: what the tail points at.</summary>
    private Vector2 Target() =>
        Vector2.Transform(npc.Top + new Vector2(0f, -8f) - Main.screenPosition, Main.GameViewMatrix.ZoomMatrix);

    /// <summary>
    ///     Works out every widget's rectangle on the face from the bubble's content origin.
    /// </summary>
    private void Layout()
    {
        Point origin = placer.Geometry.ContentOrigin;

        content = new Rectangle(origin.X, origin.Y, CONTENT_W, CONTENT_H);

        detailX = content.X + LIST_W + COLUMN_GAP;
        detailW = DetailWidth;

        int line = LineH;

        rowHeight = line + BOX_PAD_Y * 2;
        rowsTop = content.Y + line + HEADER_GAP;

        int buttonH = rowHeight;
        int buttonsY = content.Bottom - buttonH;

        secondary = new Rectangle(content.Right - BUTTON_W, buttonsY, BUTTON_W, buttonH);
        primary = new Rectangle(secondary.X - BUTTON_GAP - BUTTON_W, buttonsY, BUTTON_W, buttonH);

        // However many rows fit above the button row, so the list can never run past the frame.
        visibleRows = Math.Clamp((buttonsY - 8 - rowsTop) / Math.Max(rowHeight, 1), 0, jobs.Count);

        int count = rewardItems.Count;

        if (count > 0)
        {
            int size = Math.Clamp((detailW - (count - 1) * SLOT_GAP) / count, MIN_SLOT, SLOT);

            rewardsTop = buttonsY - REWARDS_LIFT - size;

            if (slots.Length != count)
                slots = new Rectangle[count];

            for (int i = 0; i < count; i++)
                slots[i] = new Rectangle(detailX + i * (size + SLOT_GAP), rewardsTop, size, size);
        }
        else
        {
            rewardsTop = buttonsY - 4;
            slots = [];
        }

        // Title, then the provider line, then the brief.
        descriptionTop = content.Y + line * 2 + HEADER_GAP;

        int descriptionBottom = count > 0 ? rewardsTop - line - HEADER_GAP : rewardsTop;

        maxDescriptionLines = Math.Max(1, (descriptionBottom - descriptionTop) / line);
    }

    private Rectangle RowRect(int index) => new(content.X, rowsTop + index * rowHeight, LIST_W, rowHeight);

    private void ConsumeIntent()
    {
        Widget widget = clicked;
        int index = clickedIndex;

        clicked = Widget.None;
        clickedIndex = -1;

        if (IsClosing)
            return;

        switch (widget)
        {
            case Widget.Row when index >= 0 && index < jobs.Count && jobs[index] != selected:
                SoundEngine.PlaySound(SoundID.MenuTick);
                Select(jobs[index]);
                break;

            case Widget.Primary when selected is not null:
                SoundEngine.PlaySound(SoundID.MenuTick);
                OnPrimary();
                break;

            case Widget.Secondary:
                SoundEngine.PlaySound(SoundID.MenuTick);
                OnSecondary();
                break;
        }
    }

    protected override void TrackAnchor()
    {
        if (npc is { active: true })
            WorldAnchor = npc.Top;
    }

    /// <summary>A click is "away" when it misses the quad's silhouette on screen.</summary>
    protected override bool ClickedAway() => !ProjectedHitTest.TryLocate(placement, Main.MouseScreen, out _);

    #endregion

    #region Input

    private void HandleMouse()
    {
        Widget previous = hovered;
        int previousIndex = hoveredIndex;

        hovered = Widget.None;
        hoveredIndex = -1;

        if (IsClosing || PlayerInput.IgnoreMouseInterface)
            return;

        Point? texel = ProjectedHitTest.Texel(placement, surface.Region, Main.MouseScreen);

        if (texel is null)
            return;

        // Claimed while the cursor is anywhere on the panel, so a click on its face never also
        // swings whatever the player is holding.
        Main.LocalPlayer.mouseInterface = true;

        Point p = texel.Value;

        for (int i = 0; i < visibleRows; i++)
        {
            if (!RowRect(i).Contains(p))
                continue;

            hovered = Widget.Row;
            hoveredIndex = i;
            break;
        }

        if (hovered == Widget.None && selected is not null && primary.Contains(p))
            hovered = Widget.Primary;
        else if (hovered == Widget.None && secondary.Contains(p))
            hovered = Widget.Secondary;

        if (hovered == Widget.None)
        {
            for (int i = 0; i < slots.Length; i++)
            {
                if (!slots[i].Contains(p))
                    continue;

                hovered = Widget.Reward;
                hoveredIndex = i;
                break;
            }
        }

        bool moved = hovered != previous || hoveredIndex != previousIndex;

        if (moved && hovered is Widget.Row or Widget.Primary or Widget.Secondary)
            SoundEngine.PlaySound(SoundID.MenuTick);

        // Vanilla's own hover path builds the tooltip lines -- modded lines included -- and the
        // projected card takes over the drawing from there. It is handed the board's facing so the
        // two lean the same way.
        if (hovered == Widget.Reward)
            ProjectedTooltip.Claim(rewardItems[hoveredIndex], placer.FacingBlend);

        if (hovered is Widget.Row or Widget.Primary or Widget.Secondary && Main.mouseLeft && Main.mouseLeftRelease)
        {
            Main.mouseLeftRelease = false;
            clicked = hovered;
            clickedIndex = hoveredIndex;
        }
    }

    #endregion

    #region Drawing

    public void BuildSurfaces()
    {
        if (presence > 0f)
            surface.Build(Paint);
    }

    public override void Draw(SpriteBatch spriteBatch)
    {
        if (!IsActive || presence <= 0f || !surface.Ready)
            return;

        HandleMouse();

        Color tint = Color.White * presence;
        QuadPlacement quad = placement;

        ProjectedPanelSystem.DrawQuads(spriteBatch, (mesh, device) => surface.Draw(mesh, device, quad, tint));
    }

    private Point Paint(ProjectedPainter painter)
    {
        BubbleGeometry geometry = placer.Geometry;
        PixelCanvas canvas = painter.Canvas;

        PanelStyle.DrawBubble(canvas, geometry, ProjectedChrome.JobGhost);

        if (geometry.NameTab != Rectangle.Empty)
        {
            painter.Text(
                Title,
                npc.GivenOrTypeName,
                new Vector2(geometry.NameTab.X + PanelStyle.NAME_PAD_X, geometry.NameTab.Y + 1),
                PanelStyle.Inverted);
        }

        PaintList(painter);

        ProjectedChrome.Rule(
            painter.Canvas, content.X + LIST_W + COLUMN_GAP / 2, content.Y, CONTENT_H, true, ProjectedChrome.Dim);

        if (selected is null)
            PaintNothing(painter);
        else
            PaintDetail(painter);

        PaintButtons(painter);

        return geometry.Used;
    }

    private void PaintList(ProjectedPainter painter)
    {
        painter.Text(Title, JobBoardText.ListHeader(jobs.Count), new Vector2(content.X, content.Y), ProjectedChrome.Accent);

        // Hover and selection share one vocabulary: the row being previewed is inverted, the row
        // the cursor is on is outlined, so the two never read as different kinds of state.
        PixelCanvas canvas = painter.Canvas;

        for (int i = 0; i < visibleRows; i++)
        {
            bool isSelected = jobs[i] == selected;
            bool isHovered = hovered == Widget.Row && hoveredIndex == i;

            if (isSelected || isHovered)
                ProjectedChrome.Slot(canvas, RowRect(i), isSelected, isHovered);
        }

        // No paging yet: an NPC offering more than a column's worth would have the surplus go
        // unlisted. Nothing in the mod comes close.
        for (int i = 0; i < visibleRows; i++)
        {
            Rectangle row = RowRect(i);

            painter.Text(
                Body,
                ProjectedText.Truncate(Body, jobs[i].Name, LIST_W - ROW_PAD * 2),
                new Vector2(row.X + ROW_PAD, row.Y + BOX_PAD_Y),
                jobs[i] == selected ? PanelStyle.Inverted : PanelStyle.Text);
        }
    }

    private void PaintDetail(ProjectedPainter painter)
    {
        Job job = selected!;

        painter.Text(Title, ProjectedText.Truncate(Title, job.Name, detailW), new Vector2(detailX, content.Y), PanelStyle.Text);

        painter.Text(
            Body,
            JobBoardText.From(npc.GivenOrTypeName),
            new Vector2(detailX, content.Y + LineH),
            ProjectedChrome.Dim);

        List<string> lines = ProjectedText.Clamp(Body, description, maxDescriptionLines, detailW);

        for (int i = 0; i < lines.Count; i++)
            painter.Text(Body, lines[i], new Vector2(detailX, descriptionTop + i * LineH), PanelStyle.Text);

        PaintRewards(painter);
    }

    private void PaintRewards(ProjectedPainter painter)
    {
        if (slots.Length == 0)
            return;

        painter.Text(Title, JobBoardText.Rewards, new Vector2(detailX, rewardsTop - LineH - 2), ProjectedChrome.Accent);

        PixelCanvas canvas = painter.Canvas;

        for (int i = 0; i < slots.Length; i++)
            ProjectedChrome.Slot(canvas, slots[i], false, hovered == Widget.Reward && hoveredIndex == i);

        for (int i = 0; i < slots.Length; i++)
        {
            Item item = rewardItems[i];
            Texture2D texture = TextureAssets.Item[item.type].Value;
            Rectangle frame = Main.itemAnimations[item.type]?.GetFrame(texture) ?? texture.Frame();

            Rectangle box = slots[i];
            box.Inflate(-SLOT_INSET, -SLOT_INSET);

            painter.SpriteFitted(texture, frame, box, Color.White);
        }

        for (int i = 0; i < slots.Length; i++)
        {
            Item item = rewardItems[i];

            if (item.stack <= 1)
                continue;

            string stack = item.stack.ToString();

            painter.Text(
                Body,
                stack,
                new Vector2(slots[i].Right - ProjectedText.Width(Body, stack) - 3f, slots[i].Bottom - LineH),
                PanelStyle.Text);
        }
    }

    private void PaintNothing(ProjectedPainter painter)
    {
        string none = JobBoardText.NoneAvailable;

        painter.Text(
            Body,
            none,
            new Vector2(detailX + (detailW - ProjectedText.Width(Body, none)) * 0.5f, content.Y + (CONTENT_H - LineH) * 0.5f),
            ProjectedChrome.Dim);
    }

    private void PaintButtons(ProjectedPainter painter)
    {
        bool canTake = selected is not null;

        PixelCanvas canvas = painter.Canvas;

        ProjectedChrome.ButtonFrame(canvas, primary, hovered == Widget.Primary, canTake);
        ProjectedChrome.ButtonFrame(canvas, secondary, hovered == Widget.Secondary, true);

        PaintLabel(painter, primary,
            confirming ? JobBoardText.ConfirmAccept : JobBoardText.TakeJob,
            ProjectedChrome.ButtonText(hovered == Widget.Primary, canTake));

        PaintLabel(painter, secondary,
            confirming ? JobBoardText.ConfirmBack : JobBoardText.Close,
            ProjectedChrome.ButtonText(hovered == Widget.Secondary, true));

        if (!confirming)
            return;

        // Sits beside the buttons on the confirm step, so the second click is never a blind one.
        string prompt = JobBoardText.ConfirmPrompt;

        painter.Text(
            Body,
            prompt,
            new Vector2(primary.X - BUTTON_GAP - ProjectedText.Width(Body, prompt), primary.Y + BOX_PAD_Y),
            ProjectedChrome.Accent);
    }

    private static void PaintLabel(ProjectedPainter painter, Rectangle rect, string label, Color color)
    {
        float width = ProjectedText.Width(Body, label);

        painter.Text(
            Body,
            label,
            new Vector2(rect.X + (rect.Width - width) * 0.5f, rect.Y + BOX_PAD_Y),
            color);
    }

    #endregion
}
