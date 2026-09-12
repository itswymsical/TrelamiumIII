namespace TrelamiumIII.Core.Books;

public enum BookSide
{
    Left,
    Right,
}

/// <summary>What <see cref="BookRenderer" /> needs from a book to compose a frame.</summary>
public interface IBookView
{
    /// <summary>Drives the right leaf uncurling as the book opens. 0 when not opening.</summary>
    float OpenProgress { get; }

    /// <summary>Drives both leaves folding shut. 0 when not closing.</summary>
    float CloseProgress { get; }

    /// <summary>Drives a page turn. 0 when not turning.</summary>
    float PageTurnProgress { get; }

    /// <summary>True when the turn runs right to left.</summary>
    bool IsForwardFlip { get; }

    /// <summary>True when the page targets need redrawing this frame.</summary>
    bool PagesDirty { get; }

    /// <summary>Draws one side of the current or upcoming spread, in page-local space.</summary>
    void DrawPage(SpriteBatch spriteBatch, BookSide side, bool next);

    /// <summary>True when that slot shows the cover rather than a leaf.</summary>
    bool IsCover(BookSide side, bool next);

    /// <summary>Draws the cover. Called once per book, since its face does not change.</summary>
    void DrawCover(SpriteBatch spriteBatch);
}

/// <summary>
///     Owns one open book's render targets: four page targets (two leaves, two more mid-turn), redrawn live into one composite.
/// </summary>
public sealed class BookRenderer(int pageWidth, int pageHeight, int gutter) : IDisposable
{
    private readonly int spreadWidth = pageWidth * 2 + gutter;

    private RenderTarget2D? currentLeft;
    private RenderTarget2D? currentRight;
    private RenderTarget2D? nextLeft;
    private RenderTarget2D? nextRight;
    private RenderTarget2D? cover;

    private bool coverDrawn;
    private bool targetsRequested;
    private bool disposed;

    /// <summary>The finished spread. Null until the targets have been created on the main thread.</summary>
    public RenderTarget2D? Composite { get; private set; }

    public int PageWidth => pageWidth;

    public int PageHeight => pageHeight;

    public int Gutter => gutter;

    public int SpreadWidth => spreadWidth;

    /// <summary>Queues target creation. Safe to call from anywhere; the work happens on the main thread.</summary>
    public void Initialize()
    {
        if (targetsRequested || disposed)
            return;

        targetsRequested = true;

        Main.QueueMainThreadAction(() =>
        {
            if (disposed)
                return;

            GraphicsDevice device = Main.graphics.GraphicsDevice;

            currentLeft = NewPage(device);
            currentRight = NewPage(device);
            nextLeft = NewPage(device);
            nextRight = NewPage(device);
            cover = NewPage(device);

            Composite = new RenderTarget2D(device, spreadWidth, pageHeight, false, SurfaceFormat.Color, DepthFormat.None);
        });
    }

    private RenderTarget2D NewPage(GraphicsDevice device) =>
        new(device, pageWidth, pageHeight, false, SurfaceFormat.Color, DepthFormat.None);

    /// <summary>Composes one frame. Call with no sprite batch open, where render targets may be swapped.</summary>
    public void Build(IBookView view)
    {
        if (disposed || Composite is null || cover is null || Main.gameMenu || Main.dedServ)
            return;

        GraphicsDevice device = Main.graphics.GraphicsDevice;
        SpriteBatch spriteBatch = Main.spriteBatch;

        if (!coverDrawn)
        {
            device.SetRenderTarget(cover);
            device.Clear(Color.Transparent);

            BookBatch.BeginPage(spriteBatch);
            view.DrawCover(spriteBatch);
            spriteBatch.End();

            device.SetRenderTarget(null);
            coverDrawn = true;
        }

        bool flipping = view.PageTurnProgress > 0f;

        if (view.PagesDirty || flipping)
        {
            DrawPage(device, spriteBatch, view, currentLeft, BookSide.Left, next: false);
            DrawPage(device, spriteBatch, view, currentRight, BookSide.Right, next: false);
        }

        // The pages coming up are only needed while something is moving between them.
        if (flipping)
        {
            DrawPage(device, spriteBatch, view, nextLeft, BookSide.Left, next: true);
            DrawPage(device, spriteBatch, view, nextRight, BookSide.Right, next: true);
        }

        Compose(device, spriteBatch, view, flipping);
    }

