using System.Collections.Generic;
using System.Linq;
using ReLogic.Graphics;
using TrelamiumIII.Core.Jobs;
using TrelamiumIII.Core.UI.Projected;

namespace TrelamiumIII.Core.UI.Jobs.Board;

/// <summary>
///     The card that stands up over a <see cref="JobIndicator" /> while the cursor rests on it:
///     the indicator's own icon beside the job's name and the first lines of its brief, with a
///     hint that clicking opens the board.
/// </summary>
/// <remarks>
///     <para>
///         A preview, not a second board. It answers "what is this marker for" at a glance so the
///         player can decide whether to stop, and deliberately shows nothing that would let them act
///         from it -- taking a job stays behind the board's two-step confirm.
///     </para>
///     <para>
///         Owned by the indicator, which drives it from its own tick and draws it after its own icon.
///         It outlives the indicator by however long its exit takes, which is why it is registered
///         with the panel system in its own right and asks after its owner rather than being disposed
///         by it.
///     </para>
/// </remarks>
internal sealed class JobHoverCard : IProjectedPanel
{
    #region Tuning

    private const float PIXEL = 1.2f;

    private const int SURFACE_W = 400;
    private const int SURFACE_H = 260;

    /// <summary>Wrap width of the text column, in texels.</summary>
    private const int TEXT_W = 220;

    private const int ICON_GAP = 8;
    private const int MAX_NAMES = 3;
    private const int MAX_DESCRIPTION_LINES = 3;
    private const int HINT_GAP = 6;

    private const int OPEN_FRAMES = 10;
    private const int CLOSE_FRAMES = 8;

    /// <summary>Screen pixels between the tail's point and the top of the icon.</summary>
    private const float ANCHOR_GAP = 14f;

    #endregion

    private readonly Texture2D icon;
    private readonly Func<bool> ownerAlive;

    private readonly ProjectedSurface surface = new(SURFACE_W, SURFACE_H, ProjectedInk.Pixelated);
    private readonly BubblePlacer placer;
    private readonly List<string> names = [];

    private List<string> description = [];
    private string title = string.Empty;
    private string more = string.Empty;
    private string signature = string.Empty;

    private Point content;
    private QuadPlacement placement;

    private float presence;
    private bool wanted;
    private bool retired;

    /// <param name="ownerAlive">Whether the indicator this belongs to is still registered.</param>
    public JobHoverCard(Texture2D icon, Func<bool> ownerAlive)
    {
        this.icon = icon;
        this.ownerAlive = ownerAlive;

        placer = new BubblePlacer(PIXEL, Main.rand?.Next(1, 9999) ?? 1, 3f);

        surface.Initialize();
        ProjectedPanelSystem.Register(this);
    }

    private static DynamicSpriteFont Body => ProjectedFonts.Body;

    private static DynamicSpriteFont Title => ProjectedFonts.Title;

    private static int LineH => ProjectedText.LineHeight(ProjectedFonts.Body);

    public bool ShouldBeRemoved => (retired || !ownerAlive()) && presence <= 0f;

    /// <summary>Stops the card coming back; it fades out if it is up.</summary>
    public void Retire() => retired = true;

    /// <summary>
    ///     Points the card at a set of jobs. Cheap to call every tick: nothing is rebuilt unless
    ///     the set actually changed.
    /// </summary>
    public void SetJobs(IReadOnlyList<Job> jobs)
    {
        string next = string.Join(",", jobs.Select(job => job.ID));

        if (next == signature)
            return;

        signature = next;

        names.Clear();

        foreach (Job job in jobs.Take(MAX_NAMES))
            names.Add(ProjectedText.Truncate(Title, job.Name, TEXT_W));

        if (jobs.Count > 0)
        {
            (string plain, _) = JobTextParser.ParseDescription(jobs[0].Description ?? string.Empty);

            description = ProjectedText.Clamp(
                Body, ProjectedText.Wrap(Body, plain, TEXT_W), MAX_DESCRIPTION_LINES, TEXT_W);
        }
        else
        {
            description = [];
        }

        title = jobs.Count == 1 ? JobBoardText.HoverOne : JobBoardText.HoverMany(jobs.Count);
        more = jobs.Count > MAX_NAMES ? JobBoardText.HoverMore(jobs.Count - MAX_NAMES) : string.Empty;

        Measure();
    }

