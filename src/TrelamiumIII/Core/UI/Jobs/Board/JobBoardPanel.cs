using System.Collections.Generic;
using Terraria.Audio;
using Terraria.GameContent;
using Terraria.UI;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.Jobs.Events;
using TrelamiumIII.Core.UI.World;
using TrelamiumIII.Utilities;

namespace TrelamiumIII.Core.UI.Jobs.Board;

/// <summary>
///     The job board: everything one NPC is offering, listed on the left, with the highlighted job's
///     brief and payout on the right. Hangs in the world above whoever is offering the work.
/// </summary>
/// <remarks>
///     Two things the indicator-and-hover-tooltip it replaces could not do. An NPC may offer several
///     jobs at once, so there is a list to choose from rather than a single implicit job; and taking
///     one is a two-step confirm, so brushing the cursor over a townsfolk on the way past can no
///     longer sign the player up to something irreversible.
/// </remarks>
internal sealed class JobBoardPanel : WorldPanel
{
    #region Layout
    private const float PANEL_W = 430f;

    // Tall enough that a four-line brief clears the reward grid beneath it; the existing job
    // descriptions run to three or four lines at this column width.
    private const float PANEL_H = 268f;

    // The 9-slice frame's own borders; content sits inside them.
    private const float INSET_X = 20f;
    private const float INSET_Y = 28f;

    private const float LIST_W = 140f;
    private const float COLUMN_GAP = 14f;
    private const float ROW_H = 22f;

    private const float BUTTON_W = 78f;
    private const float BUTTON_H = 24f;

    /// <summary>Vertical space between the description's top and the reward block's label.</summary>
    private const float DESCRIPTION_TOP = 44f;
    private const float REWARDS_LIFT = 44f;
    private const float LINE_HEIGHT = 19f;

    /// <summary>Rows that fit in the list column before it would run past the frame.</summary>
    private const int MAX_VISIBLE_ROWS = 8;

    private static float ContentX => INSET_X;
    private static float ContentY => INSET_Y;
    private static float ContentH => PANEL_H - INSET_Y * 2f;
    private static float DetailX => ContentX + LIST_W + COLUMN_GAP;
    private static float DetailW => PANEL_W - DetailX - INSET_X;
    #endregion

    private readonly NPC _npc;
    private readonly List<Job> _jobs;

    private JobDescriptionText? _description;
    private JobRewardGrid? _rewards;
    private WorldUIButton? _primary;
    private WorldUIButton? _secondary;

    private Job? _selected;

    /// <summary>
    ///     Second stage of the accept. While true the buttons read Accept/Back over a prompt rather
    ///     than Accept/Close, and only then does the primary button actually start the job.
    /// </summary>
    private bool _confirming;

    protected override Vector2 PanelSize => new(PANEL_W, PANEL_H);

    public JobBoardPanel(NPC npc, IEnumerable<Job> jobs)
    {
        _npc = npc;
        _jobs = [.. jobs];
        _selected = _jobs.Count > 0 ? _jobs[0] : null;

        WorldAnchor = npc.Top + new Vector2(0f, -48f);
    }

    #region Construction
    protected override void BuildPanel(UIElement body)
    {
        BuildList(body);
        BuildDetail(body);

        Select(_selected);
    }

    private void BuildList(UIElement body)
    {
        // No paging yet: an NPC offering more than a column's worth would have the surplus go
        // unlisted. Nothing in the mod comes close, and Prev/Next art is already on disk for when
        // something does.
        int visible = Math.Min(_jobs.Count, MAX_VISIBLE_ROWS);

        for (int i = 0; i < visible; i++)
        {
            var row = new JobListRow(_jobs[i], LIST_W, ROW_H, () => _selected)
            {
                Left = StyleDimension.FromPixels(ContentX),
                Top = StyleDimension.FromPixels(ContentY + 22f + i * ROW_H),
                Opacity = () => Opacity,
                OnSelected = Select
            };

            body.Append(row);
        }
    }