    private void Compose(GraphicsDevice device, SpriteBatch spriteBatch, IBookView view, bool flipping)
    {
        Texture2D? leftFlat;
        Texture2D? rightFlat;
        Texture2D? curlFront = null;
        Texture2D? curlBack = null;

        if (flipping)
        {
            if (view.IsForwardFlip)
            {
                // The right leaf lifts and turns over; the left stays, and the spread it uncovers is
                // already waiting underneath.
                leftFlat = Resolve(view, BookSide.Left, next: false);
                rightFlat = Resolve(view, BookSide.Right, next: true);
                curlFront = Resolve(view, BookSide.Right, next: false);
                curlBack = Resolve(view, BookSide.Left, next: true);
            }
            else
            {
                leftFlat = Resolve(view, BookSide.Left, next: true);
                rightFlat = Resolve(view, BookSide.Right, next: false);
                curlFront = Resolve(view, BookSide.Left, next: false);
                curlBack = Resolve(view, BookSide.Right, next: true);
            }
        }
        else
        {
            leftFlat = Resolve(view, BookSide.Left, next: false);
            rightFlat = Resolve(view, BookSide.Right, next: false);
        }

        float turn = view.PageTurnProgress;
        float opening = view.OpenProgress;
        float closing = view.CloseProgress;

        float spineX = pageWidth + gutter * 0.5f;
        float outerSpineX = pageWidth + gutter;
        Matrix projection = Matrix.CreateOrthographicOffCenter(0f, spreadWidth, pageHeight, 0f, -1000f, 1000f);

        device.SetRenderTarget(Composite);
        device.Clear(Color.Transparent);

        BookBatch.BeginPage(spriteBatch);

        // While closing, both leaves are on the mesh, so drawing them flat as well would double them.
        if (closing <= 0f)
        {
            if (leftFlat is not null)
                spriteBatch.Draw(leftFlat, Vector2.Zero, Color.White);

            if (rightFlat is not null)
                spriteBatch.Draw(rightFlat, new Vector2(outerSpineX, 0f), Color.White);
        }

        spriteBatch.End();

        PageFlipMesh? mesh = BookRenderSystem.Mesh;

        if (mesh is not null)
        {
            if (turn > 0f && curlFront is not null)
            {
                mesh.Draw(device, curlFront, curlBack, spineX, pageWidth, pageHeight, turn, !view.IsForwardFlip, projection);
            }
            else if (opening > 0f)
            {
                // Opening lifts the right leaf off the front cover, so it pivots on the outer edge.
                mesh.Draw(device, rightFlat, null, outerSpineX, pageWidth, pageHeight, opening, false, projection);
            }
            else if (closing > 0f)
            {
                // Half a turn takes a leaf from flat to standing at the spine, which reads as edge-on
                // and vanishes into the cover as the book's own opacity fades out behind it.
                float fold = closing * 0.5f;

                if (rightFlat is not null)
                    mesh.Draw(device, rightFlat, null, spineX, pageWidth, pageHeight, fold, false, projection);

                if (leftFlat is not null)
                    mesh.Draw(device, leftFlat, null, spineX, pageWidth, pageHeight, fold, true, projection);
            }
        }

        device.SetRenderTarget(null);
    }

    private Texture2D? Resolve(IBookView view, BookSide side, bool next)
    {
        if (view.IsCover(side, next))
            return cover;

        return (side, next) switch
        {
            (BookSide.Left, false)  => currentLeft,
            (BookSide.Right, false) => currentRight,
            (BookSide.Left, true)   => nextLeft,
            _                       => nextRight,
        };
    }

    private void DrawPage(
        GraphicsDevice device,
        SpriteBatch spriteBatch,
        IBookView view,
        RenderTarget2D? target,
        BookSide side,
        bool next)
    {
        if (target is null)
            return;

        device.SetRenderTarget(target);
        device.Clear(Color.Transparent);

        BookBatch.BeginPage(spriteBatch);
        view.DrawPage(spriteBatch, side, next);
        spriteBatch.End();

        device.SetRenderTarget(null);
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;

        // Snapshot into locals before clearing the fields: the queued action runs later, and would
        // otherwise dispose whatever the fields hold by then rather than what they hold now.
        RenderTarget2D?[] pending = [currentLeft, currentRight, nextLeft, nextRight, cover, Composite];

        currentLeft = currentRight = nextLeft = nextRight = cover = null;
        Composite = null;

        Main.QueueMainThreadAction(() =>
        {
            foreach (RenderTarget2D? target in pending)
                target?.Dispose();
        });
    }
}