    /// <summary>
    ///     Sizes the text area: names, then the brief, then the overflow note, then the hint, beside
    ///     a column for the icon. The icon column sets a floor on the height so a one-line job
    ///     cannot crop the marker.
    /// </summary>
    private void Measure()
    {
        int rows = names.Count + description.Count + (more.Length > 0 ? 1 : 0);
        int height = rows * LineH + HINT_GAP + LineH;

        content = new Point(icon.Width + ICON_GAP + TEXT_W, Math.Max(height, icon.Height));
    }

    /// <param name="show">Whether the cursor is on the indicator this tick.</param>
    /// <param name="anchorScreen">The top of the indicator's icon, in raw screen pixels.</param>
    public void Update(bool show, Vector2 anchorScreen)
    {
        bool wasUp = presence > 0f;

        wanted = show && !retired && names.Count > 0;

        presence = wanted
            ? MathF.Min(presence + 1f / OPEN_FRAMES, 1f)
            : MathF.Max(presence - 1f / CLOSE_FRAMES, 0f);

        if (presence <= 0f)
            return;

        int nameWidth = title.Length > 0
            ? (int)MathF.Ceiling(ProjectedText.Width(Title, title)) + PanelStyle.NAME_PAD_X * 2
            : 0;

        int nameHeight = ProjectedText.LineHeight(Title) + 4;

        // A card has to open already facing the right way; easing from wherever it was last would
        // have it swing round as it arrives.
        placer.Place(content, nameWidth, nameHeight, anchorScreen, ANCHOR_GAP, snapFacing: !wasUp);

        placement = placer.Placement(presence, closing: !wanted, farShade: 0.2f);
    }

    public void BuildSurfaces()
    {
        if (presence > 0f && names.Count > 0)
            surface.Build(Paint);
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        if (presence <= 0f || !surface.Ready)
            return;

        Color tint = Color.White * presence;
        QuadPlacement quad = placement;

        ProjectedPanelSystem.DrawQuads(spriteBatch, (mesh, device) => surface.Draw(mesh, device, quad, tint));
    }

    private Point Paint(ProjectedPainter painter)
    {
        BubbleGeometry geometry = placer.Geometry;

        PanelStyle.DrawBubble(painter.Canvas, geometry, ProjectedChrome.JobGhost);

        Point origin = geometry.ContentOrigin;

        // The marker itself, so the card is unmistakably about the thing under the cursor.
        painter.Sprite(icon, new Vector2(origin.X, origin.Y + (content.Y - icon.Height) * 0.5f), null, Color.White);

        if (geometry.NameTab != Rectangle.Empty)
        {
            painter.Text(
                Title,
                title,
                new Vector2(geometry.NameTab.X + PanelStyle.NAME_PAD_X, geometry.NameTab.Y + 1),
                PanelStyle.Inverted);
        }

        float x = origin.X + icon.Width + ICON_GAP;
        float y = origin.Y;

        foreach (string name in names)
        {
            painter.Text(Title, name, new Vector2(x, y), PanelStyle.Text);
            y += LineH;
        }

        foreach (string line in description)
        {
            painter.Text(Body, line, new Vector2(x, y), ProjectedChrome.Dim);
            y += LineH;
        }

        if (more.Length > 0)
            painter.Text(Body, more, new Vector2(x, y), ProjectedChrome.Dim);

        // Pinned to the bottom of the text area rather than flowed, so it sits in the same place
        // on every card.
        painter.Text(
            Body,
            JobBoardText.HoverHint,
            new Vector2(x, origin.Y + content.Y - LineH),
            ProjectedChrome.Accent);

        return geometry.Used;
    }

    public void Dispose() => surface.Dispose();
}