    private void BuildDetail(UIElement body)
    {
        float rewardsTop = ContentY + ContentH - BUTTON_H - REWARDS_LIFT;
        float descriptionTop = ContentY + DESCRIPTION_TOP;

        _description = new JobDescriptionText(DetailW)
        {
            Left = StyleDimension.FromPixels(DetailX),
            Top = StyleDimension.FromPixels(descriptionTop),
            Opacity = () => Opacity,

            // Derived from the gap down to the reward label rather than hard-coded, so moving any
            // of the layout constants above cannot silently let the brief overrun the rewards.
            MaxLines = Math.Max(1, (int)((rewardsTop - 17f - descriptionTop) / LINE_HEIGHT))
        };
        body.Append(_description);

        // Anchored up from the bottom edge: the description above it varies in length, and the
        // payout and the buttons should not shuffle around as the player browses.
        _rewards = new JobRewardGrid(DetailW)
        {
            Left = StyleDimension.FromPixels(DetailX),
            Top = StyleDimension.FromPixels(rewardsTop),
            Opacity = () => Opacity
        };
        body.Append(_rewards);

        float buttonY = ContentY + ContentH - BUTTON_H;

        _secondary = new WorldUIButton(JobBoardText.Close, BUTTON_W, BUTTON_H)
        {
            Left = StyleDimension.FromPixels(PANEL_W - INSET_X - BUTTON_W),
            Top = StyleDimension.FromPixels(buttonY),
            Opacity = () => Opacity,
            Accent = new Color(150, 150, 165),
            OnClick = OnSecondary
        };
        body.Append(_secondary);

        _primary = new WorldUIButton(JobBoardText.TakeJob, BUTTON_W, BUTTON_H)
        {
            Left = StyleDimension.FromPixels(PANEL_W - INSET_X - BUTTON_W * 2f - 8f),
            Top = StyleDimension.FromPixels(buttonY),
            Opacity = () => Opacity,
            Accent = new Color(120, 200, 130),
            IsEnabled = () => _selected != null,
            OnClick = OnPrimary
        };
        body.Append(_primary);
    }
    #endregion

    #region Interaction
    private void Select(Job? job)
    {
        _selected = job;

        // Browsing away from a job the player was about to take cancels the confirmation, so the
        // Accept they click next can never apply to a job they are no longer looking at.
        _confirming = false;

        _description?.SetText(job?.Description ?? string.Empty);
        _rewards?.SetJob(job);
    }

    private void OnPrimary()
    {
        if (_selected == null)
            return;

        if (!_confirming)
        {
            _confirming = true;
            return;
        }

        Accept();
    }

    private void OnSecondary()
    {
        if (_confirming)
        {
            _confirming = false;
            return;
        }

        RequestClose();
    }

    private void Accept()
    {
        Job job = _selected!;

        try
        {
            // The board knows which NPC was actually clicked, so the job is credited to that
            // instance rather than guessed at from its type.
            Main.LocalPlayer.GetModPlayer<JobPlayer>().StartJob(job.ID, _npc);

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

    protected override void OnCloseRequested() => SoundEngine.PlaySound(SoundID.MenuClose);

    public override void OnOpened() => SoundEngine.PlaySound(SoundID.MenuOpen);
    #endregion

    protected override void TrackAnchor()
    {
        if (_npc is { active: true })
            WorldAnchor = _npc.Top + new Vector2(0f, -48f);
    }

    public override void Update(GameTime gameTime)
    {
        // Nobody left to offer the work: the board has outlived its reason to be open. Note this
        // falls through to base.Update rather than returning — the close fade is driven from there,
        // so bailing out early would leave the panel frozen on screen instead of dismissing it.
        if (_npc is not { active: true })
            RequestClose();

        _primary!.Label = _confirming ? JobBoardText.ConfirmAccept : JobBoardText.TakeJob;
        _secondary!.Label = _confirming ? JobBoardText.ConfirmBack : JobBoardText.Close;

        base.Update(gameTime);
    }

    #region Drawing
    protected override void DrawChrome(SpriteBatch spriteBatch, Rectangle bounds, float opacity)
    {
        DrawUtils.DrawPanel(spriteBatch, bounds, default, null, opacity);

        var font = FontAssets.MouseText.Value;
        Texture2D pixel = TextureAssets.MagicPixel.Value;

        DrawListHeader(spriteBatch, font, bounds, opacity);

        // Divider between the two columns.
        spriteBatch.Draw(
            pixel,
            new Rectangle(
                (int)(bounds.X + ContentX + LIST_W + COLUMN_GAP / 2f),
                (int)(bounds.Y + ContentY),
                1,
                (int)ContentH),
            new Color(120, 125, 175) * (0.6f * opacity));

        if (_selected == null)
        {
            DrawCentred(spriteBatch, font, JobBoardText.NoneAvailable, bounds, opacity);
            return;
        }

        DrawDetailHeader(spriteBatch, font, bounds, opacity);

        if (_rewards!.HasRewards)
        {
            Utils.DrawBorderStringFourWay(
                spriteBatch, font, JobBoardText.Rewards,
                bounds.X + DetailX,
                _rewards.GetDimensions().Y - 17f,
                new Color(190, 195, 220) * opacity, Color.Black * opacity, Vector2.Zero, 0.75f);
        }

        if (_confirming)
            DrawConfirmPrompt(spriteBatch, font, bounds, opacity);
    }

    private void DrawListHeader(SpriteBatch spriteBatch, ReLogic.Graphics.DynamicSpriteFont font,
        Rectangle bounds, float opacity)
    {
        Utils.DrawBorderStringFourWay(
            spriteBatch, font, JobBoardText.ListHeader(_jobs.Count),
            bounds.X + ContentX,
            bounds.Y + ContentY,
            new Color(255, 215, 120) * opacity, Color.Black * opacity, Vector2.Zero, 0.8f);
    }

    private void DrawDetailHeader(SpriteBatch spriteBatch, ReLogic.Graphics.DynamicSpriteFont font,
        Rectangle bounds, float opacity)
    {
        Utils.DrawBorderStringFourWay(
            spriteBatch, font, _selected.Name,
            bounds.X + DetailX,
            bounds.Y + ContentY,
            new Color(255, 215, 0) * opacity, new Color(1, 1, 1, (int)(170 * opacity)), Vector2.Zero, 1f);

        Utils.DrawBorderStringFourWay(
            spriteBatch, font, JobBoardText.From(_npc.GivenOrTypeName),
            bounds.X + DetailX,
            bounds.Y + ContentY + 24f,
            new Color(190, 195, 220) * opacity, Color.Black * opacity, Vector2.Zero, 0.75f);
    }

    /// <summary>Sits over the buttons on the confirm step, so the second click is never a blind one.</summary>
    private void DrawConfirmPrompt(SpriteBatch spriteBatch, ReLogic.Graphics.DynamicSpriteFont font,
        Rectangle bounds, float opacity)
    {
        string prompt = JobBoardText.ConfirmPrompt;

        float width = font.MeasureString(prompt).X * 0.8f;

        Utils.DrawBorderStringFourWay(
            spriteBatch, font, prompt,
            bounds.X + PANEL_W - INSET_X - width,
            bounds.Y + ContentY + ContentH - BUTTON_H - 20f,
            new Color(255, 225, 150) * opacity, Color.Black * opacity, Vector2.Zero, 0.8f);
    }

    private static void DrawCentred(SpriteBatch spriteBatch, ReLogic.Graphics.DynamicSpriteFont font,
        string text, Rectangle bounds, float opacity)
    {
        Vector2 size = font.MeasureString(text) * 0.8f;

        Utils.DrawBorderStringFourWay(
            spriteBatch, font, text,
            bounds.X + DetailX + (DetailW - size.X) / 2f,
            bounds.Y + ContentY + ContentH / 2f,
            new Color(170, 170, 180) * opacity, Color.Black * opacity, Vector2.Zero, 0.8f);
    }
    #endregion
}
